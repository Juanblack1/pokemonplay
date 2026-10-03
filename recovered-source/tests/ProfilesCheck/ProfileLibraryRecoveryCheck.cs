using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class ProfileLibraryRecoveryCheck
{
    internal static void Run(string root)
    {
        string fixture=Path.Combine(root,"profile-library-recovery"),settings=Path.Combine(fixture,"Settings","SaveProfiles");Directory.CreateDirectory(settings);
        string path=Path.Combine(settings,"Pokemon FireRed.json");File.WriteAllText(path,"{invalid profile");byte[] original=File.ReadAllBytes(path);
        string save=Path.Combine(fixture,"Saves","Pokemon FireRed","progress.sav");Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllText(save,"private diagnostic sentinel");
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        System.Collections.Generic.IEnumerable<Control> Children(Control control)=>control.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
        using(var library=new LibraryView(null,fixture)) {
            var cards=Children(library).OfType<GameCard>().ToArray();
            Assert(cards.Any(card=>card.AccessibleName.StartsWith("FireRed,")&&card.AccessibleName.Contains("Verificar perfis")),"invalid profile metadata marks the affected game without preventing library construction");
            Assert(cards.Any(card=>card.AccessibleName.StartsWith("Emerald,")&&card.AccessibleName.Contains("Principal")),"valid games remain available beside a game with invalid profile metadata");
            var affected=cards.Single(card=>card.AccessibleName.StartsWith("FireRed,"));
            Assert(!affected.RefreshProfileStatus()&&affected.AccessibleDescription.Contains("Settings/SaveProfiles/"),"profile preflight reports recovery guidance and blocks invalid metadata before launch preparation");
            Assert(File.ReadAllBytes(path).SequenceEqual(original)&&File.ReadAllText(save)=="private diagnostic sentinel","checking invalid profiles preserves metadata and save bytes before any repair fixture");
            string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_PREVIEW");
            if(!string.IsNullOrEmpty(preview)) {
                var previousParent=affected.Parent;var previousDock=affected.Dock;
                Directory.CreateDirectory(preview);using var host=new Form {ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.None,ClientSize=new System.Drawing.Size(affected.Width,affected.Height)};
                host.Controls.Add(affected);affected.Dock=DockStyle.Fill;host.Show();Application.DoEvents();
                using var bitmap=new System.Drawing.Bitmap(affected.Width,affected.Height);affected.DrawToBitmap(bitmap,affected.ClientRectangle);bitmap.Save(Path.Combine(preview,"profile-read-warning.png"));
                host.Controls.Remove(affected);
                affected.Dock=previousDock;previousParent.Controls.Add(affected);
            }
            File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new SaveProfileState()));
            Assert(affected.RefreshProfileStatus()&&affected.AccessibleName.Contains("Jogar · Principal")&&!affected.AccessibleDescription.Contains("Não foi possível ler os perfis"),"corrected profile metadata is revalidated instead of keeping a stale launch block");
            File.WriteAllBytes(path,original);
        }
        Assert(File.ReadAllBytes(path).SequenceEqual(original)&&File.ReadAllText(save)=="private diagnostic sentinel","profile warning preserves original profile metadata and save bytes");
        File.WriteAllText(Path.Combine(settings,"Pokemon X.json"),"{invalid unrelated profile");
        Assert(GameProfilePresentation.Read(fixture,new GameInfo{Generation=6,SaveFolderName="Pokemon X"}).Problem==null,"3DS presentation does not read app-managed profile metadata it does not use");
        Assert(GameProfilePresentation.Read(fixture,new GameInfo{Generation=3,SaveFolderName="Pokemon FireRed",IsImported=true}).Problem!=null,"imported GBA games still validate profile metadata used by their save backend");
        using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None))Assert(GameProfilePresentation.Read(fixture,new GameInfo{Generation=3,SaveFolderName="Pokemon FireRed"}).Problem!=null,"profile file locked by another process produces a read issue without a library crash");
    }
}
