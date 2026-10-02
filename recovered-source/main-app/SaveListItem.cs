using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class SaveListItem : BufferedPanel
{
	private readonly GameInfo game;

	private readonly Image cover;

	private bool hover;

	public bool Selected { get; set; }

	public SaveListItem(GameInfo game, string root, EventHandler click)
	{
		this.game = game;
		Width = 192;
		Height = 64;
		Margin = new Padding(0, 0, 0, 6);
		Cursor = Cursors.Hand;
		TabStop = true;
		Click += click;
		cover = GameCoverService.Load(root, game.Cover);
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

	protected override void OnPaint(PaintEventArgs e)
	{
		Rectangle rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
		Color color;
		if (Selected)
		{
			color = AppTheme.SurfaceSelected;
		}
		else
		{
			color = (hover ? AppTheme.SurfaceRaised : Color.Transparent);
		}
		using (SolidBrush brush = new SolidBrush(color))
		{
			PaintTools.FillRounded(e.Graphics, brush, rectangle, 4);
		}
		if (Selected || hover)
		{
			using Pen pen = new Pen(Selected ? AppTheme.Focus : AppTheme.BorderSoft, 1f);
			PaintTools.DrawRounded(e.Graphics, pen, rectangle, 4);
		}
		if (cover != null)
		{
			e.Graphics.DrawImage(cover, PaintTools.FitImage(cover, new Rectangle(9, 7, 36, 42)));
		}
		TextRenderer.DrawText(e.Graphics, game.Title, AppTheme.BodyBold, new Rectangle(55, 9, Width - 65, 24), AppTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(e.Graphics, game.Subtitle.Replace("Game Boy Advance", "GBA").Replace("Nintendo DS", "DS").Replace("Nintendo 3DS", "3DS"), AppTheme.Caption, new Rectangle(55, 36, Width - 65, 22), AppTheme.TextMuted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && cover != null)
		{
			cover.Dispose();
		}
		base.Dispose(disposing);
	}
}
