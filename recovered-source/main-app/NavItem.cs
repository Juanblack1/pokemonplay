using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class NavItem : BufferedPanel
{
	private readonly string caption;

	private readonly NavIcon icon;

	private bool hover;

	private bool active;

	public bool Active
	{
		get
		{
			return active;
		}
		set
		{
			active = value;
			Invalidate();
		}
	}

	public NavItem(string caption, NavIcon icon, EventHandler click)
	{
		this.caption = caption;
		this.icon = icon;
		Height = 44;
		Margin = new Padding(14, 3, 14, 3);
		Cursor = Cursors.Hand;
		TabStop = true;
		AccessibleRole = AccessibleRole.PushButton;
		AccessibleName = caption;
		Click += click;
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		Invalidate();
		base.OnGotFocus(e);
	}

	protected override void OnLostFocus(EventArgs e)
	{
		Invalidate();
		base.OnLostFocus(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
		{
			OnClick(EventArgs.Empty);
			e.Handled = true;
		}
		base.OnKeyDown(e);
	}

 protected override void OnPaint(PaintEventArgs e)
 {
  e.Graphics.SmoothingMode=SmoothingMode.None;var rect=new Rectangle(1,1,Width-3,Height-3);
  using(var brush=new SolidBrush(active?AppTheme.SurfaceSelected:hover?AppTheme.SurfaceHover:AppTheme.Surface))PaintTools.FillRounded(e.Graphics,brush,rect,4);
  using(var pen=new Pen(active||Focused?AppTheme.Focus:AppTheme.Border,active?2f:1f))PaintTools.DrawRounded(e.Graphics,pen,rect,4);
  PaintTools.DrawIcon(e.Graphics,icon,new Rectangle((Width-22)/2,7,22,22),active?AppTheme.Focus:AppTheme.TextSecondary);
  TextRenderer.DrawText(e.Graphics,caption,AppTheme.Section,new Rectangle(8,34,Width-16,26),active?AppTheme.Focus:AppTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
 }
}
