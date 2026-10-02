using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class ControllerVisualizer : BufferedPanel
{
    private static readonly Image[] ConsoleArt={LoadArt("ConsoleGba"),LoadArt("ConsoleDs"),LoadArt("Console3ds")};
    private static Image LoadArt(string resource)
    {
        using var stream=typeof(ControllerVisualizer).Assembly.GetManifestResourceStream(resource);
        using var image=Image.FromStream(stream);return new Bitmap(image);
    }
    public bool[] Actions = new bool[12];
    public InputSnapshot Snapshot = new();
    public int DeadZone { get; set; } = 24;
    public bool Testing;
    public bool VirtualInput;
    public bool[] VirtualActions = new bool[12];
    public event EventHandler VirtualChanged;
    private readonly Dictionary<uint,int> touches = new();
    private int mouseAction = -1;
    [StructLayout(LayoutKind.Sequential)] private struct TouchInput { public int X,Y; public IntPtr Source; public uint Id,Flags,Mask,Time; public UIntPtr Extra; public uint CX,CY; }
    [DllImport("user32.dll")] private static extern bool RegisterTouchWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] private static extern bool GetTouchInputInfo(IntPtr handle,uint count,[In,Out] TouchInput[] inputs,int size);
    [DllImport("user32.dll")] private static extern bool CloseTouchInputHandle(IntPtr handle);
    public ControllerVisualizer() { BackColor = AppTheme.Background; AccessibleName = "Visualização dos controles"; SetStyle(ControlStyles.ResizeRedraw, true); }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);RegisterTouchWindow(Handle,0);}
    public int ConsoleModel { get; set; }
    public bool TouchTesting;
    public bool TouchPressed { get; private set; }
    public Point TouchPosition { get; private set; }
    public int TouchContacts => screenTouches.Count + (mouseScreen.HasValue ? 1 : 0);
    private readonly Dictionary<uint,PointF> screenTouches=new();
    private PointF? mouseScreen;
    private int CanvasHeight => ConsoleModel==0?300:450;
    private Rectangle ScreenBounds => ConsoleModel==0?Rectangle.Empty:new Rectangle(166,228,188,141);
    private PointF CanvasPoint(Point p)
    {
        float scale=Math.Min(Width/520f,(Height-26)/(float)CanvasHeight);
        return new PointF((p.X-(Width-520*scale)/2)/scale,(p.Y-(Height-26-CanvasHeight*scale)/2)/scale-(ConsoleModel>0?13:0));
    }
    private Dictionary<int,Rectangle> ButtonBounds()
    {
        if(ConsoleModel==0)return new(){[0]=new(77,116,26,26),[1]=new(77,166,26,26),[2]=new(51,142,26,26),[3]=new(103,142,26,26),[4]=new(410,112,32,32),[5]=new(374,140,32,32),[6]=new(48,46,120,24),[7]=new(352,46,120,24),[8]=new(104,205,54,22),[9]=new(45,205,54,22)};
        if(ConsoleModel==2)return new(){[0]=new(104,307,22,20),[1]=new(104,347,22,20),[2]=new(84,327,20,20),[3]=new(126,327,20,20),[4]=new(413,271,22,22),[5]=new(392,297,22,22),[10]=new(392,249,22,22),[11]=new(371,271,22,22),[6]=new(112,191,60,18),[7]=new(348,191,60,18),[8]=new(361,337,42,20),[9]=new(361,362,42,20)};
        int cy=268;
        return new(){[0]=new(129,cy-22,20,20),[1]=new(129,cy+18,20,20),[2]=new(109,cy-2,20,20),[3]=new(149,cy-2,17,20),[4]=new(389,263,20,20),[5]=new(371,282,20,20),[10]=new(371,244,20,20),[11]=new(354,263,20,20),[6]=new(112,191,60,18),[7]=new(348,191,60,18),[8]=new(361,337,42,20),[9]=new(361,362,42,20)};
    }
    private int Hit(Point p)
    {
        var point=CanvasPoint(p);if(ConsoleModel==2&&new RectangleF(96,244,42,42).Contains(point)){float dx=point.X-117,dy=point.Y-265;if(dx*dx+dy*dy<36)return -1;return Math.Abs(dx)>Math.Abs(dy)?dx<0?2:3:dy<0?0:1;}foreach(var button in ButtonBounds())if(button.Value.Contains((int)point.X,(int)point.Y))return button.Key;return -1;
    }
    private PointF? ScreenPoint(Point p)
    {
        var point=CanvasPoint(p);var r=ScreenBounds;if(!TouchTesting||!r.Contains((int)point.X,(int)point.Y))return null;
        return new PointF((point.X-r.Left)/r.Width,(point.Y-r.Top)/r.Height);
    }
    private void UpdateScreen()
    {
        TouchPressed=mouseScreen.HasValue||screenTouches.Count>0;
        if(!TouchPressed){Invalidate();return;}
        PointF normalized=mouseScreen ?? (screenTouches.Count>0?new List<PointF>(screenTouches.Values)[0]:new PointF());
        int w=ConsoleModel==2?320:256,h=ConsoleModel==2?240:192;
        TouchPosition=new Point(Math.Clamp((int)(normalized.X*w),0,w-1),Math.Clamp((int)(normalized.Y*h),0,h-1));Invalidate();
    }
    private void Changed(){Array.Clear(VirtualActions);foreach(int a in touches.Values)if(a>=0)VirtualActions[a]=true;if(mouseAction>=0)VirtualActions[mouseAction]=true;Actions=(bool[])VirtualActions.Clone();UpdateScreen();Invalidate();VirtualChanged?.Invoke(this,EventArgs.Empty);}
    public void ReleaseVirtual(){touches.Clear();screenTouches.Clear();mouseScreen=null;mouseAction=-1;Capture=false;Changed();}
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
        mouseScreen=ScreenPoint(e.Location);if(VirtualInput)mouseAction=Hit(e.Location);
        if(mouseScreen.HasValue||mouseAction>=0){Capture=true;Changed();}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(!Capture||touches.Count>0||screenTouches.Count>0)return;mouseScreen=ScreenPoint(e.Location);if(VirtualInput)mouseAction=Hit(e.Location);Changed();
    }
    protected override void OnMouseUp(MouseEventArgs e){mouseScreen=null;mouseAction=-1;Capture=false;Changed();base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture&&(mouseAction>=0||mouseScreen.HasValue)){mouseScreen=null;mouseAction=-1;Changed();}base.OnMouseCaptureChanged(e);}
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x240)
        {
            try
            {
                uint count=(uint)(m.WParam.ToInt64()&0xffff);var inputs=new TouchInput[count];
                if(GetTouchInputInfo(m.LParam,count,inputs,Marshal.SizeOf<TouchInput>()))foreach(var t in inputs)
                {
                    // WM_TOUCH owns physical touch contacts; discard the mouse
                    // event Windows promotes from that same finger.
                    mouseScreen=null;mouseAction=-1;
                    if((t.Flags&4)!=0){touches.Remove(t.Id);screenTouches.Remove(t.Id);continue;}
                    var point=PointToClient(new Point(t.X/100,t.Y/100));
                    if(VirtualInput)touches[t.Id]=Hit(point);
                    var screen=ScreenPoint(point);if(screen.HasValue)screenTouches[t.Id]=screen.Value;else screenTouches.Remove(t.Id);
                }
                Changed();
            }
            finally{CloseTouchInputHandle(m.LParam);}m.Result=IntPtr.Zero;return;
        }
        base.WndProc(ref m);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.None;
        float scale = Math.Min(Width / 520f, (Height-26) / (float)CanvasHeight);
        var saved = g.Save(); g.TranslateTransform((Width - 520 * scale) / 2, (Height-26 - CanvasHeight * scale) / 2); g.ScaleTransform(scale, scale);
        if(ConsoleModel>0)g.TranslateTransform(0,13);
        DrawConsole(g);
        g.Restore(saved);
        TextRenderer.DrawText(g, TouchTesting ? "Clique, toque e arraste na tela inferior" : VirtualInput ? "Toque ou clique nos botões para jogar" : Testing ? "Pressione e solte para ver a resposta" : "Inicie o teste para visualizar as entradas", AppTheme.Caption, new Rectangle(8, Height - 26, Width - 16, 24), AppTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    private void DrawConsole(Graphics g)
    {
        var target=ConsoleModel==0?new Rectangle(0,0,520,300):ConsoleModel==1?new Rectangle(4,-21,511,444):new Rectangle(0,-30,520,448);
        g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(ConsoleArt[Math.Clamp(ConsoleModel,0,2)],target);g.PixelOffsetMode=PixelOffsetMode.Default;
        if(ConsoleModel==0)
        {
            Screen(g,new Rectangle(167,90,185,139),false);
            Ink(g,"GAME BOY ADVANCE",new Rectangle(167,232,185,20),AppTheme.Text);
        }
        else
        {
            Screen(g,ConsoleModel==2?new Rectangle(137,30,246,147):new Rectangle(166,30,188,141),false);
            Screen(g,ScreenBounds,true);
            if(ConsoleModel==2)Stick(g,117,265,Math.Abs(Snapshot.LX)>.24f?Snapshot.LX:(Actions[3]?1:0)-(Actions[2]?1:0),Math.Abs(Snapshot.LY)>.24f?Snapshot.LY:(Actions[0]?1:0)-(Actions[1]?1:0),false);
        }
        string[] labels={"","","","","A","B","L","R","START","SEL","X","Y"};
        foreach(var b in ButtonBounds())
        {
            Rectangle bounds=ConsoleModel==0&&b.Key==6?new Rectangle(68,46,80,24):ConsoleModel==0&&b.Key==7?new Rectangle(372,46,80,24):b.Value;
            Block(g,bounds,labels[b.Key],Actions[b.Key]);
        }
    }
    private void Shell(Graphics g,Rectangle r,Color color)
    {
        using var fill=new SolidBrush(color);using var border=new Pen(AppTheme.Border,3);PaintTools.FillRounded(g,fill,r,8);PaintTools.DrawRounded(g,border,r,8);
    }
    private void Ink(Graphics g,string text,Rectangle r,Color color)
    {
        using var ink=new SolidBrush(color);using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};g.DrawString(text,AppTheme.CaptionBold,ink,r,format);
    }
    private void Screen(Graphics g,Rectangle r,bool touch)
    {
        using var fill=new SolidBrush(Color.FromArgb(13,28,48));using var border=new Pen(touch&&TouchPressed?AppTheme.Focus:AppTheme.BorderSoft,3);g.FillRectangle(fill,r);g.DrawRectangle(border,r);
        using var grid=new Pen(Color.FromArgb(38,AppTheme.Border));for(int x=r.Left+16;x<r.Right;x+=16)g.DrawLine(grid,x,r.Top,x,r.Bottom);for(int y=r.Top+16;y<r.Bottom;y+=16)g.DrawLine(grid,r.Left,y,r.Right,y);
        Ink(g,touch?"TOUCH":"TELA",new Rectangle(r.Left,r.Top+r.Height/2-10,r.Width,20),AppTheme.TextMuted);
        if(touch&&TouchPressed)
        {
            var points=new List<PointF>(screenTouches.Values);if(mouseScreen.HasValue)points.Add(mouseScreen.Value);
            using var mark=new Pen(AppTheme.Focus,2);foreach(var point in points){float x=r.Left+point.X*r.Width,y=r.Top+point.Y*r.Height;g.DrawEllipse(mark,x-9,y-9,18,18);g.DrawLine(mark,x-13,y,x+13,y);g.DrawLine(mark,x,y-13,x,y+13);}
        }
    }
    public void PreviewTouch(float x,float y){screenTouches[0]=new PointF(x,y);UpdateScreen();}
    private void Block(Graphics g, Rectangle r, string text, bool active)
    {
        if (active) r.Offset(0, 3);
        using var fill = new SolidBrush(active ? AppTheme.BlueSoft : AppTheme.Background); using var pen = new Pen(active ? AppTheme.Focus : AppTheme.Border, 2);
        PaintTools.FillRounded(g, fill, r, 4); PaintTools.DrawRounded(g, pen, r, 4);
        // DrawString participates in the canvas transform; TextRenderer does not.
        using var brush = new SolidBrush(active ? AppTheme.Background : AppTheme.Text); using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, AppTheme.CaptionBold, brush, r, format);
    }
    private void Circle(Graphics g, int x, int y, string label, bool active, Color accent)
    {
        var r = new Rectangle(x - 14, y + (active ? 3 : 0), 28, 28);
        using var brush = new SolidBrush(active ? accent : AppTheme.Background); using var pen = new Pen(accent, active ? 3 : 2); g.FillEllipse(brush, r); g.DrawEllipse(pen, r);
        using var ink = new SolidBrush(active ? AppTheme.Background : accent); using var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }; g.DrawString(label, AppTheme.BodyBold, ink, r, f);
    }
    private void Stick(Graphics g, int x, int y, float dx, float dy, bool active)
    {
        using var baseBrush = new SolidBrush(AppTheme.Background); using var rim = new Pen(AppTheme.Border, 2); g.FillEllipse(baseBrush, x-21,y-21,42,42); g.DrawEllipse(rim,x-21,y-21,42,42);
        using var knob = new SolidBrush(active ? AppTheme.BlueSoft : AppTheme.BorderSoft); using var outline = new Pen(Math.Sqrt(dx*dx+dy*dy)>DeadZone/100f ? AppTheme.Focus : AppTheme.Border,2);
        var r = new RectangleF(x-12+dx*8, y-12-dy*8,24,24); g.FillEllipse(knob,r); g.DrawEllipse(outline,r);
    }
    private void Trigger(Graphics g, int x, float value, string label)
    {
        using var background = new SolidBrush(AppTheme.Background); using var fill = new SolidBrush(AppTheme.Focus); g.FillRectangle(background,x,12,84,8); g.FillRectangle(fill,x,12,84*value,8);
    }
}
