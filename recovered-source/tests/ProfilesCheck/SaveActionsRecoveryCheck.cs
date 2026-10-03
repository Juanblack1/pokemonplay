using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class SaveActionsRecoveryCheck
{
    internal static void Run(string root)
    {
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        System.Collections.Generic.IEnumerable<Control> Children(Control control)=>control.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
        foreach(string action in new[]{"Abrir pasta","Backup ZIP","Restaurar backup"})foreach(bool locked in new[]{false,true}) {
            string fixture=Path.Combine(root,"save-action-"+Guid.NewGuid().ToString("N"));var games=GameCatalog.SaveGames(fixture);var game=games[0];
            string path=Path.Combine(fixture,"Settings","SaveProfiles",game.SaveFolderName+".json");Directory.CreateDirectory(Path.GetDirectoryName(path));
            string valid=System.Text.Json.JsonSerializer.Serialize(new SaveProfileState());File.WriteAllText(path,valid);
            string save=Path.Combine(fixture,"Saves",game.SaveFolderName,"progress.sav");Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllText(save,"save sentinel");
            string backup=Path.Combine(fixture,"Saves","Backups","existing.zip");Directory.CreateDirectory(Path.GetDirectoryName(backup));File.WriteAllBytes(backup,new byte[]{1,2,3,4});
            using var view=new SaveManagerView(fixture);
            byte[] profileBytes=System.Text.Encoding.UTF8.GetBytes(locked?valid:"{changed invalid profile");File.WriteAllBytes(path,profileBytes);
            using(var held=locked?File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None):null) {
                Children(view).OfType<Button>().Single(button=>button.Text==action).PerformClick();
                Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Tentar novamente"),action+" offers recovery when the profile becomes "+(locked?"locked":"invalid")+" after opening the view");
            }
            Assert(File.ReadAllBytes(path).SequenceEqual(profileBytes)&&File.ReadAllText(save)=="save sentinel"&&File.ReadAllBytes(backup).SequenceEqual(new byte[]{1,2,3,4})&&Directory.GetFiles(Path.GetDirectoryName(backup)).Length==1,action+" preserves profile, save and existing backup after read failure");
            var select=view.GetType().GetMethod("SelectGame",BindingFlags.NonPublic|BindingFlags.Instance);select.Invoke(view,new object[]{games[1]});
            Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Backup ZIP"),action+" read failure leaves another game selectable");
            select.Invoke(view,new object[]{game});File.WriteAllText(path,valid);
            var retry=Children(view).OfType<Button>().FirstOrDefault(button=>button.Text=="Tentar novamente");
            // A released lock already permits selection to rebuild the valid detail.
            if(retry!=null)retry.PerformClick();
            Assert(Children(view).OfType<Button>().Any(button=>button.Text=="Backup ZIP"),action+" allows recovery after the fixture is explicitly corrected");
        }
    }
}
