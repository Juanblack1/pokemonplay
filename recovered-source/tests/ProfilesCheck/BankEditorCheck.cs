using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using PKHeX.Core;

internal static class BankEditorCheck
{
    private static void Assert(bool condition, string name) { if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name); }
    internal static Control Field(PokemonEditorDialog dialog, string name)
    {
        var bindings=(System.Collections.IEnumerable)typeof(PokemonEditorDialog).GetField("bindings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(dialog);
        foreach(var b in bindings)if(((PropertyInfo)b.GetType().GetProperty("Property").GetValue(b)).Name==name)return (Control)b.GetType().GetProperty("Control").GetValue(b);
        throw new Exception(name);
    }
    internal static void Run()
    {
        string output=Environment.GetEnvironmentVariable("POKEMONPLAY_BANK_PREVIEW");
        if(output!=null)Directory.CreateDirectory(output);
        for(int generation=3;generation<=9;generation++)
        {
            var blank=PokemonEditorService.Blank(generation);
            using var editor=new PokemonEditorDialog(blank,creating:true);
            Assert(editor.ReadDraft().Data.ToArray().SequenceEqual(blank.Data.ToArray()),"opening PK"+generation+" editor preserves unedited data");
            ((NumericUpDown)Field(editor,nameof(PKM.CurrentLevel))).Value=50;
            ((NumericUpDown)Field(editor,nameof(PKM.IV_HP))).Value=31;
            ((TextBox)Field(editor,nameof(PKM.OriginalTrainerName))).Text="ASH";
            ((ComboBox)Field(editor,nameof(PKM.Nature))).SelectedIndex=3;
            ((ComboBox)Field(editor,nameof(PKM.Gender))).SelectedIndex=1;
            var edited=editor.ReadDraft();
            Assert(edited.Nature==(Nature)3 && edited.Gender==1,"PK"+generation+" applies nature and gender");
            Assert(edited.CurrentLevel==50&&edited.IV_HP==31&&edited.OriginalTrainerName=="ASH"&&blank.CurrentLevel==5,"PK"+generation+" edits clone, level, IV and trainer");
            foreach(string stat in new[]{"EV_HP","EV_ATK","EV_DEF"})((NumericUpDown)Field(editor,stat)).Value=252;
            bool rejected=false;try{editor.ReadDraft();}catch(InvalidOperationException){rejected=true;}
            Assert(rejected,"PK"+generation+" rejects EV sum above 510");
            Assert(!editor.CanApply&&editor.ValidationColor==AppTheme.Red&&editor.EvBudgetText.Contains("246"),"PK"+generation+" immediately marks excess EVs red and disables apply");
            ((NumericUpDown)Field(editor,"EV_DEF")).Value=6;
            Assert(editor.CanApply&&editor.ReadDraft().EVTotal==510&&editor.EvBudgetText.Contains("0 disponíveis"),"PK"+generation+" accepts exactly 510 EVs and restores apply after correction");
            ((ComboBox)Field(editor,nameof(PKM.HeldItem))).Text="not an item";
            Assert(!editor.CanApply,"PK"+generation+" cannot silently apply an unmatched typed choice");
            ((ComboBox)Field(editor,nameof(PKM.HeldItem))).SelectedIndex=0;
        }
        var invalid=PokemonEditorService.Blank(6);invalid.EV_ATK=255;
        using(var editor=new PokemonEditorDialog(invalid))
        {
            Assert(((NumericUpDown)Field(editor,"EV_ATK")).Value==255&&!editor.CanApply,"invalid imported EV remains visible and blocked instead of silently truncated");
            ((NumericUpDown)Field(editor,"EV_ATK")).Value=252;
            Assert(editor.CanApply&&editor.ReadDraft().EV_ATK==252&&invalid.EV_ATK==255,"imported invalid EV can be corrected without mutating source");
        }
        var probe=PokemonEditorService.Blank(3,version:GameVersion.LG);probe.Species=25;probe.CurrentLevel=20;
        var encounters=PokemonEditorService.Encounters(probe);
        Assert(encounters.Count>0&&encounters.All(e=>e.Species==25),"real PKHeX LeafGreen Pikachu encounters are available");
        var generated=PokemonEditorService.FromEncounter(encounters[0],probe);
        var legality=PokemonLegalityService.Analyze(generated);
        Console.WriteLine($"{legality.Report} consistent={legality.IsConsistent} species={generated.Species} version={generated.Version} template={encounters[0].Version}");
        Assert(legality.IsConsistent&&generated.Species==25&&generated.Version==GameVersion.LG,"encounter template passes PKHeX legality");
        using(var editor=new PokemonEditorDialog(generated))
        {
            Assert(PokemonLegalityService.Analyze(editor.ReadDraft()).IsConsistent,"opening generated encounter preserves legality");
            var live=editor.AnalyzeLive();while(!live.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}live.GetAwaiter().GetResult();
            Assert(editor.LegalityStateText.Contains("Legal segundo"),"live PKHeX status is available without manually opening report");
            var stale=editor.AnalyzeLive();
            foreach(string name in new[]{"EV_HP","EV_ATK","EV_SPE"})((NumericUpDown)Field(editor,name)).Value=255;
            while(!stale.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}stale.GetAwaiter().GetResult();
            Assert(editor.LegalityStateText.Contains("Não é possível aplicar"),"late analysis cannot overwrite a newer invalid draft");
            editor.ShowEncounters();
            var matches=(ListView)typeof(PokemonEditorDialog).GetField("encounters",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(editor);
            for(int wait=0;wait<300&&matches.Items.Count==0;wait++){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            Assert(matches.Items.Count>0,"encounter database remains usable to repair a draft with excess EVs");
            var pages=(EditorPageHost)typeof(PokemonEditorDialog).GetField("tabs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(editor);pages.SelectedTab=pages.TabPages[0];
            foreach(string name in new[]{"EV_HP","EV_ATK","EV_SPE"})((NumericUpDown)Field(editor,name)).Value=0;
            if(output!=null)
            {
                editor.Show();Application.DoEvents();
                var analysis=editor.AnalyzeLive();while(!analysis.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}analysis.GetAwaiter().GetResult();
                Assert(editor.LegalityStateText.Contains("Legal segundo"),"live editor legality uses real PKHeX result");
                foreach(var size in new[]{new Size(1000,700),new Size(1180,780)})
                {
                    editor.ClientSize=size;Application.DoEvents();
                    using var bitmap=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(bitmap,new Rectangle(Point.Empty,editor.Size));bitmap.Save(Path.Combine(output,"editor-"+size.Width+".png"));
                }
                ((NumericUpDown)Field(editor,"EV_HP")).Value=255;((NumericUpDown)Field(editor,"EV_ATK")).Value=255;((NumericUpDown)Field(editor,"EV_SPE")).Value=255;
                using(var bitmap=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(bitmap,new Rectangle(Point.Empty,editor.Size));bitmap.Save(Path.Combine(output,"editor-invalid.png"));}
                ((NumericUpDown)Field(editor,"EV_SPE")).Value=0;
                analysis=editor.AnalyzeLive();while(!analysis.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}analysis.GetAwaiter().GetResult();
                Assert(editor.LegalityStateText.Contains("Revisar legalidade"),"live legal status changes after an inconsistent edit");
                ((NumericUpDown)Field(editor,"EV_HP")).Value=0;((NumericUpDown)Field(editor,"EV_ATK")).Value=0;
                editor.ShowEncounters();
                for(int i=0;i<20;i++){Application.DoEvents();System.Threading.Thread.Sleep(50);}
                using var encounterImage=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(encounterImage,new Rectangle(Point.Empty,editor.Size));encounterImage.Save(Path.Combine(output,"encounters.png"));
                editor.Close();
            }
        }
        string path=Environment.GetEnvironmentVariable("POKEMONPLAY_REAL_LEAFGREEN_SAVE");
        if(!string.IsNullOrWhiteSpace(path))
        {
            byte[] before=File.ReadAllBytes(path);
            var game=new GameInfo{Title="LeafGreen",Generation=3,SaveFolderName="Pokemon LeafGreen"};
            var choices=ProfileSaveLocator.FindInFiles(Path.GetTempPath(),game,new[]{path});
            Assert(choices.Count==1,"valid real LeafGreen SRAM is recognized despite ambiguous FR/LG format");
            var read=ProfileSaveLocator.ReadForGame(path,game);
            Assert(read.Version==GameVersion.LG&&read.ChecksumsValid,"profile context resolves LeafGreen and retains save checksums");
            Assert(before.SequenceEqual(File.ReadAllBytes(path)),"recognizing the real save leaves every byte unchanged");
            string fixture=Path.Combine(Path.GetTempPath(),"pp-bank-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
            try
            {
                string principal=SaveProfileService.Folder(fixture,game.SaveFolderName,"default");Directory.CreateDirectory(principal);File.WriteAllBytes(Path.Combine(principal,Path.GetFileName(path)),before);
                Assert(ProfileSaveLocator.Find(fixture,game,"default").Count==1&&ProfileSaveLocator.Find(fixture,game,"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").Count==0,"save discovery isolates profiles");
                if(output!=null)
                {
                    using var form=new Form{ClientSize=new Size(1280,800)};
                    using var bank=new PokemonBankView(fixture){Dock=DockStyle.Fill};form.Controls.Add(bank);form.Show();Application.DoEvents();
                    using var empty=new Bitmap(form.Width,form.Height);form.DrawToBitmap(empty,new Rectangle(Point.Empty,form.Size));empty.Save(Path.Combine(output,"bank-empty.png"));
                    var picker=(ThemeSelect)typeof(PokemonBankView).GetField("gamePicker",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bank);picker.SelectedIndex=picker.Items.FindIndex(item=>item.ToString()=="LeafGreen");
                    typeof(PokemonBankView).GetMethod("OpenProfileSave",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{null,EventArgs.Empty});Application.DoEvents();
                    var opened=(SaveFile)typeof(PokemonBankView).GetField("save",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bank);
                    Assert(opened?.Version==GameVersion.LG,"bank opens real SRAM from selected LeafGreen profile");
                    Assert(before.SequenceEqual(File.ReadAllBytes(Path.Combine(principal,Path.GetFileName(path)))),"bank opening profile does not write save");
                    typeof(PokemonBankView).GetMethod("SelectSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{0});
                    int partyBefore=opened.PartyCount;
                    bank.TransferToBox(0,29,true);
                    Assert(opened.GetBoxSlotAtIndex(0,29).Species!=0&&opened.PartyCount==partyBefore,"explicit destination copy preserves source party");
                    typeof(PokemonBankView).GetMethod("SelectSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{0});
                    bool occupiedRejected=false;try{bank.TransferToBox(0,29,true);}catch(InvalidOperationException){occupiedRejected=true;}
                    Assert(occupiedRejected,"occupied destination rejected without overwriting");
                    bool lastRejected=false;try{bank.TransferToBox(0,28,false);}catch(InvalidOperationException){lastRejected=true;}
                    Assert(lastRejected,"last party Pokemon remains protected when moving");
                    for(int i=0;i<29;i++){var pk=PokemonEditorService.Blank(3,version:GameVersion.LG);pk.Species=(ushort)new[]{1,4,7,25,133,150,151,6,9,3}[i%10];pk.Nickname=SpeciesName.GetSpeciesNameGeneration(pk.Species,pk.Language,3);pk.IsNicknamed=false;pk.PID=0x12345678;pk.TID16=1234;pk.RefreshAbility(0);pk.CurrentLevel=(byte)(5+i);opened.SetBoxSlotAtIndex(pk,0,i);}
                    var boxes=(ThemeSelect)typeof(PokemonBankView).GetField("boxPicker",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bank);boxes.SelectedIndex=0;
                    typeof(PokemonBankView).GetMethod("SelectSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{29});
                    int initialPartyCount=opened.PartyCount;
                    bank.TransferToParty(true);
                    Assert(opened.PartyCount==initialPartyCount+1&&opened.GetBoxSlotAtIndex(0,29).Species!=0,"copy from box to team preserves its original box slot");
                    typeof(PokemonBankView).GetMethod("SelectSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{29});
                    bank.TransferToParty(false);
                    Assert(opened.PartyCount==initialPartyCount+2&&opened.GetBoxSlotAtIndex(0,29).Species==0,"move from box to team appends the Pokemon and clears only its source");
                    opened.SetBoxSlotAtIndex(opened.GetPartySlotAtIndex(0).Clone(),0,29);

                    for(int i=0;i<20;i++){Application.DoEvents();System.Threading.Thread.Sleep(50);}
                    foreach(var size in new[]{new Size(1000,560),new Size(1000,700),new Size(1280,800),new Size(1920,1000)})
                    {
                        form.ClientSize=size;Application.DoEvents();
                        var slotPanel=(FlowLayoutPanel)typeof(PokemonBankView).GetField("slots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bank);
                        var cells=slotPanel.Controls.OfType<PokemonSlotButton>().ToArray();
                        Assert(cells.Length==30&&cells.GroupBy(c=>c.Top).All(row=>row.Count()==6)&&cells.Select(c=>c.Top).Distinct().Count()==5,"box has fixed 6 by 5 grid at width "+size.Width);
                        Assert(cells.All(c=>c.Right<=slotPanel.ClientSize.Width&&c.Bottom<=slotPanel.ClientSize.Height),"all 30 box cells visible without scrolling at width "+size.Width);
                        typeof(PokemonBankView).GetMethod("SelectSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{2});Application.DoEvents();
                        using var capture=new Bitmap(form.Width,form.Height);form.DrawToBitmap(capture,new Rectangle(Point.Empty,form.Size));capture.Save(Path.Combine(output,"box-"+size.Width+".png"));
                    }
                    Assert(before.SequenceEqual(File.ReadAllBytes(Path.Combine(principal,Path.GetFileName(path)))),"transfers and previews remain pending without writing save");
                    using var loaded=new Bitmap(form.Width,form.Height);form.DrawToBitmap(loaded,new Rectangle(Point.Empty,form.Size));loaded.Save(Path.Combine(output,"bank-save.png"));
                    string bankFolder=Path.Combine(fixture,"Pokemon Bank");Directory.CreateDirectory(bankFolder);
                    for(int i=0;i<40;i++){var pk=opened.GetBoxSlotAtIndex(0,i%30);byte[] bytes=new byte[pk.SIZE_PARTY];pk.RefreshChecksum();pk.WriteDecryptedDataParty(bytes);File.WriteAllBytes(Path.Combine(bankFolder,i+".pk3"),bytes);}
                    boxes.SelectedIndex=opened.BoxCount+1;Application.DoEvents();
                    var collection=(FlowLayoutPanel)typeof(PokemonBankView).GetField("slots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bank);
                    Assert(collection.Controls.OfType<PokemonSlotButton>().Count()==30,"collection paginates in boxes of 30");
                    typeof(PokemonBankView).GetMethod("ChangeWorkspaceBox",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{1});Application.DoEvents();
                    Assert(collection.Controls.OfType<PokemonSlotButton>().Count()==10,"collection next box shows remaining Pokemon without duplication");
                    typeof(PokemonBankView).GetMethod("ChangeWorkspaceBox",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{-1});Application.DoEvents();
                    form.ClientSize=new Size(1280,800);
                    typeof(PokemonBankView).GetMethod("SelectSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bank,new object[]{(int)collection.Controls.OfType<PokemonSlotButton>().First().Tag});
                    for(int i=0;i<10;i++){Application.DoEvents();System.Threading.Thread.Sleep(50);}
                    using var bankImage=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bankImage,new Rectangle(Point.Empty,form.Size));bankImage.Save(Path.Combine(output,"collection.png"));
                    var search=(TextBox)typeof(PokemonBankView).GetField("bankSpeciesSearch",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bank);search.Text="Pikachu";Application.DoEvents();
                    Assert(collection.Controls.OfType<PokemonSlotButton>().Any()&&collection.Controls.OfType<PokemonSlotButton>().All(c=>c.AccessibleName.Contains("PIKACHU",StringComparison.OrdinalIgnoreCase)),"collection search filters by Pokemon data");
                    form.Close();
                }
            }
            finally{Directory.Delete(fixture,true);}
        }
    }
}



