using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;

internal static class SettingsResponsiveCheck
{
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll",SetLastError=true)] static extern bool GetClientRect(IntPtr window,out NativeRect rectangle);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    static Control Field(object target, string name) => (Control)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    internal static void Run(string root)
    {
        string install = Path.Combine(root, "settings-responsive");
        Directory.CreateDirectory(Path.Combine(install, "Settings"));
        string preferences = Path.Combine(install, "Settings", "input-presets.txt");
        File.WriteAllText(preferences, "1\n0\n100\nfalse\nfalse\ntrue\n");
        byte[] original = File.ReadAllBytes(preferences);
        using var view = new SettingsView(preferences);
        using var host = new Form { MaximumSize = new Size(2000, 1600), ClientSize = new Size(760, 720) };
        host.Controls.Add(view); host.Show(); Application.DoEvents();
        int borderWidth=host.Width-host.ClientSize.Width;
        var canvas = Field(view, "canvas");
        var scroll = (Panel)canvas.Parent;
        var workbench = Field(view, "controlsCard");
        var mode = (ThemeSelect)Field(workbench, "mode");
        var console = (ThemeSelect)Field(workbench, "console");
        console.SelectedIndex = 1;
        foreach (int width in new[] { 760, 1100 })
        foreach (int inputMode in new[] { 0, 1, 2, 3 })
        {
            host.MinimumSize=new Size(width+borderWidth,host.Height);
            host.ClientSize = new Size(width, 720); mode.SelectedIndex = inputMode;
            // Resize only this owned test window; skip its screen-size clamp, not the product layout.
            // NOMOVE | NOZORDER | NOACTIVATE | NOSENDCHANGING. Native client rect is the oracle.
            if(!SetWindowPos(host.Handle,IntPtr.Zero,0,0,width+borderWidth,host.Height,0x416))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            host.PerformLayout(); view.PerformLayout(); Application.DoEvents();
            if(!GetClientRect(host.Handle,out NativeRect native))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            Assert(native.Right-native.Left==width && host.ClientSize.Width==width && view.Width==width && host.Width==width+borderWidth,"native settings viewport and outer bounds match requested width "+width+" (native="+(native.Right-native.Left)+", outer="+host.Width+", view="+view.Width+")");
            foreach(string name in new[]{"restoreButton","saveButton"})
            {
                var button=Field(view,name);
                Point position=view.PointToClient(button.Parent.PointToScreen(button.Location));
                Assert(button.Visible&&position.X>=0&&position.X+button.Width<=view.ClientSize.Width,"settings footer action fits viewport: "+name+" at "+width);
            }
            string preview = Environment.GetEnvironmentVariable("POKEMONPLAY_LIBRARY_PREVIEW");
            if (!string.IsNullOrEmpty(preview))
            {
                Directory.CreateDirectory(preview); using var bitmap = new Bitmap(host.Width, host.Height);
                Assert(bitmap.Width==width+borderWidth,"native settings capture has full requested pixel width "+width);
                host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(preview, "settings-responsive-" + width + "-mode-" + inputMode + ".png"));
            }
            Assert(canvas.Width <= scroll.ClientSize.Width && !scroll.HorizontalScroll.Visible, "settings avoid horizontal scrolling at viewport " + width + " mode " + inputMode);
            foreach (Control child in workbench.Controls.Cast<Control>().Where(c => c.Visible))
                Assert(child.Left >= 0 && child.Right <= workbench.ClientSize.Width && child.Width > 0, "visible workbench control fits width: " + child.GetType().Name + " at " + width + " mode " + inputMode);
            Control[] actions = new[] { "mode", "slot", "detectController", "test" }.Select(name => Field(workbench, name)).Where(c => c.Visible).ToArray();
            for (int i = 0; i < actions.Length; i++) for (int j = i + 1; j < actions.Length; j++)
                Assert(!actions[i].Bounds.IntersectsWith(actions[j].Bounds), "input selectors and actions do not overlap at " + width + " mode " + inputMode);
            Assert(!Field(workbench, "title").Bounds.IntersectsWith(console.Bounds), "console selector leaves the settings heading readable at " + width);
            var mapping = Field(workbench, "mappingPanel");
            Assert(mapping.Width >= 226 && mapping.Controls.Count == 12 && mapping.Controls.Cast<Control>().All(row => row.Width >= 206 && row.Right <= mapping.ClientSize.Width), "all twelve binding rows retain readable keys at " + width);
            foreach (string name in new[] { "dsCard", "audioCard", "retroArchCard" })
            {
                var card = Field(view, name);
                Assert(card.Left >= 0 && card.Right <= canvas.Width, "settings card fits canvas: " + name + " at " + width);
                foreach (Control child in card.Controls.Cast<Control>().Where(c => c.Visible)) Assert(child.Left >= 0 && child.Right <= card.ClientSize.Width, "settings option fits card: " + child.GetType().Name + " at " + width);
            }
            if(inputMode==2)
            {
                ((InputWorkbench)workbench).ControllerReader=_=>new InputSnapshot{Connected=true};
                workbench.GetType().GetMethod("Capture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(workbench,new object[]{0});
                Application.DoEvents();
                var cancel=Field(workbench,"cancel");
                Assert(cancel.Visible&&cancel.Right<=workbench.Width&&!cancel.Bounds.IntersectsWith(Field(workbench,"visual").Bounds),"capture cancellation remains reachable without covering diagnostic visual at "+width);
                if(!string.IsNullOrEmpty(preview)){using var bitmap=new Bitmap(host.Width,host.Height);host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(preview,"settings-responsive-"+width+"-capture.png"));}
                workbench.GetType().GetMethod("CancelCapture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(workbench,null);
            }
            Exception dialogFailure=null;
            host.BeginInvoke(new Action(()=>
            {
                ConsoleTestForm dialog=null;
                try
                {
                    dialog=Application.OpenForms.OfType<ConsoleTestForm>().Single();
                    var toggle=Field(dialog,"toggle");
                    Assert(toggle.Text=="Pausar teste"&&dialog.SelectedConsole==1,"settings action opens a running DS command diagnostic at "+width+" mode "+inputMode);
                    typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{EventArgs.Empty});
                    Assert(toggle.Text=="Retomar teste"&&!(bool)dialog.GetType().GetField("running",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dialog),"native diagnostic pause action stops input testing");
                    if(!string.IsNullOrEmpty(preview)){using var bitmap=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(preview,"settings-diagnostic-paused-"+width+"-mode-"+inputMode+".png"));}
                    typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(toggle,new object[]{EventArgs.Empty});
                    Assert(toggle.Text=="Pausar teste"&&(bool)dialog.GetType().GetField("running",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dialog),"native diagnostic resume action restores input testing");
                }
                catch(Exception ex){dialogFailure=ex;}
                finally{dialog?.Close();}
            }));
            typeof(Control).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Field(workbench,"test"),new object[]{EventArgs.Empty});
            if(dialogFailure!=null)throw new Exception("Settings diagnostic interaction failed.",dialogFailure);
            Assert(File.ReadAllBytes(preferences).SequenceEqual(original), "responsive layout preserves preference bytes");
        }
        host.Close();
    }
}
