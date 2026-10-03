using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class LibrarySearchCheck
{
    internal static void Run(string root)
    {
        string catalogRoot=System.IO.Path.Combine(root,"search-fixture");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(catalogRoot,"Settings"));
        string rom=System.IO.Path.Combine(catalogRoot,"Ruby.gba");System.IO.File.WriteAllText(rom,"synthetic ROM metadata fixture");
        System.IO.File.WriteAllText(System.IO.Path.Combine(catalogRoot,"Settings","ImportedPokemonGames.json"),System.Text.Json.JsonSerializer.Serialize(new[]{new ImportedPokemonGame{RomPath=rom,Title="Ruby",BaseGame="Ruby",Generation=3}}));
        using var library=new LibraryView(null,catalogRoot);
        var search=(ThemeInput)typeof(LibraryView).GetField("search",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(library);
        System.Collections.Generic.IEnumerable<Control> Children(Control control)=>control.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        search.Text="ruby advance";
        Assert(Children(library).OfType<GameCard>().Any(card=>card.AccessibleName.StartsWith("Ruby,")),"library search combines title and platform words instead of requiring one exact phrase");
        search.Text="  RUBY\tGBA  ";
        Assert(Children(library).OfType<GameCard>().Count()==1,"library search supports GBA abbreviation, casing and whitespace");
        search.Text="ruby ds";
        Assert(!Children(library).OfType<GameCard>().Any(),"library search requires every query word to match the same game");
        search.Text=string.Empty;
        Assert(Children(library).OfType<GameCard>().Count()>1,"clearing search restores the collection");
        Assert(GameSearch.Matches(new GameInfo{Title="Pokémon Émeraude",Subtitle="Nintendo DS",Generation=4},"pokemon emeraude ds"),"combined search retains case and accent-insensitive matching");
        Assert(GameSearch.Matches(new GameInfo{Generation=3},"gba")&&!GameSearch.Matches(new GameInfo{Generation=3},"missing"),"partial metadata remains searchable without throwing");
    }
}
