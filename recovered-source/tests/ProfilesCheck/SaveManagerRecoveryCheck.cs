using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class SaveManagerRecoveryCheck
{
    internal static void Run(string root)
    {
        string fixture=Path.Combine(root,"save-manager-recovery");
        var games=GameCatalog.SaveGames(fixture);var first=games[0];
        string settings=Path.Combine(fixture,"Settings","SaveProfiles");Directory.CreateDirectory(settings);
        string path=Path.Combine(settings,first.SaveFolderName+".json");File.WriteAllText(path,"{invalid profile");byte[] original=File.ReadAllBytes(path);
        string save=Path.Combine(fixture,"Saves",first.SaveFolderName,"progress.sav");Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllText(save,"sentinel progress");
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
        System.Collections.Generic.IEnumerable<Control> Children(Control control)=>control.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
        using(var view=new SaveManagerView(fixture)) {
            Assert(Children(view).OfType<Label>().Any(label=>label.Text.Contains("Não foi possível")&&label.Text.Contains(first.Title)),"save manager opens with recovery guidance for invalid profile metadata");
            Assert(!Children(view).OfType<Button>().Any(button=>button.Text is "Backup ZIP" or "Restaurar backup" or "Novo perfil"),"invalid save detail offers no actions that could write or restore game data");
            var retry=Children(view).OfType<Button>().Single(button=>button.Text=="Tentar novamente");
            retry.PerformClick();Assert(File.ReadAllBytes(path).SequenceEqual(original)&&File.ReadAllText(save)=="sentinel progress","retry with invalid profiles preserves metadata and save bytes");
            string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_PREVIEW");
            if(!string.IsNullOrEmpty(preview)) {
                Directory.CreateDirectory(preview);using var host=new Form{ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.None,ClientSize=new System.Drawing.Size(1100,950)};
                host.Controls.Add(view);host.Show();Application.DoEvents();
                foreach(int width in new[]{760,1100}) {
                    host.ClientSize=new System.Drawing.Size(width,950);Application.DoEvents();
                    using var bitmap=new System.Drawing.Bitmap(view.Width,view.Height);view.DrawToBitmap(bitmap,view.ClientRectangle);bitmap.Save(Path.Combine(preview,"save-read-warning-"+width+".png"));
                }
                host.Controls.Remove(view);
            }
            Call(view,"SelectGame",games[1]);Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Backup ZIP"),"another game remains selectable after a save detail read failure");
            Call(view,"SelectGame",first);
            File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new SaveProfileState()));
            Children(view).OfType<Button>().Single(button=>button.Text=="Tentar novamente").PerformClick();
            Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Backup ZIP")&&!Children(view).OfType<Button>().Any(button=>button.Text=="Tentar novamente"),"retry restores normal save controls after an explicit profile fixture correction");
            using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None)) {
                Call(view,"BuildDetail");Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Tentar novamente"),"locked profile file shows recovery without preventing save manager navigation");
            }
            Children(view).OfType<Button>().Single(button=>button.Text=="Tentar novamente").PerformClick();
            Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Backup ZIP"),"save controls recover when the profile file lock is released");
            File.WriteAllBytes(path,original);
            ((Task)Call(view,"RunCloudAction","upload")).GetAwaiter().GetResult();
            Assert(!(bool)Field(view,"cloudBusy")&&((Control)Field(view,"gameList")).Enabled,"profile read failure before a cloud action releases busy state and game selection");
            Assert(((Label)Field(view,"cloudMessage")).ForeColor==AppTheme.Red,"cloud preflight read failure shows an error without attempting network work");
            Assert(File.ReadAllBytes(path).SequenceEqual(original)&&File.ReadAllText(save)=="sentinel progress","failed cloud preflight preserves profile metadata and local save bytes");
        }
    }
}
