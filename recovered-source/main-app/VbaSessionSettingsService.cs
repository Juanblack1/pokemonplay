using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static class VbaSessionSettingsService
{
    private const string MarkerSuffix = ".pokemonplay-vba-session";

    internal static string[] ConfigPaths(string root) => new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "visualboyadvance-m", "vbam.ini"),
        Path.Combine(root, "Pokemon - Arquivos", "pt_BR", "vbam.ini")
    };

    internal static string Begin(params string[] configPaths)
    {
        if (configPaths == null || configPaths.Length == 0) throw new ArgumentException("É necessário informar ao menos uma configuração do VBA-M.", nameof(configPaths));
        List<ConfigState> states = new();
        foreach (string configPath in configPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath)) throw new FileNotFoundException("Uma configuração do VBA-M não foi encontrada.", configPath);
            string fullPath = Path.GetFullPath(configPath);
            List<string> lines = File.ReadAllLines(fullPath).ToList();
            int index = FindPreference(lines, "pauseWhenInactive");
            states.Add(new ConfigState { ConfigPath = fullPath, HadOriginalSetting = index >= 0, OriginalLine = index >= 0 ? lines[index] : null, OriginalIndex = index, AddedSection = FindPreferenceSection(lines) < 0 });
        }

        string markerDirectory = Path.Combine(Path.GetTempPath(), "Pokemons Play", "VbaSessions");
        Directory.CreateDirectory(markerDirectory);
        string markerPath = Path.Combine(markerDirectory, Guid.NewGuid().ToString("N") + MarkerSuffix);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(states));
        try
        {
            foreach (ConfigState state in states)
            {
                List<string> lines = File.ReadAllLines(state.ConfigPath).ToList();
                if (FindPreferenceSection(lines) < 0) lines.Add("[preferences]");
                int index = FindPreference(lines, "pauseWhenInactive");
                if (index >= 0) lines[index] = "pauseWhenInactive=0";
                else lines.Insert(PreferenceEnd(lines), "pauseWhenInactive=0");
                WriteAtomically(state.ConfigPath, lines);
            }
            return markerPath;
        }
        catch
        {
            try { End(markerPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static void End(string markerPath)
    {
        if (!IsMarker(markerPath) || !File.Exists(markerPath)) return;
        List<ConfigState> states;
        try { states = JsonSerializer.Deserialize<List<ConfigState>>(File.ReadAllText(markerPath)); }
        catch (JsonException) { return; }
        if (states == null) return;

        foreach (ConfigState state in states)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.ConfigPath) || !File.Exists(state.ConfigPath)) continue;
            List<string> lines = File.ReadAllLines(state.ConfigPath).ToList();
            int index = FindPreference(lines, "pauseWhenInactive");
            if (state.HadOriginalSetting)
            {
                if (index >= 0) lines[index] = state.OriginalLine ?? "pauseWhenInactive=0";
                else lines.Insert(Math.Clamp(state.OriginalIndex, PreferenceStart(lines), PreferenceEnd(lines)), state.OriginalLine ?? "pauseWhenInactive=0");
            }
            else if (index >= 0) lines.RemoveAt(index);
            if (state.AddedSection)
            {
                int section = FindPreferenceSection(lines);
                if (section >= 0 && PreferenceEnd(lines) == section + 1) lines.RemoveAt(section);
            }
            WriteAtomically(state.ConfigPath, lines);
        }

        File.Delete(markerPath);
        string markerDirectory = Path.GetDirectoryName(Path.GetFullPath(markerPath));
        if (string.Equals(Path.GetFileName(markerDirectory), "VbaSessions", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFileName(Path.GetDirectoryName(markerDirectory)), "Pokemons Play", StringComparison.OrdinalIgnoreCase) &&
            !Directory.EnumerateFileSystemEntries(markerDirectory).Any())
            Directory.Delete(markerDirectory);
    }

    internal static bool IsMarker(string path) => !string.IsNullOrWhiteSpace(path) && path.EndsWith(MarkerSuffix, StringComparison.OrdinalIgnoreCase);

    private static int FindPreference(IList<string> lines, string key)
    {
        int start = PreferenceStart(lines), end = PreferenceEnd(lines);
        for (int i = start; i < end; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) continue;
            int equals = line.IndexOf('=');
            if (equals >= 0 && string.Equals(line.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private static int PreferenceStart(IList<string> lines)
    {
        int section = FindPreferenceSection(lines);
        return section >= 0 ? section + 1 : lines.Count;
    }

    private static int FindPreferenceSection(IList<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
            if (string.Equals(lines[i].Trim(), "[preferences]", StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static int PreferenceEnd(IList<string> lines)
    {
        int start = PreferenceStart(lines);
        if (start == lines.Count) return lines.Count;
        for (int i = start; i < lines.Count; i++)
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

    private sealed class ConfigState
    {
        public string ConfigPath { get; set; }
        public bool HadOriginalSetting { get; set; }
        public string OriginalLine { get; set; }
        public int OriginalIndex { get; set; }
        public bool AddedSection { get; set; }
    }
}
