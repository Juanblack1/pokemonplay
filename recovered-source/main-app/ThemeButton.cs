using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
internal class ThemeButton : Button
{
 private bool hover, pressed, selected;
 public ButtonKind Kind {get;set;}
 public bool Selected {get=>selected; set{selected=value;Invalidate();}}
 public ThemeButton(string text,ButtonKind kind)
 {
  Text=text; Kind=kind; Font=AppTheme.BodyBold; ForeColor=AppTheme.Text; BackColor=Color.Transparent; FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0;
  Padding=new Padding(16,0,16,0); Height=AppTheme.ControlHeight; Width=GetPreferredSize(Size.Empty).Width; TabStop=true; Cursor=Cursors.Hand; AccessibleRole=AccessibleRole.PushButton; AccessibleName=text;
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
 }
 public override Size GetPreferredSize(Size proposedSize) {var text=TextRenderer.MeasureText(Text??"",Font??AppTheme.BodyBold,Size.Empty,TextFormatFlags.NoPadding);return new Size(Math.Max(80,text.Width+32),Math.Max(AppTheme.ControlHeight,text.Height+18));}
 protected virtual Rectangle TextBounds=>new Rectangle(12,3,Math.Max(1,Width-24),Math.Max(1,Height-6));
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e)
 {
  e.Graphics.SmoothingMode=SmoothingMode.None;var rect=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));
  Color fill=Kind==ButtonKind.Primary?AppTheme.Blue:Kind==ButtonKind.Danger?Color.FromArgb(127,29,29):Kind==ButtonKind.Ghost?AppTheme.Background:AppTheme.Surface;
  Color border=Kind==ButtonKind.Ghost?fill:Kind==ButtonKind.Primary?AppTheme.Focus:AppTheme.Border;
  if(Selected){fill=AppTheme.SurfaceRaised;border=AppTheme.Border;}
  if(Enabled&&hover)fill=Kind==ButtonKind.Primary?Color.FromArgb(59,122,232):AppTheme.SurfaceHover;
  if(Enabled&&pressed)fill=Kind==ButtonKind.Primary?Color.FromArgb(35,74,171):AppTheme.SurfaceRaised;
  if(!Enabled){fill=AppTheme.SurfaceRaised;border=AppTheme.BorderSoft;}
  using(var brush=new SolidBrush(fill))PaintTools.FillRounded(e.Graphics,brush,rect,AppTheme.Radius);
  using(var pen=new Pen(Focused?AppTheme.Focus:border,Focused?2f:1f))PaintTools.DrawRounded(e.Graphics,pen,rect,AppTheme.Radius);
  var textRect=TextBounds;
  TextRenderer.DrawText(e.Graphics,Text,Font,textRect,Enabled?ForeColor:AppTheme.TextMuted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
 }
}
