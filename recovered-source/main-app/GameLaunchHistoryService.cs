using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal sealed record GameLaunchHistoryEntry(string Title, DateTimeOffset PlayedAt, long TotalPlayTimeSeconds = 0);

internal static class GameLaunchHistoryService
{
    private const int MaxEntries = 50;
    private const long MaxFileBytes = 64 * 1024;
    private static readonly long MaxPlayTimeSeconds = TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond;

    private static string PathFor(string root) => Path.Combine(root, "Settings", "GameLaunchHistory.json");

    public static IReadOnlyList<GameLaunchHistoryEntry> Load(string root)
    {
        string path = PathFor(root);
        if (!File.Exists(path)) return Array.Empty<GameLaunchHistoryEntry>();

        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxFileBytes) return Array.Empty<GameLaunchHistoryEntry>();
            var entries = JsonSerializer.Deserialize<List<GameLaunchHistoryEntry>>(File.ReadAllText(path));
            return (entries ?? new List<GameLaunchHistoryEntry>())
                .Where(IsValidEntry)
                .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.PlayedAt).First())
                .OrderByDescending(entry => entry.PlayedAt)
                .Take(MaxEntries)
                .ToArray();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Array.Empty<GameLaunchHistoryEntry>();
        }
    }

    public static bool Clear(string root)
    {
        string path = PathFor(root);
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryRecordLaunch(string root, string title, DateTimeOffset playedAt)
    {
        if (!IsValidTitle(title)) return false;

        string path = PathFor(root);
        List<GameLaunchHistoryEntry> entries;
        if (File.Exists(path))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxFileBytes) return false;
                entries = JsonSerializer.Deserialize<List<GameLaunchHistoryEntry>>(File.ReadAllText(path));
                if (entries == null) return false;
                if (entries.Any(entry => !IsValidEntry(entry))) return false;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
        else
        {
            entries = new List<GameLaunchHistoryEntry>();
        }

        long totalPlayTime = entries.FirstOrDefault(entry => string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase))?.TotalPlayTimeSeconds ?? 0;
        entries.RemoveAll(entry => string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase));
        entries.Add(new GameLaunchHistoryEntry(title.Trim(), playedAt.ToUniversalTime(), totalPlayTime));
        entries = entries.OrderByDescending(entry => entry.PlayedAt).Take(MaxEntries).ToList();

        return TryWrite(path, entries);
    }

    public static bool TryAddPlayTime(string root, string title, TimeSpan duration)
    {
        if (!IsValidTitle(title) || duration <= TimeSpan.Zero)
            return false;

        string path = PathFor(root);
        if (!File.Exists(path)) return false;

        List<GameLaunchHistoryEntry> entries;
        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxFileBytes) return false;
            entries = JsonSerializer.Deserialize<List<GameLaunchHistoryEntry>>(File.ReadAllText(path));
            if (entries == null || entries.Any(entry => !IsValidEntry(entry))) return false;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        int index = entries.FindIndex(entry => string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;

        long addedSeconds = Math.Max(1, (long)Math.Ceiling(duration.TotalSeconds));
        GameLaunchHistoryEntry entry = entries[index];
        long totalSeconds = addedSeconds > MaxPlayTimeSeconds - entry.TotalPlayTimeSeconds
            ? MaxPlayTimeSeconds
            : entry.TotalPlayTimeSeconds + addedSeconds;
        entries[index] = entry with { TotalPlayTimeSeconds = totalSeconds };
        return TryWrite(path, entries);
    }

    private static bool TryWrite(string path, List<GameLaunchHistoryEntry> entries)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static bool IsValidEntry(GameLaunchHistoryEntry entry) => entry != null && IsValidTitle(entry.Title) &&
        entry.TotalPlayTimeSeconds >= 0 && entry.TotalPlayTimeSeconds <= MaxPlayTimeSeconds;

    private static bool IsValidTitle(string title) => !string.IsNullOrWhiteSpace(title) && title.Length <= 128 &&
        title.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && title.IndexOfAny(new[] { '/', '\\' }) < 0 && !title.Any(char.IsControl);
}
