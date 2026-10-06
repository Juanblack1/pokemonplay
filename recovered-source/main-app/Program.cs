using System;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
        try { Run(args); }
        catch (Exception error) {
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.log"), error.ToString()); } catch { }
            if(args.Length==2&&args[0]=="--update-ready"){Environment.ExitCode=1;return;}
            throw;
        }
	}
    private static void Run(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		#if STARTUP_PLAYBACK_PROBE
		if (args.Length == 4 && args[0] == "--diagnose-gba")
		{
			StartupPlaybackProbe.Run(args[1], args[2], args[3]);
			return;
		}
		#endif
		if (args.Length == 3 && args[0] == "--render-previews")
		{
			Directory.CreateDirectory(args[2]);
			using LauncherForm form = new LauncherForm(args[1]){UpdatesEnabled=false};
            if (int.TryParse(Environment.GetEnvironmentVariable("POKEMONPLAY_PREVIEW_WIDTH"), out int previewWidth)) form.Width = Math.Max(1000, previewWidth);
            if (int.TryParse(Environment.GetEnvironmentVariable("POKEMONPLAY_PREVIEW_HEIGHT"), out int previewHeight)) form.Height = Math.Max(720, previewHeight);
			form.Show();
			Application.DoEvents(); // Finish the initial Shown navigation before capturing a requested page.
			foreach (string page in new[] { "library", "saves", "pokemon", "settings" })
			{
				form.Navigate(page);
				form.PerformLayout();
				Application.DoEvents();
				using System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
				form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
				bitmap.Save(Path.Combine(args[2], page + ".png"));
                if(page=="settings")
                {
                    var workbench=FindWorkbench(form);
                    foreach(var state in new[]{("settings-controller",2,false),("settings-touch",3,false),("settings-feedback-preview",2,true)})
                    {
                        workbench.RenderPreviewState(state.Item2,state.Item3);
                        using var stateBitmap=new System.Drawing.Bitmap(form.Width,form.Height);
                        form.DrawToBitmap(stateBitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));
                        stateBitmap.Save(Path.Combine(args[2],state.Item1+".png"));
                    }
                    workbench.RenderPreviewState(0,false);
                    var devices=new System.Collections.Generic.List<string>();for(int slot=0;slot<4;slot++)devices.Add("XInput "+(slot+1)+": "+(InputReader.ReadPad(slot,24).Connected?"conectado":"desconectado"));File.WriteAllLines(Path.Combine(args[2],"input-devices.txt"),devices);
                }
				if (page == "settings" || page == "saves" || page == "library")
				{
					ScrollPreview(form);
					File.WriteAllText(Path.Combine(args[2],page+"-scroll-layout.txt"), DescribeLayout(form));
					Application.DoEvents();
					using var scrolled = new System.Drawing.Bitmap(form.Width, form.Height);
					form.DrawToBitmap(scrolled, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
					scrolled.Save(Path.Combine(args[2], page + "-scrolled.png"));
				}
			}
			using var createDialog = new CreatePokemonDialog(649);
			using var editDialog = new EditPokemonDialog(new PKHeX.Core.PK3 { Species = 1 });
			using var keyDialog = new KeyCaptureDialog("Movimento — esquerda");
            using(var consoleTest=new ConsoleTestForm(new InputDeviceProfile(),new[]{"W","S","A","D","Z","X","Q","E","Enter","Backspace","C","V"},0))
            {
                if(Environment.GetEnvironmentVariable("POKEMONPLAY_PREVIEW_WIDTH")=="1000")consoleTest.ClientSize=new System.Drawing.Size(800,640);
                consoleTest.Show();Application.DoEvents();
                for(int model=0;model<3;model++)
                {
                    consoleTest.RenderIllustrativeState(model);
                    using var consoleBitmap=new System.Drawing.Bitmap(consoleTest.Width,consoleTest.Height);
                    consoleTest.DrawToBitmap(consoleBitmap,new System.Drawing.Rectangle(0,0,consoleTest.Width,consoleTest.Height));
                    consoleBitmap.Save(Path.Combine(args[2],new[]{"test-gba.png","test-ds.png","test-3ds.png"}[model]));
                }
                consoleTest.Hide();
            }
			foreach (var entry in new[] { ("create-dialog", (Form)createDialog), ("edit-dialog", (Form)editDialog), ("key-dialog", (Form)keyDialog) })
			{
				entry.Item2.Show(); Application.DoEvents();
				using var dialogBitmap = new System.Drawing.Bitmap(entry.Item2.Width, entry.Item2.Height);
				entry.Item2.DrawToBitmap(dialogBitmap, new System.Drawing.Rectangle(0, 0, entry.Item2.Width, entry.Item2.Height));
				dialogBitmap.Save(Path.Combine(args[2], entry.Item1 + ".png")); entry.Item2.Hide();
			}
			return;
		}
        string updateStage=null;
        if(args.Length==2&&args[0]=="--update-ready") {
            string candidate=Path.GetFullPath(args[1]);string name=Path.GetFileName(candidate);
            if(string.Equals(Path.GetDirectoryName(candidate),Path.GetFullPath(AppPaths.Root),StringComparison.OrdinalIgnoreCase)&&name.StartsWith(".pokemonplay-update-",StringComparison.Ordinal)&&Guid.TryParseExact(name.Substring(20),"N",out _)&&Directory.Exists(candidate)) updateStage=candidate;
        }
        if(updateStage!=null&&File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"emulators-runtime.zip")))
            UpdatePreparationForm.Prepare(AppDomain.CurrentDomain.BaseDirectory,AppPaths.Root,updateStage);
        else BundledEmulatorArchive.EnsureExtracted(AppDomain.CurrentDomain.BaseDirectory);
        GameSessionSettingsService.RecoverAbandonedSessions();
        using var launcher=new LauncherForm(AppPaths.Root);
        launcher.UpdateReadyStage=updateStage;
		Application.Run(launcher);
	}
	private static void ScrollPreview(Control parent)
	{
		foreach (Control child in parent.Controls)
		{
			if (child is Panel panel && panel.AutoScroll && panel.VerticalScroll.Visible)
				panel.AutoScrollPosition = new System.Drawing.Point(0, Math.Max(0, panel.DisplayRectangle.Height - panel.ClientSize.Height));
			else ScrollPreview(child);
		}
	}
    private static string DescribeLayout(Control parent)
    {
        string result = parent.GetType().Name + " bounds=" + parent.Bounds +
            (parent is ScrollableControl scroll ? " display=" + scroll.DisplayRectangle + " scroll=" + scroll.AutoScrollPosition : "") + "\n";
        foreach(Control child in parent.Controls) result += DescribeLayout(child);
        return result;
    }
    private static InputWorkbench FindWorkbench(Control parent)
    {
        if(parent is InputWorkbench workbench)return workbench;
        foreach(Control child in parent.Controls){var result=FindWorkbench(child);if(result!=null)return result;}return null;
    }
}
