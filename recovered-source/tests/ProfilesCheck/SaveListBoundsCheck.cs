using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class SaveListBoundsCheck
{
    internal static void Run(string root)
    {
        void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
        object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
        System.Collections.Generic.IEnumerable<Control> Children(Control control)=>control.Controls.Cast<Control>().SelectMany(child=>new[]{child}.Concat(Children(child)));
        string fixture=Path.Combine(root,"save-list-bounds");var games=GameCatalog.SaveGames(fixture);var game=games[0];
        string folder=Path.Combine(fixture,"Saves",game.SaveFolderName);Directory.CreateDirectory(folder);
        for(int index=0;index<600;index++)File.WriteAllText(Path.Combine(folder,"note-"+index.ToString("D4")+".txt"),"sentinel "+index);
        using(var view=new SaveManagerView(fixture)) {
            var rows=(Control)Field(view,"fileList");var status=(Label)Field(view,"status");
            Assert(rows.Controls.OfType<SaveFileRow>().Count()==512&&status.Text.Contains("parcial"),"large save folders show a bounded file list and an honest partial-read notice");
            Assert(Children(view).OfType<Button>().Where(button=>button.Text is "Backup ZIP" or "Restaurar backup" or "Novo perfil").All(button=>!button.Enabled),"known partial save listings disable operations that require complete data");
            Assert(Children(view).OfType<Button>().Single(button=>button.Text=="Abrir pasta").Enabled&&((Control)Field(view,"gameList")).Enabled,"partial save listings keep folder access and game navigation available");
            Assert(((Label)Field(view,"profileSummary")).Text.Contains("incompleta"),"profile summary does not claim a complete compatible-save count after a limited scan");
            ((System.Threading.Tasks.Task)Call(view,"RunCloudAction","upload")).GetAwaiter().GetResult();
            Assert(!(bool)Field(view,"cloudBusy")&&((Label)Field(view,"cloudMessage")).Text.Contains("incompleta"),"partial save listing stops cloud upload before network work");
            string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_PREVIEW");
            if(!string.IsNullOrEmpty(preview)) {
                Directory.CreateDirectory(preview);using var host=new Form{ShowInTaskbar=false,AutoScaleMode=AutoScaleMode.None,ClientSize=new System.Drawing.Size(1100,950)};
                host.Controls.Add(view);host.Show();Application.DoEvents();
                foreach(int width in new[]{760,1100}) {
                    host.ClientSize=new System.Drawing.Size(width,950);Application.DoEvents();
                    var refresh=Children(view).OfType<Button>().Single(button=>button.Text=="Atualizar lista");
                    var heading=Children(view).Single(control=>(string)control.Tag=="files-heading");
                    Assert(heading.Right<=refresh.Left&&refresh.Right<=refresh.Parent.ClientSize.Width,"partial save list heading and refresh button fit at width "+width);
                    Assert(heading.Text.StartsWith("Lista parcial")&&heading.ForeColor==AppTheme.Red&&heading.AccessibleDescription.Contains("limite"),"partial warning is shown beside the displayed files at width "+width);
                    using var bitmap=new System.Drawing.Bitmap(view.Width,view.Height);view.DrawToBitmap(bitmap,view.ClientRectangle);bitmap.Save(Path.Combine(preview,"save-list-partial-"+width+".png"));
                }
                host.Controls.Remove(view);
            }
            Call(view,"SelectGame",games[1]);Assert(Children(view).OfType<Button>().Single(button=>button.Text=="Backup ZIP").Enabled,"selecting a complete game restores save actions after a partial listing");
        }
        Assert(File.ReadAllText(Path.Combine(folder,"note-0000.txt"))=="sentinel 0"&&Directory.GetFiles(folder).Length==600,"bounded save listing preserves the original files");
        string exact=Path.Combine(root,"save-list-exact");var exactGame=GameCatalog.SaveGames(exact)[0];string exactFolder=Path.Combine(exact,"Saves",exactGame.SaveFolderName);Directory.CreateDirectory(exactFolder);
        for(int index=0;index<512;index++)File.WriteAllText(Path.Combine(exactFolder,index+".txt"),"sentinel");
        using(var view=new SaveManagerView(exact))Assert(((Control)Field(view,"fileList")).Controls.OfType<SaveFileRow>().Count()==512&&!((Label)Field(view,"status")).Text.Contains("parcial"),"exactly 512 normal files do not cause a false incomplete-list warning");
        string junctionFixture=Path.Combine(root,"save-list-junction");var junctionGame=GameCatalog.SaveGames(junctionFixture)[0];string junctionFolder=Path.Combine(junctionFixture,"Saves",junctionGame.SaveFolderName);Directory.CreateDirectory(junctionFolder);string original=Path.Combine(junctionFolder,"note.txt");File.WriteAllText(original,"sentinel");
        string link=Path.Combine(junctionFolder,"loop");
        using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe"){Arguments="/c mklink /J \""+link+"\" \""+junctionFolder+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true});process.WaitForExit(10000);if(process.ExitCode!=0)throw new Exception("Save listing junction fixture could not be created");
        try {
            using var view=new SaveManagerView(junctionFixture);
            Assert(((Control)Field(view,"fileList")).Controls.OfType<SaveFileRow>().Count()==1&&((Label)Field(view,"status")).Text.Contains("parcial"),"save directory junction is skipped without duplicates and reported as a partial listing");
            var refresh=Children(view).OfType<Button>().Single(button=>button.Text=="Atualizar lista");
            Directory.Delete(link);refresh.PerformClick();
            Assert(((Control)Field(view,"fileList")).Controls.OfType<SaveFileRow>().Count()==1&&!((Label)Field(view,"status")).Text.Contains("parcial")&&Children(view).OfType<Button>().Single(button=>button.Text=="Backup ZIP").Enabled,"removing a directory junction and refreshing restores a complete save listing");
            Assert(File.ReadAllText(original)=="sentinel","junction inspection preserves the original save-folder content");
        }finally{if(Directory.Exists(link))Directory.Delete(link);}
    }
}
