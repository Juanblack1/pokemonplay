using System.Drawing;
using System.Windows.Forms;
internal sealed class PixelGridPanel : BufferedPanel
{
 public PixelGridPanel(){SetStyle(ControlStyles.ResizeRedraw,true);BackColor=AppTheme.Background;}
 protected override void OnPaintBackground(PaintEventArgs e)
 {
  e.Graphics.Clear(AppTheme.Background);using var grid=new Pen(Color.FromArgb(35,AppTheme.Border));
  for(int x=0;x<Width;x+=32)e.Graphics.DrawLine(grid,x,0,x,Height);
  for(int y=0;y<Height;y+=32)e.Graphics.DrawLine(grid,0,y,Width,y);
 }
}
