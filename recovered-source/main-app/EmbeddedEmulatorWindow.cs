using System;
using System.Runtime.InteropServices;
using System.Text;

// Keep native emulator chrome out of the launcher and retain its pause command.
internal sealed class EmbeddedEmulatorWindow
{
    private readonly IntPtr window;
    private readonly IntPtr menu;
    private readonly bool vba;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetMenu(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetMenu(IntPtr hwnd, IntPtr menu);
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll")] private static extern IntPtr GetSubMenu(IntPtr menu, int position);
    [DllImport("user32.dll")] private static extern uint GetMenuItemID(IntPtr menu, int position);
    [DllImport("user32.dll")] private static extern uint GetMenuState(IntPtr menu, uint item, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetMenuString(IntPtr menu, uint item, StringBuilder text, int length, uint flags);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    internal EmbeddedEmulatorWindow(IntPtr window, bool vba, IntPtr owner = default)
    {
        this.window = window;
        this.vba = vba;
        menu = GetMenu(window);
        // Remove caption, resize frame and system buttons. Preserve the chosen child/popup mode.
        int style = (GetWindowLong(window, -16) & ~0x00CF0000) | 0x06000000;
        SetWindowLong(window, -16, style);
        SetWindowLong(window, -20, GetWindowLong(window, -20) & ~0x00040309); // topmost / appwindow / edges
        SetMenu(window, IntPtr.Zero);
        if (owner != IntPtr.Zero)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr(window, -8, owner);
            else SetWindowLong(window, -8, owner.ToInt32());
        }
        SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x0037); // frame changed; no move/size/activation/z-order
    }

    internal bool SetVbaPaused(bool paused)
    {
        if (!vba || menu == IntPtr.Zero) return false;
        return SetPause(menu, paused);
    }

    private bool SetPause(IntPtr currentMenu, bool paused)
    {
        for (int i = 0; i < GetMenuItemCount(currentMenu); i++)
        {
            IntPtr child = GetSubMenu(currentMenu, i);
            if (child != IntPtr.Zero) { if (SetPause(child, paused)) return true; continue; }
            var text = new StringBuilder(256);
            GetMenuString(currentMenu, (uint)i, text, text.Capacity, 0x400);
            string label = text.ToString().Replace("&", "").Split('\t')[0].Trim().TrimEnd('.');
            if (!label.Equals("Pause", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("Pausar", StringComparison.OrdinalIgnoreCase) &&
                !label.Equals("Pausa", StringComparison.OrdinalIgnoreCase)) continue;
            uint state = GetMenuState(currentMenu, (uint)i, 0x400);
            if (state == uint.MaxValue || (state & 3) != 0) return false;
            if (((state & 8) != 0) == paused) return true;
            return SendMessageTimeout(window, 0x0111, (IntPtr)GetMenuItemID(currentMenu, i), IntPtr.Zero, 2, 250, out _) != IntPtr.Zero;
        }
        return false;
    }
}
