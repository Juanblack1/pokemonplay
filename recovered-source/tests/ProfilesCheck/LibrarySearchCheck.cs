using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Diagnostics;

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
        Type favoriteService=typeof(LibraryView).Assembly.GetType("GameFavoriteService");
        string favoritePath=System.IO.Path.Combine(catalogRoot,"Settings","GameFavorites.json");
        void SetFavorite(string title,bool value)=>favoriteService.GetMethod("Set",BindingFlags.Static|BindingFlags.Public).Invoke(null,new object[]{catalogRoot,title,value});
        Control EmptyState()=>Children(library).Single(control=>control.GetType().Name=="EmptyStatePanel");
        string[] EmptyLabels()=>EmptyState().Controls.OfType<Label>().Select(label=>label.Text).ToArray();
        Button favoriteFilter=(Button)typeof(LibraryView).GetField("favoriteFilter",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(library);
        Button recentFilter=Children(library).OfType<Button>().Single(button=>button.Text=="Recentes");
        void PumpUntil(Func<bool> condition,string failure){var wait=Stopwatch.StartNew();while(!condition()&&wait.ElapsedMilliseconds<2000){Application.DoEvents();Thread.Sleep(10);}Assert(condition(),failure);}
        SetFavorite("Ruby",true);byte[] favoriteBytes=File.ReadAllBytes(favoritePath);
        recentFilter.PerformClick();favoriteFilter.PerformClick();search.Text="no matching favorite";
        PumpUntil(()=>Children(library).Any(control=>control.GetType().Name=="EmptyStatePanel"),"filtered favorites settle to their empty state");
        Assert(!Children(library).OfType<GameCard>().Any(),"nonmatching query hides all saved favorites");
        Assert(EmptyLabels()[0]=="Nenhum favorito encontrado"&&EmptyLabels()[1].Contains("busca",StringComparison.OrdinalIgnoreCase),"empty search in Favorites and Recentes explains that saved favorites did not match");
        Assert(File.ReadAllBytes(favoritePath).SequenceEqual(favoriteBytes),"searching Favorites leaves persisted favorites byte-for-byte unchanged");
        EmptyState().Controls.OfType<Button>().Single().PerformClick();PumpUntil(()=>search.Text.Length==0&&Children(library).OfType<GameCard>().Any(card=>card.AccessibleName.StartsWith("Ruby,")),"MOSTRAR TODOS restores the saved favorite card");
        Assert(File.ReadAllBytes(favoritePath).SequenceEqual(favoriteBytes),"MOSTRAR TODOS leaves persisted favorites byte-for-byte unchanged");
        Assert(search.Text.Length==0&&favoriteFilter.AccessibleName=="Mostrar somente jogos favoritos"&&Children(library).OfType<GameCard>().Any(card=>card.AccessibleName.StartsWith("Ruby,")),"MOSTRAR TODOS clears the search and filter and restores cards");
        SetFavorite("Ruby",false);favoriteFilter.PerformClick();
        PumpUntil(()=>Children(library).Any(control=>control.GetType().Name=="EmptyStatePanel"),"empty Favorites settles after removing its last star");
        Assert(EmptyLabels()[0]=="Nenhum favorito ainda"&&EmptyLabels()[1].Contains("estrela",StringComparison.OrdinalIgnoreCase),"truly empty Favorites retains star guidance");
        EmptyState().Controls.OfType<Button>().Single().PerformClick();Application.DoEvents();
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
