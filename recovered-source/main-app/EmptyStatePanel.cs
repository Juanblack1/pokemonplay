using System;
using System.Drawing;
using System.Windows.Forms;
internal sealed class EmptyStatePanel : BufferedPanel
{
 private readonly Label titleLabel, descriptionLabel;
 private readonly ThemeButton action;
 public EmptyStatePanel(string title,string description,string actionText,EventHandler actionClick)
 {
  Width=700;Height=184;BackColor=AppTheme.Surface;Margin=new Padding(0,0,0,16);
  titleLabel=new Label{Text=title,Font=AppTheme.Section,ForeColor=AppTheme.Text,AutoSize=false,AutoEllipsis=true,Location=new Point(24,24),Height=28};
  descriptionLabel=new Label{Text=description,Font=AppTheme.Body,ForeColor=AppTheme.TextMuted,AutoSize=false,Location=new Point(24,62),Height=52};
  action=new ThemeButton(actionText,ButtonKind.Secondary){AutoSize=true,Location=new Point(24,126)};action.Click+=actionClick;
  Controls.AddRange(new Control[]{titleLabel,descriptionLabel,action});Resize+=(_,_)=>LayoutContents();LayoutContents();
 }
 private void LayoutContents(){titleLabel.Width=descriptionLabel.Width=Math.Max(1,Width-48);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;using var pen=new Pen(AppTheme.BorderSoft);PaintTools.DrawRounded(e.Graphics,pen,new Rectangle(0,0,Width-1,Height-1),12);}
}
