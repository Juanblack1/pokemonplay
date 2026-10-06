using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using PKHeX.Core;

internal sealed class SaveProfile
{
    public string Id { get; set; } = "default";
    public string Name { get; set; } = "Principal";
    public override string ToString() => Name;
}

internal sealed class SaveProfileState
{
    public string ActiveId { get; set; } = "default";
    public List<SaveProfile> Profiles { get; set; } = new() { new SaveProfile() };
}

internal static class SaveProfileService
{
    private static void ValidateGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game) || game != Path.GetFileName(game) || game.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || game is "." or "..")
            throw new InvalidDataException("Identificação de jogo inválida.");
    }
    private static string StatePath(string root, string game)
    {
        ValidateGame(game);
        return Path.Combine(root, "Settings", "SaveProfiles", game + ".json");
    }
    public static SaveProfileState Load(string root, string game)
    {
        string path = StatePath(root, game);
        if (!File.Exists(path)) return new SaveProfileState();
        var state = JsonSerializer.Deserialize<SaveProfileState>(File.ReadAllText(path));
        if (state?.Profiles == null || state.Profiles.Count == 0 || state.Profiles.Any(p => p == null || !ValidId(p.Id) || string.IsNullOrWhiteSpace(p.Name)) ||
            state.Profiles.Select(p => p.Id).Distinct().Count() != state.Profiles.Count || !state.Profiles.Any(p => p.Id == "default") || !state.Profiles.Any(p => p.Id == state.ActiveId))
            throw new InvalidDataException("A lista de perfis está inválida. Os saves foram preservados.");
        return state;
    }
    private static bool ValidId(string id) => id == "default" || Guid.TryParseExact(id, "N", out _);
    private static void Write(string root, string game, SaveProfileState state)
    {
        string path = StatePath(root, game);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string Folder(string root, string game, string id)
    {
        ValidateGame(game);
        if (!ValidId(id)) throw new InvalidDataException("Perfil inválido.");
        return id == "default" ? Path.Combine(root, "Saves", game) : Path.Combine(root, "Saves", "ProfileSaves", game, id);
    }
    public static string ActiveFolder(string root, string game) => Folder(root, game, Load(root, game).ActiveId);
    public static string CloudId(string root, string game)
    {
        string id = Load(root, game).ActiveId;
        return id == "default" ? game : game + "-profile-" + id;
    }
    public static void EnsureEmulatorsClosed()
    {
        foreach (string name in new[] { "visualboyadvance-m", "melonDS", "retroarch" })
            foreach (Process process in Process.GetProcessesByName(name))
                using (process) { if (!process.HasExited) throw new IOException("Feche o emulador antes de criar ou trocar de perfil."); }
    }
    public static void Activate(string root, string game, string id)
    {
        EnsureEmulatorsClosed();
        var state = Load(root, game);
        if (!state.Profiles.Any(p => p.Id == id)) throw new InvalidDataException("Perfil não encontrado.");
        Directory.CreateDirectory(Folder(root, game, id));
        state.ActiveId = id;
        Write(root, game, state);
    }
    public static SaveProfile Create(string root, GameInfo game, string name, bool copyCurrent, string importPath = null)
    {
        EnsureEmulatorsClosed();
        if (game.Generation is < 3 or > 5) throw new InvalidOperationException("Perfis disponíveis para GBA e DS.");
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 40 || name.Any(char.IsControl)) throw new ArgumentException("Use um nome de 1 a 40 caracteres.");
        var state = Load(root, game.SaveFolderName);
        if (state.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um perfil com esse nome.");
        if (copyCurrent && importPath != null) throw new ArgumentException("Escolha copiar ou importar.");
        byte[] import = null;
        if (importPath != null)
        {
            if (new FileInfo(importPath).Length > 2 * 1024 * 1024) throw new InvalidDataException("O save é grande demais.");
            var save = ProfileSaveLocator.ReadForGame(importPath, game);
            var expected = GameVersionFor(game.Title);
            if (save == null || save.Generation != game.Generation || !save.Version.Contains(expected) || !save.ChecksumsValid)
                throw new InvalidDataException("Selecione um save normal deste jogo com checksums válidos.");
            import = save.Write(BinaryExportSetting.ExcludeHeader | BinaryExportSetting.ExcludeFooter).ToArray();
        }
        var profile = new SaveProfile { Id = Guid.NewGuid().ToString("N"), Name = name };
        string destination = Folder(root, game.SaveFolderName, profile.Id);
        Directory.CreateDirectory(destination);
        try
        {
            if (copyCurrent)
            {
                string source = Folder(root, game.SaveFolderName, state.ActiveId);
                if (Directory.Exists(source))
                {
                    string snapshot = Path.Combine(destination, ".snapshot.zip");
                    if (Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Any())
                    {
                        SaveBackupService.CreateVerifiedArchive(source, snapshot);
                        SaveBackupService.ExtractLocalArchive(snapshot, destination);
                        File.Delete(snapshot);
                    }
                }
            }
            if (import != null) File.WriteAllBytes(Path.Combine(destination, LauncherSettings.SaveFileName(root, game)), import);
            EnsureEmulatorsClosed();
            state.Profiles.Add(profile);
            state.ActiveId = profile.Id;
            Write(root, game.SaveFolderName, state);
            return profile;
        }
        catch
        {
            // Keep any staged files recoverable; they are not published as a profile.
            throw;
        }
    }

    public static void Rename(string root, string game, string id, string name)
    {
        EnsureEmulatorsClosed();
        if (!ValidId(id)) throw new InvalidDataException("Perfil inválido.");
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 40 || name.Any(char.IsControl))
            throw new ArgumentException("Use um nome de 1 a 40 caracteres.");
        var state = Load(root, game);
        SaveProfile profile = state.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile == null) throw new InvalidDataException("Perfil não encontrado.");
        if (state.Profiles.Any(p => p.Id != id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Já existe um perfil com esse nome.");
        profile.Name = name;
        Write(root, game, state);
    }
    public static bool IsSaveForGame(SaveFile save, GameInfo game) => save != null && save.Generation == game.Generation && save.Version.Contains(GameVersionFor(game.Title)) && save.HasBox;

    internal static GameVersion GameVersionFor(string title) => title switch
    {
        "FireRed" => GameVersion.FR, "LeafGreen" => GameVersion.LG, "Emerald" => GameVersion.E,
        "HeartGold" => GameVersion.HG, "SoulSilver" => GameVersion.SS, "Platinum" => GameVersion.Pt,
        "Black" => GameVersion.B, "White" => GameVersion.W, "Black 2" => GameVersion.B2, "White 2" => GameVersion.W2,
        _ => throw new InvalidOperationException("Jogo sem configuração de save.")
    };
}
