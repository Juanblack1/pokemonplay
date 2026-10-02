using System.Drawing;
using System.Windows.Forms;

internal sealed class SaveProfileDialog : Form
{
    private readonly TextBox name = new() { MaxLength = 40 };
    private readonly ThemeSelect mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    public string ProfileName => name.Text.Trim();
    public int Mode => mode.SelectedIndex;
    public SaveProfileDialog()
    {
        Text = "Novo perfil"; ClientSize = new Size(480, 340); BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text; Font = AppTheme.Body; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = MaximizeBox = false;
        var title = new Label { Text = "Nome da aventura", AutoSize = true, Location = new Point(24, 24) };
        name.SetBounds(24, 56, 432, 32); name.BackColor = AppTheme.SurfaceRaised; name.ForeColor = AppTheme.Text;
        var source = new Label { Text = "Como começar", AutoSize = true, Location = new Point(24, 110) };
        mode.SetBounds(24, 140, 432, 36); mode.Items.AddRange(new object[] { "Aventura nova", "Copiar progresso do perfil ativo", "Importar arquivo de save" }); mode.SelectedIndex = 0;
        var help = new Label { Text = "Cada perfil guarda seu próprio progresso. Na aventura nova, salve dentro do jogo para criar o primeiro arquivo.", Location = new Point(24, 192), Size = new Size(432, 64), ForeColor = AppTheme.TextMuted };
        var cancel = new ThemeButton("Cancelar", ButtonKind.Secondary) { DialogResult = DialogResult.Cancel, Location = new Point(220, 282), Width = 104 };
        var create = new ThemeButton("Criar perfil", ButtonKind.Primary) { DialogResult = DialogResult.OK, Location = new Point(336, 282), Width = 120 };
        Controls.AddRange(new Control[] { title, name, source, mode, help, cancel, create }); AcceptButton = create; CancelButton = cancel;
    }
}

internal sealed class RenameSaveProfileDialog : Form
{
    private readonly TextBox name = new() { MaxLength = 40 };
    public string ProfileName => name.Text.Trim();

    public RenameSaveProfileDialog(string currentName)
    {
        Text = "Renomear perfil"; ClientSize = new Size(440, 210); BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text; Font = AppTheme.Body; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = MaximizeBox = false;
        var label = new Label { Text = "Nome da aventura", AutoSize = true, Location = new Point(24, 24) };
        name.SetBounds(24, 58, 392, 34); name.Text = currentName; name.BackColor = AppTheme.SurfaceRaised; name.ForeColor = AppTheme.Text;
        var help = new Label { Text = "O progresso salvo e a identificação da nuvem permanecem iguais.", AutoSize = true, Location = new Point(24, 108), ForeColor = AppTheme.TextMuted };
        var cancel = new ThemeButton("Cancelar", ButtonKind.Secondary) { DialogResult = DialogResult.Cancel, Location = new Point(196, 154), Width = 104 };
        var save = new ThemeButton("Salvar nome", ButtonKind.Primary) { DialogResult = DialogResult.OK, Location = new Point(312, 154), Width = 104 };
        Controls.AddRange(new Control[] { label, name, help, cancel, save }); AcceptButton = save; CancelButton = cancel;
        Shown += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
