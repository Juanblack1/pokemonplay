using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static class GameFavoriteService
{
    private static string PathFor(string root) => Path.Combine(root, "Settings", "GameFavorites.json");

    public static HashSet<string> Load(string root)
    {
        string path = PathFor(root);
        return Read(path, out _);
    }

    private static HashSet<string> Read(string path, out bool corrupt)
    {
        corrupt = false;
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new List<string>();
            return new HashSet<string>(names.Where(IsValidTitle), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { corrupt = true; return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
        catch (IOException) { corrupt = true; return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    public static void Set(string root, string title, bool favorite)
    {
        if (!IsValidTitle(title)) throw new ArgumentException("Jogo inválido.", nameof(title));
        string path = PathFor(root);
        HashSet<string> names = Read(path, out bool corrupt);
        if (favorite) names.Add(title.Trim());
        else names.Remove(title.Trim());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (corrupt)
        {
            string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            File.Copy(path, backup, false);
        }
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool IsValidTitle(string title) => !string.IsNullOrWhiteSpace(title) && title.Length <= 128 &&
        title.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && title.IndexOfAny(new[] { '/', '\\' }) < 0 && !title.Any(char.IsControl);
}
