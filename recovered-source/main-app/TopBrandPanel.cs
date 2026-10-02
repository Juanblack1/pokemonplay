using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class TopBrandPanel : BufferedPanel
{
	public TopBrandPanel()
	{
		Width = 220;
		Height = 58;
		BackColor = AppTheme.Sidebar;
	}

 protected override void OnPaint(PaintEventArgs e)
 {
  e.Graphics.SmoothingMode=SmoothingMode.None;PaintTools.DrawPokeball(e.Graphics,new Rectangle(20,18,30,30),AppTheme.Blue,AppTheme.Sidebar);
  TextRenderer.DrawText(e.Graphics,"Pokemons Play",AppTheme.BodyBold,new Rectangle(62,15,140,25),AppTheme.Text,TextFormatFlags.NoPadding);
  TextRenderer.DrawText(e.Graphics,"Sua coleção Pokémon",AppTheme.Caption,new Rectangle(62,40,140,20),AppTheme.TextMuted,TextFormatFlags.NoPadding);
 }
}
