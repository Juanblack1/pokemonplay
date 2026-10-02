using System;
using System.Drawing;
using System.Windows.Forms;
internal class PixelHeader : BufferedPanel
{
 private readonly string title;
 private string subtitle;
 public PixelHeader(string title,string subtitle,int height){this.title=title;this.subtitle=subtitle;Height=Math.Max(100,height);Dock=DockStyle.Top;BackColor=AppTheme.Surface;SetStyle(ControlStyles.ResizeRedraw,true);}
 protected void SetSubtitle(string value){subtitle=value??string.Empty;Invalidate();}
 protected override void OnPaint(PaintEventArgs e)
 {
  base.OnPaint(e);var g=e.Graphics;g.Clear(AppTheme.Surface);
  using(var grid=new Pen(Color.FromArgb(36,AppTheme.Border))){for(int x=0;x<Width;x+=32)g.DrawLine(grid,x,0,x,Height);for(int y=0;y<Height;y+=32)g.DrawLine(grid,0,y,Width,y);}
  using(var border=new Pen(AppTheme.Border))g.DrawLine(border,0,Height-1,Width,Height-1);
  PaintTools.DrawIcon(g,title.Contains("Configura")?NavIcon.Settings:title.Contains("Banco")?NavIcon.Bank:title.Contains("save",StringComparison.OrdinalIgnoreCase)?NavIcon.Saves:NavIcon.Library,new Rectangle(24,20,26,26),AppTheme.Focus);
  TextRenderer.DrawText(g,title,AppTheme.PageTitle,new Rectangle(66,10,Math.Max(1,Width-90),44),AppTheme.Text,TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
  TextRenderer.DrawText(g,subtitle,AppTheme.Body,new Rectangle(67,62,Math.Max(1,Width-90),25),AppTheme.TextSecondary,TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
 }
}
