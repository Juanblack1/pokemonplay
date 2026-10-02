using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static class AzaharSessionSettingsService
{
    private const string MarkerSuffix = ".pokemonplay-azahar-session";
    private const string Section = "UI";
    private const string Setting = "pauseWhenInBackground";

    internal static string Begin(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath)) throw new ArgumentException("O caminho da configuração é obrigatório.", nameof(configPath));
        configPath = Path.GetFullPath(configPath);
        List<string> lines = File.Exists(configPath) ? File.ReadAllLines(configPath).ToList() : new List<string>();
        var location = FindSetting(lines);
        var shortcut = FindResumeShortcut(lines);
        var state = new SessionState
        {
            ConfigPath = configPath,
            HadOriginalSetting = location.Index >= 0,
            OriginalLine = location.Index >= 0 ? lines[location.Index] : null,
            OriginalIndex = location.Index,
            HadOriginalShortcut = shortcut.Index >= 0,
            OriginalShortcutLine = shortcut.Index >= 0 ? lines[shortcut.Index] : null,
            OriginalShortcutIndex = shortcut.Index >= 0 ? shortcut.Index - shortcut.SectionIndex - 1 : shortcut.Index
        };
        string directory = Path.Combine(Path.GetTempPath(), "Pokemons Play", "AzaharSessions");
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, Guid.NewGuid().ToString("N") + MarkerSuffix);
        try
        {
            File.WriteAllText(marker, JsonSerializer.Serialize(state));
            if (location.Index >= 0) lines[location.Index] = Setting + "=true";
            else
            {
                int sectionIndex = FindSection(lines, Section);
                if (sectionIndex < 0)
                {
                    if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) lines.Add(string.Empty);
                    lines.Add("[" + Section + "]");
                    lines.Add(Setting + "=true");
                }
                else
                {
                    int end = NextSection(lines, sectionIndex + 1);
                    lines.Insert(end, Setting + "=true");
                }
            }
            shortcut = FindResumeShortcut(lines);
            if (shortcut.Index >= 0)
            {
                int equals = lines[shortcut.Index].IndexOf('=');
                lines[shortcut.Index] = lines[shortcut.Index].Substring(0, equals + 1) + "F4";
            }
            WriteAtomically(configPath, lines);
            return marker;
        }
        catch
        {
            TryDelete(marker);
            TryRemoveEmptyMarkerDirectory(directory);
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
            var current = FindSetting(lines);
            if (state.HadOriginalSetting)
            {
                int index = current.Index >= 0 ? current.Index : Math.Clamp(state.OriginalIndex, 0, lines.Count);
                string original = state.OriginalLine ?? Setting + "=false";
                if (current.Index >= 0) lines[current.Index] = original;
                else lines.Insert(index, original);
            }
            else if (current.Index >= 0)
            {
                lines.RemoveAt(current.Index);
                int sectionIndex = FindSection(lines, Section);
                if (sectionIndex >= 0 && NextSection(lines, sectionIndex + 1) == sectionIndex + 1)
                    lines.RemoveAt(sectionIndex);
            }
            var currentShortcut = FindResumeShortcut(lines);
            if (state.HadOriginalShortcut)
            {
                if (currentShortcut.Index >= 0) lines[currentShortcut.Index] = state.OriginalShortcutLine;
                else
                {
                    int sectionIndex = FindSection(lines, "Shortcuts");
                    if (sectionIndex < 0)
                    {
                        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) lines.Add(string.Empty);
                        lines.Add("[Shortcuts]");
                        lines.Add(state.OriginalShortcutLine);
                    }
                    else
                    {
                        int insertIndex = Math.Clamp(sectionIndex + 1 + state.OriginalShortcutIndex, sectionIndex + 1, NextSection(lines, sectionIndex + 1));
                        lines.Insert(insertIndex, state.OriginalShortcutLine);
                    }
                }
            }
            WriteAtomically(state.ConfigPath, lines);
        }

        File.Delete(markerPath);
        string markerDirectory = Path.GetDirectoryName(Path.GetFullPath(markerPath));
        TryRemoveEmptyMarkerDirectory(markerDirectory);
    }

    private static void TryRemoveEmptyMarkerDirectory(string markerDirectory)
    {
        if (string.Equals(Path.GetFileName(markerDirectory), "AzaharSessions", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFileName(Path.GetDirectoryName(markerDirectory)), "Pokemons Play", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(markerDirectory) && !Directory.EnumerateFileSystemEntries(markerDirectory).Any())
        {
            try { Directory.Delete(markerDirectory); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static bool IsMarker(string path) => !string.IsNullOrWhiteSpace(path) && path.EndsWith(MarkerSuffix, StringComparison.OrdinalIgnoreCase);

    private static (int Index, int SectionIndex) FindSetting(IList<string> lines)
    {
        int section = FindSection(lines, Section);
        if (section < 0) return (-1, -1);
        int end = NextSection(lines, section + 1);
        for (int i = section + 1; i < end; i++)
        {
            int equals = lines[i].IndexOf('=');
            if (equals > 0 && string.Equals(lines[i].Substring(0, equals).Trim(), Setting, StringComparison.OrdinalIgnoreCase)) return (i, section);
        }
        return (-1, section);
    }

    private static (int Index, int SectionIndex) FindResumeShortcut(IList<string> lines)
    {
        int section = FindSection(lines, "Shortcuts");
        if (section < 0) return (-1, -1);
        int end = NextSection(lines, section + 1);
        for (int i = section + 1; i < end; i++)
        {
            int equals = lines[i].IndexOf('=');
            if (equals <= 0) continue;
            string key = lines[i].Substring(0, equals).Trim();
            bool keySequence = key.EndsWith("\\KeySeq", StringComparison.OrdinalIgnoreCase) || key.EndsWith("/KeySeq", StringComparison.OrdinalIgnoreCase);
            if (keySequence && key.Contains("Continue", StringComparison.OrdinalIgnoreCase) && key.Contains("Pause", StringComparison.OrdinalIgnoreCase))
                return (i, section);
        }
        return (-1, section);
    }

    private static int FindSection(IList<string> lines, string name)
    {
        string expected = "[" + name + "]";
        for (int i = 0; i < lines.Count; i++)
            if (string.Equals(lines[i].Trim(), expected, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static int NextSection(IList<string> lines, int start)
    {
        for (int i = start; i < lines.Count; i++)
            if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal)) return i;
        return lines.Count;
    }

    private static void WriteAtomically(string path, IList<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllLines(temporary, lines); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class SessionState
    {
        public string ConfigPath { get; set; }
        public bool HadOriginalSetting { get; set; }
        public string OriginalLine { get; set; }
        public int OriginalIndex { get; set; }
        public bool HadOriginalShortcut { get; set; }
        public string OriginalShortcutLine { get; set; }
        public int OriginalShortcutIndex { get; set; }
    }
}
