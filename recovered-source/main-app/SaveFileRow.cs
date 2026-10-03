using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class SaveFileRow : BufferedPanel
{
	private readonly FileInfo file;

	public SaveFileRow(FileInfo file)
	{
		this.file = file;
		Height = 60;
		Margin = new Padding(0, 0, 0, 5);
		BackColor = Color.Transparent;
		AccessibleName = file.Name;
		AccessibleRole = AccessibleRole.ListItem;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Rectangle rectangle = new Rectangle(0, 0, Width - 1, Height - 1);
		using (SolidBrush brush = new SolidBrush(AppTheme.SurfaceRaised))
		{
			PaintTools.FillRounded(e.Graphics, brush, rectangle, 7);
		}
		PaintTools.DrawFileIcon(e.Graphics, new Rectangle(12, 11, 18, 21), AppTheme.BlueSoft);
		TextRenderer.DrawText(e.Graphics, file.Name, AppTheme.Body, new Rectangle(43, 8, Math.Max(80, Width - 192), 24), AppTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		string size="Indisponível",date="";
		bool available=false;
		try
		{
			file.Refresh();
			if(file.Exists){size=FormatSize(file.Length);date=file.LastWriteTime.ToString("dd/MM/yyyy HH:mm");available=true;}
		}
		catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
		AccessibleDescription=available?size+" · "+date:"Arquivo indisponível. Selecione o jogo novamente depois de restaurar o arquivo ou o acesso à pasta.";
		TextRenderer.DrawText(e.Graphics, size, AppTheme.Caption, new Rectangle(43, 34, Math.Max(95,Width-60), 20), available?AppTheme.TextMuted:AppTheme.Red, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
		TextRenderer.DrawText(e.Graphics, date, AppTheme.Caption, new Rectangle(Math.Max(145, Width - 135), 20, 128, 24), AppTheme.TextSecondary, TextFormatFlags.Right | TextFormatFlags.NoPadding);
	}

	private static string FormatSize(long bytes)
	{
		if (bytes >= 1048576)
		{
			return ((double)bytes / 1048576.0).ToString("0.0") + " MB";
		}
		if (bytes >= 1024)
		{
			return ((double)bytes / 1024.0).ToString("0") + " KB";
		}
		return bytes + " B";
	}
}
