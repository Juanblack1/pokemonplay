using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class GameInputBridge : IDisposable
{
    [StructLayout(LayoutKind.Explicit, Size=40)] private struct KeyboardInput
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(12)] public uint Flags;
    }
    [DllImport("user32.dll", SetLastError=true)] private static extern uint SendInput(uint count, KeyboardInput[] inputs, int size);
    private readonly InputDeviceProfile profile;
    private readonly Keys[] keys;
    private readonly bool[] held = new bool[12];
    private readonly Timer timer;
    private readonly Func<int> emulatorPid;
    private readonly Func<bool> hostFocused;
    private readonly Func<bool[]> virtualActions;
    private readonly Action focusGame;
    public GameInputBridge(string root, Func<int> emulatorPid, Func<bool> hostFocused, Func<bool[]> virtualActions, Action focusGame)
    {
        this.emulatorPid=emulatorPid;this.hostFocused=hostFocused;this.virtualActions=virtualActions;this.focusGame=focusGame;
        profile=InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json"));
        var custom=new[] { "W","S","A","D","Z","X","Q","E","Enter","Backspace" };int preset=1;
        var file=Path.Combine(root,"Settings","input-presets.txt");
        if(File.Exists(file)){var lines=File.ReadAllLines(file);if(lines.Length>0&&int.TryParse(lines[0],out var p)&&p>=0&&p<6)preset=p;for(int i=0;i<10&&i+6<lines.Length;i++)if(!string.IsNullOrWhiteSpace(lines[i+6]))custom[i]=lines[i+6];}
        keys=InputDeviceProfile.KeyboardKeys(preset,custom).Concat(profile.ExtraKeys).Select(InputReader.ParseKey).ToArray();
        timer=new Timer{Interval=16};timer.Tick+=(_,_)=>Poll();if(profile.Mode==2||profile.Mode==3)timer.Start();
    }
    private void Poll()
    {
        InputReader.GetWindowThreadProcessId(InputReader.GetForegroundWindow(),out uint foregroundPid);
        int pid=emulatorPid();bool focused=pid>0&&(foregroundPid==pid||hostFocused());
        var pad=focused&&profile.Mode==2?InputReader.ReadPad(profile.ControllerSlot,profile.DeadZone):new InputSnapshot();
        var touch=virtualActions();
        for(int i=0;i<12;i++)
        {
            bool pressed=profile.Mode==3?focused&&touch[i]:pad.Connected&&pad.Pressed.Contains(profile.Bindings[i]);
            if(pad.Connected&&i<4&&profile.Bindings[i]==InputReader.PadInputs[i])pressed|=pad.Pressed.Contains(new[]{"StickUp","StickDown","StickLeft","StickRight"}[i]);
            if(pressed!=held[i]){if(pressed)focusGame();if(Inject(keys[i],pressed))held[i]=pressed;}
        }
    }
    private static bool Inject(Keys key,bool down)
    {
        if(key==Keys.None)return false;
        uint flags=down?0u:2u;if(key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Insert or Keys.Delete or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown)flags|=1;
        return SendInput(1,new[]{new KeyboardInput{Type=1,Key=(ushort)key,Flags=flags}},40)==1;
    }
    public void Dispose(){timer.Stop();timer.Dispose();for(int i=0;i<12;i++)if(held[i])Inject(keys[i],false);}
}
