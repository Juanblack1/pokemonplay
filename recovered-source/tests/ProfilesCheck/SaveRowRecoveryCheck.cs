using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class SaveRowRecoveryCheck
{
    internal static void Run(string root)
    {
        void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
        string folder=Path.Combine(root,"save-row-recovery");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"progress.sav");File.WriteAllText(path,"sentinel progress");
        using var row=new SaveFileRow(new FileInfo(path)){Width=440};
        using var bitmap=new Bitmap(row.Width,row.Height);
        void Paint(){using var graphics=Graphics.FromImage(bitmap);typeof(SaveFileRow).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(row,new object[]{new PaintEventArgs(graphics,row.ClientRectangle)});}
        File.Delete(path);
        Paint();
        Assert(row.AccessibleName=="progress.sav"&&row.AccessibleDescription.Contains("indisponível"),"removed save file paints recovery information instead of throwing");
        Assert(!File.Exists(path),"painting an unavailable save never recreates it");
        string preview=Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_PREVIEW");
        if(!string.IsNullOrEmpty(preview)){Directory.CreateDirectory(preview);bitmap.Save(Path.Combine(preview,"save-row-unavailable.png"));}
        File.WriteAllText(path,"restored sentinel");
        Paint();
        Assert(!row.AccessibleDescription.Contains("indisponível")&&row.AccessibleDescription.Contains("17 B"),"restored save refreshes its real metadata on the same row");
        Assert(File.ReadAllText(path)=="restored sentinel","recovered save painting preserves restored file bytes");
        File.Delete(path);Paint();
        Assert(row.AccessibleDescription.Contains("indisponível"),"save disappearance after an earlier successful paint is detected");
    }
}
