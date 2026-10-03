using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class FirstUseCheck
{
    private static object Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static Control[] Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child))).ToArray();
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    internal static void Run(string root, Assembly app)
    {
        string fixture = Path.Combine(root, "first-use");
        Directory.CreateDirectory(fixture);
        Type dialogType = app.GetType("RomImportDialog");
        Type identityType = app.GetType("RomIdentity");
        foreach (var sample in new[] { ("Pokemon_Unbound.gba", "FireRed", 3, 5), ("Pokemon_Renegade_Platinum.nds", "Platinum", 4, 9), ("Pokemon_X.3ds", "X", 6, 8) })
        {
            string rom = Path.Combine(fixture, sample.Item1);
            object identity = Activator.CreateInstance(identityType, new object[] { "POKEMON", sample.Item2, sample.Item3, true });
            using var dialog = (Form)Activator.CreateInstance(dialogType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { rom, identity, null }, null);
            var picker = (ComboBox)Field(dialog, "basePicker");
            Assert(picker.Items.Count == sample.Item4 && (string)picker.SelectedItem == sample.Item2, "ROM picker limits bases to " + Path.GetExtension(rom));
            Assert(((TextBox)Field(dialog, "titleInput")).Text == Path.GetFileNameWithoutExtension(rom).Replace('_', ' ') && ((TextBox)Field(dialog, "titleInput")).MaxLength == 80, "ROM title keeps the filename rather than a recycled Pokémon header");
            Assert(((CheckBox)Field(dialog, "confirmContent")).Checked == (sample.Item3 < 6), "3DS still requires explicit Pokémon confirmation");
            Assert(picker.AccessibleName.Contains("compatível") && dialog.AcceptButton is Button button && button.Text == "Adicionar", "import dialog exposes its system restriction and action accessibly");
        }

        Type libraryType = app.GetType("LibraryView");
        using (var library = (Control)Activator.CreateInstance(libraryType, new object[] { null, fixture }))
        {
            var hero = (Control)Field(library, "heroBanner");
            Assert(hero.AccessibleDescription == "10 jogos prontos para jogar · selecione uma aventura", "library banner exposes the full catalog count before filtering");
            Assert(Descendants(library).Count(control => Equals(control.Tag, "library-getting-started")) == 1, "clean library provides actionable first-use guidance");
            ((Control)Field(library, "search")).Text = "not-a-game";
            Assert(hero.AccessibleDescription == "Exibindo 0 de 10 jogos encontrados", "library banner announces zero search results");
            Assert(!Descendants(library).Any(control => Equals(control.Tag, "library-getting-started")), "first-use guidance does not replace filtered empty states");
            ((Control)Field(library, "search")).Text = "Emerald";
            Assert(hero.AccessibleDescription == "Exibindo 1 de 10 jogos encontrados", "library banner announces the count for one matching game");
            ((Control)Field(library, "search")).Text = "";
            Assert(hero.AccessibleDescription == "10 jogos prontos para jogar · selecione uma aventura", "clearing search restores the unfiltered library summary");
            Assert(Descendants(library).Any(control => Equals(control.Tag, "library-getting-started")), "clearing search restores first-use guidance");
            using var host = new Form { Width = 1000, Height = 840 };
            library.Dock = DockStyle.Fill;
            host.Controls.Add(library);
            host.Show();
            Application.DoEvents();
            Control guide = Descendants(library).Single(control => Equals(control.Tag, "library-getting-started"));
            Assert(guide.Controls.Cast<Control>().All(control => control.Right <= guide.Width && control.Bottom <= guide.Height), "first-use guidance controls fit at minimum launcher width");
            ((Button)guide.Controls.Cast<Control>().Single(control => control is Button)).PerformClick();
            var menu = ((Control)Field(library, "importButton")).ContextMenuStrip;
            Assert(menu.Visible && menu.Items.Cast<ToolStripItem>().Any(item => item.Text.Contains("arquivo")) && menu.Items.Cast<ToolStripItem>().Any(item => item.Text.Contains("pasta")), "first-use action opens file and folder import choices even from the keyboard");
            menu.Close();
            host.Controls.Remove(library);
            host.Close();
        }
        Type importedType = app.GetType("ImportedPokemonGame");
        object existing = Activator.CreateInstance(importedType);
        importedType.GetProperty("Title").SetValue(existing, "My Pokémon hack");
        importedType.GetProperty("BaseGame").SetValue(existing, "Emerald");
        importedType.GetProperty("RomPath").SetValue(existing, Path.Combine(fixture, "my-custom-rom.gba"));
        importedType.GetProperty("Generation").SetValue(existing, 3);
        object gbaIdentity = Activator.CreateInstance(identityType, new object[] { "POKEMON", "FireRed", 3, true });
        using (var editDialog = (Form)Activator.CreateInstance(dialogType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { Path.Combine(fixture, "another-name.gba"), gbaIdentity, existing }, null))
        {
            Assert(((TextBox)Field(editDialog, "titleInput")).Text == "My Pokémon hack" && ((ComboBox)Field(editDialog, "basePicker")).SelectedItem.ToString() == "Emerald" && ((Button)editDialog.AcceptButton).Text == "Salvar", "editing keeps the custom title and base and names the save action correctly");
        }
        importedType.GetProperty("Title").SetValue(existing, "FireRed");
        object importedGame = app.GetType("ImportedGameCatalog").GetMethod("ToGameInfo", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { existing });
        Type launcherSettings = app.GetType("LauncherSettings");
        object CallSaveName(string method, object[] arguments) => launcherSettings.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single(candidate => candidate.Name == method && candidate.GetParameters().Length == arguments.Length).Invoke(null, arguments);
        Assert((string)CallSaveName("SaveFileName", new[] { importedGame }) == "my-custom-rom.sav" && (string)CallSaveName("RetroArchSaveFileName", new[] { importedGame }) == "my-custom-rom.srm" && (string)CallSaveName("SaveFileName", new[] { fixture, importedGame }) == "my-custom-rom.sav", "imported ROMs use their own save filename even when named like a legacy game");
        string launcher = Path.Combine(fixture, "Pokemon - Executaveis", "Pokemon FireRed", "Pokemon FireRed.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(launcher));
        File.WriteAllText(launcher, "test fixture, never executed");
        using var configuredLibrary = (Control)Activator.CreateInstance(libraryType, new object[] { null, fixture });
        Assert(!Descendants(configuredLibrary).Any(control => Equals(control.Tag, "library-getting-started")), "existing local installations keep the familiar library without onboarding");
    }
}
