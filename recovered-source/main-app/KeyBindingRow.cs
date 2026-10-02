using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class KeyBindingRow : BufferedPanel
{
	private readonly string action;

	private string key;

	public bool Editable { get; set; }

    private bool active;
    public bool Active { get => active; set { if(active != value){active=value;Invalidate();} } }

	public event EventHandler RowClick;

	public KeyBindingRow(string action, string key)
	{
		this.action = action;
		this.key = key;
		Height = 28;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = action;
	}

	public void SetKey(string value)
	{
		key = value;
		Invalidate();
	}

	protected override void OnClick(EventArgs e)
	{
		base.OnClick(e);
		if (Editable && RowClick != null)
		{
			RowClick(this, e);
		}
	}

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Editable && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) { RowClick?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }
	protected override void OnPaint(PaintEventArgs e)
	{
		TextRenderer.DrawText(e.Graphics, action, AppTheme.Body, new Rectangle(0, 0, Math.Max(120, Width - 100), Height), AppTheme.TextSecondary, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
		Rectangle rectangle = new Rectangle(Math.Max(120, Width - 92), 2, 86, Height - 4);
		using (SolidBrush brush = new SolidBrush(Active ? AppTheme.Blue : AppTheme.SurfaceRaised))
		{
			PaintTools.FillRounded(e.Graphics, brush, rectangle, 3);
		}
		using (Pen pen = new Pen(Focused && Editable ? AppTheme.Focus : AppTheme.Border, 1f))
		{
			PaintTools.DrawRounded(e.Graphics, pen, rectangle, 3);
		}
		TextRenderer.DrawText(e.Graphics, key, AppTheme.BodyBold, rectangle, AppTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
	}
}
