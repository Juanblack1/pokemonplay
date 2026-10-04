using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class GameInputBridge : IDisposable
{
    private const uint MapVirtualKeyToScanCodeEx=4,KeyEventExtended=1,KeyEventUp=2,KeyEventScanCode=8;
    [StructLayout(LayoutKind.Explicit, Size=40)] private struct KeyboardInput
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint Flags;
    }
    [DllImport("user32.dll", SetLastError=true)] private static extern uint SendInput(uint count, KeyboardInput[] inputs, int size);
    [DllImport("user32.dll", EntryPoint="MapVirtualKeyExW")] private static extern uint MapVirtualKeyEx(uint code,uint mapType,IntPtr keyboardLayout);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    internal sealed record InputFocusTrace(long ForegroundHwnd,uint ForegroundPid,uint ForegroundThread,long CallingThreadFocusHwnd);
    internal sealed record InjectionTrace(long Sequence,long PollSequence,long AtQpc,long QpcFrequency,int VirtualKey,int ScanCode,bool Down,uint Flags,uint InsertedCount,int Win32Error,InputFocusTrace Before,InputFocusTrace After);
    internal sealed record InputPollTrace(long Sequence,long StartedQpc,long CompletedQpc,long QpcFrequency,int Mode,int EmulatorPid,uint ForegroundPid,bool? HostEligible,bool Focused,bool FocusSettling,string KeyMap,string SuppliedActions,string ResolvedActions,string HeldBefore,string HeldAfter,long LastInjectionSequence);
    internal InputPollTrace LastInputTrace { get; private set; }
    internal InjectionTrace LastInjectionTrace { get; private set; }
    private readonly Queue<InjectionTrace> injectionHistory=new();
    internal InjectionTrace[] RecentInjectionAttempts => injectionHistory.ToArray();
    private long pollSequence,injectionSequence;
    private readonly Dictionary<Keys,(ushort ScanCode,bool Extended)> heldScanCodes=new();
    private readonly InputDeviceProfile profile;
    private readonly InputKeyDispatcher dispatcher;
    private bool disposed;
    private readonly Timer timer;
    private readonly Func<int> emulatorPid;
    private readonly Func<bool> hostFocused;
    private readonly Func<bool[]> virtualActions;
    private readonly Func<bool> virtualActionHeld;
    private readonly Func<bool> emulatorTopLevel;
    private readonly Action focusGame;
    public GameInputBridge(string root, Func<int> emulatorPid, Func<bool> hostFocused, Func<bool[]> virtualActions, Func<bool> virtualActionHeld, Action focusGame, Func<bool> emulatorTopLevel)
    {
        this.emulatorPid=emulatorPid;this.hostFocused=hostFocused;this.virtualActions=virtualActions;this.virtualActionHeld=virtualActionHeld;this.emulatorTopLevel=emulatorTopLevel;this.focusGame=focusGame;
        profile=InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json"));
        var custom=new[] { "W","S","A","D","Z","X","Q","E","Enter","Backspace" };int preset=1;
        var file=Path.Combine(root,"Settings","input-presets.txt");
        if(File.Exists(file)){var lines=File.ReadAllLines(file);if(lines.Length>0&&int.TryParse(lines[0],out var p)&&p>=0&&p<6)preset=p;for(int i=0;i<10&&i+6<lines.Length;i++)if(!string.IsNullOrWhiteSpace(lines[i+6]))custom[i]=lines[i+6];}
        var keys=InputDeviceProfile.KeyboardKeys(preset,custom).Concat(profile.ExtraKeys).Select(InputReader.ParseKey).ToArray();
        keyMap=string.Join(",",keys.Select(key=>((int)key).ToString()));
        dispatcher=new InputKeyDispatcher(keys,Inject,focusGame);
        timer=new Timer{Interval=16};timer.Tick+=(_,_)=>Poll();if(profile.Mode==2||profile.Mode==3)timer.Start();
    }
    private void Poll()
    {
        long sequence=++pollSequence,started=Stopwatch.GetTimestamp();
        uint foregroundBefore=ReadForegroundProcessId(),foregroundPid=foregroundBefore;int pid=emulatorPid();var supplied=virtualActions();
        bool topLevel=emulatorTopLevel();
        if(profile.Mode==3)foregroundPid=VirtualFocusRecovery.RestoreForHeldVirtualAction((uint)Math.Max(0,pid),foregroundPid,topLevel,virtualActionHeld(),focusGame,ReadForegroundProcessId);
        bool focusSettling=profile.Mode==3&&VirtualFocusRecovery.ShouldDeferFirstFocusedPoll(foregroundBefore,foregroundPid,(uint)Math.Max(0,pid),topLevel,virtualActionHeld());
        bool? eligible=pid>0&&foregroundPid!=pid?hostFocused():null;
        bool focused=!focusSettling&&VirtualFocusRecovery.CanRoute(pid,foregroundPid,topLevel,eligible==true);
        var pad=focused&&profile.Mode==2?InputReader.ReadPad(profile.ControllerSlot,profile.DeadZone):new InputSnapshot();
        string suppliedSnapshot=Bits(supplied);
        var resolved=InputActionResolver.Resolve(profile,pad,supplied,focused);string heldBefore=dispatcher.HeldKeysSnapshot;
        dispatcher.Update(resolved);
        LastInputTrace=new(sequence,started,Stopwatch.GetTimestamp(),Stopwatch.Frequency,profile.Mode,pid,foregroundPid,eligible,focused,focusSettling,keyMap,suppliedSnapshot,Bits(resolved),heldBefore,dispatcher.HeldKeysSnapshot,injectionSequence);
    }
    private readonly string keyMap;
    private static uint ReadForegroundProcessId(){InputReader.GetWindowThreadProcessId(InputReader.GetForegroundWindow(),out uint pid);return pid;}
    private static string Bits(bool[] actions)=>actions==null?"null":string.Concat(actions.Select(value=>value?'1':'0'));
    private static InputFocusTrace ReadInputFocus(){IntPtr foreground=InputReader.GetForegroundWindow();uint thread=InputReader.GetWindowThreadProcessId(foreground,out uint pid);return new(foreground.ToInt64(),pid,thread,GetFocus().ToInt64());}
    private bool Inject(Keys key,bool down)
    {
        if(key==Keys.None)return false;
        bool hasHeldScanCode=heldScanCodes.TryGetValue(key,out var heldScanCode);
        uint mapped=MapVirtualKeyEx((uint)key,MapVirtualKeyToScanCodeEx,GetKeyboardLayout(0));int mappedScanCode=(int)(mapped&0xff);int prefix=(int)((mapped>>8)&0xff);
        bool scanCodeInput=down?mappedScanCode!=0&&prefix!=0xe1:hasHeldScanCode;
        int scanCode=down?mappedScanCode:heldScanCode.ScanCode;
        bool extended=down?prefix==0xe0:heldScanCode.Extended;
        uint flags=scanCodeInput?KeyEventScanCode:0u;if(!down)flags|=KeyEventUp;
        if((scanCodeInput&&extended)||(!scanCodeInput&&key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Insert or Keys.Delete or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown))flags|=KeyEventExtended;
        var before=ReadInputFocus();long at=Stopwatch.GetTimestamp();
        uint inserted=SendInput(1,new[]{new KeyboardInput{Type=1,Key=scanCodeInput?(ushort)0:(ushort)key,Scan=scanCodeInput?(ushort)scanCode:(ushort)0,Flags=flags}},40);int error=Marshal.GetLastPInvokeError();
        LastInjectionTrace=new(++injectionSequence,pollSequence,at,Stopwatch.Frequency,(int)key,scanCodeInput?scanCode:0,down,flags,inserted,error,before,ReadInputFocus());
        if(down&&inserted==1&&scanCodeInput)heldScanCodes[key]=((ushort)scanCode,extended);
        else if(!down)heldScanCodes.Remove(key);
        injectionHistory.Enqueue(LastInjectionTrace);while(injectionHistory.Count>32)injectionHistory.Dequeue();
        return inserted==1;
    }
    public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Dispose();dispatcher.Release();}
}
