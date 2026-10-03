using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class EmulatorSettingsRecoveryCheck
{
    static object Call(Type type, string name, params object[] args) => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    internal static void Run(string root, Assembly app)
    {
        string install = Path.Combine(root, "emulator-settings-recovery");
        string bundle = Path.Combine(install, "PokemonPlayRuntime", "Emulators", "RetroArch");
        Directory.CreateDirectory(Path.Combine(bundle, "cores"));
        foreach (string file in new[] { "retroarch.exe", "cores/mgba_libretro.dll", "cores/melondsds_libretro.dll" }) File.WriteAllText(Path.Combine(bundle, file), "owned fixture, never executed");
        string path = Path.Combine(install, "Settings", "retroarch.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Type service = app.GetType("RetroArchSettingsService");
        foreach (string content in new[] { "{broken configuration", "null" })
        {
            File.WriteAllText(path, content);
            byte[] original = File.ReadAllBytes(path);
            var settings = Call(service, "Load", install);
            Assert(!string.IsNullOrEmpty((string)Property(settings, "LoadWarning")), "damaged emulator settings expose recovery warning");
            Assert(File.ReadAllBytes(path).SequenceEqual(original), "reading damaged emulator settings preserves original bytes");
            foreach (int generation in new[] { 3, 4 })
            {
                var game = Activator.CreateInstance(app.GetType("GameInfo"));
                game.GetType().GetField("Generation").SetValue(game, generation);
                game.GetType().GetField("IsImported").SetValue(game, true);
                game.GetType().GetField("SaveFolderName").SetValue(game, "Recovery " + generation);
                string rom = Path.Combine(install, generation == 3 ? "owned.gba" : "owned.nds");
                File.WriteAllText(rom, "owned fixture, never executed");
                game.GetType().GetField("RomPath").SetValue(game, rom);
                object[] arguments = { install, game, "", "", null, null };
                try
                {
                    Call(app.GetType("LauncherSettings"), "PrepareGame", arguments);
                    Assert((string)arguments[2] == Path.Combine(bundle, "retroarch.exe"), "damaged emulator settings keep bundled GBA/DS launch available without legacy executable");
                }
                finally { if (arguments[5] is string session) Call(service, "DeleteSessionConfig", session); }
                Assert(File.ReadAllBytes(path).SequenceEqual(original), "launch planning preserves damaged settings bytes");
            }
        }
        File.WriteAllText(path, "{locked original");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var settings = Call(service, "Load", install);
            Assert((bool)Property(settings, "UseForGba") && (bool)Property(settings, "UseForDs") && !string.IsNullOrEmpty((string)Property(settings, "LoadWarning")), "locked settings recover installed defaults with a warning");
            bool rejected = false;
            try { Call(service, "Save", install, settings); }
            catch (TargetInvocationException ex) when (ex.InnerException is IOException or UnauthorizedAccessException) { rejected = true; }
            Assert(rejected, "saving locked emulator preferences reports failure");
        }
        Assert(File.ReadAllText(path) == "{locked original", "failed save preserves locked original bytes");
        using (var view = (Control)Activator.CreateInstance(app.GetType("SettingsView"), new object[] { Path.Combine(install, "Settings", "settings.txt") }))
        using (var host = new Form { Width = 1100, Height = 800 })
        {
            host.Controls.Add(view); host.Show(); Application.DoEvents();
            var status = (Label)view.GetType().GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert(status.Text.Contains("arquivo original", StringComparison.OrdinalIgnoreCase) && status.AccessibleDescription == status.Text, "settings view displays accessible recovery warning without rewriting original");
            Assert(File.ReadAllText(path) == "{locked original", "opening settings preserves damaged emulator preferences");
            var settings = Call(app.GetType("BundledEmulators"), "Defaults", install);
            settings.GetType().GetProperty("UseForDs").SetValue(settings, false);
            Call(service, "Save", install, settings);
            view.GetType().GetMethod("LoadRetroArchSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var ds = (CheckBox)view.GetType().GetField("retroArchDs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert(!ds.Checked && status.Text == "Preferências do emulador carregadas.", "settings reload recovers after repair and preserves valid opt-out");
            Assert(!File.ReadAllText(path).Contains("LoadWarning", StringComparison.Ordinal), "transient recovery warning is excluded from saved configuration");
            host.Close();
        }
        string empty = Path.Combine(root, "settings-recovery-without-bundle");
        Directory.CreateDirectory(Path.Combine(empty, "Settings"));
        File.WriteAllText(Path.Combine(empty, "Settings", "retroarch.json"), "{bad");
        var unavailable = Call(service, "Load", empty);
        Assert(!(bool)Property(unavailable, "UseForGba") && !(bool)Property(unavailable, "UseForDs") && (string)Property(unavailable, "ExecutablePath") == "", "recovery never invents an absent emulator");
    }
}
