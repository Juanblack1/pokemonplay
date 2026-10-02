using System.Drawing;
using System.Windows.Forms;

internal sealed class UpdateNoticeButton : ThemeButton
{
    internal bool Available {get;set;}
    internal UpdateNoticeButton():base("Atualizações",ButtonKind.Secondary){Padding=new Padding(36,0,16,0);AccessibleName="Verificar atualizações do aplicativo";}
    public override Size GetPreferredSize(Size proposedSize){var size=base.GetPreferredSize(proposedSize);return new Size(size.Width+20,size.Height);}
    protected override Rectangle TextBounds=>new Rectangle(36,3,System.Math.Max(1,Width-52),System.Math.Max(1,Height-6));
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var pen=new Pen(Available?AppTheme.Focus:AppTheme.TextSecondary,2);
        int x=20,y=Height/2;e.Graphics.DrawLine(pen,x,y-8,x,y+3);e.Graphics.DrawLines(pen,new[]{new Point(x-5,y-2),new Point(x,y+3),new Point(x+5,y-2)});e.Graphics.DrawLines(pen,new[]{new Point(x-7,y+3),new Point(x-7,y+8),new Point(x+7,y+8),new Point(x+7,y+3)});
    }
}
