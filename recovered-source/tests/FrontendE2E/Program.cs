using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        string root=null;
        try{
            var options=new Dictionary<string,string>();if(args.Length!=8)throw new ArgumentException("Expected --root --scenario --port --commit");
            for(int i=0;i<args.Length;i+=2)options.Add(args[i],args[i+1]);
            root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(options["--root"]));
            string scenario=options["--scenario"];int port=int.Parse(options["--port"]);
            if(scenario is not ("positive" or "suppressed-a")||port<49152||port>65535)throw new ArgumentException("Invalid scenario/port");
            if(!Regex.IsMatch(options["--commit"],"^[0-9a-fA-F]{40}$"))throw new ArgumentException("Expected immutable commit SHA");
            string runtime=Path.Combine(root,"PokemonPlayRuntime");
            if(!Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).Equals(runtime,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("root_identity: apphost must run from fixture PokemonPlayRuntime");
            foreach(string name in new[]{"TEMP","TMP","POKEMONPLAY_RETROARCH_TEMP"})SessionConfig.Owned(root,Environment.GetEnvironmentVariable(name)??throw new InvalidDataException("Missing owned "+name));
            foreach(string name in new[]{"LIBRETRO_VIDEO_SHADER_DIRECTORY","LIBRETRO_VIDEO_FILTER_DIRECTORY","LIBRETRO_ASSETS_DIRECTORY","LIBRETRO_AUTOCONFIG_DIRECTORY","LIBRETRO_CHEATS_DIRECTORY","LIBRETRO_DATABASE_DIRECTORY","LIBRETRO_SYSTEM_DIRECTORY","LIBRETRO_DIRECTORY"})if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))throw new InvalidDataException("configuration: inherited LIBRETRO environment");
            Directory.CreateDirectory(Path.Combine(root,"evidence"));
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using var run=new FrontendRun(root,scenario,port,options["--commit"]);Application.Run(run.Launcher);return run.Passed?0:1;
        }catch(Exception error){
            Console.Error.WriteLine(error);
            if(root!=null&&Directory.Exists(Path.Combine(root,"evidence"))){
                File.WriteAllText(Path.Combine(root,"evidence","startup-failed.json"),JsonSerializer.Serialize(new{schema=1,passed=false,error=error.ToString()}));
                var failedChecks=new Dictionary<string,object>();foreach(string key in new[]{"boot","process_identity","root_identity","embedding","focus","cadence","neutral","A-held","A-release","B-held","B-release","negative_validity","cleanup","default_driver","configuration","capture_identity","guest_hold_color"})failedChecks[key]=new{status="not_run",reason="startup failed before observation"};
                string startupPrefix=error.Message.Split(':')[0];
                failedChecks[failedChecks.ContainsKey(startupPrefix)?startupPrefix:"boot"]=new{status="failed",reason=error.Message};
                string scenarioIndex=Array.IndexOf(args,"--scenario") is int index&&index>=0&&index+1<args.Length?args[index+1]:"unknown";
                string commitIndex=Array.IndexOf(args,"--commit") is int shaIndex&&shaIndex>=0&&shaIndex+1<args.Length?args[shaIndex+1]:"unknown";
                File.WriteAllText(Path.Combine(root,"evidence","result.json"),JsonSerializer.Serialize(new{schema=1,scenario=scenarioIndex,commit=commitIndex,passed=false,checks=failedChecks,error=error.Message}));
            }
            return 1;
        }
    }
}

