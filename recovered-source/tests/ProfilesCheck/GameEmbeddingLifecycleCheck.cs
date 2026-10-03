using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class GameEmbeddingLifecycleCheck
{
    record Identity(int Pid, long StartUtcTicks, string Executable, long Hwnd);
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static object Field(object host, string name) => host.GetType().GetField(name, Flags).GetValue(host);
    static object Invoke(object host, string name, params object[] args) => host.GetType().GetMethod(name, Flags).Invoke(host, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread, WindowCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    // Child mode is an actual GUI process, without game content or emulator input.
    internal static void Child(string directory)
    {
        directory = Path.GetFullPath(directory);
        Require(File.Exists(Path.Combine(directory, "fixture-owned")), "Child requires fixture ownership marker");
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using var form = new Form { Text = "PokemonPlay owned lifecycle child", Width = 320, Height = 200 };
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        var deadline = Stopwatch.StartNew();
        form.Shown += (_, _) => {
            using var self = Process.GetCurrentProcess();
            File.WriteAllText(Path.Combine(directory,"ready.json"), JsonSerializer.Serialize(new Identity(self.Id,self.StartTime.ToUniversalTime().Ticks,self.MainModule.FileName,form.Handle.ToInt64())));
            timer.Start();
        };
        form.FormClosed += (_, _) => File.WriteAllText(Path.Combine(directory,"closed"), "normal WM_CLOSE/form close");
        timer.Tick += (_, _) => {
            if (File.Exists(Path.Combine(directory,"close-request"))) form.Close();
            else if (deadline.Elapsed > TimeSpan.FromSeconds(90)) { File.WriteAllText(Path.Combine(directory,"child-timeout"), "failed"); form.Close(); }
        };
        Application.Run(form);
    }
    static void PumpUntil(Func<bool> condition, int milliseconds, string reason)
    {
        var clock=Stopwatch.StartNew();
        while(!condition()) { if(clock.ElapsedMilliseconds>milliseconds) throw new TimeoutException(reason); Application.DoEvents(); System.Threading.Thread.Sleep(10); }
    }
    static Identity ReadIdentity(string directory)
    {
        PumpUntil(()=>File.Exists(Path.Combine(directory,"ready.json")),8000,"Child did not become ready");
        return JsonSerializer.Deserialize<Identity>(File.ReadAllText(Path.Combine(directory,"ready.json")));
    }
    static bool Matches(Process process, Identity identity)
    {
        try { return !process.HasExited && process.Id==identity.Pid && process.StartTime.ToUniversalTime().Ticks==identity.StartUtcTicks && string.Equals(process.MainModule.FileName,identity.Executable,StringComparison.OrdinalIgnoreCase); }
        catch(InvalidOperationException) { return false; }
    }
    static void CheckIdentity(Process process, Identity identity, string executable)
    {
        Require(string.Equals(identity.Executable,executable,StringComparison.OrdinalIgnoreCase)&&Matches(process,identity),"PID/start-time/path ownership mismatch");
        GetWindowThreadProcessId(new IntPtr(identity.Hwnd),out uint windowPid);
        Require(windowPid==identity.Pid,"Ready HWND does not belong to owned PID");
    }
    static bool CloseWithAnswer(Form host, bool yes, out int answered, Action registeredClose=null)
    {
        int count=0; Exception callbackError=null; var clock=Stopwatch.StartNew(); uint thread=GetCurrentThreadId();
        using var responder=new System.Windows.Forms.Timer { Interval=25 };
        responder.Tick += (_,_) => {
            EnumThreadWindows(thread,(window,_)=> {
                GetWindowThreadProcessId(window,out uint pid);
                var kind=new StringBuilder(64);var title=new StringBuilder(128);GetClassName(window,kind,kind.Capacity);GetWindowText(window,title,title.Capacity);
                if(pid!=Environment.ProcessId||kind.ToString()!="#32770") return true;
                if(title.ToString()!="Voltar ao menu") {
                    callbackError=new Exception("Unexpected owned modal: "+title);PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);return true;
                }
                if(clock.ElapsedMilliseconds>6000) { callbackError ??= new TimeoutException("Confirmation exceeded bounded callback deadline");PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);return true; }
                if(count==0) { count++; PostMessage(window,0x0111,new IntPtr(yes?6:7),IntPtr.Zero); }
                return true;
            },IntPtr.Zero);
            if(clock.ElapsedMilliseconds>6000) callbackError ??= new TimeoutException("Confirmation exceeded bounded callback deadline");
        };
        responder.Start();
        Task<bool> task=null;
        if(registeredClose==null) task=(Task<bool>)Invoke(host,"RequestCloseAsync"); else registeredClose();
        PumpUntil(()=>task!=null?task.IsCompleted:host.IsDisposed,22000,"Host close exceeded deadline");
        responder.Stop();answered=count;
        if(callbackError!=null) throw callbackError;
        return task==null?host.IsDisposed:task.GetAwaiter().GetResult();
    }
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        => root.Controls.Cast<Control>().SelectMany(control=>new[]{control}.Concat(Descendants(control)));
    static void WaitForActualStartup(Form host,string directory)
    {
        var clock=Stopwatch.StartNew();Exception timeout=null;
        try { PumpUntil(()=>Field(host,"emulator")!=null||(bool)Field(host,"launchFailed"),8000,"Queued Shown/default starter did not reach process or failure state"); }
        catch(TimeoutException error) { timeout=error; }
        finally {
            (Field(host,"timer") as System.Windows.Forms.Timer)?.Stop();
            var process=(Process)Field(host,"emulator");
            File.WriteAllText(Path.Combine(directory,"startup-facts.json"),JsonSerializer.Serialize(new {
                elapsedMs=clock.ElapsedMilliseconds,host.Visible,host.IsHandleCreated,host.IsDisposed,
                launchFailed=(bool)Field(host,"launchFailed"),hasStartedProcess=process!=null,
                launcher=(string)Field(host,"launcher"),processName=(string)Field(host,"processName"),
                failureTexts=Descendants((Control)Field(host,"gamePanel")).Select(control=>control.Text).Where(text=>!string.IsNullOrWhiteSpace(text)).ToArray(),
                timeout=timeout?.Message
            },new JsonSerializerOptions {WriteIndented=true}));
        }
        if(timeout!=null) throw timeout;
    }
    static Form NewHost(string executable,string directory,string processName=null)
    {
        var host=new GameHostForm(executable,processName??Path.GetFileNameWithoutExtension(executable),"--embedding-lifecycle-child \""+directory+"\"","Synthetic lifecycle","Fixture",null);
        host.Show();
        try { WaitForActualStartup(host,directory); } catch {host.Dispose();throw;}
        return host;
    }
    static void Fail(Form host) => Invoke(host,"ShowLaunchFailure",new InvalidOperationException("Owned synthetic embedding failure"));
    static void Emergency(Process process, Identity identity, string directory)
    {
        if(process==null||process.HasExited) return;
        // Exact identity only: emergency cleanup never constitutes a successful test.
        Require(identity!=null&&Matches(process,identity),"Emergency cleanup refused unverified identity");
        File.WriteAllText(Path.Combine(directory,"emergency-cleanup-failed"),"PID="+identity.Pid);
        process.Kill();Require(process.WaitForExit(5000),"Owned emergency cleanup failed");
        throw new Exception("Emergency cleanup required; lifecycle test FAILED");
    }
    static void LiveCase(string executable,string directory,bool cancelFirst)
    {
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"fixture-owned"),"owned");
        Form host=null;Process observer=null;Identity identity=null;
        try {
            host=NewHost(executable,directory);
            var launched=(Process)Field(host,"emulator");Require(launched!=null,"Default host starter did not create child");
            // Capture independent identity before host.Dispose can dispose its Process.
            observer=Process.GetProcessById(launched.Id);
            identity=new Identity(observer.Id,observer.StartTime.ToUniversalTime().Ticks,observer.MainModule.FileName,0);
            File.WriteAllText(Path.Combine(directory,"parent-owned.json"),JsonSerializer.Serialize(identity));
            var ready=ReadIdentity(directory);CheckIdentity(observer,ready,executable);identity=ready;Fail(host);
            if(cancelFirst) {
                Require(!CloseWithAnswer(host,false,out int denied)&&denied==1,"No did not cancel actual modal close");
                Require(Matches(observer,identity)&&!host.IsDisposed&&(bool)Field(host,"launchFailed"),"Cancel lost failed host or live child");
                Require(!((System.Windows.Forms.Timer)Field(host,"timer")).Enabled,"Failed host timer restarted after cancel");
                object trace=host.GetType().GetProperty("LastEmbeddingAttempt",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(host);
                for(int i=0;i<4;i++) { Invoke(host,"TryEmbed"); Application.DoEvents(); }
                Require(ReferenceEquals(trace,host.GetType().GetProperty("LastEmbeddingAttempt",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(host))&&Field(host,"inputBridge")==null&&!(bool)Field(host,"emulatorEmbedded"),"Failed host resumed embedding/bridge");
                Console.WriteLine("PASS embedding lifecycle No preserves failed live child with stopped timer");
            }
            Require(CloseWithAnswer(host,true,out int accepted)&&accepted==1,"Yes did not close through real confirmation");
            Require(observer.WaitForExit(2000)&&File.Exists(Path.Combine(directory,"closed"))&&!File.Exists(Path.Combine(directory,"child-timeout")),"Host returned success before normal owned child exit");
            Console.WriteLine("PASS embedding lifecycle Yes confirms normal owned PID exit"+(cancelFirst?" after No":""));
        } finally {
            try { Emergency(observer,identity,directory); } finally { host?.Dispose();observer?.Dispose(); }
        }
    }
    static void InvalidStart(string executable,string directory)
    {
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"fixture-owned"),"owned");
        using var independent=Process.Start(new ProcessStartInfo(executable,"--embedding-lifecycle-child \""+directory+"\""){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(executable)});
        Identity identity=new(independent.Id,independent.StartTime.ToUniversalTime().Ticks,independent.MainModule.FileName,0);
        File.WriteAllText(Path.Combine(directory,"parent-owned.json"),JsonSerializer.Serialize(identity));
        Form host=null;
        try {
            identity=ReadIdentity(directory);CheckIdentity(independent,identity,executable);
            host=NewHost(Path.Combine(Path.GetDirectoryName(executable),"missing-owned.exe"),directory,Path.GetFileNameWithoutExtension(executable));

            Require((bool)Field(host,"launchFailed")&&Field(host,"emulator")==null,"Invalid starter did not reach actual no-child failure");
            var clock=Stopwatch.StartNew();Require(CloseWithAnswer(host,true,out int dialogs)&&dialogs==0&&clock.ElapsedMilliseconds<2000,"No-child failed host did not close promptly without modal");
            Require(Matches(independent,identity)&&!File.Exists(Path.Combine(directory,"closed"))&&Field(host,"emulator")==null&&Field(host,"inputBridge")==null,"No-child close adopted or affected independent helper");
            File.WriteAllText(Path.Combine(directory,"close-request"),"normal owned protocol");Require(independent.WaitForExit(5000)&&File.Exists(Path.Combine(directory,"closed"))&&!File.Exists(Path.Combine(directory,"child-timeout")),"Independent helper normal cleanup failed");
            Console.WriteLine("PASS embedding lifecycle failed start does not adopt same-name independent child");
        } finally { try { Emergency(independent,identity,directory); } finally {host?.Dispose();} }
    }
    internal static string PrepareSyntheticExecutable(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"fixture-owned"),"owned synthetic GUI fixture");
        string runtime=Path.Combine(directory,"owned runtime espaços");Directory.CreateDirectory(runtime);
        foreach(string file in Directory.GetFiles(AppContext.BaseDirectory)) File.Copy(file,Path.Combine(runtime,Path.GetFileName(file)),true);
        string executable=Path.Combine(runtime,Path.GetFileName(Environment.ProcessPath));
        Require(Path.GetExtension(executable).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(executable),"Synthetic child requires built Check.exe apphost");
        return executable;
    }
    internal sealed class AttachedChild : IDisposable
    {
        readonly string directory;
        readonly Process observer;
        readonly Identity identity;
        internal AttachedChild(Form host,string directory,string executable)
        {
            this.directory=directory;
            WaitForActualStartup(host,directory);
            Require(!(bool)Field(host,"launchFailed"),"Synthetic active session unexpectedly failed startup");
            var launched=(Process)Field(host,"emulator");Require(launched!=null,"Synthetic session requires default started process");
            observer=Process.GetProcessById(launched.Id);
            identity=new Identity(observer.Id,observer.StartTime.ToUniversalTime().Ticks,observer.MainModule.FileName,0);
            File.WriteAllText(Path.Combine(directory,"parent-owned.json"),JsonSerializer.Serialize(identity));
            try { var ready=ReadIdentity(directory);CheckIdentity(observer,ready,executable);identity=ready; }
            catch { try { Emergency(observer,identity,directory); } finally {observer.Dispose();} throw; }
        }
        internal void ConfirmRegisteredClose(Form host,Action close)
        {
            Require(CloseWithAnswer(host,true,out int answered,close)&&answered==1,"Synthetic registered session did not use actual Yes close confirmation");
            Require(observer.WaitForExit(2000)&&File.Exists(Path.Combine(directory,"closed"))&&!File.Exists(Path.Combine(directory,"child-timeout")),"Registered synthetic session closed without normal owned PID exit");
        }
        public void Dispose()
        {
            try {
                if(!observer.HasExited) {
                    Require(Matches(observer,identity),"Synthetic cleanup refused mismatched process identity");
                    File.WriteAllText(Path.Combine(directory,"close-request"),"normal owned protocol");
                    if(!observer.WaitForExit(5000)) Emergency(observer,identity,directory);
                }
                Require(File.Exists(Path.Combine(directory,"closed"))&&!File.Exists(Path.Combine(directory,"child-timeout")),"Synthetic child lacks normal close sentinel");
            } finally {observer.Dispose();}
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll",SetLastError=true)] static extern bool GetWindowRect(IntPtr window,out NativeRect rectangle);
    static System.Drawing.Rectangle NativeBounds(Control control)
    {
        Require(GetWindowRect(control.Handle,out var rect),"Cannot observe native layout bounds");
        return System.Drawing.Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
    }
    internal static void LayoutChild(string directory)
    {
        directory=Path.GetFullPath(directory);
        string expected=Path.Combine(directory,"owned runtime espaços");
        Require(File.Exists(Path.Combine(directory,"fixture-owned"))&&string.Equals(Path.GetFullPath(AppPaths.Root),expected,StringComparison.OrdinalIgnoreCase)&&string.Equals(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),expected,StringComparison.OrdinalIgnoreCase),"Layout child must run inside its copied owned runtime; no preference redirection");
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string profile=Path.Combine(AppPaths.Root,"Settings","input-device.json");
        Require(!File.Exists(profile),"Layout fixture settings must be fresh");
        var failures=new System.Collections.Generic.List<string>();
        foreach(int mode in new[]{0,1,2,3}) foreach(var size in new[]{new System.Drawing.Size(1024,720),new System.Drawing.Size(1280,900)}) {
            var name=$"mode{mode}-{size.Width}x{size.Height}";
            try {
                new InputDeviceProfile {Mode=mode}.Save(profile);
                Require(InputDeviceProfile.Load(profile).Mode==mode,"Actual saved mode did not round-trip");
                using var host=new GameHostForm(Path.Combine(expected,"never-launched.exe"),"synthetic-layout",string.Empty);
                host.ClientSize=size;host.PerformLayout();
                // Native handles without Show: no process launch, guest, input, or visibility claim.
                _=host.Handle;
                var game=(Control)Field(host,"gamePanel");var pad=(Control)Field(host,"virtualPad");
                var toolbar=host.Controls.Cast<Control>().Single(control=>control.Dock==DockStyle.Top);
                foreach(Control control in host.Controls) _=control.Handle;
                host.PerformLayout();
                var client=host.RectangleToScreen(host.ClientRectangle);
                var gameNative=NativeBounds(game);var toolbarNative=NativeBounds(toolbar);
                System.Drawing.Rectangle? padNative=pad==null?null:NativeBounds(pad);
                File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(new {mode,requestedWidth=size.Width,requestedHeight=size.Height,actualWidth=host.ClientSize.Width,actualHeight=host.ClientSize.Height,client,gameManaged=game.Bounds,toolbarManaged=toolbar.Bounds,padManaged=pad?.Bounds,gameNative,toolbarNative,padNative},new JsonSerializerOptions{WriteIndented=true}));
                Require(host.ClientSize.Width>0&&host.ClientSize.Height>0,"Actual client dimensions are empty");
                Require((pad!=null)==(mode==3),"Virtual pad presence does not match actual profile mode");
                var controls=pad==null?new[]{game,toolbar}:new[]{game,toolbar,pad};
                foreach(Control control in controls) {
                    Require(host.ClientRectangle.Contains(control.Bounds)&&control.Width>0&&control.Height>0,"Managed region outside host or empty");
                    var native=NativeBounds(control);Require(client.Contains(native),"Native region outside host client");
                    Require(native==host.RectangleToScreen(control.Bounds),"Managed/native layout regions disagree");
                }
                for(int i=0;i<controls.Length;i++) for(int j=i+1;j<controls.Length;j++) {
                    Require(!controls[i].Bounds.IntersectsWith(controls[j].Bounds),"Managed game/toolbar/pad regions overlap");
                    Require(!NativeBounds(controls[i]).IntersectsWith(NativeBounds(controls[j])),"Native game/toolbar/pad regions overlap");
                }
                Require(game.Height==size.Height-toolbar.Height-(pad?.Height??0),"Game panel does not reserve toolbar/pad height");
                Require(Field(host,"emulator")==null&&Field(host,"inputBridge")==null,"NoShow layout case unexpectedly launched process or bridge");
                Console.WriteLine("PASS synthetic managed/native dock regions "+name);
            } catch(Exception error) { failures.Add(name+": "+error.Message);Console.WriteLine("FAIL synthetic managed/native dock regions "+name+": "+error.Message); }
        }
        File.WriteAllText(Path.Combine(directory,"layout-result.json"),JsonSerializer.Serialize(new {cases=8,failures,scope="synthetic noShow host geometry; no ROM/render/input proof"}));
        if(failures.Count>0) throw new Exception(string.Join("; ",failures));
        File.WriteAllText(Path.Combine(directory,"layout-passed"),"8 actual mode/size cases; noShow managed/native geometry");
    }
    internal static void RunLayout(string directory)
    {
        directory=Path.GetFullPath(directory);Require(!Directory.Exists(directory)||!Directory.EnumerateFileSystemEntries(directory).Any(),"Layout root must be new or empty");
        string executable=PrepareSyntheticExecutable(directory);
        using var child=Process.Start(new ProcessStartInfo(executable,"--embedding-layout-child \""+directory+"\""){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(executable),RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true});
        // The short geometry child can finish before MainModule is readable.
        // ProcessStartInfo already identifies the exact owned executable.
        var identity=new Identity(child.Id,child.StartTime.ToUniversalTime().Ticks,executable,0);
        File.WriteAllText(Path.Combine(directory,"parent-owned.json"),JsonSerializer.Serialize(identity));
        var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
        if(!child.WaitForExit(30000)) { Emergency(child,identity,directory);throw new TimeoutException("Owned layout child deadline exceeded"); }
        File.WriteAllText(Path.Combine(directory,"layout-child.stdout.log"),output.GetAwaiter().GetResult());
        File.WriteAllText(Path.Combine(directory,"layout-child.stderr.log"),errors.GetAwaiter().GetResult());
        Console.Write(output.GetAwaiter().GetResult());
        Require(child.ExitCode==0&&File.Exists(Path.Combine(directory,"layout-passed")),"Owned layout child failed; inspect fixture evidence");
    }
    internal static void Run(string root)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        root=Path.GetFullPath(root);Require(!Directory.Exists(root)||!Directory.EnumerateFileSystemEntries(root).Any(),"Lifecycle root must be new or empty");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"fixture-owned"),"owned lifecycle fixture");
        string runtime=Path.Combine(root,"owned runtime espaços" );Directory.CreateDirectory(runtime);
        foreach(string file in Directory.GetFiles(AppContext.BaseDirectory)) File.Copy(file,Path.Combine(runtime,Path.GetFileName(file)),true);
        string executable=Path.Combine(runtime,Path.GetFileName(Environment.ProcessPath));
        Require(Path.GetExtension(executable).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(executable),"Run lifecycle checks through built Check.exe apphost");
        Console.WriteLine("EMBEDDING_LIFECYCLE_ROOT "+root);
        LiveCase(executable,Path.Combine(root,"yes"),false);
        LiveCase(executable,Path.Combine(root,"no-then-yes"),true);
        InvalidStart(executable,Path.Combine(root,"invalid-start"));
        RunLayout(Path.Combine(root,"dock-regions"));
        File.WriteAllText(Path.Combine(root,"passed"),"All lifecycle cases completed with normal owned exits");
        Console.WriteLine("PASS all embedding lifecycle cases");
    }
}
