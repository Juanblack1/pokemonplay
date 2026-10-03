using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class EmulatorSettingsRecoveryCheck
{
    static object Call(Type type, string name, params object[] args) => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
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
    }
}