internal sealed class FrontendRun:IDisposable
{
    internal LauncherForm Launcher {get;}
    internal bool Passed {get;private set;}
    readonly string root,scenario,commit,evidence;readonly int port;readonly object hashes;
    readonly Stopwatch wall=Stopwatch.StartNew();
    readonly Dictionary<string,object> checks=new();readonly List<object> events=new();
    readonly List<(long Time,GuestFrame Frame,Bitmap Image)> ring=new();
    readonly System.Windows.Forms.Timer sampler=new(){Interval=100};
    GameHostForm host;ControllerVisualizer pad;Process child;IntPtr hwnd;string childPath;DateTime childStart;
    Exception callbackFailure,sampleFailure;int held=-1;bool rawNegative;Point mousePoint;long lastRequest=-2000;
    uint? internalCounter,desktopCounter;object configEvidence,identity;string current="startup";bool keyReleaseProved;long phaseStarted,lastEvidencePersist;
    static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    internal FrontendRun(string root,string scenario,int port,string commit)
    {
        this.root=root;this.scenario=scenario;this.port=port;this.commit=commit;evidence=Path.Combine(root,"evidence");
        string runtime=Path.Combine(root,"PokemonPlayRuntime"),retroarch=Path.Combine(runtime,"Emulators","RetroArch");
        hashes=new{harnessExe=SessionConfig.Hash(Path.Combine(runtime,"Check.exe")),harnessDll=SessionConfig.Hash(Path.Combine(runtime,"Check.dll")),app=SessionConfig.Hash(Path.Combine(runtime,"Pokemons Play.dll")),retroarch=SessionConfig.Hash(Path.Combine(retroarch,"retroarch.exe")),core=SessionConfig.Hash(Path.Combine(retroarch,"cores","mgba_libretro.dll")),rom=SessionConfig.Hash(Path.Combine(root,"roms","diagnostic.gba"))};
        identity=new{root,runId=Path.GetFileName(root),hashes};
        foreach(string key in new[]{"boot","process_identity","root_identity","embedding","focus","cadence","neutral","A-held","A-release","B-held","B-release","negative_validity","cleanup","default_driver","configuration","capture_identity","guest_hold_color"})Set(key,"not_run","not reached");
        var existing=Process.GetProcessesByName("retroarch");bool conflict=existing.Length!=0;foreach(var item in existing)item.Dispose();if(conflict)throw new InvalidDataException("process_identity: existing RetroArch; no third-party process killed");
        if(!AppPaths.Root.Equals(root,StringComparison.OrdinalIgnoreCase)||InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json")).Mode!=3)throw new InvalidDataException("root_identity: wrong root/profile");
        SessionConfig.Owned(root,Path.Combine(root,"roms","diagnostic.gba"));
        try{
            string diagnostic=Path.GetFullPath(Path.Combine(root,"roms","diagnostic.gba"));
            var entries=ImportedGameCatalog.Entries(root);var games=ImportedGameCatalog.Build(root);
            if(entries.Count!=1||entries[0].Generation!=3||!File.Exists(diagnostic)||
                !Path.GetFullPath(entries[0].RomPath).Equals(diagnostic,StringComparison.OrdinalIgnoreCase)||
                games.Count!=1||!games[0].IsImported||!Path.GetFullPath(games[0].RomPath).Equals(diagnostic,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("expected exactly one existing generation3 diagnostic ROM in actual catalog Entries/Build");
        }catch(Exception error){throw new InvalidDataException("configuration: diagnostic catalog preflight failed before UI: "+error.Message,error);}
        Launcher=new LauncherForm(root){UpdatesEnabled=false,Width=1280,Height=900,StartPosition=FormStartPosition.CenterScreen};
        if(!Field<string>(Launcher,"root").Equals(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("root_identity: launcher root differs");
        foreach(Control control in Descendants(Launcher))control.ControlAdded+=OnAdded;
        sampler.Tick+=(_,_)=>Sample();
        Launcher.Shown+=async(_,_)=>await Execute();
        Write("result.json",Result());
    }
    static T Field<T>(object target,string name)=>(T)(target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.GetValue(target)??throw new InvalidDataException("Missing observation field "+name));
    static IEnumerable<Control> Descendants(Control parent){yield return parent;foreach(Control c in parent.Controls)foreach(Control item in Descendants(c))yield return item;}
    void OnAdded(object sender,ControlEventArgs args)
    {
        if(args.Control is not GameHostForm session)return;
        try{
            if(host!=null)throw new InvalidDataException("Multiple sessions");host=session;
            if(host.Visible||host.TopLevel)throw new InvalidDataException("configuration: pre-Shown seam absent");
            if(!Field<string>(host,"historyRoot").Equals(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("root_identity: session root differs");
            pad=Field<ControllerVisualizer>(host,"virtualPad");if(pad.ConsoleModel!=0)throw new InvalidDataException("root_identity: expected GBA pad");
            string cfg=Field<string>(host,"temporaryConfigPath"),arguments=Field<string>(host,"arguments");
            if(!arguments.Contains('"'+cfg+'"',StringComparison.Ordinal)||!arguments.Contains('"'+Path.Combine(root,"roms","diagnostic.gba")+'"',StringComparison.Ordinal))throw new InvalidDataException("configuration: unexpected config/ROM arguments");
            string expectedCore=Path.Combine(root,"PokemonPlayRuntime","Emulators","RetroArch","cores","mgba_libretro.dll");
            if(!arguments.Contains("-L \""+expectedCore+"\"",StringComparison.Ordinal))throw new InvalidDataException("process_identity: launch did not select bundled mGBA core");
            string executable=Path.GetFullPath(Path.Combine(root,Field<string>(host,"launcher")));
            if(!executable.Equals(BundledEmulators.RetroArch(root),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("process_identity: not bundled RetroArch");
            configEvidence=SessionConfig.Instrument(root,cfg,port);
            Set("configuration","passed","pre-Shown whitelist; default starter preserved");Set("root_identity","passed","owned roots, profile and actual session agree");
            Write("launch.json",new{executable,arguments,configEvidence,profile=InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json")),keyboard=LauncherSettings.KeyboardFor(root)});
            if(scenario=="suppressed-a")pad.VirtualChanged+=(_,_)=>{
                if(held==4){bool raw=pad.Actions[4]&&Field<int>(pad,"mouseAction")==4&&pad.Capture;rawNegative|=raw;
                    events.Add(new{kind="negative-seam",time=wall.ElapsedMilliseconds,raw, suppliedBefore=pad.VirtualActions[4],suppliedAfter=false});pad.VirtualActions[4]=false;}
            };
        }catch(Exception error){callbackFailure=error;Set("configuration","failed",error.Message);throw;}
    }
    async Task Execute()
    {
        try{
            Progress("library");Launcher.Navigate("library");await Task.Delay(250);
            GameCard card=Descendants(Launcher).OfType<GameCard>().Single(c=>Field<GameInfo>(c,"game").IsImported&&Path.GetFullPath(Field<GameInfo>(c,"game").RomPath).Equals(Path.Combine(root,"roms","diagnostic.gba"),StringComparison.OrdinalIgnoreCase));
            if(!Field<string>(card,"root").Equals(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("root_identity: card root differs");
            await PreparePlayClick(card);
            events.Add(new{kind="play-ui",point=mousePoint,card=card.AccessibleName});
            Native.MouseAt(mousePoint,true);await Task.Delay(80);Native.MouseAt(mousePoint,false);
            sampler.Start();await Until(()=>host!=null&&child!=null&&hwnd!=IntPtr.Zero,30000,"boot");
            ValidateEmbedding();Set("boot","passed","real visible Library Play started bundled frontend");Set("embedding","passed","owned child ancestry/styles/geometry");
            // Normal UI focus, never private focus method invocation or fake guard.
            var bounds=Native.Bounds(hwnd);mousePoint=new Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);
            Native.MouseAt(mousePoint,true);await Task.Delay(80);Native.MouseAt(mousePoint,false);await Task.Delay(200);
            await Phase("neutral",0,15000);
            await Hold(4);
            GuestMaskMismatch negativeMismatch=null;
            try{await Phase("A-held",1,10000);}
            catch(GuestMaskMismatch error) when(scenario=="suppressed-a"&&error.Expected==1&&error.Observed==0&&error.ValidatedPairs==2&&error.RawGuardValidated){negativeMismatch=error;}
            if(scenario=="suppressed-a"&&negativeMismatch==null)throw new InvalidDataException("negative_validity: actual expected mask mismatch absent");
            await Release();await Phase("A-release",0,10000);keyReleaseProved=true;
            if(scenario=="positive"){
                await Hold(5);await Phase("B-held",2,10000);await Release();await Phase("B-release",0,10000);keyReleaseProved=true;
                Set("negative_validity","not_run","positive scenario");
            }else{
                if(!rawNegative)throw new InvalidDataException("negative_validity: no real A raw event");
                Set("negative_validity","passed","typed actual mask mismatch; raw A held; two advancing pairs; only supplied A suppressed");
            }
            ValidateDriverLogs();Set("focus","passed","exact eligible bridge guard sampled throughout phases");Set("cadence","passed","two fresh paired advancing counters per phase");
            Passed=true;
        }catch(Exception error){
            Passed=false;SetFailure(error);events.Add(new{kind="failure",phase=current,error=error.ToString()});
            if(scenario=="suppressed-a")Set("negative_validity","failed","run invalid: "+error.Message);
        }finally{
            try{if(ring.Count>0)ring[^1].Image.Save(Path.Combine(evidence,"last-desktop.png"));}catch(Exception error){events.Add(new{kind="failure-capture",error=error.Message});}
            try{await Cleanup();}catch(Exception error){Passed=false;Set("cleanup","failed",error.ToString());}
            Write("observations.json",events);Write("result.json",Result());sampler.Stop();
            if(Passed)Launcher.Close();else{host?.Dispose();Launcher.Dispose();Application.ExitThread();}
        }
    }
    async Task PreparePlayClick(GameCard card)
    {
        var ancestors=new List<Control>();for(Control ancestor=card.Parent;ancestor!=null;ancestor=ancestor.Parent)ancestors.Add(ancestor);
        var scrolling=new List<object>();long deadline=wall.ElapsedMilliseconds+2000;
        try{
            foreach(Control ancestor in ancestors){
                if(ancestor is not ScrollableControl scroll||!scroll.AutoScroll)continue;
                if(wall.ElapsedMilliseconds>=deadline)throw new TimeoutException("boot: bounded Play scrolling deadline");
                Point before=scroll.AutoScrollPosition;scroll.ScrollControlIntoView(card);scroll.PerformLayout();
                await Task.Delay(100);
                scrolling.Add(new{type=scroll.GetType().Name,hwnd=scroll.Handle.ToInt64(),before,after=scroll.AutoScrollPosition});
            }
            Launcher.PerformLayout();await Task.Delay(100);
        }finally{
            mousePoint=card.PointToScreen(new Point(card.Width/2,card.Height-32));
            Rectangle desktop=SystemInformation.VirtualScreen;string imageError=null;
            try{using var image=new Bitmap(desktop.Width,desktop.Height);using(var graphics=Graphics.FromImage(image))graphics.CopyFromScreen(desktop.Location,Point.Empty,desktop.Size,CopyPixelOperation.SourceCopy);image.Save(Path.Combine(evidence,"pre-play-desktop.png"));}
            catch(Exception error){imageError=error.Message;}
            IntPtr hit=Native.WindowFromPoint(mousePoint);Native.GetWindowThreadProcessId(hit,out uint hitPid);
            var clips=ancestors.Select(control=>new{type=control.GetType().Name,hwnd=control.Handle.ToInt64(),visible=control.Visible,enabled=control.Enabled,clientClip=control.RectangleToScreen(control.ClientRectangle),containsPoint=control.RectangleToScreen(control.ClientRectangle).Contains(mousePoint)}).ToArray();
            bool screenContains=Screen.AllScreens.Any(screen=>screen.Bounds.Contains(mousePoint));
            bool ownedCardHit=hit!=IntPtr.Zero&&hitPid==Environment.ProcessId&&(hit==card.Handle||Native.IsChild(card.Handle,hit))&&Native.GetAncestor(hit,2)==Launcher.Handle;
            Write("pre-play-geometry.json",new{point=mousePoint,desktop,screens=Screen.AllScreens.Select(screen=>new{screen.Bounds,screen.WorkingArea}),cardHwnd=card.Handle.ToInt64(),cardClip=card.RectangleToScreen(card.ClientRectangle),card.Visible,card.Enabled,scrolling,clips,hitHwnd=hit.ToInt64(),hitPid,rootHwnd=Native.GetAncestor(hit,2).ToInt64(),ownedCardHit,screenContains,imageError});
            if(imageError!=null)throw new InvalidDataException("boot: pre-Play desktop evidence capture failed: "+imageError);
            if(!card.Visible||!card.Enabled||!card.RectangleToScreen(card.ClientRectangle).Contains(mousePoint)||!screenContains||clips.Any(clip=>!clip.visible||!clip.enabled||!clip.containsPoint)||!ownedCardHit)
                throw new InvalidDataException("boot: Play point is clipped/offscreen or native hit does not reach owned card; inspect pre-play geometry/desktop");
        }
    }
    void Sample()
    {
        try{
            if(host!=null&&child==null){
                child=Field<Process>(host,"emulator"); // Process may still be null between ControlAdded and Shown.
                childPath=child.MainModule.FileName;childStart=child.StartTime.ToUniversalTime();SessionConfig.Owned(root,childPath);
                if(!childPath.Equals(BundledEmulators.RetroArch(root),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("process_identity: executable mismatch");
                if(!child.StartInfo.UseShellExecute||!Path.GetFullPath(child.StartInfo.FileName).Equals(childPath,StringComparison.OrdinalIgnoreCase)||child.StartInfo.Arguments!=Field<string>(host,"arguments")||!child.StartInfo.WorkingDirectory.Equals(Path.GetDirectoryName(childPath),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("process_identity: production launch contract changed");
                identity=new{root,runId=Path.GetFileName(root),hashes,pid=child.Id,startUtc=childStart.ToString("O"),path=childPath,sha256=SessionConfig.Hash(childPath)};
                Write("child-identity.json",identity);Set("process_identity","passed","PID/start/executable from production Process.Start");
                events.Add(new{kind="process-start-info",child.StartInfo.FileName,child.StartInfo.Arguments,child.StartInfo.WorkingDirectory,child.StartInfo.UseShellExecute});
                Write("hashes.json",new{commit,harness=SessionConfig.Hash(Environment.ProcessPath),app=SessionConfig.Hash(typeof(LauncherForm).Assembly.Location),retroarch=SessionConfig.Hash(childPath),core=SessionConfig.Hash(Path.Combine(Path.GetDirectoryName(childPath),"cores","mgba_libretro.dll")),rom=SessionConfig.Hash(Path.Combine(root,"roms","diagnostic.gba"))});
            }
            if(host==null||child==null)return;
            hwnd=Field<IntPtr>(host,"embeddedWindowHandle");if(hwnd==IntPtr.Zero)return;
            ValidateEmbedding();var focus=Focus();events.Add(new{kind="focus-sample",timeMs=wall.ElapsedMilliseconds,phase=current,focus,held,raw=held>=0?pad.Actions[held]:false,supplied=held>=0?pad.VirtualActions[held]:false,capture=pad.Capture,mouseAction=Field<int>(pad,"mouseAction"),childHwnd=hwnd.ToInt64(),panelHwnd=Field<Panel>(host,"gamePanel").Handle.ToInt64(),hostHwnd=host.Handle.ToInt64(),launcherHwnd=Launcher.Handle.ToInt64(),style=Native.GetWindowLong(hwnd,-16)});
            if(held>=0){
                if(!pad.Capture||!pad.Actions[held]||Field<int>(pad,"mouseAction")!=held)throw new InvalidDataException("focus: raw hold/capture lost");
                if(!focus.guard)throw new InvalidDataException("focus: exact bridge guard false during virtual hold");
            }
            var bounds=Native.Bounds(hwnd);using var screenshot=new Bitmap(bounds.Width,bounds.Height);
            using(var g=Graphics.FromImage(screenshot))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);
            try{var decoded=GuestDecoder.Decode(screenshot,true);ring.Add((wall.ElapsedMilliseconds,decoded,(Bitmap)screenshot.Clone()));}
            catch(InvalidDataException error){string undecoded=Path.Combine(evidence,current+"-undecoded-desktop.png");if(!File.Exists(undecoded)){screenshot.Save(undecoded);events.Add(new{kind="desktop-decode",phase=current,error=error.Message,path=undecoded});}}
            while(ring.Count>0&&wall.ElapsedMilliseconds-ring[0].Time>2000){ring[0].Image.Dispose();ring.RemoveAt(0);}
            Write("progress.json",new{schema=1,scenario,phase=current,timeMs=wall.ElapsedMilliseconds,identity,focus,checks});
            if(wall.ElapsedMilliseconds-lastEvidencePersist>=1000){Write("observations.json",events);lastEvidencePersist=wall.ElapsedMilliseconds;}
        }catch(InvalidDataException error){if(host!=null&&child==null&&error.Message.StartsWith("Missing observation field emulator"))return;sampleFailure=error;}
        catch(Exception error){sampleFailure=error;}
    }
    internal readonly record struct FocusState(uint foregroundPid,bool activeHost,bool containsFocus,bool guard,string activeForm,long foregroundHwnd,object nativeFocus);
    FocusState Focus()
    {
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out uint pid);
        bool active=Form.ActiveForm==host,contains=host.ContainsFocus;return new(pid,active,contains,pid==child.Id||(active&&contains),Form.ActiveForm?.GetType().Name??"none",Native.GetForegroundWindow().ToInt64(),Native.FocusEvidence(Native.GetForegroundWindow()));
    }
    void ValidateEmbedding()
    {
        Native.GetWindowThreadProcessId(hwnd,out uint pid);
        var panel=Field<Panel>(host,"gamePanel");
        if(pid!=child.Id||Native.GetParent(hwnd)!=panel.Handle||(Native.GetWindowLong(hwnd,-16)&0x40000000)==0||!Native.IsWindowVisible(hwnd)||host.TopLevel||host.FindForm()!=host||!Descendants(Launcher).Contains(host))throw new InvalidDataException("embedding: identity/ancestry/style invalid");
        IntPtr ancestor=panel.Handle;bool reached=false;for(int depth=0;depth<20&&ancestor!=IntPtr.Zero;depth++){if(ancestor==Launcher.Handle){reached=true;break;}ancestor=Native.GetParent(ancestor);}
        if(!reached)throw new InvalidDataException("embedding: native HWND ancestry does not reach real launcher");
        var childBounds=Native.Bounds(hwnd);var panelBounds=panel.RectangleToScreen(panel.ClientRectangle);
        if(childBounds.Width<=0||childBounds.Height<=0||!panelBounds.Contains(childBounds)||Launcher.WindowState==FormWindowState.Minimized)throw new InvalidDataException("embedding: geometry/visibility invalid");
    }
    async Task Hold(int action)
    {
        keyReleaseProved=false;held=action;float scale=Math.Min(pad.Width/520f,(pad.Height-26)/300f);float x=action==4?426:390,y=action==4?128:156;
        var point=new Point((int)((pad.Width-520*scale)/2+x*scale),(int)((pad.Height-26-300*scale)/2+y*scale));
        int hit=(int)typeof(ControllerVisualizer).GetMethod("Hit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pad,new object[]{point});if(hit!=action)throw new InvalidDataException("focus: virtual hit transform differs");
        mousePoint=pad.PointToScreen(point);events.Add(new{kind="pad-ui-down",action,point=mousePoint});Native.MouseAt(mousePoint,true);await Task.Delay(150);CheckFailures();
    }
    async Task Release(){Native.MouseAt(mousePoint,false);held=-1;await Task.Delay(150);CheckFailures();if(pad.VirtualActions.Any(x=>x)||pad.Actions.Any(x=>x))throw new InvalidDataException("guest_release_color: virtual release absent");}
    async Task Phase(string name,int expected,int timeout)
    {
        Progress(name);phaseStarted=wall.ElapsedMilliseconds;long deadline=wall.ElapsedMilliseconds+timeout;int accepted=0;GuestMaskMismatch mismatch=null;bool rawGuard=true;
        // Baseline previous observation, including initial phase bootstrap.
        while(accepted<2){
            CheckFailures();if(wall.ElapsedMilliseconds>deadline)throw new TimeoutException("frame_missing_or_stale: phase deadline");
            if(wall.ElapsedMilliseconds-lastRequest<1100)await Task.Delay((int)(1100-(wall.ElapsedMilliseconds-lastRequest)));
            if(!Focus().guard)throw new InvalidDataException("focus: exact bridge guard false");
            var pair=await Capture(name,deadline);int observed=pair.Internal.Mask;
            if(pair.Desktop.Mask!=observed)throw new InvalidDataException("guest_hold_color: internal/desktop disagreement");
            events.Add(new{kind="guest-mask-comparison",phase=name,expected,observed,status=observed==expected?"passed":"failed"});
            if(observed!=expected){
                var actual=new GuestMaskMismatch(expected,observed,0,false);
                if(scenario!="suppressed-a"||name!="A-held"||expected!=1||observed!=0)throw actual;
                mismatch=actual;
            }else if(mismatch!=null||scenario=="suppressed-a"&&name=="A-held")throw new InvalidDataException("negative_validity: suppression did not consistently produce actual mask mismatch");
            rawGuard&=held==4&&pad.Actions[4]&&Field<int>(pad,"mouseAction")==4&&pad.Capture&&!pad.VirtualActions[4]&&rawNegative&&Focus().guard;
            if(internalCounter.HasValue&&desktopCounter.HasValue&&GuestDecoder.Advances(internalCounter.Value,pair.Internal.Counter)&&GuestDecoder.Advances(desktopCounter.Value,pair.Desktop.Counter))accepted++;
            else if(internalCounter.HasValue)throw new InvalidDataException("frame_missing_or_stale: counter not advancing");
            internalCounter=pair.Internal.Counter;desktopCounter=pair.Desktop.Counter;
        }
        if(mismatch!=null){
            var actual=new GuestMaskMismatch(expected,mismatch.Observed,accepted,rawGuard);
            Set("guest_hold_color","failed",actual.Message);Set(name,"failed",actual.Message);
            events.Add(new{kind="validated-mask-mismatch",expected=actual.Expected,observed=actual.Observed,validatedPairs=actual.ValidatedPairs,rawGuardValidated=actual.RawGuardValidated});throw actual;
        }
        Set(name,"passed","two advancing internal+desktop pairs, full ten-bit mask verified");
    }
    async Task<(GuestFrame Internal,GuestFrame Desktop)> Capture(string phase,long deadline)
    {
        Progress(phase);object endpoint=Native.UdpOwner(port,child.Id);Set("capture_identity","passed","UDP endpoint belongs to spawned identity");
        if(child.StartTime.ToUniversalTime()!=childStart||child.HasExited)throw new InvalidDataException("process_identity: lost owned process");
        string inbox=Path.Combine(root,"captures","inbox");if(Directory.EnumerateFiles(inbox).Any())throw new InvalidDataException("capture_ambiguous: inbox not empty");
        string request=Guid.NewGuid().ToString("N");long sent=wall.ElapsedMilliseconds;lastRequest=sent;
        using(var udp=new UdpClient()){byte[] data=System.Text.Encoding.ASCII.GetBytes("SCREENSHOT\n");await udp.SendAsync(data,new IPEndPoint(IPAddress.Loopback,port));}
        string path=null;GuestFrame frame=default;long arrival=0;
        while(wall.ElapsedMilliseconds<deadline){
            CheckFailures();var files=Directory.GetFiles(inbox);if(files.Length>1)throw new InvalidDataException("capture_ambiguous: multiple arrivals");
            if(files.Length==1){
                try{using var stream=new FileStream(files[0],FileMode.Open,FileAccess.Read,FileShare.None);using var image=new Bitmap(stream);frame=GuestDecoder.Decode(image,false);path=files[0];arrival=wall.ElapsedMilliseconds;break;}
                catch(IOException){}catch(ArgumentException){}
            }
            await Task.Delay(50);
        }
        if(path==null)throw new TimeoutException("capture_timeout: run invalidated; no retry");
        var matches=ring.Where(r=>r.Time>=phaseStarted&&Math.Abs(arrival-r.Time)<=500&&GuestDecoder.Distance(frame.Counter,r.Frame.Counter)<=12&&r.Frame.Mask==frame.Mask).OrderBy(r=>GuestDecoder.Distance(frame.Counter,r.Frame.Counter)).ToArray();
        if(matches.Length==0)throw new InvalidDataException("frame_missing_or_stale: no composed pair within 12 frames/500ms");
        var match=matches[0];string prefix=phase+"-"+request;
        File.Move(path,Path.Combine(evidence,prefix+"-internal.png"));match.Image.Save(Path.Combine(evidence,prefix+"-desktop.png"));
        events.Add(new{kind="capture",phase,request,sentMs=sent,arrivalMs=arrival,desktopMs=match.Time,internalFrame=frame,desktopFrame=match.Frame,endpoint,childBounds=Native.Bounds(hwnd),dpi=Native.GetDpiForWindow(hwnd),focus=Focus(),internalPng=prefix+"-internal.png",desktopPng=prefix+"-desktop.png"});
        return(frame,match.Frame);
    }
    void ValidateDriverLogs()
    {
        var paths=Directory.GetFiles(Path.Combine(evidence,"retroarch-log"),"*",SearchOption.AllDirectories);string log=string.Join("\n",paths.Select(File.ReadAllText));
        // Pinned 69a4f0ea gfx/drivers/gdi_gfx.c: post-init success, unlike generic selection messages.
        var video=log.Split('\n').Where(l=>Regex.IsMatch(l,@"^(?:\[INFO\] )?\[GDI\] Init complete\.\s*$")).ToArray();
        events.Add(new{kind="driver-evidence",video,logs=paths,source="69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576",inputProof="no verified post-init keyboard-driver success marker"});
        Set("default_driver","not_run","generic driver selection lines do not establish effective initialization");
        if(!log.Contains("diagnostic-core-options.cfg",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("configuration: effective owned core options evidence absent");
        throw new EvidenceUnavailable("verified post-init input driver evidence unavailable; no diagnostic fallback");
    }
    async Task Cleanup()
    {
        Progress("cleanup");if(pad!=null){try{Native.MouseAt(mousePoint,false);}catch{}held=-1;pad.ReleaseVirtual();}
        if(child!=null&&!child.HasExited){
            if(child.StartTime.ToUniversalTime()!=childStart||!child.MainModule.FileName.Equals(childPath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("cleanup: process identity changed");
            using var exitObservation=Process.GetProcessById(child.Id);
            sampler.Stop();
            if(host!=null&&!host.IsDisposed){
                using var confirm=new System.Windows.Forms.Timer{Interval=100};
                confirm.Tick+=(_,_)=>{try{if(Native.ClickOwnedCloseConfirmation()){events.Add(new{kind="cleanup-confirmation-ui",timeMs=wall.ElapsedMilliseconds});confirm.Stop();}}catch(Exception error){events.Add(new{kind="cleanup-confirmation-failed",error=error.Message});confirm.Stop();}};
                var completion=new TaskCompletionSource<bool>();confirm.Start();
                host.BeginInvoke(async()=>{try{completion.TrySetResult(await host.RequestCloseAsync());}catch(Exception error){completion.TrySetException(error);}});
                bool closed=await completion.Task.WaitAsync(TimeSpan.FromSeconds(7));
                if(!closed)throw new IOException("cleanup: host declined bounded close");
            }
            await exitObservation.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        }
        if(child==null&&host!=null)throw new InvalidDataException("cleanup: child identity unavailable, supervisor must reconcile");
        if(!keyReleaseProved&&child!=null){Set("cleanup","failed","owned process exited; guest key-up not proven");Passed=false;}
        else Set("cleanup","passed",child==null?"no owned process started":"owned process exited; release observed before close");
    }
    async Task Until(Func<bool> predicate,int timeout,string key){long deadline=wall.ElapsedMilliseconds+timeout;while(!predicate()){CheckFailures();if(wall.ElapsedMilliseconds>deadline)throw new TimeoutException(key+": deadline");await Task.Delay(50);}}
    void CheckFailures(){if(callbackFailure!=null)throw callbackFailure;if(sampleFailure!=null)throw sampleFailure;if(wall.ElapsedMilliseconds>110000)throw new TimeoutException("internal wall budget exhausted");}
    void SetFailure(Exception error){
        string prefix=error.Message.Split(':')[0];string key=checks.ContainsKey(prefix)?prefix:prefix switch{
            "guest_release_color"=>current=="B-release"?"B-release":"A-release",
            "frame_missing_or_stale"=>"cadence", "capture_timeout" or "capture_ambiguous"=>"capture_identity",
            _=>checks.ContainsKey(current)?current:"boot"};
        Set(key,error is EvidenceUnavailable?"not_run":"failed",error.Message);
    }
    void Set(string key,string status,string reason)=>checks[key]=new{status,reason};
    void Progress(string phase){current=phase;Write("progress.json",new{schema=1,scenario,phase,timeMs=wall.ElapsedMilliseconds,identity,checks});Write("result.json",Result());}
    object Result()=>new{schema=1,scenario,commit,passed=Passed,checks,identity,config=configEvidence,evidence=Directory.GetFiles(evidence,"*",SearchOption.AllDirectories).Where(path=>!path.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(path) is not ("result.json" or "progress.json")).Select(path=>Path.GetRelativePath(evidence,path)).OrderBy(path=>path,StringComparer.Ordinal).ToArray()};
    void Write(string name,object value){string path=Path.Combine(evidence,name);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,JsonOptions));File.Move(path+".tmp",path,true);}
    public void Dispose(){sampler.Dispose();foreach(var item in ring)item.Image.Dispose();}
}

internal sealed class GuestMaskMismatch:Exception
{
    internal int Expected{get;} internal int Observed{get;} internal int ValidatedPairs{get;} internal bool RawGuardValidated{get;}
    internal GuestMaskMismatch(int expected,int observed,int pairs,bool guard):base($"guest_hold_color: expected {expected}, observed {observed}; validated pairs {pairs}; raw guard {guard}"){Expected=expected;Observed=observed;ValidatedPairs=pairs;RawGuardValidated=guard;}
}
internal sealed class EvidenceUnavailable:Exception
{
    internal EvidenceUnavailable(string reason):base("default_driver: "+reason){}
}
