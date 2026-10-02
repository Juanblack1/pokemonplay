using System.Windows.Forms;



internal class BufferedPanel : Panel

{

	public BufferedPanel()

	{

		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, value: true);

		UpdateStyles();

	}

}
