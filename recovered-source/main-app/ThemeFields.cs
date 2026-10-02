using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class ThemeSelect : BufferedPanel
{
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Enter or Keys.Space || base.IsInputKey(keyData);
    private int selectedIndex=-1;
    private string accessibleContext;
    private ContextMenuStrip menu;
    public System.Collections.Generic.List<object> Items {get;}=new();
    public event EventHandler SelectedIndexChanged;
    public ComboBoxStyle DropDownStyle {get;set;}=ComboBoxStyle.DropDownList;
    public FlatStyle FlatStyle {get;set;}=FlatStyle.Flat;
    public string AccessibleContext
    {
        get=>accessibleContext;
        set{accessibleContext=value;UpdateAccessibleName();}
    }
    public int SelectedIndex
    {
        get=>selectedIndex;
        set {if(value < -1 || value >= Items.Count)throw new ArgumentOutOfRangeException(nameof(value));bool changed=selectedIndex!=value;selectedIndex=value;UpdateAccessibleName();Invalidate();if(changed)SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}
    }
    private string SelectedText=>selectedIndex>=0&&selectedIndex<Items.Count?Convert.ToString(Items[selectedIndex]):"Selecione uma caixa";
    private void UpdateAccessibleName()=>AccessibleName=string.IsNullOrWhiteSpace(accessibleContext)?SelectedText:$"{accessibleContext}: {SelectedText}";
    public ThemeSelect(){Height=40;Font=AppTheme.Body;ForeColor=AppTheme.Text;BackColor=AppTheme.Surface;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.ComboBox;SetStyle(ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.None;var rect=new Rectangle(1,1,Width-3,Height-3);using var pen=new Pen(Focused?AppTheme.Focus:AppTheme.Border);PaintTools.DrawRounded(e.Graphics,pen,rect,8);
        TextRenderer.DrawText(e.Graphics,SelectedText,Font,new Rectangle(12,0,Width-44,Height),Enabled?AppTheme.Text:AppTheme.TextMuted,TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        using var arrow=new Pen(AppTheme.TextMuted,1.5f);int x=Width-22,y=Height/2;e.Graphics.DrawLines(arrow,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});
    }
    protected override void OnClick(EventArgs e){Focus();OpenMenu();base.OnClick(e);}
    private void OpenMenu()
    {
        if(!Enabled||Items.Count==0||IsDisposed||Disposing)return;
        if(menu!=null&&menu.Visible)return;
        menu??=new ContextMenuStrip{Font=AppTheme.Body,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,ShowImageMargin=false,Renderer=new ThemeMenuRenderer()};
        while(menu.Items.Count>0){var old=menu.Items[0];menu.Items.RemoveAt(0);old.Dispose();}
        for(int i=0;i<Items.Count;i++)
        {
            int index=i;var item=new ToolStripMenuItem(Convert.ToString(Items[i])){ForeColor=AppTheme.Text,Padding=new Padding(12,6,12,6),Checked=i==SelectedIndex};
            item.Click+=(_,_)=>
            {
                if(IsDisposed||Disposing||!IsHandleCreated)return;
                // Finish ToolStrip's click/close sequence before a selection callback
                // can change or dispose the owning view.
                BeginInvoke(new Action(()=>{if(IsDisposed||Disposing||index>=Items.Count)return;SelectedIndex=index;if(!IsDisposed&&!Disposing)Focus();}));
            };
            menu.Items.Add(item);
        }
        menu.Show(this,new Point(0,Height+4));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.KeyCode is Keys.Space or Keys.Enter or Keys.Down){OpenMenu();e.Handled=true;}
        if(e.KeyCode==Keys.Up&&Items.Count>0){SelectedIndex=Math.Max(0,SelectedIndex-1);e.Handled=true;}
        base.OnKeyDown(e);
    }
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void Dispose(bool disposing)
    {
        if(disposing){var owned=menu;menu=null;owned?.Dispose();}
        base.Dispose(disposing);
    }
}
internal sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){using var brush=new SolidBrush(e.Item.Selected?AppTheme.SurfaceHover:AppTheme.SurfaceRaised);e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using var pen=new Pen(AppTheme.Border);e.Graphics.DrawRectangle(pen,new Rectangle(0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1));}
}

