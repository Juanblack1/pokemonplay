using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class PokemonBasesCheck
{
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    internal static void Run(string root, Assembly app)
    {
        string fixture = Path.Combine(root, "pokemon-bases"); Directory.CreateDirectory(fixture);
        Type catalog = app.GetType("ImportedGameCatalog");
        foreach (var sample in new[] { ("Ruby", ".gba", 3), ("Sapphire", ".gba", 3), ("Diamond", ".nds", 4), ("Pearl", ".nds", 4), ("Omega Ruby", ".3ds", 6), ("Alpha Sapphire", ".3ds", 6) })
        {
            string path = Path.Combine(fixture, "Pokemon_" + sample.Item1.Replace(' ', '_') + sample.Item2);
            byte[] header = new byte[0xB0];
            if (sample.Item3 < 6) System.Text.Encoding.ASCII.GetBytes("POKEMON").CopyTo(header, sample.Item3 == 3 ? 0xA0 : 0);
            File.WriteAllBytes(path, header);
            object identity = catalog.GetMethod("ReadIdentity", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { path });
            Assert((string)identity.GetType().GetProperty("BaseGame").GetValue(identity) == sample.Item1 && (int)identity.GetType().GetProperty("Generation").GetValue(identity) == sample.Item3, "base detection respects full name and console: " + sample.Item1);
            using var dialog = (Form)Activator.CreateInstance(app.GetType("RomImportDialog"), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { path, identity, null }, null);
            var picker = (ComboBox)Field(dialog, "basePicker");
            Assert(picker.SelectedItem.ToString() == sample.Item1 && picker.Items.Count == (sample.Item3 == 3 ? 5 : sample.Item3 == 4 ? 9 : 8), "ROM picker offers correct console-specific base: " + sample.Item1);
            Type recordType = app.GetType("ImportedPokemonGame"); object entry = Activator.CreateInstance(recordType);
            recordType.GetProperty("RomPath").SetValue(entry, path); recordType.GetProperty("Title").SetValue(entry, sample.Item1);
            recordType.GetProperty("BaseGame").SetValue(entry, sample.Item1); recordType.GetProperty("Generation").SetValue(entry, sample.Item3);
            object game = catalog.GetMethod("ToGameInfo", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { entry });
            string cover = (string)game.GetType().GetField("Cover").GetValue(game);
            Assert(cover != null && !File.Exists(Path.Combine(fixture, cover)) && cover.Contains(sample.Item1), "base references its own cover rather than another game: " + sample.Item1);
            using var card = (Control)Activator.CreateInstance(app.GetType("GameCard"), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new[] { game, fixture }, null);
            using var bitmap = new Bitmap(card.Width, card.Height);
            card.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            Assert(card.AccessibleName.Contains(sample.Item1), "missing base cover keeps the game card readable: " + sample.Item1);
        }
    }
}
