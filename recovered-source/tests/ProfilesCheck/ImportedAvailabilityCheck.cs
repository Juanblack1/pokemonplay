using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

internal static class ImportedAvailabilityCheck
{
    internal static void Run(string root)
    {
        string fixture=Path.Combine(root,"imported-availability");Directory.CreateDirectory(Path.Combine(fixture,"Settings"));
        string path=Path.Combine(fixture,"Settings","ImportedPokemonGames.json"),rom=Path.Combine(fixture,"moved.gba");
        var entry=new ImportedPokemonGame {Title="Missing Pokémon",BaseGame="FireRed",Generation=3,RomPath=rom};
        File.WriteAllText(path,JsonSerializer.Serialize(new[]{entry}));byte[] before=File.ReadAllBytes(path);
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        System.Collections.Generic.IEnumerable<Control> Children(Control control)=>control.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
        void Preview(LibraryView library,string name) {
            string output=Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_PREVIEW");if(string.IsNullOrEmpty(output))return;
            Directory.CreateDirectory(output);
            var notice=Children(library).Single(control=>Equals(control.Tag,"imported-rom-notice"));
            foreach(int width in new[]{760,1100}) {
                notice.Width=width;notice.CreateControl();notice.PerformLayout();
                using var bitmap=new Bitmap(notice.Width,notice.Height);notice.DrawToBitmap(bitmap,notice.ClientRectangle);
                bitmap.Save(Path.Combine(output,name+"-"+width+".png"));
            }
        }
        Assert(ImportedGameAvailability.Inspect(fixture).UnavailableCount==1,"unavailable imported ROM is counted without removing its metadata");
        using(var library=new LibraryView(null,fixture)){Assert(Children(library).Any(control=>Equals(control.Tag,"imported-rom-notice")&&control.AccessibleName.Contains("não está acessível")),"library offers recovery guidance when an imported ROM disappears");Preview(library,"missing-rom");}
        Assert(File.ReadAllBytes(path).SequenceEqual(before),"availability notice preserves catalog bytes and save association");
        File.WriteAllText(rom,"fixture");
        Assert(ImportedGameAvailability.Inspect(fixture).UnavailableCount==0,"reconnected ROM is available again without reimporting");
        File.WriteAllText(path,"{invalid catalog");before=File.ReadAllBytes(path);
        using(var library=new LibraryView(null,fixture)){Assert(Children(library).Any(control=>Equals(control.Tag,"imported-rom-notice")&&control.AccessibleName.Contains("Não foi possível ler")),"malformed imported catalog is visible in the library without crashing");Preview(library,"invalid-catalog");}
        Assert(File.ReadAllBytes(path).SequenceEqual(before),"malformed catalog notice does not overwrite the original file");
        entry.RomPath=null;File.WriteAllText(path,JsonSerializer.Serialize(new[]{entry}));
        Assert(ImportedGameAvailability.Inspect(fixture).CatalogUnreadable&&!ImportedGameCatalog.IsSupportedRom(null),"null ROM path is treated as invalid catalog data instead of dereferenced");
    }
}
