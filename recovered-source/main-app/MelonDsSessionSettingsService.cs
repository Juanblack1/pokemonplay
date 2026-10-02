using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static class MelonDsSessionSettingsService
{
    private const string MarkerSuffix = ".pokemonplay-melonds-session";

    internal static string Begin(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath)) throw new ArgumentException("O caminho da configuração é obrigatório.", nameof(configPath));
        configPath = Path.GetFullPath(configPath);
        if (!File.Exists(configPath)) throw new FileNotFoundException("A configuração do melonDS não foi encontrada.", configPath);

        List<string> lines = File.ReadAllLines(configPath).ToList();
        int settingIndex = FindRootSetting(lines, "PauseLostFocus");
        string previousLine = settingIndex >= 0 ? lines[settingIndex] : null;
        var state = new SessionState { ConfigPath = configPath, HadOriginalSetting = settingIndex >= 0, OriginalLine = previousLine, OriginalIndex = settingIndex };
        string markerDirectory = Path.Combine(Path.GetTempPath(), "Pokemons Play", "MelonDSSessions");
        Directory.CreateDirectory(markerDirectory);
        string markerPath = Path.Combine(markerDirectory, Guid.NewGuid().ToString("N") + MarkerSuffix);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(state));

        try
        {
            if (settingIndex >= 0) lines[settingIndex] = "PauseLostFocus = true";
            else lines.Insert(FirstTableIndex(lines), "PauseLostFocus = true");
            WriteAtomically(configPath, lines);
            return markerPath;
        }
        catch
        {
            try { File.Delete(markerPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static void End(string markerPath)
    {
        if (!IsMarker(markerPath) || !File.Exists(markerPath)) return;
        SessionState state;
        try { state = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(markerPath)); }
        catch (JsonException) { return; }
        if (state == null || string.IsNullOrWhiteSpace(state.ConfigPath)) return;

        if (File.Exists(state.ConfigPath))
        {
            List<string> lines = File.ReadAllLines(state.ConfigPath).ToList();
            int currentIndex = FindRootSetting(lines, "PauseLostFocus");
            if (state.HadOriginalSetting)
            {
                int restoreIndex = currentIndex >= 0 ? currentIndex : Math.Clamp(state.OriginalIndex, 0, FirstTableIndex(lines));
                if (currentIndex >= 0) lines[currentIndex] = state.OriginalLine ?? "PauseLostFocus = false";
                else lines.Insert(restoreIndex, state.OriginalLine ?? "PauseLostFocus = false");
            }
            else if (currentIndex >= 0)
            {
                lines.RemoveAt(currentIndex);
            }
            WriteAtomically(state.ConfigPath, lines);
        }

        File.Delete(markerPath);
        string markerDirectory = Path.GetDirectoryName(Path.GetFullPath(markerPath));
        if (string.Equals(Path.GetFileName(markerDirectory), "MelonDSSessions", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFileName(Path.GetDirectoryName(markerDirectory)), "Pokemons Play", StringComparison.OrdinalIgnoreCase) &&
            !Directory.EnumerateFileSystemEntries(markerDirectory).Any())
            Directory.Delete(markerDirectory);
    }

    internal static bool IsMarker(string path) => !string.IsNullOrWhiteSpace(path) && path.EndsWith(MarkerSuffix, StringComparison.OrdinalIgnoreCase);

    internal static int FindRootSetting(IList<string> lines, string key)
    {
        for (int i = 0; i < FirstTableIndex(lines); i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;
            int equals = line.IndexOf('=');
            if (equals >= 0 && string.Equals(line.Substring(0, equals).Trim(), key, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private static int FirstTableIndex(IList<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal)) return i;
        return lines.Count;
    }

    private static void WriteAtomically(string path, IList<string> lines)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllLines(temporary, lines);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed class SessionState
    {
        public string ConfigPath { get; set; }
        public bool HadOriginalSetting { get; set; }
        public string OriginalLine { get; set; }
        public int OriginalIndex { get; set; }
    }
}

internal static class GameSessionSettingsService
{
    internal static void RecoverAbandonedSessions()
    {
        try
        {
            RecoverMarkers("melonDS", "MelonDSSessions", ".pokemonplay-melonds-session", false);
            RecoverMarkers("visualboyadvance-m", "VbaSessions", ".pokemonplay-vba-session", true);
            RecoverMarkers("azahar", "AzaharSessions", ".pokemonplay-azahar-session", false, true);

            string retroArchDirectory = Environment.GetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP");
            if (string.IsNullOrWhiteSpace(retroArchDirectory)) retroArchDirectory = Path.Combine(Path.GetTempPath(), "Pokemons Play", "RetroArchSessions");
            else retroArchDirectory = Path.GetFullPath(retroArchDirectory);
            if (IsProcessRunning("retroarch") || !Directory.Exists(retroArchDirectory)) return;
            foreach (string config in Directory.GetFiles(retroArchDirectory, "launch-*.cfg"))
            {
                try { File.Delete(config); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            try
            {
                if (!Directory.EnumerateFileSystemEntries(retroArchDirectory).Any()) Directory.Delete(retroArchDirectory);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void RecoverMarkers(string processName, string directoryName, string suffix, bool vba, bool azahar = false)
    {
        if (IsProcessRunning(processName)) return;
        string directory = Path.Combine(Path.GetTempPath(), "Pokemons Play", directoryName);
        if (!Directory.Exists(directory)) return;
        string[] markers;
        try { markers = Directory.GetFiles(directory, "*" + suffix); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        foreach (string marker in markers)
        {
            try
            {
                if (vba) VbaSessionSettingsService.End(marker);
                else if (azahar) AzaharSessionSettingsService.End(marker);
                else MelonDsSessionSettingsService.End(marker);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try { if (!process.HasExited) return true; }
                catch (InvalidOperationException) { }
            }
        }
        return false;
    }

    internal static void Cleanup(string path)
    {
        if (VbaSessionSettingsService.IsMarker(path))
        {
            try { VbaSessionSettingsService.End(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return;
        }
        if (MelonDsSessionSettingsService.IsMarker(path))
        {
            try { MelonDsSessionSettingsService.End(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return;
        }
        if (AzaharSessionSettingsService.IsMarker(path))
        {
            try { AzaharSessionSettingsService.End(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return;
        }
        RetroArchSettingsService.DeleteSessionConfig(path);
    }
}
