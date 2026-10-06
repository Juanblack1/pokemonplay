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
        Assert(AppRelease.IsNewer("v2.0.0",new Version(171,10,16,0))&&!AppRelease.IsNewer("v2.0.0",new Version(2,0,0,0))&&AppRelease.IsNewer("v2.0.1",new Version(2,0,0,0)),"v2 release line upgrades legacy v171 installs and keeps v2 patch ordering");
        Assert(AppUpdateService.NormalizeRepository("https://github.com/owner/repository.git")=="owner/repository","update source accepts GitHub repository URLs");
        Reject(()=>AppUpdateService.NormalizeRepository("https://evil.example/owner/repository"),"update source rejects arbitrary servers");
        Reject(()=>AppRelease.ParseVersion(AppRelease.Tag+"-beta"),"stable updater rejects ambiguous prerelease version strings");
        JsonElement Release(string tag=null,bool prerelease=false,string digest=null,string host="github.com")
        {tag??=nextTag;return JsonSerializer.SerializeToElement(new{tag_name=tag,draft=false,prerelease,body="Novidades",assets=new[]{new{name=AppUpdateService.AssetName,size=1,browser_download_url="https://"+host+"/owner/repository/releases/download/"+tag+"/"+AppUpdateService.AssetName,digest=digest??"sha256:"+new string('a',64)}}});}
        Assert(AppUpdateService.ParseRelease(Release(),"owner/repository",AppRelease.Version).Tag==nextTag,"stable GitHub release provides the expected update asset");
        Assert(AppUpdateService.ParseRelease(Release(tag:"v2.0.0"),"owner/repository",new Version(171,10,16,0)).Tag=="v2.0.0"&&AppUpdateService.ParseRelease(Release(tag:"v2.0.0"),"owner/repository",new Version(2,0,0,0))==null,"v2.0.0 is offered to legacy installs but not repeatedly offered to the updated app");
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
        string cleanupStage=Path.Combine(fixture,"cleanup-stage");Directory.CreateDirectory(cleanupStage);
        File.WriteAllText(Path.Combine(cleanupStage,"success"),"ok");
        Assert(!AppUpdateService.CanCleanCompletedStage(cleanupStage),"early bootstrap success retains the preparation backup");
        File.WriteAllText(Path.Combine(cleanupStage,"prepared"),"ok");File.WriteAllText(Path.Combine(cleanupStage,"PokemonPlayPreparationUpdater.exe"),"fixture");
        Assert(!AppUpdateService.CanCleanCompletedStage(cleanupStage),"completed launcher retains backup until supervision completes");
        File.WriteAllText(Path.Combine(cleanupStage,"preparation-complete"),"ok");
        Assert(AppUpdateService.CanCleanCompletedStage(cleanupStage),"completed preparation permits stage cleanup");
        File.WriteAllText(Path.Combine(cleanupStage,"failed"),"failed");Directory.CreateDirectory(Path.Combine(cleanupStage,"previous-runtime"));
        Assert(!AppUpdateService.CanCleanCompletedStage(cleanupStage),"failed rollback retains the previous runtime backup");
        Directory.Delete(Path.Combine(cleanupStage,"previous-runtime"));
        Assert(AppUpdateService.CanCleanCompletedStage(cleanupStage),"completed rollback permits stage cleanup");
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
            string downloaded=service.DownloadAsync(update,null,CancellationToken.None).GetAwaiter().GetResult();Assert(File.ReadAllText(Path.Combine(downloaded,"PokemonPlayUpdater.exe"))=="current trusted helper","download validates SHA-256 and stages the currently installed updater helper");PendingAppUpdate.Save(downloaded,update,service.Repository);
            Assert(PendingAppUpdate.Find(downloadRoot,service.Repository)==downloaded,"verified automatic update survives closing the app");
            Assert(PendingAppUpdate.Find(downloadRoot,"other/repository")==null,"automatic update is bound to its configured repository");
            File.WriteAllText(Path.Combine(downloaded,"failed"),"simulated failed install");
            Assert(PendingAppUpdate.Find(downloadRoot,service.Repository)==null,"failed automatic installation is never retried on startup");File.Delete(Path.Combine(downloaded,"failed"));
            File.AppendAllText(Path.Combine(downloaded,"PokemonPlayRuntime","Pokemons Play.dll"),"tampered");
            Assert(PendingAppUpdate.Find(downloadRoot,service.Repository)==null,"modified staged payload cannot be automatically installed");
            AppUpdateService.TryClean(downloaded);
            Reject(()=>service.DownloadAsync(update with{Sha256=new string('0',64)},null,CancellationToken.None).GetAwaiter().GetResult(),"download integrity failure preserves installed runtime");
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();Reject(()=>service.DownloadAsync(update,null,cancellation.Token).GetAwaiter().GetResult(),"cancelled downloads do not apply updates");
            Assert(!Directory.GetDirectories(downloadRoot,".pokemonplay-update-*").Any(),"failed and cancelled downloads clean their temporary stages");
            using var dialog=new AppUpdateDialog(service,update);Assert(Controls(dialog).OfType<Button>().Any(b=>b.Text=="Agora não")&&Controls(dialog).OfType<Button>().Any(b=>b.Text=="Atualizar e reabrir"&&b.Enabled),"update prompt offers install and defer as separate choices");
            string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_UPDATE_PREVIEW");
            if(!string.IsNullOrEmpty(preview)){Directory.CreateDirectory(preview);dialog.Show();Application.DoEvents();using var image=new System.Drawing.Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(preview,"update-dialog.png"));dialog.Close();using var launcher=new LauncherForm(downloadRoot){UpdatesEnabled=false};typeof(LauncherForm).GetField("availableUpdate",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(launcher,update);typeof(LauncherForm).GetMethod("ShowUpdateNotice",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(launcher,null);launcher.Width=1000;launcher.Height=720;launcher.Show();Application.DoEvents();using var shell=new System.Drawing.Bitmap(launcher.Width,launcher.Height);launcher.DrawToBitmap(shell,new System.Drawing.Rectangle(0,0,shell.Width,shell.Height));shell.Save(Path.Combine(preview,"update-notice.png"));launcher.Close();}
        }
        var automaticHttp=new AutomaticHttp(bytes,nextTag,digest);
        using(var launcher=new LauncherForm(downloadRoot,new HttpClient(automaticHttp)){UpdatesEnabled=false})
        {
            launcher.Show();Application.DoEvents();
            var method=typeof(LauncherForm).GetMethod("CheckForUpdates",BindingFlags.Instance|BindingFlags.NonPublic);
            var pending=(Task)method.Invoke(launcher,null);
            var duplicate=(Task)method.Invoke(launcher,null);
            var deadline=DateTime.UtcNow.AddSeconds(30);
            while(!pending.IsCompleted&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}
            Assert(pending.IsCompleted,"background automatic update completes without blocking the launcher");pending.GetAwaiter().GetResult();
            Assert(duplicate.IsCompleted&&automaticHttp.Checks==1&&automaticHttp.Downloads==1,"concurrent update checks share one download");
            string prepared=PendingAppUpdate.Find(downloadRoot,"owner/repository");
            Assert(prepared!=null&&Controls(launcher).OfType<Button>().Any(b=>b.Text=="Reiniciar e atualizar"&&b.Enabled),"opening app automatically downloads update and offers restart when ready");
            launcher.Close();Assert(PendingAppUpdate.Find(downloadRoot,"owner/repository")==prepared,"closing launcher preserves verified update for next startup");AppUpdateService.TryClean(prepared);
        }
        string helper=Path.GetFullPath("recovered-source/updater/bin/Release/net10.0-windows/PokemonPlayUpdater.dll");Type engine=Assembly.LoadFrom(helper).GetType("Program");
        void Apply(string install,string staged,Action start)
        {try{engine.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{install,staged,start});}catch(TargetInvocationException e){throw e.InnerException;}}
        string installRoot=Path.Combine(fixture,"install");string runtime=Path.Combine(installRoot,"PokemonPlayRuntime");Directory.CreateDirectory(runtime);File.WriteAllText(Path.Combine(runtime,"Pokemons Play.exe"),"old");Directory.CreateDirectory(Path.Combine(installRoot,"Saves"));File.WriteAllText(Path.Combine(installRoot,"Saves","progress.sav"),"saved progress");
        string Stage(){string value=Path.Combine(installRoot,".pokemonplay-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(value,"PokemonPlayRuntime"));File.WriteAllText(Path.Combine(value,"PokemonPlayRuntime","Pokemons Play.exe"),"new");return value;}
        string rollback=Stage();try{Apply(installRoot,rollback,()=>throw new IOException("simulated startup failure"));throw new Exception("rollback did not run");}catch(IOException){}
        Assert(File.ReadAllText(Path.Combine(runtime,"Pokemons Play.exe"))=="old","installer restores the old runtime when the new app cannot start");
        Apply(installRoot,Stage(),()=>{});Assert(File.ReadAllText(Path.Combine(runtime,"Pokemons Play.exe"))=="new"&&File.ReadAllText(Path.Combine(installRoot,"Saves","progress.sav"))=="saved progress","successful installer replaces only runtime and preserves user saves");
        File.WriteAllText(Path.Combine(runtime,"Pokemons Play.exe"),"stable before preparation");
        string preparation=Stage();Apply(installRoot,preparation,()=>{});File.WriteAllText(Path.Combine(preparation,"success"),"bootstrap acknowledged");
        engine.GetMethod("RestorePrepared",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{installRoot,preparation});
        Assert(File.ReadAllText(Path.Combine(runtime,"Pokemons Play.exe"))=="stable before preparation"&&File.ReadAllText(Path.Combine(installRoot,"Saves","progress.sav"))=="saved progress"&&!File.Exists(Path.Combine(preparation,"success")),"preparation failure restores the previous runtime after bootstrap acknowledgment and preserves saves");
    }
    private static System.Collections.Generic.IEnumerable<Control> Controls(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Controls(c)));
    private sealed class AutomaticHttp(byte[] bytes,string tag,string digest):HttpMessageHandler
    {
        internal int Checks,Downloads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
        {
            await Task.Delay(30,cancel);
            if(request.RequestUri.Host=="api.github.com")
            {
                Checks++;return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{tag_name=tag,draft=false,prerelease=false,body="Notes",assets=new[]{new{name=AppUpdateService.AssetName,size=bytes.Length,browser_download_url="https://github.com/owner/repository/releases/download/"+tag+"/"+AppUpdateService.AssetName,digest="sha256:"+digest}}}))};
            }
            Downloads++;return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
        }
    }
    private sealed class FakeHttp(byte[] bytes):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){cancel.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});}
    }
}
