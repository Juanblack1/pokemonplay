using System;
using System.IO;

internal static class BundledEmulators
{
    internal static string DirectoryFor(string root, string emulator) => Path.Combine(root, "PokemonPlayRuntime", "Emulators", emulator);
    internal static string RetroArch(string root) => Path.Combine(DirectoryFor(root, "RetroArch"), "retroarch.exe");
    internal static string Azahar(string root) => Path.Combine(DirectoryFor(root, "Azahar"), "azahar.exe");
    internal static RetroArchSettings Defaults(string root)
    {
        string executable = RetroArch(root);
        string gba = RetroArchSettingsService.DiscoverCore(executable, true);
        string ds = RetroArchSettingsService.DiscoverCore(executable, false);
        return new RetroArchSettings { ExecutablePath = File.Exists(executable) ? executable : string.Empty,
            GbaCorePath = gba, DsCorePath = ds, UseForGba = File.Exists(executable) && gba.Length > 0 && !File.Exists(Path.Combine(root,"Pokemon - Arquivos","visualboyadvance-m.exe")),
            UseForDs = File.Exists(executable) && ds.Length > 0 && !File.Exists(Path.Combine(root,"Pokemon DS - Arquivos","melonDS.exe")) };
    }
    // These directories survive replacement of PokemonPlayRuntime by the updater.
    internal static string DataDirectory(string root, string emulator) => Path.Combine(root, "Settings", "Emulators", emulator);
    internal static string RetroArchConfig(string root)
    {
        string data=DataDirectory(root,"RetroArch"); Directory.CreateDirectory(data);
        string file=Path.Combine(data,"retroarch.cfg");
        if(!File.Exists(file)) File.WriteAllText(file,"config_save_on_exit = \"true\"\n");
        return file;
    }
}
