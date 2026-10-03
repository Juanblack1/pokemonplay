using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class KeyBindingCaptureCheck
{
    static readonly string[] Custom = { "U", "I", "O", "P", "H", "J", "K", "L", "N", "M" };
    static readonly List<string> failures = new();
    static int caseNumber;
    record State(int Preset, string[] Custom, string[] Extra, string[] Effective);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Invoke(object target, string name, params object[] args)
    {
        try { target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    static State Read(SettingsView view)
    {
        int preset = Field<ThemeSelect>(view, "preset").SelectedIndex;
        string[] custom = (string[])Field<string[]>(view, "customKeys").Clone();
        string[] extra = (string[])Field<InputWorkbench>(view, "controlsCard").Profile.ExtraKeys.Clone();
        return new(preset, custom, extra, InputDeviceProfile.KeyboardKeys(preset, custom).Concat(extra).ToArray());
    }
    static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Unchanged(State before, State after)
    {
        Expect(before.Preset == after.Preset, $"preset changed from {before.Preset} to {after.Preset}");
        Expect(before.Custom.SequenceEqual(after.Custom), "stored custom mapping changed");
        Expect(before.Extra.SequenceEqual(after.Extra), "extra mapping changed");
        Expect(before.Effective.SequenceEqual(after.Effective), "effective mapping changed");
    }
    // The production CaptureKey still opens the real ShowDialog. The queued
    // owner callback executes inside that modal loop and drives its real handler.
    static void Capture(Form owner, SettingsView view, int action, Keys? key)
    {
        Exception callbackFailure = null;
        bool observed = false;
        KeyCaptureDialog ownedDialog = null;
        var clock = Stopwatch.StartNew();
        using var watchdog = new System.Windows.Forms.Timer { Interval = 50 };
        watchdog.Tick += (_, _) => {
            if (clock.ElapsedMilliseconds < 5000) return;
            callbackFailure ??= new TimeoutException("Owned capture modal exceeded five seconds");
            foreach (var dialog in Application.OpenForms.Cast<Form>().OfType<KeyCaptureDialog>().Where(d => d.Owner == owner).ToArray()) dialog.Close();
        };
        owner.BeginInvoke((Action)(() => {
            try {
                ownedDialog = Application.OpenForms.Cast<Form>().OfType<KeyCaptureDialog>().Single(d => d.Owner == owner && d.Visible);
                observed = true;
                if (key.HasValue) typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ownedDialog, new object[] { new KeyEventArgs(key.Value) });
                else ownedDialog.Close();
            } catch (Exception error) { callbackFailure = error; ownedDialog?.Close(); }
        }));
        watchdog.Start();
        try { Invoke(view, "CaptureKey", action); }
        finally {
            watchdog.Stop();
            if (ownedDialog != null && !ownedDialog.IsDisposed) ownedDialog.Close();
        }
        if (callbackFailure != null) throw new Exception("Modal fixture callback failed", callbackFailure);
        Expect(observed, "Production CaptureKey did not show the owned real modal");
    }
    static void Case(string root, string name, int preset, Action<Form, SettingsView, string> body)
    {
        string folder = Path.Combine(root, "keybinding-capture", (++caseNumber).ToString("D3"));
        string settings = Path.Combine(folder, "Settings", "input-presets.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(settings));
        File.WriteAllLines(settings, new[] { preset.ToString(), "0", "100", "0", "0", "1" }.Concat(Custom));
        var profile = new InputDeviceProfile { ExtraKeys = new[] { "F9", "F10" } };
        profile.Save(InputDeviceProfile.PathFor(settings));
        try {
            using var view = new SettingsView(settings);
            using var owner = new Form { Width = 1100, Height = 800 };
            owner.Controls.Add(view); owner.Show(); Application.DoEvents();
            body(owner, view, settings);
            owner.Close();
            Console.WriteLine("PASS key capture " + name);
        } catch (Exception error) { failures.Add(name + ": " + error.Message); Console.WriteLine("FAIL key capture " + name + ": " + error); }
    }
    internal static void Run(string root)
    {
        failures.Clear(); caseNumber = 0;
        for (int preset = 0; preset < 5; preset++) {
            foreach (bool escape in new[] { true, false }) {
                string name = $"preset {preset} {(escape ? "Escape" : "window close")} + save";
                Case(root, name, preset, (owner, view, file) => {
                    State before = Read(view);
                    Capture(owner, view, 4, escape ? Keys.Escape : (Keys?)null);
                    State after = Read(view);
                    // Check persistence even when the in-memory regression is red.
                    Field<CheckBox>(view, "mute").Checked = true;
                    Invoke(view, "Save");
                    string[] saved = File.ReadAllLines(file);
                    Expect(saved[3] == "1", "unrelated mute preference was not saved");
                    Expect(saved[0] == before.Preset.ToString(), "save after cancellation persisted a different preset");
                    Expect(saved.Skip(6).SequenceEqual(before.Custom), "save after cancellation persisted different custom keys");
                    Expect(InputDeviceProfile.Load(InputDeviceProfile.PathFor(file)).ExtraKeys.SequenceEqual(before.Extra), "save after cancellation changed extras");
                    using var reopened = new SettingsView(file);
                    Unchanged(before, Read(reopened)); Unchanged(before, after);
                });
            }
            foreach (bool extra in new[] { false, true }) {
                Case(root, $"preset {preset} duplicate {(extra ? "extra" : "other action")}", preset, (owner, view, _) => {
                    State before = Read(view);
                    Keys key = extra ? Keys.F9 : InputReader.ParseKey(before.Effective[1]);
                    Capture(owner, view, 4, key);
                    Expect(Field<Label>(view, "status").Text == "Essa tecla já está atribuída a outra ação.", "existing duplicate rejection message missing");
                    Unchanged(before, Read(view));
                });
            }
            for (int action = 0; action < 10; action++) {
                int selected = action;
                Case(root, $"preset {preset} accepts action {selected} only", preset, (owner, view, _) => {
                    State before = Read(view); Capture(owner, view, selected, Keys.F11); State after = Read(view);
                    Expect(after.Preset == 5, "accepted key did not select Personalizado");
                    string[] expected = before.Effective.Take(10).ToArray(); expected[selected] = "F11";
                    Expect(after.Custom.SequenceEqual(expected), "valid conversion changed another action or lost the preset mapping");
                    Expect(after.Effective.Take(10).SequenceEqual(expected), "effective accepted key mismatch");
                    Expect(after.Extra.SequenceEqual(before.Extra), "valid main key changed extras");
                });
            }
        }
        foreach (int preset in new[] { 0, 1, 2, 3, 4, 5 })
        foreach (int action in preset == 5 ? new[] { 0, 4, 9, 10, 11 } : new[] { 10, 11 })
        foreach (bool escape in new[] { true, false }) {
            int selected = action;
            Case(root, $"preset {preset} cancel action {selected} {(escape ? "Escape" : "close")}", preset, (owner, view, _) => {
                State before = Read(view); Capture(owner, view, selected, escape ? Keys.Escape : (Keys?)null); Unchanged(before, Read(view));
            });
        }
        if (failures.Count > 0) throw new Exception($"Key binding capture: {failures.Count}/{caseNumber} cases failed\n" + string.Join("\n", failures));
        Console.WriteLine($"PASS all {caseNumber} actual modal key capture cases");
    }
}
