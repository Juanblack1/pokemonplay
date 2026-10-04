using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal sealed record AppUpdate(string Tag,string Notes,string ReleaseUrl,string AssetUrl,long Size,string Sha256);
internal sealed class AppUpdateService : IDisposable
{
    internal const string AssetName="pokemon-play-win-x64-update.zip";
    private readonly string root;
    private readonly HttpClient http;
    private const long MaxDownload=512L*1024*1024;
    internal AppUpdateService(string root,HttpClient client=null)
    {
        this.root=Path.GetFullPath(root);http=client??new HttpClient();http.Timeout=TimeSpan.FromMinutes(15);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PokemonsPlay/"+AppRelease.Tag);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version","2022-11-28");
    }
    internal string Repository
    {
        get
        {
            foreach(string file in new[]{Path.Combine(root,"Settings","Updates.json"),Path.Combine(root,"PokemonPlayRuntime","UpdateSource.json"),Path.Combine(AppContext.BaseDirectory,"UpdateSource.json")})
            {
                if(!File.Exists(file))continue;
                try{using var doc=JsonDocument.Parse(File.ReadAllText(file));return NormalizeRepository(doc.RootElement.GetProperty("repository").GetString());}
                catch(Exception e)when(e is IOException or JsonException or FormatException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException){}
            }
            return string.Empty;
        }
    }
    internal void SetRepository(string value)
    {
        string repo=NormalizeRepository(value);string folder=Path.Combine(root,"Settings");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"Updates.json");string temporary=path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(new{repository=repo}));File.Move(temporary,path,true);
    }
    internal static string NormalizeRepository(string value)
    {
        string input=(value??string.Empty).Trim().TrimEnd('/');
        if(input.StartsWith("https://github.com/",StringComparison.OrdinalIgnoreCase))input=input.Substring(19);
        if(input.EndsWith(".git",StringComparison.OrdinalIgnoreCase))input=input.Substring(0,input.Length-4);
        if(!System.Text.RegularExpressions.Regex.IsMatch(input,@"^[A-Za-z0-9][A-Za-z0-9_-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$")||input.Split('/')[1] is "." or "..")throw new FormatException("Informe o endereço de um repositório público: https://github.com/usuario/pokemon-play.");
        return input;
    }
    internal async Task<AppUpdate> CheckAsync(CancellationToken cancel=default)
    {
        string repo=Repository;if(repo.Length==0)return null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response=await http.GetAsync("https://api.github.com/repos/"+repo+"/releases/latest",timeout.Token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)throw new InvalidOperationException("Repositório público ou release publicada não encontrada. Confira o endereço.");
        response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        return ParseRelease(doc.RootElement,repo,AppRelease.Version);
    }
    internal static AppUpdate ParseRelease(JsonElement release,string repo,Version current)
    {
        if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean())return null;
        string tag=release.GetProperty("tag_name").GetString();if(!AppRelease.IsNewer(tag,current))return null;
        JsonElement asset=release.GetProperty("assets").EnumerateArray().FirstOrDefault(a=>a.GetProperty("name").GetString()==AssetName);
        if(asset.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("Esta release ainda não tem o pacote de atualização do Windows.");
        string url=asset.GetProperty("browser_download_url").GetString();
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||!uri.AbsolutePath.StartsWith("/"+repo+"/releases/download/"+Uri.EscapeDataString(tag)+"/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Endereço do pacote não corresponde à release configurada.");
        long size=asset.GetProperty("size").GetInt64();if(size<=0||size>MaxDownload)throw new InvalidDataException("Tamanho do pacote de atualização não suportado.");
        string digest=asset.TryGetProperty("digest",out var hash)?hash.GetString():null;
        if(digest==null||!System.Text.RegularExpressions.Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$"))throw new InvalidDataException("A release não oferece a verificação SHA-256 necessária para atualizar.");
        string releaseUrl="https://github.com/"+repo+"/releases/tag/"+Uri.EscapeDataString(tag);
        return new AppUpdate(tag,release.TryGetProperty("body",out var body)?body.GetString()??"":"",releaseUrl,url,size,digest.Substring(7));
    }
    internal async Task<string> DownloadAsync(AppUpdate update,IProgress<int> progress,CancellationToken cancel)
    {
        string stage=Path.Combine(root,".pokemonplay-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            string zip=Path.Combine(stage,"download.zip");
            using(var response=await http.GetAsync(update.AssetUrl,HttpCompletionOption.ResponseHeadersRead,cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if(response.Content.Headers.ContentLength is long declared&&declared!=update.Size)throw new InvalidDataException("O tamanho anunciado do download mudou. Verifique a release novamente.");
                using var source=await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);using var output=File.Create(zip);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer=new byte[128*1024];long total=0;int read;
                while((read=await source.ReadAsync(buffer,cancel).ConfigureAwait(false))>0){total+=read;if(total>update.Size||total>MaxDownload)throw new InvalidDataException("Download maior que o pacote anunciado.");hash.AppendData(buffer,0,read);await output.WriteAsync(buffer.AsMemory(0,read),cancel).ConfigureAwait(false);progress?.Report((int)(total*100/update.Size));}
                if(total!=update.Size||!Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A verificação do download falhou. A versão atual foi preservada.");
            }
            await Task.Run(()=>ExtractPackage(zip,stage,update.Tag,Repository,cancel),cancel).ConfigureAwait(false);cancel.ThrowIfCancellationRequested();File.Delete(zip);
            string helper=Path.Combine(root,"PokemonPlayRuntime","PokemonPlayUpdater.exe");
            if(!File.Exists(helper))helper=Path.Combine(AppContext.BaseDirectory,"PokemonPlayUpdater.exe");
            if(!File.Exists(helper))throw new FileNotFoundException("O atualizador não está instalado. A versão atual foi preservada.");
            File.Copy(helper,Path.Combine(stage,"PokemonPlayUpdater.exe"));return stage;
        }
        catch{TryClean(stage);throw;}
    }
    internal static void ExtractPackage(string zip,string stage,string expectedTag,string repository,CancellationToken cancel=default)
    {
        string runtime=Path.Combine(stage,"PokemonPlayRuntime");string prefix=Path.GetFullPath(runtime)+Path.DirectorySeparatorChar;
        using var archive=ZipFile.OpenRead(zip);long total=0;var names=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(archive.Entries.Count==0||archive.Entries.Count>4096)throw new InvalidDataException("Quantidade de arquivos inválida no pacote.");
        foreach(var entry in archive.Entries)
        {
            cancel.ThrowIfCancellationRequested();string name=entry.FullName.Replace('\\','/');
            if(!name.StartsWith("PokemonPlayRuntime/",StringComparison.Ordinal)||name.Contains(':')||name.Split('/').Any(part=>part is ".." or ".")||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("O pacote contém um caminho não permitido.");
            string path=Path.GetFullPath(Path.Combine(stage,name));if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!names.Add(path))throw new InvalidDataException("Caminho duplicado ou fora do runtime.");
            if(entry.Length<0||entry.Length>2L*1024*1024*1024-total)throw new InvalidDataException("Pacote descompactado muito grande.");total+=entry.Length;
            if(name.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path));entry.ExtractToFile(path,false);
        }
        foreach(string required in new[]{"Pokemons Play.exe","Pokemons Play.dll","Pokemons Play.runtimeconfig.json","Pokemons Play.deps.json","PokemonPlayUpdater.exe","app-release.json"})if(!File.Exists(Path.Combine(runtime,required)))throw new InvalidDataException("Pacote incompleto: "+required);
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(runtime,"app-release.json")));
        if(manifest.RootElement.GetProperty("version").GetString()!=expectedTag||!NormalizeRepository(manifest.RootElement.GetProperty("repository").GetString()).Equals(repository,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("O pacote não corresponde à versão e ao repositório selecionados.");
    }
    internal static bool CanCleanCompletedStage(string stage)
    {
        if(File.Exists(Path.Combine(stage,"failed")))return !Directory.Exists(Path.Combine(stage,"previous-runtime"));
        return File.Exists(Path.Combine(stage,"success"))&&File.Exists(Path.Combine(stage,"prepared"))&&
            (!File.Exists(Path.Combine(stage,"PokemonPlayPreparationUpdater.exe"))||File.Exists(Path.Combine(stage,"preparation-complete")));
    }
    internal static void TryClean(string stage){try{if(Directory.Exists(stage))Directory.Delete(stage,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    public void Dispose()=>http.Dispose();
}
