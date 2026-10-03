using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class KeyBindingAccessibilityCheck
{
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowVisible(IntPtr window);
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS accessibility " + message); }
    static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    static string Description(KeyBindingRow row) => row.AccessibilityObject.Description ?? "";
    static void Mapping(KeyBindingRow row, string action, string key, bool editable)
    {
        var accessible = row.AccessibilityObject;
        Assert(accessible.Name == action && accessible.Role == AccessibleRole.PushButton, "stable action name and button role: " + action);
        string description = Description(row);
        Assert(description.Contains("Atribuição atual: " + key + ".", StringComparison.Ordinal), "actual accessible object exposes current mapping " + action + "=" + key);
        Assert(description.Contains("Enter", StringComparison.Ordinal) == editable && description.Contains("Espaço", StringComparison.Ordinal) == editable, "editing instructions reflect Editable: " + action);
    }
    static void Standalone()
    {
        using var row = new KeyBindingRow("Ação A", "Z");
        Assert(!row.IsHandleCreated, "row starts without native handle");
        row.SetKey("F9"); row.Editable = true;
        Assert(!row.IsHandleCreated, "metadata changes are safe before handle creation");
        Mapping(row, "Ação A", "F9", true);
        string before = Description(row);
        row.SetKey("F9"); row.Editable = true; row.Active = true; row.Active = false;
        Assert(Description(row) == before, "idempotent metadata and gameplay Active preserve description");
        foreach (string empty in new[] { "", "  ", null }) {
            row.SetKey(empty);
            Assert(Description(row).Contains("não atribuído", StringComparison.OrdinalIgnoreCase), "missing mapping has clear accessible indication");
        }
        row.SetKey("LB"); row.Editable = false;
        Mapping(row, "Ação A", "LB", false);
        using var host = new Form { ClientSize = new Size(400, 100) };
        host.Controls.Add(row); host.Show(); Application.DoEvents();
        int clicks = 0; row.RowClick += (_, _) => clicks++;
        foreach (Keys key in new[] { Keys.Enter, Keys.Space }) Invoke(row, "OnKeyDown", new KeyEventArgs(key));
        Assert(clicks == 0, "noneditable row cannot activate keyboard capture");
        row.Editable = true;
        foreach (Keys key in new[] { Keys.Enter, Keys.Space }) {
            var input = new KeyEventArgs(key); Invoke(row, "OnKeyDown", input);
            Assert(input.Handled, "editable row consumes activation key " + key);
        }
        Assert(clicks == 2, "Enter and Space retain one activation each");
        Mapping(row, "Ação A", "LB", true);
        host.Close();
    }
    // Exercise the existing row event -> InputWorkbench -> SettingsView.CaptureKey
    // -> real ShowDialog. Only the fixture drives the modal's existing key handler.
    static void Capture(Form owner, KeyBindingRow row, Keys key)
    {
        Exception failure = null; bool observed = false; KeyCaptureDialog dialog = null;
        using var watchdog = new Timer { Interval = 5000 };
        watchdog.Tick += (_, _) => {
            failure ??= new TimeoutException("Owned accessibility capture exceeded five seconds");
            foreach (var item in Application.OpenForms.Cast<Form>().OfType<KeyCaptureDialog>().Where(d => d.Owner == owner).ToArray()) item.Close();
        };
        owner.BeginInvoke((Action)(() => {
            try {
                dialog = Application.OpenForms.Cast<Form>().OfType<KeyCaptureDialog>().Single(d => d.Owner == owner && d.Visible);
                observed = true; Invoke(dialog, "OnKeyDown", new KeyEventArgs(key));
            } catch (Exception error) { failure = error; dialog?.Close(); }
        }));
        watchdog.Start();
        try { Invoke(row, "OnKeyDown", new KeyEventArgs(Keys.Enter)); }
        finally { watchdog.Stop(); if (dialog != null && !dialog.IsDisposed) dialog.Close(); }
        if (failure != null) throw new Exception("Accessibility modal fixture failed", failure);
        Assert(observed, "row keyboard activation opens real owned capture modal");
    }
    static void Settings(string root)
    {
        string folder = Path.Combine(root, "keybinding-accessibility", "Settings"); Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "input-presets.txt");
        File.WriteAllText(file, "1\n0\n100\n0\n0\n1\n");
        var originalProfile = new InputDeviceProfile { ExtraKeys = new[] { "F9", "F10" } };
        string profileFile = InputDeviceProfile.PathFor(file); originalProfile.Save(profileFile);
        byte[] originalSettings = File.ReadAllBytes(file), originalDevice = File.ReadAllBytes(profileFile);
        using var view = new SettingsView(file);
        using var owner = new Form { ClientSize = new Size(1100, 800) };
        owner.Controls.Add(view); owner.Show(); Application.DoEvents();
        var workbench = Field<InputWorkbench>(view, "controlsCard");
        var mode = Field<ThemeSelect>(workbench, "mode");
        var model = Field<ThemeSelect>(workbench, "console");
        var preset = Field<ThemeSelect>(view, "preset");
        string[] actions = { "Cima", "Baixo", "Esquerda", "Direita", "Ação A", "Ação B", "Ombro L", "Ombro R", "Start", "Select", "Ação X", "Ação Y" };
        void CheckRows(string[] keys) {
            for (int i = 0; i < 12; i++) {
                var row = workbench.Rows[i];
                // Hidden X/Y never received an HWND in the initial GBA layout.
                // MSAA State falls back to the standard HWND accessible object;
                // Description/Name alone do not initialize that native fallback.
                // Source: dotnet/winforms v10.0.0 Accessibility/
                // Control.ControlAccessibleObject.cs (Handle setter) and
                // AccessibleObject.cs (State -> SystemIAccessible.TryGetState).
                IntPtr rowHandle = row.Handle;
                Assert(row.IsHandleCreated && rowHandle != IntPtr.Zero, "native row exists for MSAA visibility measurement: " + actions[i]);
                Mapping(row, actions[i], keys[i], true);
                Assert(row.TabStop && row.Visible == (i < 10 || model.SelectedIndex > 0), "model visibility and tab eligibility preserved: " + actions[i]);
                if (i >= 10 && model.SelectedIndex == 0) {
                    Assert(!IsWindowVisible(rowHandle), "hidden GBA extra action has no visible native window");
                    Assert((row.AccessibilityObject.State & AccessibleStates.Invisible) != 0, "native-backed MSAA marks hidden GBA extra action invisible");
                }
            }
            int expectedVisible = model.SelectedIndex == 0 ? 10 : 12;
            Assert(workbench.Rows.Count(row => row.Visible && row.Enabled && row.TabStop) == expectedVisible, "exact visible enabled tab-stop count for selected model: " + expectedVisible);
        }
        foreach (int selectedModel in new[] { 0, 1, 2 }) {
            model.SelectedIndex = selectedModel;
            foreach (int selectedPreset in new[] { 0, 1, 2, 3, 4, 5 }) {
                preset.SelectedIndex = selectedPreset;
                var current = InputDeviceProfile.KeyboardKeys(selectedPreset, Field<string[]>(view, "customKeys")).Concat(new[] { "F9", "F10" }).ToArray();
                CheckRows(current);
            }
            mode.SelectedIndex = 2;
            CheckRows(workbench.Profile.Bindings.Select(InputReader.Label).ToArray());
            mode.SelectedIndex = 0;
        }
        model.SelectedIndex = 1; preset.SelectedIndex = 0;
        string[] beforeCancel = workbench.Rows.Select(Description).ToArray();
        Capture(owner, workbench.Rows[4], Keys.Escape);
        Assert(workbench.Rows.Select(Description).SequenceEqual(beforeCancel), "cancelled real capture preserves all accessible mappings");
        Capture(owner, workbench.Rows[4], Keys.F8);
        Mapping(workbench.Rows[4], "Ação A", "F8", true);
        Assert(workbench.Rows.Where((_, i) => i != 4).Select(Description).SequenceEqual(beforeCancel.Where((_, i) => i != 4)), "successful capture updates only its own accessible assignment");
        Invoke(Field<ThemeButton>(view, "restoreButton"), "OnClick", EventArgs.Empty);
        Assert(preset.SelectedIndex == 1 && mode.SelectedIndex == 0 && model.SelectedIndex == 0, "real restore action resets mode/preset/model");
        CheckRows(new[] { "W", "S", "A", "D", "Z", "X", "Q", "E", "Enter", "Backspace", "C", "V" });
        Assert(File.ReadAllBytes(file).SequenceEqual(originalSettings) && File.ReadAllBytes(profileFile).SequenceEqual(originalDevice), "queries and unsaved UI changes preserve settings files byte for byte");
        var bindings = (string[])workbench.Profile.Bindings.Clone();
        for (int i = 0; i < 12; i++) _ = workbench.Rows[i].AccessibilityObject.Description;
        Assert(workbench.Profile.Bindings.SequenceEqual(bindings), "accessibility queries preserve bindings");
        owner.Close();
    }
    internal static void Run(string root) { Standalone(); Settings(root); Console.WriteLine("PASS key binding actual AccessibilityObject and SettingsView update paths"); }
}
