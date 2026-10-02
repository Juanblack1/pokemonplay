using System;
using System.Collections.Generic;
using System.IO;
using PKHeX.Core;

internal sealed class ProfileSaveChoice
{
    public string Path { get; }
    public ProfileSaveChoice(string path) { Path = path; }
    public override string ToString() => System.IO.Path.GetFileName(Path);
}

internal static class ProfileSaveLocator
{
    public static List<ProfileSaveChoice> Find(string root, GameInfo game, string profileId)
    {
        string folder = SaveProfileService.Folder(root, game.SaveFolderName, profileId);
        var choices = new List<ProfileSaveChoice>();
        if (!Directory.Exists(folder)) return choices;
        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (extension is not (".sav" or ".srm" or ".dsv" or ".dat" or ".bin")) continue;
            try
            {
                if (new FileInfo(file).Length > 2 * 1024 * 1024) continue;
                var save = SaveUtil.GetSaveFile(file);
                if (extension == ".srm")
                {
                    if (!string.Equals(System.IO.Path.GetFileName(file), LauncherSettings.RetroArchSaveFileName(game), StringComparison.OrdinalIgnoreCase)) continue;
                    save = ReadRetroArchSave(file, game, true);
                }
                if (SaveProfileService.IsSaveForGame(save, game)) choices.Add(new ProfileSaveChoice(file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        string expected = LauncherSettings.SaveFileName(root, game);
        choices.Sort((a, b) =>
        {
            bool first = string.Equals(System.IO.Path.GetFileName(a.Path), expected, StringComparison.OrdinalIgnoreCase);
            bool second = string.Equals(System.IO.Path.GetFileName(b.Path), expected, StringComparison.OrdinalIgnoreCase);
            return first != second ? (first ? -1 : 1) : StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path);
        });
        return choices;
    }

    internal static SaveFile ReadRetroArchSave(string path, GameInfo game, bool requireRomName)
    {
        if (requireRomName && !string.Equals(System.IO.Path.GetFileName(path), LauncherSettings.RetroArchSaveFileName(game), StringComparison.OrdinalIgnoreCase)) return null;
        byte[] data = File.ReadAllBytes(path);
        SaveFile detected = SaveUtil.GetSaveFile(data.AsMemory(), System.IO.Path.ChangeExtension(path, ".sav"));
        if (detected == null || detected.Generation != game.Generation) return null;
        return game.Title switch
        {
            "FireRed" or "LeafGreen" => new SAV3FRLG(data.AsMemory()),
            "Emerald" => new SAV3E(data.AsMemory()),
            "HeartGold" or "SoulSilver" => new SAV4HGSS(data.AsMemory()),
            "Platinum" => new SAV4Pt(data.AsMemory()),
            "Black" or "White" => new SAV5BW(data.AsMemory()),
            "Black 2" or "White 2" => new SAV5B2W2(data.AsMemory()),
            _ => null
        };
    }
}
