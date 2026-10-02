using System;
using System.IO;
using System.Linq;
using System.Text.Json;

internal sealed class RetroArchSettings
{
    public bool UseForGba { get; set; }
    public bool UseForDs { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string GbaCorePath { get; set; } = string.Empty;
    public string DsCorePath { get; set; } = string.Empty;
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
            var settings = JsonSerializer.Deserialize<RetroArchSettings>(File.ReadAllText(path)) ?? new RetroArchSettings();
            // Stored bundle paths are derived again when the portable folder moves.
            if (settings.ExecutablePath.Replace('\\', '/').Contains("/PokemonPlayRuntime/Emulators/RetroArch/", StringComparison.OrdinalIgnoreCase))
            {
                var bundled = BundledEmulators.Defaults(root);
                settings.ExecutablePath = bundled.ExecutablePath;
                settings.GbaCorePath = bundled.GbaCorePath;
                settings.DsCorePath = bundled.DsCorePath;
            }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new RetroArchSettings();
        }
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
