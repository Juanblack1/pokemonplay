using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ImportedRomRecoveryCheck
{
    private static Assembly app;
    private static Type Catalog => app.GetType("ImportedGameCatalog");
    private static object Call(Type type, string name, object target, params object[] args) => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single(method => method.Name == name && method.GetParameters().Length == args.Length).Invoke(target, args);
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }

    internal static void Run(string root, Assembly assembly)
    {
        app = assembly;
        string fixture = Path.Combine(root, "rom-recovery");
        string rom = Path.Combine(fixture, "new-location", "My Hack.gba");
        Directory.CreateDirectory(Path.GetDirectoryName(rom));
        byte[] romBytes = new byte[0xB0];
        System.Text.Encoding.ASCII.GetBytes("POKEMON").CopyTo(romBytes, 0xA0);
        File.WriteAllBytes(rom, romBytes);
        Type recordType = app.GetType("ImportedPokemonGame");
        object entry = Activator.CreateInstance(recordType);
        void Set(string property, object value) => recordType.GetProperty(property).SetValue(entry, value);
        Set("Title", "My Pokémon Hack"); Set("BaseGame", "FireRed"); Set("Generation", 3); Set("IsHackRom", true);
        Set("RomPath", Path.Combine(fixture, "old-location", "My Hack.gba"));
        var entries = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(recordType));
        entries.Add(entry);
        Call(Catalog, "Save", null, fixture, entries);
        string id = (string)recordType.GetProperty("Id").GetValue(entry);
        object originalGame = Call(Catalog, "ToGameInfo", null, entry);
        string saveFolder = (string)originalGame.GetType().GetField("SaveFolderName").GetValue(originalGame);
        string savePath = Path.Combine(fixture, "Saves", saveFolder, "My Hack.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)); File.WriteAllText(savePath, "existing progress");
        Assert(((IList)Call(Catalog, "Build", null, fixture)).Count == 0 && ((IList)Call(Catalog, "Entries", null, fixture)).Count == 1, "missing ROM remains manageable even when absent from the playable catalog");

        string catalogPath = Path.Combine(fixture, "Settings", "ImportedPokemonGames.json");
        byte[] originalCatalog = File.ReadAllBytes(catalogPath);
        foreach (string invalid in new[] { Path.Combine(fixture, "absent", "My Hack.gba"), Path.Combine(fixture, "new-location", "different-name.gba"), Path.Combine(fixture, "new-location", "My Hack.nds") })
        {
            if (!invalid.Contains("absent")) File.WriteAllBytes(invalid, romBytes);
            bool rejected = false;
            try { Call(Catalog, "Relocate", null, fixture, id, invalid); }
            catch (TargetInvocationException exception) when (exception.InnerException is IOException) { rejected = true; }
            Assert(rejected && File.ReadAllBytes(catalogPath).SequenceEqual(originalCatalog), "invalid replacement preserves catalog: " + Path.GetFileName(invalid));
        }

        using (var dialog = (Form)Activator.CreateInstance(app.GetType("ImportedGameManagerDialog"), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { fixture }, null))
        {
            var list = (ListBox)Field(dialog, "gameList");
            var locate = (Button)Field(dialog, "locateButton");
            Assert(list.Items.Count == 1 && list.Items[0].ToString().Contains("Não encontrado") && locate.Enabled && ((TextBox)Field(dialog, "details")).Text.Contains("old-location"), "manager explains missing ROM and enables locating it");
            dialog.Show(); dialog.Width = 620; Application.DoEvents();
            Assert(locate.Visible && locate.Right <= locate.Parent.ClientSize.Width && list.Height > 60 && ((TextBox)Field(dialog, "details")).Height >= 70, "ROM manager keeps list, path and actions usable at minimum width");
            Call(dialog.GetType(), "RecoverSelected", dialog, Path.Combine(fixture, "new-location", "different-name.gba"));
            Assert(File.ReadAllBytes(catalogPath).SequenceEqual(originalCatalog) && ((Label)Field(dialog, "status")).AccessibleDescription.Contains("mesmo nome"), "manager explains filename restriction and preserves catalog after an invalid selection");
            Call(dialog.GetType(), "RecoverSelected", dialog, rom);
            Assert((bool)dialog.GetType().GetProperty("Changed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog) && list.Items[0].ToString().Contains("Disponível") && ((Label)Field(dialog, "status")).AccessibleDescription.Contains("saves mantidos"), "manager refreshes availability and exposes the recovery result accessibly");
            dialog.Close();
        }
        object restored = ((IList)Call(Catalog, "Build", null, fixture))[0];
        Assert((string)restored.GetType().GetField("SaveFolderName").GetValue(restored) == saveFolder && (string)restored.GetType().GetField("Title").GetValue(restored) == "My Pokémon Hack" && (bool)restored.GetType().GetField("IsHackRom").GetValue(restored), "relocation keeps title, hack badge and save-folder identity");
        Assert(File.ReadAllBytes(rom).SequenceEqual(romBytes) && File.ReadAllText(savePath) == "existing progress" && !Directory.Exists(Path.Combine(fixture, "old-location")), "relocation changes metadata without copying ROMs or touching saves");

        string anotherRom = Path.Combine(fixture, "other-game", "My Hack.gba");
        Directory.CreateDirectory(Path.GetDirectoryName(anotherRom)); File.WriteAllBytes(anotherRom, romBytes);
        object anotherEntry = Activator.CreateInstance(recordType);
        recordType.GetProperty("RomPath").SetValue(anotherEntry, anotherRom);
        recordType.GetProperty("Title").SetValue(anotherEntry, "Another game");
        recordType.GetProperty("BaseGame").SetValue(anotherEntry, "FireRed");
        recordType.GetProperty("Generation").SetValue(anotherEntry, 3);
        var currentEntries = (IList)Call(Catalog, "Entries", null, fixture); currentEntries.Add(anotherEntry);
        Call(Catalog, "Save", null, fixture, currentEntries);
        byte[] beforeDuplicate = File.ReadAllBytes(catalogPath);
        bool duplicateRejected = false;
        try { Call(Catalog, "Relocate", null, fixture, id, anotherRom); }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidDataException error && error.Message.Contains("outro jogo")) { duplicateRejected = true; }
        Assert(duplicateRejected && File.ReadAllBytes(catalogPath).SequenceEqual(beforeDuplicate), "ROM already associated with another entry cannot replace the selected game");

        using (var emptyDialog = (Form)Activator.CreateInstance(app.GetType("ImportedGameManagerDialog"), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { Path.Combine(root, "rom-recovery-empty") }, null))
            Assert(!((Button)Field(emptyDialog, "locateButton")).Enabled && ((Label)Field(emptyDialog, "status")).Text.Contains("Nenhum jogo importado"), "empty manager offers import guidance and disables locating without a selection");

        string corruptRoot = Path.Combine(root, "rom-recovery-corrupt");
        string corruptPath = Path.Combine(corruptRoot, "Settings", "ImportedPokemonGames.json");
        Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)); File.WriteAllText(corruptPath, "{ broken");
        Call(app.GetType("GameCatalog"), "Build", null, corruptRoot);
        using var corruptDialog = (Form)Activator.CreateInstance(app.GetType("ImportedGameManagerDialog"), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { corruptRoot }, null);
        Assert(!((Button)Field(corruptDialog, "locateButton")).Enabled && ((Label)Field(corruptDialog, "status")).AccessibleDescription.Contains("catálogo") && File.ReadAllText(corruptPath) == "{ broken", "malformed catalog is reported without preventing the library or overwriting user metadata");
    }
}
