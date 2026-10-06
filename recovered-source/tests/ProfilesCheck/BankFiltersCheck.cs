using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PKHeX.Core;

internal static class BankFiltersCheck
{
    internal static void Run(string root, Assembly app)
    {
        Type spriteService=app.GetType("PokemonSpriteService");
        Uri[] SpritePaths(bool shiny,bool female)=>((System.Collections.IEnumerable)spriteService.GetMethod("SpriteUris",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{25,shiny,female})).Cast<Uri>().ToArray();
        const string spriteBase="/PokeAPI/sprites/master/sprites/pokemon/";
        Assert(SpritePaths(false,false).Select(uri=>uri.AbsolutePath).SequenceEqual(new[]{spriteBase+"25.png"}),"default Pokémon sprite uses its National Pokédex image");
        Assert(SpritePaths(true,false).Select(uri=>uri.AbsolutePath).SequenceEqual(new[]{spriteBase+"shiny/25.png",spriteBase+"25.png"}),"shiny Pokémon sprite prefers shiny art and falls back to the default");
        Assert(SpritePaths(false,true).Select(uri=>uri.AbsolutePath).SequenceEqual(new[]{spriteBase+"female/25.png",spriteBase+"25.png"}),"female Pokémon sprite prefers the female variant and falls back to the default");
        Assert(SpritePaths(true,true).Select(uri=>uri.AbsolutePath).SequenceEqual(new[]{spriteBase+"shiny/female/25.png",spriteBase+"shiny/25.png",spriteBase+"female/25.png",spriteBase+"25.png"}),"shiny female sprite falls back through the available variants");
        bool invalidSpeciesRejected=false;try{spriteService.GetMethod("SpriteUris",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{0,false,false});}catch(TargetInvocationException ex)when(ex.InnerException is ArgumentOutOfRangeException){invalidSpeciesRejected=true;}
        Assert(invalidSpeciesRejected,"sprite lookup rejects species outside the National Pokédex range");

        string fixture=Path.Combine(root,"bank-filters");
        string folder=Path.Combine(fixture,"Pokemon Bank");Directory.CreateDirectory(folder);
        void Write(string name,PKM p)
        {
            p.PID=name=="shiny.pk3"?0x00FF00FFu:0x12345678u;
            p.RefreshChecksum();byte[] bytes=new byte[p.SIZE_PARTY];p.WriteDecryptedDataParty(bytes);File.WriteAllBytes(Path.Combine(folder,name),bytes);
        }
        Write("0025-misleading.pk3",new PK3{Species=152,CurrentLevel=30,Gender=0});
        Write("pikachu.pk3",new PK3{Species=25,CurrentLevel=20,Gender=1});
        Write("bulbasaur.pk3",new PK3{Species=1,CurrentLevel=10,Gender=0});
        Write("treecko.pk3",new PK3{Species=252,CurrentLevel=50,Gender=0});
        Write("pikachu.pk9",new PK9{Species=25,CurrentLevel=70,Gender=1});
        Write("egg.pk3",new PK3{Species=133,CurrentLevel=5,Gender=0,IsEgg=true});
        Write("shiny.pk3",new PK3{Species=25,CurrentLevel=40,Gender=0});
        Type viewType=app.GetType("PokemonBankView");
        using var view=(Control)Activator.CreateInstance(viewType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{fixture},null);
        object Get(string name)=>viewType.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
        void Call(string name,params object[] args)=>viewType.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,args);
        void Pick(string name,int index){object picker=Get(name);picker.GetType().GetProperty("SelectedIndex").SetValue(picker,index);}
        Assert(((Label)Get("bankSummary")).Text.Contains("7 Pokémon · 5 espécies"),"global bank summary distinguishes valid Pokémon files from unique species");
        int[] Results()=>(int[])Get("bankViewIndices");
        ushort[] Species()=>Results().Select(i=>PokemonBankFileServiceRead(((string[])Get("bankFiles"))[i]).Species).ToArray();
        PKM PokemonBankFileServiceRead(string path)=>(PKM)app.GetType("PokemonBankFileService").GetMethod("Read",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{path});
        void Assert(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
        string[] configuredTypeNames=((System.Collections.IEnumerable)Get("bankTypePicker").GetType().GetProperty("Items").GetValue(Get("bankTypePicker"))).Cast<object>().Select(value=>Convert.ToString(value)).ToArray();
        string[] catalogTypeNames=((System.Collections.IEnumerable)app.GetType("PokemonTypeCatalog").GetProperty("Names",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Cast<string>().ToArray();
        Assert(configuredTypeNames.Skip(1).SequenceEqual(catalogTypeNames)&&catalogTypeNames.Length==18,"type badges and the bank filter share the same localized type names");
        var visibleCards=((Control)Get("slots")).Controls.OfType<Button>().Where(button=>button.GetType().Name=="PokemonSlotButton").ToArray();
        var bulbasaurCard=visibleCards.Single(button=>(int)button.GetType().GetProperty("PrimaryType").GetValue(button)==11&&(int)button.GetType().GetProperty("SecondaryType").GetValue(button)==3);
        Assert(bulbasaurCard.AccessibleName.Contains("tipos Planta · Veneno")&&bulbasaurCard.Height>=118,"global-bank card displays and announces both Pokémon types");
        string typePreview=Environment.GetEnvironmentVariable("POKEMONPLAY_BANK_TYPES_PREVIEW");
        if(!string.IsNullOrWhiteSpace(typePreview)){Directory.CreateDirectory(Path.GetDirectoryName(typePreview));using var image=new Bitmap(bulbasaurCard.Width,bulbasaurCard.Height);bulbasaurCard.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(typePreview,ImageFormat.Png);}
        Pick("bankGenerationPicker",1);
        Assert(Results().Length==5&&Species().All(s=>s<=151),"species Gen 1 includes Kanto in both PK3 and PK9, excludes Johto and Hoenn");
        Pick("bankFormatPicker",7);Assert(Results().Length==1,"species generation and file format filters intersect independently");
        Call("ClearBankFilters");Pick("bankDexPicker",1);
        Assert(Results().Length==5,"FireRed/LeafGreen regional dex contains Kanto species only");
        Pick("bankGenerationPicker",2);Assert(Results().Length==0,"regional and species generation filters combine, with no automatic widening");
        Call("ClearBankFilters");Pick("bankTypePicker",13);
        Assert(Results().Length==3&&Species().All(s=>s==25),"Electric type filter reads actual personal data across PK3 and PK9");
        Pick("bankShinyPicker",1);Assert(Results().Length==1,"shiny filter intersects type filter");
        Call("ClearBankFilters");Pick("bankTypePicker",4);Assert(Species().SequenceEqual(new ushort[]{1}),"type filter includes the secondary Poison type of Bulbasaur");
        Call("ClearBankFilters");Pick("bankGenderPicker",2);Assert(Results().Length==2,"gender filter selects female Pokemon");
        Call("ClearBankFilters");Pick("bankEggPicker",1);Assert(Results().Length==1&&Species()[0]==133,"egg filter selects eggs independently of species");
        Call("ClearBankFilters");((NumericUpDown)Get("bankMinLevel")).Value=30;((NumericUpDown)Get("bankMaxLevel")).Value=50;
        Assert(Results().Length==3,"inclusive level range combines lower and upper bounds");
        ((NumericUpDown)Get("bankMinLevel")).Value=60;Assert(Results().Length==0,"invalid level interval does not silently widen results");
        Call("ClearBankFilters");((TextBox)Get("bankSpeciesSearch")).Text="25";
        Assert(Results().Length==3&&Species().All(s=>s==25),"numeric search ignores misleading filenames and matches actual species");
        Call("ClearBankFilters");
        string accentedNicknameFile=Path.Combine(folder,"accented-nickname.pk9");
        Write(Path.GetFileName(accentedNicknameFile),new PK9{Species=25,CurrentLevel=20,Nickname="Flabébé",IsNicknamed=true});
        Call("RefreshBankFiles");((TextBox)Get("bankSpeciesSearch")).Text="flabebe";
        Assert(Results().Length==1&&Species()[0]==25,"global bank nickname search ignores accents like the game library search");
        File.Delete(accentedNicknameFile);Call("RefreshBankFiles");
        Call("ClearBankFilters");Assert(Results().Length==7,"clear filters resets every filter and level range");
        Pick("bankDuplicatePicker",1);
        Assert(Results().Length==3&&Species().All(species=>species==25),"global bank duplicate filter finds every entry of a repeated species");
        Pick("bankGenderPicker",2);
        Assert(Results().Length==2&&Species().All(species=>species==25),"global bank duplicate filter intersects with other active filters");
        Pick("bankDuplicatePicker",2);
        Assert(Results().Length==1&&Species().Distinct().Count()==1,"global bank one-per-species filter keeps one result after sorting and filtering");
        Call("ClearBankFilters");
        Assert(Results().Length==7,"clearing filters resets the duplicate-species mode");
        string uniqueOnlyRoot=Path.Combine(root,"unique-only-bank");string uniqueOnlyFolder=Path.Combine(uniqueOnlyRoot,"Pokemon Bank");Directory.CreateDirectory(uniqueOnlyFolder);
        File.Copy(Path.Combine(folder,"bulbasaur.pk3"),Path.Combine(uniqueOnlyFolder,"bulbasaur.pk3"));
        File.Copy(Path.Combine(folder,"treecko.pk3"),Path.Combine(uniqueOnlyFolder,"treecko.pk3"));
        using(var uniqueOnly=(Control)Activator.CreateInstance(viewType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{uniqueOnlyRoot},null))
        {
            var duplicatePicker=(ThemeSelect)viewType.GetField("bankDuplicatePicker",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(uniqueOnly);duplicatePicker.SelectedIndex=1;
            var uniqueSlots=(FlowLayoutPanel)viewType.GetField("slots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(uniqueOnly);
            var emptyState=uniqueSlots.Controls.OfType<EmptyStatePanel>().Single();
            Assert(emptyState.Controls.OfType<Label>().First().Text=="Nenhuma espécie repetida"&&emptyState.Controls.OfType<Label>().Skip(1).First().Text.Contains("resultados atuais"),"empty state explains when no repeated species match");
            viewType.GetMethod("ClearBankFilters",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(uniqueOnly,null);
            Assert(((int[])viewType.GetField("bankViewIndices",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(uniqueOnly)).Length==2,"clearing the duplicate empty state restores the distinct Pokémon");
        }
        Write("caught-new.pk9",new PK9{Species=25,CurrentLevel=10,MetDate=new DateOnly(2025,6,1)});
        Write("caught-old.pk9",new PK9{Species=25,CurrentLevel=10,MetDate=new DateOnly(2021,3,14)});
        var sortItems=(System.Collections.Generic.List<object>)Get("bankSortPicker").GetType().GetProperty("Items").GetValue(Get("bankSortPicker"));
        Assert(sortItems.Count>8&&sortItems[8].ToString()=="Captura · recentes","global bank exposes a compact capture-date sort label");
        Call("RefreshBankFiles");Pick("bankSortPicker",8);
        string[] captureOrder=Results().Select(i=>Path.GetFileName(((string[])Get("bankFiles"))[i])).ToArray();
        Assert(captureOrder.Take(2).SequenceEqual(new[]{"caught-new.pk9","caught-old.pk9"}),"capture-date sort orders dated Pokémon from newest to oldest");
        Assert(captureOrder.Skip(2).All(name=>name!="caught-new.pk9"&&name!="caught-old.pk9"),"capture-date sort places Pokémon without a date after dated entries");
        Call("SelectSlot",Results()[0]);
        Assert(((Label)Get("details")).Text.Contains("Data de captura: 01/06/2025"),"selected Pokémon details show its captured date");
        Type catalog=app.GetType("BankPokedexCatalog");
        bool InDex(int dex,ushort species)=>(bool)catalog.GetMethod("Contains",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{dex,species});
        Assert(InDex(2,25)&&!InDex(2,152)&&InDex(3,473),"regional dex membership includes older species and HGSS regional additions");
        Assert(!InDex(4,479)&&InDex(5,479)&&!InDex(6,133)&&InDex(7,133),"Sinnoh and Unova regional dex variants use distinct species lists");
        foreach(int index in Enumerable.Range(0,49))Write($"paging-{index:D2}.pk3",new PK3{Species=25,CurrentLevel=25,Gender=0});
        string previousSpriteCache=Environment.GetEnvironmentVariable("POKEMONPLAY_SPRITE_CACHE");
        string spriteCache=Path.Combine(fixture,"sprite-cache");Directory.CreateDirectory(spriteCache);
        foreach(string sprite in new[]{"0001.png","0025.png","0025-female.png","0025-shiny.png","0133.png","0152.png","0252.png"})
        {
            using var image=new Bitmap(8,8);using(var graphics=Graphics.FromImage(image))graphics.Clear(sprite.StartsWith("0025",StringComparison.Ordinal)?Color.Magenta:Color.Cyan);image.Save(Path.Combine(spriteCache,sprite),ImageFormat.Png);
        }
        Environment.SetEnvironmentVariable("POKEMONPLAY_SPRITE_CACHE",spriteCache);
        Call("RefreshBankFiles");Call("ClearBankFilters");
        Assert(Results().Length>48,"paging fixture provides more than one full bank page");
        using var host=new Form{Width=1000,Height=720};host.Controls.Add(view);host.Show();Application.DoEvents();
        var slotPanel=(FlowLayoutPanel)Get("slots");Control first=slotPanel.Controls[0];Call("SelectSlot",Results()[0]);
        Assert(ReferenceEquals(first,slotPanel.Controls[0]),"Pokemon selection preserves existing cards and navigation focus");
        var bankCards=slotPanel.Controls.OfType<Button>().Where(button=>button.GetType().Name=="PokemonSlotButton").ToArray();
        DateTime spriteLoadDeadline=DateTime.UtcNow.AddSeconds(3);
        while(bankCards.Any(button=>button.GetType().GetProperty("Sprite").GetValue(button)==null)&&DateTime.UtcNow<spriteLoadDeadline)
        {
            Application.DoEvents();System.Threading.Thread.Sleep(10);
        }
        Assert(bankCards.All(button=>button.GetType().GetProperty("Sprite").GetValue(button)!=null),"global-bank cards load cached Pokémon sprites, including shiny and female variants");
        var selectedSpriteCard=bankCards.Single(button=>Convert.ToInt32(button.Tag)==Convert.ToInt32(Get("selectedSlot")));
        var selectedSpritePreview=(PictureBox)Get("selectedSpritePreview");
        Assert(selectedSpritePreview.Visible&&selectedSpritePreview.Image is Bitmap selectedPreviewImage&&selectedPreviewImage.Size==new Size(8,8)&&selectedSpritePreview.AccessibleDescription.Contains(selectedSpriteCard.AccessibleName),"selected global-bank Pokémon shows an accessible enlarged copy of its loaded sprite");
        var firstBankCard=bankCards[0];
        firstBankCard.Focus();
        var searchProcessCmdKey=viewType.GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance);
        object[] searchArguments={Message.Create(IntPtr.Zero,0x100,IntPtr.Zero,IntPtr.Zero),Keys.Control|Keys.F};
        bool searchShortcutHandled=(bool)searchProcessCmdKey.Invoke(view,searchArguments);
        Assert(searchShortcutHandled&&((TextBox)Get("bankSpeciesSearch")).ContainsFocus,"Ctrl+F focuses the global-bank search field");
        var bankSearch=(TextBox)Get("bankSpeciesSearch");bankSearch.Text="999";
        Assert(Results().Length==0,"global bank search can produce an empty result before clearing");
        Assert(!selectedSpritePreview.Visible&&selectedSpritePreview.Image==null,"filtering out the selected Pokémon clears and hides its enlarged sprite preview");
        object[] escapeArguments={Message.Create(IntPtr.Zero,0x100,IntPtr.Zero,IntPtr.Zero),Keys.Escape};
        bool escapeShortcutHandled=(bool)searchProcessCmdKey.Invoke(view,escapeArguments);
        Assert(escapeShortcutHandled&&bankSearch.Text.Length==0&&bankSearch.ContainsFocus&&Results().Length>0,"Escape clears the global-bank query and restores results without moving focus");
        bankCards=slotPanel.Controls.OfType<Button>().Where(button=>button.GetType().Name=="PokemonSlotButton").ToArray();
        firstBankCard=bankCards[0];
        firstBankCard.Focus();
        var findNeighbor=viewType.GetMethod("FindDirectionalBankCard",BindingFlags.NonPublic|BindingFlags.Static);
        var rightNeighbor=(Button)findNeighbor.Invoke(null,new object[]{firstBankCard,Keys.Right});
        if(rightNeighbor==null)throw new Exception("bank grid lays out a right-hand keyboard target at the test window size");
        var keyEvent=new KeyEventArgs(Keys.Right);
        firstBankCard.GetType().GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(firstBankCard,new object[]{keyEvent});
        Assert(keyEvent.Handled&&Convert.ToInt32(Get("selectedSlot"))==(int)rightNeighbor.Tag&&rightNeighbor.AccessibleDescription.Contains("setas"),"arrow navigation focuses the neighboring bank card and updates Pokémon details");
        firstBankCard=slotPanel.Controls.OfType<Button>().First(button=>button.GetType().Name=="PokemonSlotButton");
        firstBankCard.Focus();
        var isInputKey=firstBankCard.GetType().GetMethod("IsInputKey",BindingFlags.Instance|BindingFlags.NonPublic);
        Assert((bool)isInputKey.Invoke(firstBankCard,new object[]{Keys.PageUp})&&(bool)isInputKey.Invoke(firstBankCard,new object[]{Keys.PageDown}),"PageUp and PageDown are treated as Pokémon card input keys by WinForms");
        var pageDownEvent=new KeyEventArgs(Keys.PageDown);
        firstBankCard.GetType().GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(firstBankCard,new object[]{pageDownEvent});
        var secondPageFirstCard=slotPanel.Controls.OfType<Button>().First(button=>button.GetType().Name=="PokemonSlotButton");
        Assert(pageDownEvent.Handled&&(int)Get("bankPage")==1&&((Label)Get("bankPageStatus")).Text.Contains("Página 2 de 2")&&Convert.ToInt32(Get("selectedSlot"))==(int)secondPageFirstCard.Tag&&secondPageFirstCard.Focused&&secondPageFirstCard.AccessibleDescription.Contains("PageUp e PageDown"),"PageDown opens the next global-bank page and selects its first Pokémon");
        var pageUpEvent=new KeyEventArgs(Keys.PageUp);
        secondPageFirstCard.GetType().GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(secondPageFirstCard,new object[]{pageUpEvent});
        var firstPageFirstCard=slotPanel.Controls.OfType<Button>().First(button=>button.GetType().Name=="PokemonSlotButton");
        Assert(pageUpEvent.Handled&&(int)Get("bankPage")==0&&Convert.ToInt32(Get("selectedSlot"))==(int)firstPageFirstCard.Tag&&firstPageFirstCard.Focused,"PageUp returns to the previous global-bank page and selects its first Pokémon");
        var boundaryPageUpEvent=new KeyEventArgs(Keys.PageUp);
        firstPageFirstCard.GetType().GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(firstPageFirstCard,new object[]{boundaryPageUpEvent});
        Assert(!boundaryPageUpEvent.Handled&&(int)Get("bankPage")==0,"PageUp at the first global-bank page does not move or change selection");
        var pageDownAgainEvent=new KeyEventArgs(Keys.PageDown);
        firstPageFirstCard.GetType().GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(firstPageFirstCard,new object[]{pageDownAgainEvent});
        secondPageFirstCard=slotPanel.Controls.OfType<Button>().First(button=>button.GetType().Name=="PokemonSlotButton");
        var boundaryPageDownEvent=new KeyEventArgs(Keys.PageDown);
        secondPageFirstCard.GetType().GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(secondPageFirstCard,new object[]{boundaryPageDownEvent});
        Assert(pageDownAgainEvent.Handled&&!boundaryPageDownEvent.Handled&&(int)Get("bankPage")==1,"PageDown at the last global-bank page does not move or change selection");
        string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_BANK_FILTER_PREVIEW");
        if(!string.IsNullOrEmpty(preview))
        {
            Directory.CreateDirectory(preview);
            foreach(int width in new[]{1000,1280})
            {
                host.Width=width;host.Height=width==1000?720:820;Call("ClearBankFilters");Pick("bankGenerationPicker",1);Pick("bankDexPicker",1);Application.DoEvents();
                void Capture(string suffix){using var bitmap=new System.Drawing.Bitmap(host.Width,host.Height);host.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,host.Width,host.Height));bitmap.Save(Path.Combine(preview,width+suffix+".png"));}
                Capture("-basic");
                var more=Descendants(view).OfType<Button>().First(b=>b.Text=="Mais filtros");more.PerformClick();Application.DoEvents();Capture("-filters");more.PerformClick();
                Call("ClearBankFilters");Pick("bankSortPicker",8);Call("RefreshBankFiles");Call("SelectSlot",Results()[0]);Application.DoEvents();Capture("-capture-date");
            }
        }
        byte[] rawSave=new byte[0x20000];
        for(int group=0;group<2;group++)for(int sector=0;sector<14;sector++){int offset=(group*14+sector)*0x1000;System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rawSave.AsSpan(offset+0xFF4),(ushort)sector);System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rawSave.AsSpan(offset+0xFF8),0x08012025);}
        var fixtureSave=new SAV3E(rawSave);fixtureSave.ClearBoxes();fixtureSave.OT="TESTE";fixtureSave.Language=2;
        PKM savePokemon=fixtureSave.BlankPKM.Clone();EntityTemplates.TemplateFields(savePokemon,fixtureSave);savePokemon.Species=25;savePokemon.PID=0x12345678;savePokemon.CurrentLevel=20;savePokemon.Nickname="TESTE";savePokemon.IsNicknamed=true;savePokemon.RefreshChecksum();fixtureSave.SetBoxSlotAtIndex(savePokemon,0,0);
        string savePath=Path.Combine(fixture,"sprite-preview.sav");File.WriteAllBytes(savePath,fixtureSave.Write().ToArray());Call("LoadSave",savePath,null);Application.DoEvents();
        var savePokemonCard=slotPanel.Controls.OfType<Button>().Single(button=>button.GetType().Name=="PokemonSlotButton"&&Convert.ToInt32(button.Tag)==0);
        DateTime saveSpriteDeadline=DateTime.UtcNow.AddSeconds(3);
        while(savePokemonCard.GetType().GetProperty("Sprite").GetValue(savePokemonCard)==null&&DateTime.UtcNow<saveSpriteDeadline){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        Assert(savePokemonCard.GetType().GetProperty("PokemonName").GetValue(savePokemonCard)!=null&&savePokemonCard.AccessibleDescription.Contains("sprite")&&savePokemonCard.GetType().GetProperty("Sprite").GetValue(savePokemonCard) is Image,"occupied save slots asynchronously load and describe their Pokémon sprite");
        var loadedSaveSprite=(Bitmap)savePokemonCard.GetType().GetProperty("Sprite").GetValue(savePokemonCard);var paintMethod=savePokemonCard.GetType().GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic);using(var paintedCard=new Bitmap(savePokemonCard.Width,savePokemonCard.Height)){using var graphics=Graphics.FromImage(paintedCard);using var paintArgs=new PaintEventArgs(graphics,new Rectangle(Point.Empty,paintedCard.Size));paintMethod.Invoke(savePokemonCard,new object[]{paintArgs});int magentaPixels=0;for(int y=0;y<paintedCard.Height;y++)for(int x=0;x<paintedCard.Width;x++)if(paintedCard.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb())magentaPixels++;Assert(loadedSaveSprite.GetPixel(4,4).ToArgb()==Color.Magenta.ToArgb()&&magentaPixels>0,"occupied save-slot card paints its cached Pokémon sprite");}
        host.Controls.Remove(view);
        Environment.SetEnvironmentVariable("POKEMONPLAY_SPRITE_CACHE",previousSpriteCache);
    }
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Descendants(c)));
}
