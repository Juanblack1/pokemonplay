#if STARTUP_PLAYBACK_PROBE
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;

// Bounded diagnostic of the actual host; all configs and saves live in a new
// output directory. The caller supplies an original diagnostic ROM.
internal static class StartupPlaybackProbe
{
    internal static void Run(string output, string rom, string emulatorDirectory)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Diagnostic output must be a new directory.");
        Directory.CreateDirectory(output);
        string ownedEmulator = Path.Combine(output,"emulator");
        Directory.CreateDirectory(Path.Combine(ownedEmulator,"cores"));
        foreach(string file in Directory.GetFiles(emulatorDirectory))
            if(Path.GetExtension(file) is ".dll" or ".exe") File.Copy(file,Path.Combine(ownedEmulator,Path.GetFileName(file)));
        File.Copy(Path.Combine(emulatorDirectory,"cores","mgba_libretro.dll"),Path.Combine(ownedEmulator,"cores","mgba_libretro.dll"));
        var settings = new RetroArchSettings { UseForGba = true,
            ExecutablePath = Path.Combine(ownedEmulator, "retroarch.exe"),
            GbaCorePath = Path.Combine(ownedEmulator, "cores", "mgba_libretro.dll") };
        RetroArchSettingsService.Save(output, settings);
        var game = new GameInfo { Title = "Original GBA diagnostic", Generation = 3, SaveFolderName = "Diagnostic" };
        string previous = Environment.GetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP");
        Environment.SetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP", Path.Combine(output, "sessions"));
        RetroArchLaunchPlan plan;
        try { plan = RetroArchSettingsService.CreateLaunch(output, game, rom); }
        finally { Environment.SetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP", previous); }
        if(Environment.GetEnvironmentVariable("POKEMONPLAY_PROBE_NO_FOCUS_PAUSE") == "1")
            File.WriteAllText(plan.TemporaryConfigPath, File.ReadAllText(plan.TemporaryConfigPath).Replace("pause_nonactive = \"true\"", "pause_nonactive = \"false\""));
        string config = Path.Combine(output, "base.cfg");
        File.WriteAllText(config, "config_save_on_exit = \"false\"\ncheevos_enable = \"false\"\n");
        string args = "--config \"" + config + "\" --verbose --log-file \"" + Path.Combine(output,"retroarch.log") +
            "\" --max-frames 240 " + plan.Arguments;
        using var launcher = new LauncherForm(output) { UpdatesEnabled = false };
        using var timer = new Timer { Interval = 250 };
        var clock = Stopwatch.StartNew();
        GameHostForm host = null;
        Process observer = null;
        bool activationSent = false;
        object Field(string name) => typeof(GameHostForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(host);
        var samples = new System.Collections.Generic.List<object>();
        launcher.Shown += (_, _) => launcher.BeginInvoke((Action)(() => {
            host = new GameHostForm(settings.ExecutablePath, "retroarch", args, game.Title, "Diagnostic", output, plan.TemporaryConfigPath, 0);
            launcher.RegisterGameSession(host);
            timer.Start();
        }));
        timer.Tick += (_, _) => {
            try {
            var process = (Process)Field("emulator");
            if (observer == null && process != null) observer = Process.GetProcessById(process.Id);
            IntPtr foreground = InputReader.GetForegroundWindow();
            InputReader.GetWindowThreadProcessId(foreground, out uint foregroundPid);
            samples.Add(new { ms=clock.ElapsedMilliseconds, foregroundPid, emulatorPid=observer?.Id,
                embedded=(bool)Field("emulatorEmbedded"), failed=(bool)Field("launchFailed") });
            File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(samples));
            // Reproduce activation of the launcher while its separate game window is visible.
            if (!activationSent && clock.ElapsedMilliseconds >= 3000 && (bool)Field("emulatorEmbedded") && !observer.HasExited)
            {
                activationSent = true;
                launcher.Activate();
            }
            bool exited = observer?.HasExited == true;
            if (!exited && clock.ElapsedMilliseconds < 16000) return;
            timer.Stop();
            bool passed = exited && File.ReadAllText(Path.Combine(output,"retroarch.log")).Contains("00 hours, 00 minutes, 04 seconds");
            File.WriteAllText(Path.Combine(output,"result.json"), JsonSerializer.Serialize(new { passed, exited,
                samples }, new JsonSerializerOptions { WriteIndented=true }));
            if(!exited && observer != null) {
                observer.CloseMainWindow();
                if(!observer.WaitForExit(3000)) throw new TimeoutException("Owned diagnostic emulator did not close normally.");
            }
            typeof(GameHostForm).GetField("closing", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(host,true);
            host.Close(); launcher.Close();
            Environment.ExitCode = passed ? 0 : 1;
            } catch(Exception error) {
                File.WriteAllText(Path.Combine(output,"probe-error.txt"),error.ToString());
                timer.Stop();
                if(observer != null && !observer.HasExited) observer.CloseMainWindow();
                launcher.Dispose();
                Application.ExitThread();
                Environment.ExitCode = 1;
            }
        };
        try { Application.Run(launcher); }
        finally {
            if(!File.Exists(Path.Combine(output,"result.json")))
                File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {
                    passed=observer?.HasExited == true && File.ReadAllText(Path.Combine(output,"retroarch.log")).Contains("00 hours, 00 minutes, 04 seconds"),
                    exited=observer?.HasExited, samples },new JsonSerializerOptions {WriteIndented=true}));
            observer?.Dispose();
        }
    }
}
#endif
