using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class AzaharSessionCheck
{
    internal static void Run(Assembly app, string temporaryRoot)
    {
        Type service = app.GetType("AzaharSessionSettingsService");
        string config = Path.Combine(temporaryRoot, "azahar", "qt-config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(config));
        File.WriteAllText(config, "[UI]\npauseWhenInBackground=false\ntheme=dark\n\n[Controls]\nprofile=0\n");
        string marker = (string)Call(service, "Begin", null, config);
        Assert(File.ReadAllText(config).Contains("pauseWhenInBackground=true") && File.ReadAllText(config).Contains("theme=dark"), "Azahar pauses on background while preserving other UI preferences");
        File.WriteAllText(config, "[UI]\npauseWhenInBackground=true\ntheme=light\n\n[Controls]\nprofile=1\n");
        Call(service, "End", null, marker);
        string restored = File.ReadAllText(config);
        Assert(restored.Contains("pauseWhenInBackground=false") && restored.Contains("theme=light") && restored.Contains("profile=1") && !File.Exists(marker), "Azahar session restores its prior pause setting and preserves emulator changes");

        File.WriteAllText(config, "[UI]\ntheme=dark\n\n[Controls]\nprofile=0\n");
        marker = (string)Call(service, "Begin", null, config);
        Assert(File.ReadAllText(config).Contains("pauseWhenInBackground=true"), "Azahar adds the pause setting when it did not exist");
        Call(service, "End", null, marker);
        Assert(!File.ReadAllText(config).Contains("pauseWhenInBackground") && File.ReadAllText(config).Contains("theme=dark"), "Azahar removes only its temporary pause setting");

        File.WriteAllText(config, "[UI]\ntheme=dark\n\n[Shortcuts]\nMain Window\\Continue/Pause Emulation\\KeySeq=Ctrl+F9\nMain Window\\Audio Mute/Unmute\\KeySeq=Ctrl+M\n");
        marker = (string)Call(service, "Begin", null, config);
        Assert(File.ReadAllText(config).Contains("Main Window\\Continue/Pause Emulation\\KeySeq=F4"), "Azahar temporarily maps a custom resume shortcut to the launcher F4 command");
        File.WriteAllText(config, "[UI]\npauseWhenInBackground=true\ntheme=light\n\n[Shortcuts]\nMain Window\\Continue/Pause Emulation\\KeySeq=F4\nMain Window\\Audio Mute/Unmute\\KeySeq=Ctrl+Shift+M\n");
        Call(service, "End", null, marker);
        string shortcutRestored = File.ReadAllText(config);
        Assert(!shortcutRestored.Contains("pauseWhenInBackground") && shortcutRestored.Contains("theme=light") && shortcutRestored.Contains("Main Window\\Continue/Pause Emulation\\KeySeq=Ctrl+F9") && shortcutRestored.Contains("Main Window\\Audio Mute/Unmute\\KeySeq=Ctrl+Shift+M"), "Azahar restores the custom resume shortcut and preserves unrelated settings");

        string install = Path.Combine(temporaryRoot, "azahar-install");
        string configDirectory = Path.Combine(install, "Pokemon 3DS - Arquivos", "Azahar", "user", "config");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(Path.Combine(install, "Settings"));
        string localConfig = Path.Combine(configDirectory, "qt-config.ini");
        File.WriteAllText(localConfig, "[UI]\ntheme=dark\n");
        object game = Activator.CreateInstance(app.GetType("GameInfo"));
        game.GetType().GetField("Title").SetValue(game, "X");
        game.GetType().GetField("Generation").SetValue(game, 6);
        object[] args = { install, game, "azahar.exe", "", null, null };
        Call(app.GetType("LauncherSettings"), "PrepareGame", null, args);
        marker = (string)args[5];
        Assert((string)args[4] == "azahar" && IsMarker(service, marker) && File.ReadAllText(localConfig).Contains("pauseWhenInBackground=true"), "3DS launch selects Azahar and creates a managed pause session");
        Call(app.GetType("GameSessionSettingsService"), "Cleanup", null, marker);
        Assert(!File.ReadAllText(localConfig).Contains("pauseWhenInBackground"), "closing the managed 3DS session restores Azahar configuration");

        string recoveryConfig = Path.Combine(temporaryRoot, "abandoned-azahar.ini");
        File.WriteAllText(recoveryConfig, "[UI]\npauseWhenInBackground=false\n");
        marker = (string)Call(service, "Begin", null, recoveryConfig);
        bool running = System.Diagnostics.Process.GetProcessesByName("azahar").Any(process => { using (process) { try { return !process.HasExited; } catch (InvalidOperationException) { return false; } } });
        if (!running)
        {
            Call(app.GetType("GameSessionSettingsService"), "RecoverAbandonedSessions", null);
            Assert(File.ReadAllText(recoveryConfig).Contains("pauseWhenInBackground=false") && !File.Exists(marker), "startup recovery restores abandoned Azahar settings when the emulator is closed");
        }

        string blockedConfig = Path.Combine(temporaryRoot, "blocked-azahar-config.ini");
        Directory.CreateDirectory(blockedConfig);
        string markerDirectory = Path.Combine(Path.GetTempPath(), "Pokemons Play", "AzaharSessions");
        bool markerDirectoryExisted = Directory.Exists(markerDirectory);
        int markerCountBefore = markerDirectoryExisted ? Directory.GetFiles(markerDirectory).Length : 0;
        bool writeRejected = false;
        try { Call(service, "Begin", null, blockedConfig); }
        catch (TargetInvocationException exception) when (exception.InnerException is IOException or UnauthorizedAccessException) { writeRejected = true; }
        Assert(writeRejected, "Azahar session reports a configuration write failure");
        int markerCountAfter = Directory.Exists(markerDirectory) ? Directory.GetFiles(markerDirectory).Length : 0;
        Assert(markerCountAfter == markerCountBefore && (markerDirectoryExisted || !Directory.Exists(markerDirectory)), "failed Azahar setup removes its temporary marker and empty directory");
    }

    private static bool IsMarker(Type service, string marker) => (bool)Call(service, "IsMarker", null, marker);
    private static object Call(Type type, string name, object target, params object[] args) => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | (target == null ? BindingFlags.Static : BindingFlags.Instance)).Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(target, args);
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
