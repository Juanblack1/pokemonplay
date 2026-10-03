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
        using var window=new Form {ClientSize=new System.Drawing.Size(1000,800),ShowInTaskbar=false};
        window.Controls.Add(library);window.Show();Application.DoEvents();
        var original=Children(library).OfType<GameCard>().First();int disposed=0;original.Disposed+=(_,_)=>disposed++;
        search.Focus();search.Text="r";search.Text="ru";search.Text="ruby";
        Assert(disposed==0,"rapid search input keeps existing cards until the typing interval settles");
        var deadline=System.Diagnostics.Stopwatch.StartNew();
        while(disposed==0&&deadline.ElapsedMilliseconds<2000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        Assert(disposed==1&&Children(library).OfType<GameCard>().Count()==1,"settled search applies only the final query and disposes prior cards once");
        bool Command(Keys key){object[] args={new Message(),key};return (bool)typeof(LibraryView).GetMethod("ProcessCmdKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(library,args);}
        search.Focus();search.Text="emerald";
        Assert(Command(Keys.Enter)&&Children(library).OfType<GameCard>().Single().AccessibleName.StartsWith("Emerald,"),"Enter flushes pending search without launching a game");
        search.Text="nothing here";
        Assert(Command(Keys.Escape)&&search.Text.Length==0&&Children(library).OfType<GameCard>().Count()>1,"Escape restores all search results immediately even with pending typing");
        search.Text="black";
        var system=(ThemeSelect)typeof(LibraryView).GetField("systemFilter",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(library);
        system.SelectedIndex=1;
        Assert(!Children(library).OfType<GameCard>().Any(),"changing platform flushes the current query before the debounce timer fires");
        search.Text=string.Empty;system.SelectedIndex=0;
        search.Text="ruby";library.Hide();
        deadline.Restart();while(deadline.ElapsedMilliseconds<200){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        library.Show();Application.DoEvents();
        Assert(Children(library).OfType<GameCard>().Single().AccessibleName.StartsWith("Ruby,"),"showing the library applies the query that was pending when hidden");
        search.Text="emerald";library.Dispose();
        deadline.Restart();while(deadline.ElapsedMilliseconds<200){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        Assert(library.IsDisposed,"closing the library cancels its pending search without invoking disposed controls");
    }
}
