using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;

// Only completed, verified downloads survive a restart. Failed installations remain
// available for recovery, but are never retried automatically.
internal static class PendingAppUpdate
{
    private const string Marker="automatic-update.json";
    internal sealed record Receipt(string Tag,string Repository,Dictionary<string,string> Files);
    internal static void Save(string stage,AppUpdate update,string repository)
    {
        var files=Inventory(stage);
        File.WriteAllText(Path.Combine(stage,Marker+".tmp"),JsonSerializer.Serialize(new Receipt(update.Tag,repository,files)));
        File.Move(Path.Combine(stage,Marker+".tmp"),Path.Combine(stage,Marker),true);
    }
    private static Dictionary<string,string> Inventory(string stage)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        void Scan(string directory)
        {
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Pasta de atualização inválida.");
            foreach(string file in Directory.GetFiles(directory))
            {
                string name=Path.GetRelativePath(stage,file);
                if(name==Marker||name==Marker+".tmp")continue;
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0||result.Count>=4096)throw new InvalidDataException("Arquivo de atualização inválido.");
                using var stream=File.OpenRead(file);result.Add(name,Convert.ToHexString(SHA256.HashData(stream)));
            }
            foreach(string child in Directory.GetDirectories(directory))Scan(child);
        }
        Scan(stage);return result;
    }
    internal static string Find(string root,string repository)
    {
        if(repository.Length==0||!Directory.Exists(root))return null;
        foreach(string stage in Directory.GetDirectories(root,".pokemonplay-update-*"))
        {
            try
            {
                if(!Guid.TryParseExact(Path.GetFileName(stage).Substring(20),"N",out _)||File.Exists(Path.Combine(stage,"failed"))||File.Exists(Path.Combine(stage,"success")))continue;
                string marker=Path.Combine(stage,Marker);
                if(!File.Exists(marker)||new FileInfo(marker).Length>1024*1024)continue;
                var receipt=JsonSerializer.Deserialize<Receipt>(File.ReadAllText(marker));
                if(receipt==null||receipt.Repository!=repository||!AppRelease.IsNewer(receipt.Tag,AppRelease.Version))continue;
                var files=Inventory(stage);
                if(receipt.Files==null||files.Count!=receipt.Files.Count||files.Any(f=>!receipt.Files.TryGetValue(f.Key,out string hash)||hash!=f.Value))continue;
                if(!files.ContainsKey("PokemonPlayUpdater.exe")||!files.ContainsKey(Path.Combine("PokemonPlayRuntime","Pokemons Play.exe")))continue;
                using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(stage,"PokemonPlayRuntime","app-release.json")));
                if(manifest.RootElement.GetProperty("version").GetString()!=receipt.Tag||manifest.RootElement.GetProperty("repository").GetString()!=repository)continue;
                return stage;
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException){}
        }
        return null;
    }
    internal static void Start(string root,string stage)
    {
        var start=new ProcessStartInfo(Path.Combine(stage,"PokemonPlayUpdater.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=stage};
        start.ArgumentList.Add(Environment.ProcessId.ToString());start.ArgumentList.Add(Path.GetFullPath(root));start.ArgumentList.Add(stage);
        if(Process.Start(start)==null)throw new IOException("Não foi possível iniciar o atualizador.");
        // A crash or launch failure must not produce a restart loop.
        try{File.Delete(Path.Combine(stage,Marker));}catch(IOException){}catch(UnauthorizedAccessException){}
    }
}
