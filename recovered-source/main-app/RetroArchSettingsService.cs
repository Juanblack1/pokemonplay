using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

internal sealed class RetroArchSettings
{
    public bool UseForGba { get; set; }
    public bool UseForDs { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string GbaCorePath { get; set; } = string.Empty;
    public string DsCorePath { get; set; } = string.Empty;
    [JsonIgnore]
    public string LoadWarning { get; set; } = string.Empty;
}

internal static class RetroArchSettingsService
{
    private static string SettingsPath(string root) => Path.Combine(root, "Settings", "retroarch.json");

    internal static string DiscoverCore(string executablePath, bool gba)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return string.Empty;
        string coresDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? string.Empty, "cores");
        string[] candidates = gba
            ? new[] { "mgba_libretro.dll", "vbam_libretro.dll", "vba_next_libretro.dll" }
            : new[] { "melonds_ds_libretro.dll", "melondsds_libretro.dll", "melonds_libretro.dll", "desmume_libretro.dll" };
        return candidates.Select(name => Path.Combine(coresDirectory, name)).FirstOrDefault(File.Exists) ?? string.Empty;
    }

    internal static RetroArchSettings Load(string root)
    {
        string path = SettingsPath(root);
        if (!File.Exists(path)) return BundledEmulators.Defaults(root);
        try
        {
            var settings = JsonSerializer.Deserialize<RetroArchSettings>(File.ReadAllText(path));
            if (settings == null) return Recover(root);
            // Stored bundle paths are derived again when the portable folder moves.
            if (settings.ExecutablePath?.Replace('\\', '/').Contains("/PokemonPlayRuntime/Emulators/RetroArch/", StringComparison.OrdinalIgnoreCase)==true)
            {
                var bundled = BundledEmulators.Defaults(root);
                settings.ExecutablePath = bundled.ExecutablePath;
                if(settings.GbaCorePath?.Replace('\\','/').Contains("/PokemonPlayRuntime/Emulators/RetroArch/",StringComparison.OrdinalIgnoreCase)==true) settings.GbaCorePath = bundled.GbaCorePath;
                if(settings.DsCorePath?.Replace('\\','/').Contains("/PokemonPlayRuntime/Emulators/RetroArch/",StringComparison.OrdinalIgnoreCase)==true) settings.DsCorePath = bundled.DsCorePath;
            }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException)
        {
            return Recover(root);
        }
    }

    private static RetroArchSettings Recover(string root)
    {
        var settings = BundledEmulators.Defaults(root);
        settings.LoadWarning = "Preferências ilegíveis. Opções da instalação em uso; arquivo original preservado. Confira antes de salvar.";
        return settings;
    }

    internal static void Save(string root, RetroArchSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        string path = SettingsPath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
            settings.LoadWarning = string.Empty;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static string ValidationError(RetroArchSettings settings)
    {
        if (settings == null) return "As configurações do RetroArch não foram carregadas.";
        if (!settings.UseForGba && !settings.UseForDs) return null;
        if (!File.Exists(settings.ExecutablePath)) return "Selecione o executável do RetroArch.";
        if (settings.UseForGba && !File.Exists(settings.GbaCorePath)) return "Selecione um core Libretro para Game Boy Advance.";
        if (settings.UseForDs && !File.Exists(settings.DsCorePath)) return "Selecione um core Libretro para Nintendo DS.";
        return null;
    }

    internal static bool UsesRetroArch(string root, GameInfo game)
    {
        if (game == null || game.Generation is < 3 or > 5) return false;
        RetroArchSettings settings = Load(root);
        return game.Generation == 3 ? settings.UseForGba : settings.UseForDs;
    }

    internal static RetroArchLaunchPlan CreateLaunch(string root, GameInfo game, string romPath)
    {
        RetroArchSettings settings = Load(root);
        bool enabled = game.Generation == 3 ? settings.UseForGba : settings.UseForDs;
        if (!enabled) return null;
        string validation = ValidationError(settings);
        if (validation != null) throw new InvalidOperationException(validation);
        string corePath = game.Generation == 3 ? settings.GbaCorePath : settings.DsCorePath;
        string saveDirectory = SaveProfileService.ActiveFolder(root, game.SaveFolderName);
        Directory.CreateDirectory(saveDirectory);

        string sessionDirectory = Environment.GetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP");
        if (string.IsNullOrWhiteSpace(sessionDirectory))
            sessionDirectory = Path.Combine(Path.GetTempPath(), "Pokemons Play", "RetroArchSessions");
        Directory.CreateDirectory(sessionDirectory);
        string configPath = Path.Combine(sessionDirectory, "launch-" + Guid.NewGuid().ToString("N") + ".cfg");
        string[] config =
        {
            "cheevos_enable = \"true\"",
            "cheevos_visibility_unlock = \"true\"",
            "pause_nonactive = \"true\"",
            "video_fullscreen = \"false\"",
            "video_windowed_fullscreen = \"false\"",
            "video_window_show_decorations = \"false\"",
            "savefile_directory = \"" + ConfigValue(saveDirectory) + "\"",
            "savestate_directory = \"" + ConfigValue(saveDirectory) + "\""
        };
        if (string.Equals(Path.GetFullPath(settings.ExecutablePath), BundledEmulators.RetroArch(root), StringComparison.OrdinalIgnoreCase))
        {
            string data = BundledEmulators.DataDirectory(root, "RetroArch");
            Directory.CreateDirectory(data);
            BundledEmulators.RetroArchConfig(root);
            string binary = Path.GetDirectoryName(settings.ExecutablePath);
            config = config.Concat(new[] { "libretro_directory = \"" + ConfigValue(Path.Combine(binary, "cores")) + "\"",
                "libretro_info_path = \"" + ConfigValue(Path.Combine(binary, "info")) + "\"",
                "assets_directory = \"" + ConfigValue(Path.Combine(binary, "assets")) + "\"",
                "system_directory = \"" + ConfigValue(Path.Combine(data, "system")) + "\"" }).ToArray();
        }
        InputDeviceProfile profile=InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json"));
        string[] keys=LauncherSettings.KeyboardFor(root).Concat(profile.ExtraKeys).ToArray();
        string[] actions={"up","down","left","right","a","b","l","r","start","select","x","y"};
        string[] mappedKeys=keys.Select(RetroArchKey).ToArray();
        string modifier=new[]{"f11","f10","f9","f8","f7","f6","f5","f4","f3","f2","f1","pause","scroll_lock","numlock","capslock"}.First(key=>!mappedKeys.Contains(key));
        config=config.Concat(actions.Select((action,index)=>"input_player1_"+action+" = \""+mappedKeys[index]+"\""))
            .Concat(new[]{"config_save_on_exit = \"false\"","input_enable_hotkey = \""+modifier+"\""}).ToArray();
        if(profile.Mode is 2 or 3) {
            // The launcher supplies keyboard events for its remapped/virtual actions.
            // Explicit nul binds prevent RetroArch's autoconfig from also supplying input.
            string[] physical=actions.Concat(new[]{"l2","r2","l3","r3","l_x_plus","l_x_minus","l_y_plus","l_y_minus","r_x_plus","r_x_minus","r_y_plus","r_y_minus"}).ToArray();
            config=config.Concat(physical.SelectMany(action=>new[]{"input_player1_"+action+"_btn = \"nul\"","input_player1_"+action+"_axis = \"nul\""}))
                .Concat(new[]{"input_enable_hotkey_btn = \"nul\"","input_enable_hotkey_axis = \"nul\"","input_menu_toggle_gamepad_combo = \"0\"","input_quit_gamepad_combo = \"0\""}).ToArray();
        }
        File.WriteAllLines(configPath, config);
        string arguments = "-L " + Quote(corePath) + " --appendconfig " + Quote(configPath) + " " + Quote(Path.GetFullPath(romPath));
        if (string.Equals(Path.GetFullPath(settings.ExecutablePath), BundledEmulators.RetroArch(root), StringComparison.OrdinalIgnoreCase))
            arguments = "--config " + Quote(BundledEmulators.RetroArchConfig(root)) + " " + arguments;
        return new RetroArchLaunchPlan(settings.ExecutablePath, arguments, configPath);
    }

    internal static void DeleteSessionConfig(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ConfigValue(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);
    private static string RetroArchKey(string key)
    {
        Keys value=InputReader.ParseKey(key??string.Empty);
        if(value>=Keys.A&&value<=Keys.Z)return value.ToString().ToLowerInvariant();
        if(value>=Keys.D0&&value<=Keys.D9)return "num"+((int)value-(int)Keys.D0);
        if(value>=Keys.NumPad0&&value<=Keys.NumPad9)return "keypad"+((int)value-(int)Keys.NumPad0);
        if(value>=Keys.F1&&value<=Keys.F15)return value.ToString().ToLowerInvariant();
        return value switch {
            Keys.Up=>"up",Keys.Down=>"down",Keys.Left=>"left",Keys.Right=>"right",
            Keys.Return=>"enter",Keys.Back=>"backspace",Keys.ShiftKey or Keys.LShiftKey=>"shift",Keys.RShiftKey=>"rshift",
            Keys.ControlKey or Keys.LControlKey=>"ctrl",Keys.RControlKey=>"rctrl",Keys.Menu or Keys.LMenu=>"alt",Keys.RMenu=>"ralt",
            Keys.Tab=>"tab",Keys.Space=>"space",Keys.Escape=>"escape",Keys.Insert=>"insert",Keys.Delete=>"del",
            Keys.Home=>"home",Keys.End=>"end",Keys.PageUp=>"pageup",Keys.PageDown=>"pagedown",
            Keys.CapsLock=>"capslock",Keys.NumLock=>"numlock",Keys.Scroll=>"scroll_lock",Keys.Pause=>"pause",Keys.PrintScreen=>"print_screen",
            Keys.Add=>"add",Keys.Subtract=>"subtract",Keys.Multiply=>"multiply",Keys.Divide=>"divide",Keys.Decimal=>"kp_period",
            Keys.OemPeriod=>"period",Keys.Oemcomma=>"comma",Keys.OemMinus=>"minus",Keys.Oemplus=>"equals",
            Keys.OemQuestion=>"slash",Keys.OemSemicolon=>"semicolon",Keys.OemQuotes=>"quote",Keys.Oemtilde=>"tilde",
            Keys.OemOpenBrackets=>"leftbracket",Keys.OemCloseBrackets=>"rightbracket",Keys.OemPipe=>"backslash",Keys.OemBackslash=>"oem102",
            _=>"nul"
        };
    }

    private static string Quote(string value)
    {
        if (value == null) return "\"\"";
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}

internal sealed class RetroArchLaunchPlan
{
    internal string ExecutablePath { get; }
    internal string Arguments { get; }
    internal string TemporaryConfigPath { get; }

    internal RetroArchLaunchPlan(string executablePath, string arguments, string temporaryConfigPath)
    {
        ExecutablePath = executablePath;
        Arguments = arguments;
        TemporaryConfigPath = temporaryConfigPath;
    }
}
