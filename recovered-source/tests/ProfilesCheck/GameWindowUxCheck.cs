using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class GameWindowUxCheck
{
    [DllImport("user32.dll")] private static extern IntPtr CreateMenu();
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, IntPtr id, string text);
    [DllImport("user32.dll")] private static extern bool SetMenu(IntPtr hwnd, IntPtr menu);
    [DllImport("user32.dll")] private static extern IntPtr GetMenu(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern uint CheckMenuItem(IntPtr menu, uint id, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    private static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    private sealed class EmulatorFixture : Form
    {
        internal IntPtr PauseMenu;
        internal bool Paused;
        internal int Commands;
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x111 && message.WParam.ToInt32() == 300)
            {
                Paused = !Paused; Commands++;
                CheckMenuItem(PauseMenu, 300, Paused ? 8u : 0u);
            }
            base.WndProc(ref message);
        }
    }

    internal static void Run()
    {
        using var native = new EmulatorFixture { Text = "Native emulator fixture", Size = new Size(440, 300) };
        native.Show();
        IntPtr menu = CreateMenu(), commands = CreatePopupMenu();
        native.PauseMenu = commands;
        AppendMenu(commands, 0, (IntPtr)301, "Pause when inactive");
        AppendMenu(commands, 0, (IntPtr)300, "&Pausar\tCtrl+P");
        AppendMenu(menu, 0x10, commands, "Command");
        SetMenu(native.Handle, menu);
        var chrome = new EmbeddedEmulatorWindow(native.Handle, true);
        Assert(GetMenu(native.Handle) == IntPtr.Zero && (GetWindowLong(native.Handle, -16) & 0x00CF0000) == 0, "native menu, caption and resize borders no longer overlap launcher controls");
        Assert(chrome.SetVbaPaused(true) && native.Paused && native.Commands == 1, "VBA pause dispatches the localized native command rather than the inactive-focus preference");
        Assert(chrome.SetVbaPaused(true) && native.Commands == 1, "repeated pause requests preserve state");
        Assert(chrome.SetVbaPaused(false) && !native.Paused && native.Commands == 2, "VBA resumes through its retained menu even with the native bar hidden");
        DestroyMenu(menu);

        string root = Path.Combine(Path.GetTempPath(), "pp-game-ux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Settings"));
        File.WriteAllText(Path.Combine(root, "Settings", "input-presets.txt"), "1\n0\n100\n0\n0\n0\n");
        using var launcher = new LauncherForm(root) { UpdatesEnabled = false };
        using var host = new GameHostForm("fixture", "retroarch", "", "LeafGreen", "Principal", root, Path.Combine(root, "session.cfg"), 0);
        host.ProcessStarter = _ => Process.GetCurrentProcess();
        int focusRequests = 0; host.ForegroundActivator = _ => {focusRequests++; return true;};
        Set(host, "emulatorEmbedded", true);
        Set(host, "emulatorTopLevel", true);
        Set(host, "embeddedWindowHandle", native.Handle);
        launcher.Show(); Application.DoEvents();
        launcher.RegisterGameSession(host); Application.DoEvents();
        try
        {
            var toolbar = host.Controls.Cast<Control>().Single(c => c.Dock == DockStyle.Top);
            foreach (int width in new[] { 1000, 1280, 1920 })
            {
                launcher.Width = width; launcher.PerformLayout(); Application.DoEvents();
                var buttons = toolbar.Controls.OfType<ThemeButton>().OrderBy(b => b.Left).ToArray();
                Assert(buttons.Length == 4 && buttons.All(b => b.Top >= 0 && b.Bottom <= toolbar.Height && b.Right <= toolbar.Width) && buttons.Zip(buttons.Skip(1), (a,b) => a.Right + 8 <= b.Left).All(v => v), "game actions fit without overlap at width " + width);
            }
            focusRequests = 0;
            launcher.Activate(); Application.DoEvents();
            host.NotifyApplicationActivated();
            Invoke(host, "TryEmbed"); Application.DoEvents();
            Assert(focusRequests == 0, "launcher activation and emulator polling do not steal mouse focus from its buttons");
            toolbar.Controls.OfType<ThemeButton>().Single(b => b.Text == "Retomar").PerformClick();
            Assert(focusRequests == 1, "Retomar explicitly restores emulator focus");
            toolbar.Controls.OfType<ThemeButton>().Single(b => b.Text == "Áudio").PerformClick(); Application.DoEvents();
            var content = (Control)Get(launcher, "contentHost");
            var settings = (SettingsView)content.Controls[0];
            var scroll = (Panel)Get(settings, "scroll");
            Assert(!host.Visible && ((Control)Get(launcher, "resumeGameButton")).Visible && scroll.AutoScrollPosition.Y < 0, "audio action pauses the session and opens the audio section with a resume action");
            Invoke(launcher, "ResumeGameSession"); Application.DoEvents();
            toolbar.Controls.OfType<ThemeButton>().Single(b => b.Text == "Controles").PerformClick(); Application.DoEvents();
            settings = (SettingsView)content.Controls[0]; scroll = (Panel)Get(settings, "scroll");
            Assert(!host.Visible && scroll.AutoScrollPosition.Y == 0, "controls action opens the input workbench and preserves the game");
            Invoke(launcher, "ResumeGameSession"); Application.DoEvents();
            string previews = Environment.GetEnvironmentVariable("POKEMONPLAY_GAME_UX_PREVIEWS");
            if (!string.IsNullOrEmpty(previews))
            {
                Directory.CreateDirectory(previews);
                foreach (int width in new[] { 1000, 1920 })
                {
                    launcher.Size = new Size(width, width == 1000 ? 720 : 1080); Application.DoEvents();
                    using var bitmap = new Bitmap(launcher.Width, launcher.Height);
                    launcher.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(previews, "game-" + width + ".png"));
                }
            }
        }
        finally { Set(host, "closing", true); host.Close(); launcher.Close(); }
    }
}
