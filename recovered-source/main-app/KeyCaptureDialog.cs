using System.Drawing;
using System.Windows.Forms;

internal sealed class KeyCaptureDialog : Form
{
	public string CapturedKey { get; private set; }

	public KeyCaptureDialog(string action)
	{
		Text = "Personalizar controle";
		ClientSize = new Size(460, 196);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

		StartPosition = FormStartPosition.CenterParent;
		BackColor = AppTheme.Surface;
		ForeColor = AppTheme.Text;
		KeyPreview = true;
		Label value = new Label
		{
			Text = "Pressione a tecla para: " + action + "\n(Esc cancela)",
			Font = AppTheme.Body,
			ForeColor = AppTheme.Text,
			AutoSize = false,
            Size = new Size(412, 140),
			Location = new Point(22, 22)
		};
		Controls.Add(value);
		KeyEventHandler value2 = (object sender, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Escape)
			{
				DialogResult = DialogResult.Cancel;
				Close();
			}
			else if(e.KeyCode != Keys.F12 && e.KeyCode != Keys.None)
			{
				CapturedKey = DisplayKey(e.KeyCode);
				DialogResult = DialogResult.OK;
				Close();
			}
		};
		KeyDown += value2;
	}

	private static string DisplayKey(Keys key)
	{
		return key switch
		{
			Keys.Up => "Up",
			Keys.Down => "Down",
			Keys.Left => "Left",
			Keys.Right => "Right",
			Keys.Return => "Enter",
			Keys.Back => "Backspace",
			Keys.ShiftKey => "Shift",
			Keys.ControlKey => "Ctrl",
			Keys.Menu => "Alt",
			_ => key >= Keys.D0 && key <= Keys.D9 ? ((int)key-(int)Keys.D0).ToString() : key.ToString(),
		};
	}
}
