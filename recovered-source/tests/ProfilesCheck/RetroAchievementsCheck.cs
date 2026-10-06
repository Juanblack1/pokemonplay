using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using PKHeX.Core;

internal static class RetroAchievementsCheck
{
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    private static object Call(Type type, string name, object target, params object[] args)
        => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
            .Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(target, args);

    private static object Get(object target, string name)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    private static string statusText(Control settingsView) => ((Label)Get(settingsView, "status")).Text;

    private static string ActiveFolder(Assembly app, string root, string game)
        => (string)Call(app.GetType("SaveProfileService"), "ActiveFolder", null, root, game);

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    record SyntheticIdentity(int Pid,long StartUtcTicks,string Executable,long Hwnd);
    static void PumpUntil(Func<bool> condition,int limit,string reason)
    {
        var deadline=Stopwatch.StartNew();
        while(!condition()) { if(deadline.ElapsedMilliseconds>limit) throw new TimeoutException(reason);Application.DoEvents();System.Threading.Thread.Sleep(10); }
    }
    static void ObserveSyntheticActiveTime(LauncherForm launcher,Form host,Stopwatch sessionClock,string directory,string executable)
    {
        var observations=new System.Collections.Generic.List<object>();
        var identity=JsonSerializer.Deserialize<SyntheticIdentity>(File.ReadAllText(Path.Combine(directory,"ready.json")));
        using var child=Process.GetProcessById(identity.Pid);
        void Record(string phase) {
            IntPtr foreground=GetForegroundWindow();GetWindowThreadProcessId(foreground,out uint foregroundPid);
            observations.Add(new {phase,foreground=foreground.ToInt64(),foregroundPid,launcher=launcher.Handle.ToInt64(),hostVisible=host.Visible,launchFailed=(bool)Get(host,"launchFailed"),clockRunning=sessionClock.IsRunning,elapsedMilliseconds=sessionClock.ElapsedMilliseconds});
        }
        try {
            Assert(!child.HasExited&&child.StartTime.ToUniversalTime().Ticks==identity.StartUtcTicks&&string.Equals(child.MainModule.FileName,identity.Executable,StringComparison.OrdinalIgnoreCase)&&string.Equals(identity.Executable,executable,StringComparison.OrdinalIgnoreCase),"synthetic active-time transition verifies owned child PID, start time and executable");
            GetWindowThreadProcessId(new IntPtr(identity.Hwnd),out uint windowPid);
            Assert(windowPid==identity.Pid,"synthetic active-time transition verifies owned child HWND");
            Record("after-child-ready");
            bool childForegroundRequested=SetForegroundWindow(new IntPtr(identity.Hwnd));
            PumpUntil(()=>GetForegroundWindow()==new IntPtr(identity.Hwnd)&&!sessionClock.IsRunning,5000,"Owned child foreground did not pause actual launcher session clock");
            Record("owned-child-foreground-clock-stopped");
            Call(host.GetType(),"ReturnToMenu",host);
            Call(launcher.GetType(),"ResumeGameSession",launcher);
            launcher.Activate();bool launcherForegroundRequested=SetForegroundWindow(launcher.Handle);
            PumpUntil(()=>GetForegroundWindow()==launcher.Handle&&host.Visible&&sessionClock.IsRunning,5000,"Real launcher activation/resume did not restart actual visible session clock");
            Record("launcher-foreground-resumed");
            long start=sessionClock.ElapsedMilliseconds;
            var wall=Stopwatch.StartNew();
            while(sessionClock.ElapsedMilliseconds-start<1200) {
                Application.DoEvents();
                AssertClockForeground();
                if(wall.ElapsedMilliseconds>5000) throw new TimeoutException("Actual active clock did not accumulate 1200ms under launcher foreground");
                System.Threading.Thread.Sleep(10);
            }
            Record("launcher-foreground-active-interval-complete");
            observations.Add(new {childForegroundRequested,launcherForegroundRequested,activeElapsedMilliseconds=sessionClock.ElapsedMilliseconds-start,wallElapsedMilliseconds=wall.ElapsedMilliseconds});
            void AssertClockForeground() {
                if(GetForegroundWindow()!=launcher.Handle||!sessionClock.IsRunning||!host.Visible) throw new Exception("Synthetic active-time interval lost launcher foreground, running clock or visible session");
            }
        } finally {
            Record("final");
            File.WriteAllText(Path.Combine(directory,"active-time-observations.json"),JsonSerializer.Serialize(observations,new JsonSerializerOptions {WriteIndented=true}));
        }
    }
    internal static void Run(string root, Assembly app)
    {
        Type settingsType = app.GetType("RetroArchSettings");
        Type serviceType = app.GetType("RetroArchSettingsService");
        var settings = Activator.CreateInstance(settingsType);
        string retroArchRoot = Path.Combine(root, "retroarch-integration");
        string retroArchPath = Path.Combine(retroArchRoot, "RetroArch", "retroarch.exe");
        string gbaCore = Path.Combine(retroArchRoot, "RetroArch", "cores", "vba_next_libretro.dll");
        string dsCore = Path.Combine(retroArchRoot, "RetroArch", "cores", "melonds_libretro.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(retroArchPath));
        Directory.CreateDirectory(Path.GetDirectoryName(gbaCore));
        File.WriteAllText(retroArchPath, "fixture");
        File.WriteAllText(gbaCore, "fixture");
        File.WriteAllText(dsCore, "fixture");
        string preferredGbaCore = Path.Combine(retroArchRoot, "RetroArch", "cores", "mgba_libretro.dll");
        string preferredDsCore = Path.Combine(retroArchRoot, "RetroArch", "cores", "melonds_ds_libretro.dll");
        File.WriteAllText(preferredGbaCore, "fixture");
        File.WriteAllText(preferredDsCore, "fixture");
        Assert((string)Call(serviceType, "DiscoverCore", null, retroArchPath, true) == preferredGbaCore &&
            (string)Call(serviceType, "DiscoverCore", null, retroArchPath, false) == preferredDsCore,
            "RetroArch setup discovers preferred GBA and DS cores beside its executable");
        Assert((string)Call(serviceType, "DiscoverCore", null, Path.Combine(retroArchRoot, "Missing", "retroarch.exe"), true) == string.Empty,
            "RetroArch setup leaves core selection empty when no compatible core is installed");

        settingsType.GetProperty("UseForGba").SetValue(settings, true);
        settingsType.GetProperty("UseForDs").SetValue(settings, true);
        settingsType.GetProperty("ExecutablePath").SetValue(settings, retroArchPath);
        settingsType.GetProperty("GbaCorePath").SetValue(settings, gbaCore);
        settingsType.GetProperty("DsCorePath").SetValue(settings, dsCore);
        Call(serviceType, "Save", null, retroArchRoot, settings);
        var loaded = Call(serviceType, "Load", null, retroArchRoot);
        Assert((bool)settingsType.GetProperty("UseForGba").GetValue(loaded) && (bool)settingsType.GetProperty("UseForDs").GetValue(loaded) &&
            (string)settingsType.GetProperty("ExecutablePath").GetValue(loaded) == retroArchPath &&
            !File.ReadAllText(Path.Combine(retroArchRoot, "Settings", "retroarch.json")).Contains("password", StringComparison.OrdinalIgnoreCase),
            "RetroArch paths and per-system choices persist without credentials");

        string uiRoot = Path.Combine(root, "retroarch-settings-ui");
        string inputSettings = Path.Combine(uiRoot, "Settings", "input-presets.txt");
        using (var settingsView = (Control)Activator.CreateInstance(app.GetType("SettingsView"), new object[] { inputSettings }))
        {
            var accountButton = (ThemeButton)Get(settingsView, "retroArchAccountButton");
            var supportButton = (ThemeButton)Get(settingsView, "retroArchSupportButton");
            Assert(!accountButton.Enabled && accountButton.AccessibleName.Contains("conquistas"),
                "RetroArch account setup stays disabled and clearly named until an executable is saved");
            var retroArchCard = (SectionCard)Get(settingsView, "retroArchCard");
            var retroArchHint = retroArchCard.Controls.OfType<Label>().Single(label => label.AccessibleName == "Detecção de cores, notificações e saves do RetroArch");
            Call(settingsView.GetType(), "LayoutCards", settingsView, 850, 520);
            Assert(accountButton.Top >= retroArchHint.Bottom + 4 && accountButton.Right + 8 <= supportButton.Left && supportButton.Right <= retroArchCard.ClientSize.Width - 16 && accountButton.Bottom <= retroArchCard.ClientSize.Height && supportButton.Bottom <= retroArchCard.ClientSize.Height,
                "RetroArch account and core-compatibility actions fit below setup guidance at compact settings width");
            string openedSupportUrl = null;
            settingsView.GetType().GetProperty("RetroArchBrowserOpener", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(settingsView, new Action<string>(url => openedSupportUrl = url));
            supportButton.PerformClick();
            Assert(openedSupportUrl == "https://docs.retroachievements.org/general/emulator-support-and-issues.html" && statusText(settingsView).Contains("aberta no navegador"),
                "RetroArch settings opens the official live core-compatibility list");
            var viewSettings = Get(settingsView, "retroArchSettings");
            settingsType.GetProperty("UseForGba").SetValue(viewSettings, true);
            settingsType.GetProperty("UseForDs").SetValue(viewSettings, true);
            settingsType.GetProperty("ExecutablePath").SetValue(viewSettings, retroArchPath);
            settingsType.GetProperty("GbaCorePath").SetValue(viewSettings, gbaCore);
            settingsType.GetProperty("DsCorePath").SetValue(viewSettings, dsCore);
            ((CheckBox)Get(settingsView, "retroArchGba")).Checked = true;
            ((CheckBox)Get(settingsView, "retroArchDs")).Checked = true;
            Call(settingsView.GetType(), "Save", settingsView);
            System.Diagnostics.ProcessStartInfo launched = null;
            settingsView.GetType().GetProperty("RetroArchProcessStarter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(settingsView, new Func<System.Diagnostics.ProcessStartInfo, System.Diagnostics.Process>(startInfo => { launched = startInfo; return null; }));
            accountButton.PerformClick();
            Assert(accountButton.Enabled && launched != null && launched.FileName == retroArchPath && launched.Arguments.Length == 0 && launched.UseShellExecute &&
                statusText(settingsView).Contains("Configurações > Conquistas"),
                "RetroArch account action opens the saved app without game arguments and gives the account setup path");
        }
        var uiSettings = Call(serviceType, "Load", null, uiRoot);
        Assert((bool)settingsType.GetProperty("UseForGba").GetValue(uiSettings) && (bool)settingsType.GetProperty("UseForDs").GetValue(uiSettings) &&
            (string)settingsType.GetProperty("ExecutablePath").GetValue(uiSettings) == retroArchPath,
            "settings screen saves RetroArch executable, core paths and generation toggles");

        var hostType = app.GetType("GameHostForm");
        var hostConstructor = hostType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(constructor => constructor.GetParameters().Length == 8);
        Type historyType = app.GetType("GameLaunchHistoryService");
        Call(historyType, "TryRecordLaunch", null, uiRoot, "FireRed", DateTimeOffset.UtcNow);
        string syntheticDirectory=Path.Combine(root,"synthetic-active-session-"+Guid.NewGuid().ToString("N"));
        string syntheticExecutable=GameEmbeddingLifecycleCheck.PrepareSyntheticExecutable(syntheticDirectory);
        var host = (Form)hostConstructor.Invoke(new object[] { syntheticExecutable, Path.GetFileNameWithoutExtension(syntheticExecutable), "--embedding-lifecycle-child \""+syntheticDirectory+"\"", "FireRed", "Principal", uiRoot, Path.Combine(root, "session.cfg"), 0 });
        using (var launcher = new LauncherForm(root) { UpdatesEnabled = false })
        {
            launcher.Show();
            launcher.Width = 1000;
            Application.DoEvents();
            Control libraryPage = ((Control)Get(launcher, "contentHost")).Controls[0];
            Call(launcher.GetType(), "RegisterGameSession", launcher, host);
            using var syntheticChild=new GameEmbeddingLifecycleCheck.AttachedChild(host,syntheticDirectory,syntheticExecutable);
            var sessionClock=(System.Diagnostics.Stopwatch)Get(host,"sessionClock");
            var contentHost = (Control)Get(launcher, "contentHost");
            Assert(!host.TopLevel && ReferenceEquals(host.Parent, contentHost) && host.Visible && launcher.Controls.OfType<Panel>().Any(panel => panel.Dock == DockStyle.Top && panel.Visible) && launcher.Controls.OfType<Panel>().Any(panel => panel.Dock == DockStyle.Bottom && panel.Visible),
                "synthetic active session host sits inside the launcher while the app top bar and navigation remain visible");
            Label pageTitle = (Label)Get(launcher, "pageTitle");
            string longGameTitle = "Pokémon Mystery Dungeon: Explorers of Sky — Special Edition";
            pageTitle.Text = longGameTitle;
            ((Action)Get(launcher, "layoutTopbar"))();
            Application.DoEvents();
            var topbar = launcher.Controls.OfType<Panel>().Single(control => control.Dock == DockStyle.Top);
            var updateButton = topbar.Controls.OfType<UpdateNoticeButton>().Single();
            Assert(pageTitle.Visible && pageTitle.AutoEllipsis && pageTitle.Width >= 80 && pageTitle.Right + 16 <= updateButton.Left,
                "active long game title stays visible before the top-bar actions");
            ObserveSyntheticActiveTime(launcher,host,sessionClock,syntheticDirectory,syntheticExecutable);
            Call(hostType, "ReturnToMenu", host);
            var resumeButton = (ThemeButton)Get(launcher, "resumeGameButton");
            var endButton = (ThemeButton)Get(launcher, "endGameButton");
            Assert((bool)hostType.GetProperty("CanPauseToMenu", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(host) && resumeButton.Visible && endButton.Visible && !sessionClock.IsRunning &&
                resumeButton.Text == "Retomar · FireRed" && resumeButton.AccessibleName.Contains("FireRed") && resumeButton.AccessibleName.Contains("Principal") && resumeButton.Focused && ReferenceEquals(contentHost.Controls[0], libraryPage),
                "returning to the launcher restores the existing library page and focuses the accessible resume action");
            var folderButton = topbar.Controls.OfType<ThemeButton>().Single(button => button.Text == "Pasta de jogos 3DS");
            Assert(updateButton.Right + 8 <= resumeButton.Left && resumeButton.Right + 8 <= endButton.Left && endButton.Right + 8 <= folderButton.Left && folderButton.Right <= topbar.ClientSize.Width,
                "paused-session actions fit without overlap in the launcher minimum-width layout");
            resumeButton.Text = "Retomar · " + longGameTitle;
            resumeButton.AccessibleName = "Retomar " + longGameTitle + ", perfil Principal";
            ((Action)Get(launcher, "layoutTopbar"))();
            Application.DoEvents();
            Assert(resumeButton.Width <= 180 && resumeButton.AccessibleName.Contains(longGameTitle) && !pageTitle.Visible,
                "long paused-game title remains available to accessibility while the page title yields space to its controls");
            object historyEntry = ((System.Collections.IEnumerable)Call(historyType, "Load", null, uiRoot)).Cast<object>().Single();
            long recordedSeconds = (long)historyEntry.GetType().GetProperty("TotalPlayTimeSeconds").GetValue(historyEntry);
            Assert(recordedSeconds >= 1, "returning to the launcher persists synthetic active session time while its owned child remains alive");
            Call(launcher.GetType(), "ResumeGameSession", launcher);
            Application.DoEvents();
            Assert(host.Visible && !resumeButton.Visible && !endButton.Visible && sessionClock.IsRunning, "resuming the synthetic active session hides launcher actions, restores the game host and restarts active play time");
            Call(launcher.GetType(), "Navigate", launcher, "settings");
            Assert(!host.Visible && resumeButton.Visible && ((Label)Get(launcher, "pageTitle")).Text == "Configurações",
                "using launcher navigation pauses the synthetic active session and keeps a resume action in the app bar");
            Call(launcher.GetType(), "ResumeGameSession", launcher);
            Application.DoEvents();
            Call(hostType, "ReturnToMenu", host);
            Assert(!sessionClock.IsRunning, "returning to the launcher stops active play-time counting");
            syntheticChild.ConfirmRegisteredClose(host,()=>Call(launcher.GetType(), "EndGameSession", launcher));
            Assert(host.IsDisposed && !resumeButton.Visible && !endButton.Visible, "ending a paused synthetic active session normally exits its owned child, closes the host and clears its launcher actions");

            var exitHost = (Form)hostConstructor.Invoke(new object[] { "missing-retroarch.exe", "retroarch", string.Empty, "FireRed", "Principal", uiRoot, Path.Combine(root, "exit-session.cfg"), 0 });
            Call(launcher.GetType(), "RegisterGameSession", launcher, exitHost);
            exitHost.Show();
            Application.DoEvents();
            Call(hostType, "ReturnToMenu", exitHost);
            Assert((bool)Get(exitHost,"launchFailed") && Get(exitHost,"emulator")==null && !resumeButton.Visible && !endButton.Visible, "real failed startup cannot become a resumable synthetic active session");
            launcher.Close();
            Application.DoEvents();
            Assert(exitHost.IsDisposed && launcher.IsDisposed, "exiting the launcher closes its failed startup session and completes the original exit action");
        }

        string tempDirectory = Path.Combine(root, "retroarch-session-fixtures");
        string previousTemp = Environment.GetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP");
        Environment.SetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP", tempDirectory);
        try
        {
            Type gameType = app.GetType("GameInfo");
            object gbaGame = Activator.CreateInstance(gameType);
            gameType.GetField("Title").SetValue(gbaGame, "FireRed");
            gameType.GetField("Generation").SetValue(gbaGame, 3);
            gameType.GetField("SaveFolderName").SetValue(gbaGame, "Pokemon FireRed");
            string gbaRom = Path.Combine(retroArchRoot, "Pokemon - Arquivos", "1636 - Pokemon Fire Red (U)(Squirrels).gba");
            Directory.CreateDirectory(Path.GetDirectoryName(gbaRom));
            File.WriteAllText(gbaRom, "fixture");

            MethodInfo prepare = app.GetType("LauncherSettings").GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == "PrepareGame" && method.GetParameters().Length == 6);
            object[] gbaArgs = { retroArchRoot, gbaGame, null, string.Empty, null, null };
            prepare.Invoke(null, gbaArgs);
            string gbaConfig = File.ReadAllText((string)gbaArgs[5]);
            string gbaSaveDirectory = ActiveFolder(app, retroArchRoot, "Pokemon FireRed");
            Assert((string)gbaArgs[2] == retroArchPath && (string)gbaArgs[4] == "retroarch" &&
                ((string)gbaArgs[3]).Contains("-L \"" + gbaCore + "\"") && ((string)gbaArgs[3]).Contains("\"" + gbaRom + "\"") &&
                gbaConfig.Contains("savefile_directory = \"" + gbaSaveDirectory + "\"") && gbaConfig.Contains("cheevos_enable = \"true\"") &&
                gbaConfig.Contains("cheevos_visibility_unlock = \"true\"") && gbaConfig.Contains("pause_nonactive = \"true\"") &&
                gbaConfig.Contains("video_fullscreen = \"false\"") && gbaConfig.Contains("video_windowed_fullscreen = \"false\"") &&
                gbaConfig.Contains("video_window_show_decorations = \"false\"") && !gbaConfig.Contains("cheevos_password"),
                "GBA launches windowed and undecorated inside the app with profile saves, achievement popups and pause-on-inactive enabled");
            string importedSaveName = (string)Call(app.GetType("LauncherSettings"), "SaveFileName", null, retroArchRoot, gbaGame);
            Assert(importedSaveName.EndsWith(".srm", StringComparison.OrdinalIgnoreCase), "imported saves use RetroArch's SRAM extension while its core is selected");
            File.Delete((string)gbaArgs[5]);

            object dsGame = Activator.CreateInstance(gameType);
            gameType.GetField("Title").SetValue(dsGame, "Platinum");
            gameType.GetField("Generation").SetValue(dsGame, 4);
            gameType.GetField("SaveFolderName").SetValue(dsGame, "Pokemon Platinum");
            string dsRom = Path.Combine(retroArchRoot, "Pokemon DS - Arquivos", "Pokemon - Platinum Version (USA) (Rev 1).nds");
            Directory.CreateDirectory(Path.GetDirectoryName(dsRom));
            File.WriteAllText(dsRom, "fixture");
            object[] dsArgs = { retroArchRoot, dsGame, null, string.Empty, null, null };
            prepare.Invoke(null, dsArgs);
            string dsConfig = File.ReadAllText((string)dsArgs[5]);
            Assert((string)dsArgs[2] == retroArchPath && ((string)dsArgs[3]).Contains("-L \"" + dsCore + "\"") &&
                dsConfig.Contains("savefile_directory = \"" + ActiveFolder(app, retroArchRoot, "Pokemon Platinum") + "\""),
                "Nintendo DS launches through its selected core and isolated profile saves");
            File.Delete((string)dsArgs[5]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP", previousTemp);
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }

        string saveRoot = Path.Combine(root, "retroarch-srm-profile");
        byte[] rawSave = new byte[0x20000];
        for (int group = 0; group < 2; group++)
            for (int sector = 0; sector < 14; sector++)
            {
                int offset = (group * 14 + sector) * 0x1000;
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rawSave.AsSpan(offset + 0xFF4), (ushort)sector);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rawSave.AsSpan(offset + 0xFF8), 0x08012025);
            }
        // Emerald needs its security key and expanded small-block data for native format detection.
        foreach(int group in new[]{0,1}) {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rawSave.AsSpan(group*14*0x1000+0xAC),0x12345678);
            rawSave[group*14*0x1000+0x890]=1;
        }
        var gameSave = new SAV3E(rawSave);
        gameSave.ClearBoxes();
        PKM pokemon = gameSave.BlankPKM.Clone();
        EntityTemplates.TemplateFields(pokemon, gameSave);
        pokemon.Species = 25;
        pokemon.PID = 0x12345678;
        pokemon.CurrentLevel = 12;
        pokemon.RefreshChecksum();
        gameSave.SetBoxSlotAtIndex(pokemon, 0, 0);
        string profile = ActiveFolder(app, saveRoot, "Pokemon Emerald");
        Directory.CreateDirectory(profile);
        string srmPath = Path.Combine(profile, "Pokemon - Emerald Version (USA, Europe).srm");
        File.WriteAllBytes(srmPath, gameSave.Write().ToArray());
        Assert(SaveUtil.GetSaveFile(File.ReadAllBytes(srmPath).AsMemory(),Path.ChangeExtension(srmPath,".sav")) is SAV3E,"SRAM fixture is natively detected as Emerald before profile lookup");
        var fireRed = Activator.CreateInstance(app.GetType("GameInfo"));
        app.GetType("GameInfo").GetField("Title").SetValue(fireRed, "Emerald");
        app.GetType("GameInfo").GetField("Generation").SetValue(fireRed, 3);
        app.GetType("GameInfo").GetField("SaveFolderName").SetValue(fireRed, "Pokemon Emerald");
        var choices = (System.Collections.IList)Call(app.GetType("ProfileSaveLocator"), "Find", null, saveRoot, fireRed, "default");
        Assert(choices.Count == 1 && Path.GetExtension((string)choices[0].GetType().GetProperty("Path").GetValue(choices[0])) == ".srm",
            "RetroArch SRAM files are recognized as valid saves in the active profile");
        var saveRootSettings = Activator.CreateInstance(settingsType);
        settingsType.GetProperty("UseForGba").SetValue(saveRootSettings, true);
        settingsType.GetProperty("ExecutablePath").SetValue(saveRootSettings, retroArchPath);
        settingsType.GetProperty("GbaCorePath").SetValue(saveRootSettings, gbaCore);
        Call(serviceType, "Save", null, saveRoot, saveRootSettings);
        object importedProfile = Call(app.GetType("SaveProfileService"), "Create", null, saveRoot, fireRed, "Importado do RetroArch", false, srmPath);
        string importedProfileId = (string)importedProfile.GetType().GetProperty("Id").GetValue(importedProfile);
        string importedProfileFolder = (string)Call(app.GetType("SaveProfileService"), "Folder", null, saveRoot, "Pokemon Emerald", importedProfileId);
        var importedChoices = (System.Collections.IList)Call(app.GetType("ProfileSaveLocator"), "Find", null, saveRoot, fireRed, importedProfileId);
        Assert(importedChoices.Count == 1 && File.Exists(Path.Combine(importedProfileFolder, "Pokemon - Emerald Version (USA, Europe).srm")),
            "RetroArch SRAM import creates a recognized save in the new profile");

        string dsSaveRoot = Path.Combine(root, "retroarch-ds-srm-profile");
        var dsRootSettings = Activator.CreateInstance(settingsType);
        settingsType.GetProperty("UseForDs").SetValue(dsRootSettings, true);
        settingsType.GetProperty("ExecutablePath").SetValue(dsRootSettings, retroArchPath);
        settingsType.GetProperty("DsCorePath").SetValue(dsRootSettings, dsCore);
        Call(serviceType, "Save", null, dsSaveRoot, dsRootSettings);
        byte[] rawDsSave = new byte[0x80000];
        foreach (int start in new[] { 0, 0x40000 })
        {
            const int length = 53036;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rawDsSave.AsSpan(start + length - 12), (uint)length);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rawDsSave.AsSpan(start + length - 8), 537265699);
        }
        var dsSave = new SAV4Pt(rawDsSave);
        dsSave.ClearBoxes();
        PKM dsPokemon = dsSave.BlankPKM.Clone();
        EntityTemplates.TemplateFields(dsPokemon, dsSave);
        dsPokemon.Species = 25;
        dsPokemon.PID = 0x12345678;
        dsPokemon.CurrentLevel = 20;
        dsPokemon.RefreshChecksum();
        dsSave.SetBoxSlotAtIndex(dsPokemon, 0, 0);
        string dsProfile = ActiveFolder(app, dsSaveRoot, "Pokemon Platinum");
        Directory.CreateDirectory(dsProfile);
        string dsSrmPath = Path.Combine(dsProfile, "Pokemon - Platinum Version (USA) (Rev 1).srm");
        File.WriteAllBytes(dsSrmPath, dsSave.Write().ToArray());
        Type dsGameType = app.GetType("GameInfo");
        var platinum = Activator.CreateInstance(dsGameType);
        dsGameType.GetField("Title").SetValue(platinum, "Platinum");
        dsGameType.GetField("Generation").SetValue(platinum, 4);
        dsGameType.GetField("SaveFolderName").SetValue(platinum, "Pokemon Platinum");
        var dsChoices = (System.Collections.IList)Call(app.GetType("ProfileSaveLocator"), "Find", null, dsSaveRoot, platinum, "default");
        Assert(dsChoices.Count == 1 && Path.GetExtension((string)dsChoices[0].GetType().GetProperty("Path").GetValue(dsChoices[0])) == ".srm",
            "Nintendo DS RetroArch SRAM files are validated and listed in the active profile");
        object dsImportedProfile = Call(app.GetType("SaveProfileService"), "Create", null, dsSaveRoot, platinum, "Importado do RetroArch", false, dsSrmPath);
        string dsImportedId = (string)dsImportedProfile.GetType().GetProperty("Id").GetValue(dsImportedProfile);
        string dsImportedFolder = (string)Call(app.GetType("SaveProfileService"), "Folder", null, dsSaveRoot, "Pokemon Platinum", dsImportedId);
        Assert(File.Exists(Path.Combine(dsImportedFolder, "Pokemon - Platinum Version (USA) (Rev 1).srm")),
            "Nintendo DS RetroArch SRAM import creates a recognized save in the new profile");
    }
}
