using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

internal sealed class InputDeviceProfile
{
    public int Mode { get; set; }
    public int TestConsole { get; set; }
    public int ControllerSlot { get; set; }
    public int DeadZone { get; set; } = 24;
    public string[] ExtraKeys { get; set; } = new[] { "C", "V" };
    public string[] Bindings { get; set; } = new[] { "PadUp", "PadDown", "PadLeft", "PadRight", "PadA", "PadB", "PadLB", "PadRB", "PadStart", "PadBack", "PadX", "PadY" };
    public static string PathFor(string settingsFile) => Path.Combine(Path.GetDirectoryName(settingsFile), "input-device.json");
    public static InputDeviceProfile Load(string file)
    {
        try
        {
            var p = JsonSerializer.Deserialize<InputDeviceProfile>(File.ReadAllText(file));
            if (p == null || p.Bindings == null || p.Bindings.Length != 12 || p.Bindings.Any(b => !InputReader.PadInputs.Contains(b)) || p.Bindings.Distinct().Count() != 12 || p.ExtraKeys == null || p.ExtraKeys.Length != 2 || p.ExtraKeys.Any(k=>InputReader.ParseKey(k)==Keys.None)) return new();
            p.Mode = Math.Clamp(p.Mode, 0, 3); p.TestConsole=Math.Clamp(p.TestConsole,0,2); p.ControllerSlot = Math.Clamp(p.ControllerSlot, 0, 3); p.DeadZone = Math.Clamp(p.DeadZone, 10, 60); return p;
        }
        catch { return new(); }
    }
    public void Save(string file) { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(file + ".tmp", file, true); }
    public static string[] KeyboardKeys(int preset, string[] custom) => preset switch
    {
        0 => new[] { "Up", "Down", "Left", "Right", "Z", "X", "A", "S", "Enter", "Backspace" },
        2 => new[] { "8", "5", "4", "6", "1", "2", "7", "9", "Enter", "0" },
        3 => new[] { "E", "D", "S", "F", "J", "K", "A", "G", "Enter", "Shift" },
        4 => new[] { "W", "S", "A", "D", "J", "K", "Q", "E", "Enter", "Backspace" },
        5 => (string[])custom.Clone(),
        _ => new[] { "W", "S", "A", "D", "Z", "X", "Q", "E", "Enter", "Backspace" }
    };
}

internal sealed class InputSnapshot
{
    public HashSet<string> Pressed = new();
    public bool Connected;
    public float LX, LY, RX, RY, LT, RT;
}

internal static class InputReader
{
    [StructLayout(LayoutKind.Sequential)] private struct Gamepad { public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
    [StructLayout(LayoutKind.Sequential)] private struct State { public uint Packet; public Gamepad Pad; }
    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out State state);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    public static readonly string[] PadInputs = { "PadUp", "PadDown", "PadLeft", "PadRight", "PadA", "PadB", "PadX", "PadY", "PadLB", "PadRB", "PadStart", "PadBack", "PadLS", "PadRS", "PadLT", "PadRT", "StickUp", "StickDown", "StickLeft", "StickRight" };
    public static readonly string[] PadLabels = { "D-pad cima", "D-pad baixo", "D-pad esq.", "D-pad dir.", "A", "B", "X", "Y", "LB", "RB", "Start", "Back", "L3", "R3", "LT", "RT", "Analóg. cima", "Analóg. baixo", "Analóg. esq.", "Analóg. dir." };
    public static string Label(string token) { int i = Array.IndexOf(PadInputs, token); return i < 0 ? token : PadLabels[i]; }
    public static Keys ParseKey(string text)
    {
        if (text.Length == 1 && char.IsDigit(text[0])) return (Keys)((int)Keys.D0 + text[0] - '0');
        return text switch { "Enter" => Keys.Return, "Backspace" => Keys.Back, "Shift" => Keys.ShiftKey, "Ctrl" => Keys.ControlKey, "Alt" => Keys.Menu, _ => Enum.TryParse<Keys>(text, true, out var k) ? k : Keys.None };
    }
    public static bool Down(Keys key) => key != Keys.None && (GetAsyncKeyState((int)key) & 0x8000) != 0;
    public static InputSnapshot ReadPad(int slot, int deadZone)
    {
        var s = new InputSnapshot();
        try
        {
            if (XInputGetState((uint)slot, out var state) != 0) return s;
            s.Connected = true; var p = state.Pad;
            ushort[] masks = { 1, 2, 4, 8, 0x1000, 0x2000, 0x4000, 0x8000, 0x100, 0x200, 0x10, 0x20, 0x40, 0x80 };
            for (int i = 0; i < masks.Length; i++) if ((p.Buttons & masks[i]) != 0) s.Pressed.Add(PadInputs[i]);
            s.LX = Math.Clamp(p.LX / 32767f, -1, 1); s.LY = Math.Clamp(p.LY / 32767f, -1, 1); s.RX = Math.Clamp(p.RX / 32767f, -1, 1); s.RY = Math.Clamp(p.RY / 32767f, -1, 1); s.LT = p.LT / 255f; s.RT = p.RT / 255f;
            float d = deadZone / 100f;
            if (s.LT > .12f) s.Pressed.Add("PadLT"); if (s.RT > .12f) s.Pressed.Add("PadRT");
            if (s.LY > d) s.Pressed.Add("StickUp"); if (s.LY < -d) s.Pressed.Add("StickDown"); if (s.LX < -d) s.Pressed.Add("StickLeft"); if (s.LX > d) s.Pressed.Add("StickRight");
        }
        catch (DllNotFoundException) { }
        return s;
    }
}
