using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
internal sealed class SectionCard : BufferedPanel
{
 public SectionCard(string title,string description)
 {
  BackColor=AppTheme.Surface;Padding=new Padding(24,88,24,24);SetStyle(ControlStyles.ResizeRedraw,true);
  var header=new Label{Text=title,Font=AppTheme.Section,ForeColor=AppTheme.Text,BackColor=AppTheme.Surface,AutoSize=false,AutoEllipsis=true,Location=new Point(24,20),Height=25,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Tag="card-header"};
  var hint=new Label{Text=description,Font=AppTheme.Caption,ForeColor=AppTheme.TextMuted,BackColor=AppTheme.Surface,AutoSize=false,AutoEllipsis=true,Location=new Point(24,50),Height=30,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Tag="card-header"};
  Controls.AddRange(new Control[]{header,hint}); Resize+=(_,_)=>{header.Width=hint.Width=System.Math.Max(1,Width-48);};
 }
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.None;using var pen=new Pen(AppTheme.BorderSoft);PaintTools.DrawRounded(e.Graphics,pen,new Rectangle(0,0,Width-1,Height-1),12);}
}