internal sealed class ThemeInput : BufferedPanel
{
    protected override void OnEnter(EventArgs e) {base.OnEnter(e);if(input!=null){placeholder.Visible=false;input.Visible=true;input.Focus();}}
    private readonly TextBox input;
    private readonly Label placeholder;
    public override string Text { get => input == null ? "" : input.Text; set { if (input != null) input.Text = value; } }
    public string PlaceholderText { get => input.PlaceholderText; set => input.PlaceholderText = value; }
    public void SetAccessibleMetadata(string name,string description)
    {
        AccessibleName=name;AccessibleDescription=description;
        input.AccessibleName=name;input.AccessibleDescription=description;
    }
    public ThemeInput()
    {
        Height = AppTheme.ControlHeight; BackColor = AppTheme.Surface; TabStop=true;
        input = new TextBox { BorderStyle = BorderStyle.None, Font = AppTheme.Body, BackColor = AppTheme.Surface, ForeColor = AppTheme.Text, PlaceholderText = "Buscar jogos…" };
        placeholder=new Label{Text="Buscar jogos…",Font=AppTheme.Body,ForeColor=AppTheme.TextMuted,BackColor=AppTheme.Surface,AutoSize=false,Height=24};
        input.Visible=false;Controls.Add(input);Controls.Add(placeholder);placeholder.BringToFront();placeholder.Click+=(_,_)=>{placeholder.Visible=false;input.Visible=true;input.Focus();};
        input.TextChanged += (_, _) => {placeholder.Visible=string.IsNullOrEmpty(input.Text)&&!input.Focused; OnTextChanged(EventArgs.Empty);};
        input.GotFocus += (_, _) => {placeholder.Visible=false;Invalidate();}; input.LostFocus += (_, _) => {placeholder.Visible=string.IsNullOrEmpty(input.Text);input.Visible=!placeholder.Visible;Invalidate();};
        Resize += (_, _) => {input.SetBounds(13, Math.Max(8, (Height - input.PreferredHeight) / 2), Math.Max(20, Width - 26), input.PreferredHeight);placeholder.SetBounds(13,(Height-24)/2,Width-26,24);};
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.None;
        using var pen = new Pen(input.Focused ? AppTheme.Focus : AppTheme.Border);
        PaintTools.DrawRounded(e.Graphics, pen, new Rectangle(1, 1, Math.Max(1, Width - 3), Height - 3), AppTheme.Radius);
    }
}

internal sealed class ThemeSlider : Control
{
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    private int current = 100;
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;
    public int TickFrequency { get; set; } = 10;
    public int SmallChange { get; set; } = 5;
    public int LargeChange { get; set; } = 10;
    public int Value { get => current; set { int next = Math.Clamp(value, Minimum, Maximum); if (next == current) return; current = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler ValueChanged;
    public ThemeSlider() { Height = 36; Width = 240; TabStop = true; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.None;
        int length = Math.Max(1, Width - 20), x = 10 + (int)(length * (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum));
        using var track = new Pen(AppTheme.Border, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var active = new Pen(AppTheme.Text, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(track, 10, Height / 2, Width - 10, Height / 2); e.Graphics.DrawLine(active, 10, Height / 2, x, Height / 2);
        using var brush = new SolidBrush(AppTheme.Background); e.Graphics.FillEllipse(brush, x - 7, Height / 2 - 7, 14, 14);
        using var outline = new Pen(Focused ? AppTheme.Focus : AppTheme.Text, 2); e.Graphics.DrawEllipse(outline, x - 7, Height / 2 - 7, 14, 14);
    }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); Capture = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (Capture && e.Button == MouseButtons.Left) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
    private void SetFromX(int x) => Value = Minimum + (int)Math.Round(Math.Clamp((x - 10) / (double)Math.Max(1, Width - 20), 0, 1) * (Maximum - Minimum));
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value -= SmallChange; if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value += SmallChange; if (e.KeyCode == Keys.Home) Value = Minimum; if (e.KeyCode == Keys.End) Value = Maximum; base.OnKeyDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
