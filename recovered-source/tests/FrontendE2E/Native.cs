using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; public Rectangle Rectangle=>Rectangle.FromLTRB(Left,Top,Right,Bottom); }
    [StructLayout(LayoutKind.Sequential)] struct Mouse { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit,Size=40)] struct Input { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public Mouse Mouse; }
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] input,int size);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] internal static extern bool IsChild(IntPtr parent,IntPtr child);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr hwnd,ref Point point);
    [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)] struct GuiThreadInfo {public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public Rect CaretRect;}
    [DllImport("user32.dll",SetLastError=true)] static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
    [DllImport("user32.dll")] static extern IntPtr GetFocus();
    internal static object FocusEvidence(IntPtr foreground){
        uint thread=GetWindowThreadProcessId(foreground,out uint pid);var info=new GuiThreadInfo{Size=(uint)Marshal.SizeOf<GuiThreadInfo>()};
        bool available=GetGUIThreadInfo(thread,ref info);int error=available?0:Marshal.GetLastWin32Error();
        return new{thread,pid,available,error,activeHwnd=info.Active.ToInt64(),focusHwnd=info.Focus.ToInt64(),captureHwnd=info.Capture.ToInt64(),callingThreadFocusHwnd=GetFocus().ToInt64()};
    }
    delegate bool WindowCallback(IntPtr hwnd,IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,System.Text.StringBuilder text,int length);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr hwnd,int id);
    [DllImport("iphlpapi.dll")] static extern uint GetExtendedUdpTable(IntPtr table,ref int size,bool order,uint family,int tableClass,uint reserved);
    internal static void MouseAt(Point position,bool down)
    {
        if(!SetCursorPos(position.X,position.Y))throw new Win32Exception();
        if(SendInput(1,new[]{new Input{Mouse=new Mouse{Flags=down?2u:4u}}},40)!=1)throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static Rectangle Bounds(IntPtr hwnd)
    {
        if(!GetClientRect(hwnd,out var r))throw new Win32Exception();
        var origin=new Point();if(!ClientToScreen(hwnd,ref origin))throw new Win32Exception();
        return new Rectangle(origin,r.Rectangle.Size);
    }
    internal static bool ClickOwnedCloseConfirmation()
    {
        bool clicked=false;
        EnumWindows((hwnd,_)=>{
            GetWindowThreadProcessId(hwnd,out uint pid);if(pid!=Environment.ProcessId||!IsWindowVisible(hwnd))return true;
            var text=new System.Text.StringBuilder(256);GetWindowText(hwnd,text,text.Capacity);
            if(text.ToString()!="Voltar ao menu")return true;
            IntPtr yes=GetDlgItem(hwnd,6);if(yes==IntPtr.Zero)return true;
            Rectangle bounds=Bounds(yes);var point=new Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2);
            MouseAt(point,true);MouseAt(point,false);clicked=true;return false;
        },IntPtr.Zero);return clicked;
    }
    internal static object UdpOwner(int port,int pid)
    {
        int size=0;uint error=GetExtendedUdpTable(IntPtr.Zero,ref size,false,2,1,0);
        if(error!=122)throw new Win32Exception((int)error);
        IntPtr memory=Marshal.AllocHGlobal(size);
        try{
            error=GetExtendedUdpTable(memory,ref size,false,2,1,0);if(error!=0)throw new Win32Exception((int)error);
            int count=Marshal.ReadInt32(memory);var matches=new List<object>();
            for(int i=0;i<count;i++){
                int offset=4+i*12;uint address=unchecked((uint)Marshal.ReadInt32(memory,offset));
                uint raw=unchecked((uint)Marshal.ReadInt32(memory,offset+4));int actual=(int)(((raw&255)<<8)|((raw>>8)&255));
                int owner=Marshal.ReadInt32(memory,offset+8);
                if(actual==port){if(owner!=pid)throw new InvalidDataException("capture_identity: UDP port belongs to another PID");matches.Add(new{address=new System.Net.IPAddress(address).ToString(),port,pid=owner});}
            }
            if(matches.Count!=1)throw new InvalidDataException("capture_identity: expected exactly one owned IPv4 UDP endpoint");
            return matches[0];
        }finally{Marshal.FreeHGlobal(memory);}
    }
}
