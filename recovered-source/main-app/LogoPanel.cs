using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class LogoPanel : BufferedPanel
{
	public LogoPanel()
	{
		Height = 106;
		BackColor = AppTheme.Sidebar;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.None;
		PaintTools.DrawPokeball(e.Graphics, new Rectangle(18, 20, 38, 38), AppTheme.Red, Color.FromArgb(5, 13, 31));
		TextRenderer.DrawText(e.Graphics, "POKEMONS", AppTheme.BodyBold, new Rectangle(68, 21, Width - 80, 18), AppTheme.Text, TextFormatFlags.NoPadding);
		TextRenderer.DrawText(e.Graphics, "PLAY", new Font("Segoe UI", 12f, FontStyle.Bold), new Rectangle(68, 42, Width - 80, 22), AppTheme.Gold, TextFormatFlags.NoPadding);
		TextRenderer.DrawText(e.Graphics, "BIBLIOTECA DE AVENTURAS", AppTheme.Caption, new Rectangle(20, 78, Width - 40, 16), AppTheme.TextMuted, TextFormatFlags.NoPadding);
	}
}
