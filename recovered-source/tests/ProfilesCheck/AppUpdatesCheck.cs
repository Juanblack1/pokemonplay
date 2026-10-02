using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class AppUpdatesCheck
{
    internal static void Run(string root)
    {
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        void Reject(Action action,string text){try{action();}catch(Exception e)when(e is FormatException or InvalidDataException or OperationCanceledException){Assert(true,text);return;}throw new Exception(text);}
        string nextTag=$"v{AppRelease.Version.Major+1}";
        string dottedNextTag=$"v{AppRelease.Version.Major}.{AppRelease.Version.Minor+1}.0";
        Assert(AppRelease.ParseVersion(AppRelease.Tag)==AppRelease.Version&&AppRelease.ParseVersion(nextTag)>AppRelease.Version&&AppRelease.ParseVersion(dottedNextTag)>AppRelease.Version,"app assembly version and release comparison support integer and dotted tags");
        Assert(AppUpdateService.NormalizeRepository("https://github.com/owner/repository.git")=="owner/repository","update source accepts GitHub repository URLs");
        Reject(()=>AppUpdateService.NormalizeRepository("https://evil.example/owner/repository"),"update source rejects arbitrary servers");
        Reject(()=>AppRelease.ParseVersion(AppRelease.Tag+"-beta"),"stable updater rejects ambiguous prerelease version strings");
        JsonElement Release(string tag=null,bool prerelease=false,string digest=null,string host="github.com")
        {tag??=nextTag;return JsonSerializer.SerializeToElement(new{tag_name=tag,draft=false,prerelease,body="Novidades",assets=new[]{new{name=AppUpdateService.AssetName,size=1,browser_download_url="https://"+host+"/owner/repository/releases/download/"+tag+"/"+AppUpdateService.AssetName,digest=digest??"sha256:"+new string('a',64)}}});}
        Assert(AppUpdateService.ParseRelease(Release(),"owner/repository",AppRelease.Version).Tag==nextTag,"stable GitHub release provides the expected update asset");
        Assert(AppUpdateService.ParseRelease(Release("v1"),"owner/repository",AppRelease.Version)==null&&AppUpdateService.ParseRelease(Release(prerelease:true),"owner/repository",AppRelease.Version)==null,"updater ignores old releases and prereleases");
        Reject(()=>AppUpdateService.ParseRelease(Release(host:"evil.example"),"owner/repository",AppRelease.Version),"release asset URL must belong to the configured GitHub repository");
        Reject(()=>AppUpdateService.ParseRelease(Release(digest:"sha256:bad"),"owner/repository",AppRelease.Version),"updater rejects missing or malformed integrity metadata");
        string fixture=Path.Combine(root,"updates");Directory.CreateDirectory(fixture);
        string Zip(string name,string extra=null,string manifestTag=null)
        {
            manifestTag??=nextTag;
            string file=Path.Combine(fixture,name+".zip");using var archive=ZipFile.Open(file,ZipArchiveMode.Create);
            foreach(string entry in new[]{"Pokemons Play.exe","Pokemons Play.dll","Pokemons Play.runtimeconfig.json","Pokemons Play.deps.json","PokemonPlayUpdater.exe","app-release.json"})
            {using var writer=new StreamWriter(archive.CreateEntry("PokemonPlayRuntime/"+entry).Open());writer.Write(entry=="app-release.json"?JsonSerializer.Serialize(new{version=manifestTag,repository="owner/repository"}):"fixture");}
            if(extra!=null){using var writer=new StreamWriter(archive.CreateEntry(extra).Open());writer.Write("unsafe");}return file;
        }
        string valid=Zip("valid");string stage=Path.Combine(fixture,"valid-stage");Directory.CreateDirectory(stage);AppUpdateService.ExtractPackage(valid,stage,nextTag,"owner/repository");
        Assert(File.Exists(Path.Combine(stage,"PokemonPlayRuntime","Pokemons Play.exe")),"validated update extracts only a complete application runtime");
        Reject(()=>AppUpdateService.ExtractPackage(Zip("traversal","PokemonPlayRuntime/../Settings/config.json"),Path.Combine(fixture,"traversal"),nextTag,"owner/repository"),"update rejects zip traversal before touching user data");
        Reject(()=>AppUpdateService.ExtractPackage(Zip("outside","Saves/progress.sav"),Path.Combine(fixture,"outside"),nextTag,"owner/repository"),"update package cannot replace saves or files outside the runtime");
        Reject(()=>AppUpdateService.ExtractPackage(Zip("duplicate","PokemonPlayRuntime/POKEMONS PLAY.EXE"),Path.Combine(fixture,"duplicate"),nextTag,"owner/repository"),"update rejects case-insensitive duplicate paths");
        Reject(()=>AppUpdateService.ExtractPackage(Zip("wrong-version",manifestTag:dottedNextTag),Path.Combine(fixture,"wrong-version"),nextTag,"owner/repository"),"update manifest must match the selected release version");
        byte[] bytes=File.ReadAllBytes(valid);string digest=Convert.ToHexString(SHA256.HashData(bytes));
        string downloadRoot=Path.Combine(fixture,"download");Directory.CreateDirectory(Path.Combine(downloadRoot,"PokemonPlayRuntime"));File.WriteAllText(Path.Combine(downloadRoot,"PokemonPlayRuntime","PokemonPlayUpdater.exe"),"current trusted helper");
        using(var service=new AppUpdateService(downloadRoot,new HttpClient(new FakeHttp(bytes))))
        {
            service.SetRepository("owner/repository");
            var update=new AppUpdate(nextTag,"Notes","https://github.com/owner/repository/releases/tag/"+nextTag,"https://github.com/owner/repository/releases/download/"+nextTag+"/"+AppUpdateService.AssetName,bytes.Length,digest);
            string downloaded=service.DownloadAsync(update,null,CancellationToken.None).GetAwaiter().GetResult();Assert(File.ReadAllText(Path.Combine(downloaded,"PokemonPlayUpdater.exe"))=="current trusted helper","download validates SHA-256 and stages the currently installed updater helper");AppUpdateService.TryClean(downloaded);
            Reject(()=>service.DownloadAsync(update with{Sha256=new string('0',64)},null,CancellationToken.None).GetAwaiter().GetResult(),"download integrity failure preserves installed runtime");
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();Reject(()=>service.DownloadAsync(update,null,cancellation.Token).GetAwaiter().GetResult(),"cancelled downloads do not apply updates");
            Assert(!Directory.GetDirectories(downloadRoot,".pokemonplay-update-*").Any(),"failed and cancelled downloads clean their temporary stages");
            using var dialog=new AppUpdateDialog(service,update);Assert(Controls(dialog).OfType<Button>().Any(b=>b.Text=="Agora não")&&Controls(dialog).OfType<Button>().Any(b=>b.Text=="Atualizar e reabrir"&&b.Enabled),"update prompt offers install and defer as separate choices");
            string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_UPDATE_PREVIEW");
            if(!string.IsNullOrEmpty(preview)){Directory.CreateDirectory(preview);dialog.Show();Application.DoEvents();using var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(preview,"update-dialog.png"));dialog.Close();using var launcher=new LauncherForm(downloadRoot){UpdatesEnabled=false};typeof(LauncherForm).GetField("availableUpdate",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(launcher,update);typeof(LauncherForm).GetMethod("ShowUpdateNotice",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(launcher,null);launcher.Width=1000;launcher.Height=720;launcher.Show();Application.DoEvents();using var shell=new System.Drawing.Bitmap(launcher.Width,launcher.Height);launcher.DrawToBitmap(shell,new System.Drawing.Rectangle(0,0,shell.Width,shell.Height));shell.Save(Path.Combine(preview,"update-notice.png"));launcher.Close();}
        }
        string helper=Path.GetFullPath("recovered-source/updater/bin/Release/net48/PokemonPlayUpdater.exe");Type engine=Assembly.LoadFrom(helper).GetType("Program");
        void Apply(string install,string staged,Action start)
        {try{engine.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{install,staged,start});}catch(TargetInvocationException e){throw e.InnerException;}}
        string installRoot=Path.Combine(fixture,"install");string runtime=Path.Combine(installRoot,"PokemonPlayRuntime");Directory.CreateDirectory(runtime);File.WriteAllText(Path.Combine(runtime,"Pokemons Play.exe"),"old");Directory.CreateDirectory(Path.Combine(installRoot,"Saves"));File.WriteAllText(Path.Combine(installRoot,"Saves","progress.sav"),"saved progress");
        string Stage(){string value=Path.Combine(installRoot,".pokemonplay-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(value,"PokemonPlayRuntime"));File.WriteAllText(Path.Combine(value,"PokemonPlayRuntime","Pokemons Play.exe"),"new");return value;}
        string rollback=Stage();try{Apply(installRoot,rollback,()=>throw new IOException("simulated startup failure"));throw new Exception("rollback did not run");}catch(IOException){}
        Assert(File.ReadAllText(Path.Combine(runtime,"Pokemons Play.exe"))=="old","installer restores the old runtime when the new app cannot start");
        Apply(installRoot,Stage(),()=>{});Assert(File.ReadAllText(Path.Combine(runtime,"Pokemons Play.exe"))=="new"&&File.ReadAllText(Path.Combine(installRoot,"Saves","progress.sav"))=="saved progress","successful installer replaces only runtime and preserves user saves");
    }
    private static System.Collections.Generic.IEnumerable<Control> Controls(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Controls(c)));
    private sealed class FakeHttp(byte[] bytes):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){cancel.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});}
    }
}
