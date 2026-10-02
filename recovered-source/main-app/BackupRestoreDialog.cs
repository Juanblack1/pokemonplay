using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class BackupArchiveChoice
{
    public string Path { get; }
    public DateTime Modified { get; }
    public long Size { get; }

    public BackupArchiveChoice(string path)
    {
        Path = path;
        Modified = File.GetLastWriteTime(path);
        Size = new FileInfo(path).Length;
    }

    public override string ToString()
    {
        string size = Size < 1024 * 1024 ? Math.Max(1, Size / 1024) + " KB" : (Size / (1024d * 1024d)).ToString("0.0") + " MB";
        string fileName = System.IO.Path.GetFileName(Path);
        string kind = fileName.Contains(" - ", StringComparison.Ordinal) ? "Backup manual" : "Backup automático";
        return DisplayFileName(fileName) + "   ·   " + Modified.ToString("dd/MM/yyyy HH:mm") + "   ·   " + size + "   ·   " + kind;
    }

    internal static string DisplayFileName(string fileName)
    {
        const int maxLength = 42;
        const int suffixLength = 18;
        if (string.IsNullOrEmpty(fileName) || fileName.Length <= maxLength) return fileName;
        int prefixLength = maxLength - suffixLength - 1;
        return fileName.Substring(0, prefixLength) + "…" + fileName.Substring(fileName.Length - suffixLength);
    }
}

internal sealed class BackupRestoreDialog : Form
{
    private readonly ListBox backups;
    private readonly ThemeButton restore;

    public string SelectedBackup => (backups.SelectedItem as BackupArchiveChoice)?.Path;
    public BackupArchiveChoice SelectedChoice => backups.SelectedItem as BackupArchiveChoice;

    internal static string BuildRestoreConfirmation(string profileName, BackupArchiveChoice choice)
    {
        if (choice == null) throw new ArgumentNullException(nameof(choice));
        return $"Restaurar {choice} no perfil {profileName}? Isso substituirá os arquivos locais. O estado atual será guardado em Backups/Automaticos antes da troca. Deseja continuar?";
    }

    public BackupRestoreDialog(string profileName, IReadOnlyList<BackupArchiveChoice> choices)
    {
        Text = "Restaurar backup";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(740, 490);
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;

        var title = new Label
        {
            Text = "Backups de " + profileName,
            Font = AppTheme.Section,
            ForeColor = AppTheme.Text,
            Location = new Point(24, 20),
            AutoSize = true
        };
        var hint = new Label
        {
            Text = "A lista mostra somente cópias deste perfil. O conteúdo será validado antes da restauração.",
            Font = AppTheme.Body,
            ForeColor = AppTheme.TextMuted,
            Location = new Point(24, 60),
            Size = new Size(690, 42)
        };
        backups = new ListBox
        {
            Location = new Point(24, 112),
            Size = new Size(692, 300),
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = AppTheme.Caption,
            HorizontalScrollbar = true,
            IntegralHeight = false
        };
        foreach (BackupArchiveChoice choice in choices)
            backups.Items.Add(choice);
        backups.SelectedIndexChanged += (_, _) => restore.Enabled = backups.SelectedIndex >= 0;

        var empty = new Label
        {
            Text = choices.Count == 0 ? "Ainda não há backups para este perfil. Crie um em Backup ZIP ou inicie o jogo para gerar uma cópia automática." : "",
            Font = AppTheme.Body,
            ForeColor = AppTheme.TextMuted,
            Location = new Point(32, 220),
            Size = new Size(670, 64),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = choices.Count == 0
        };
        restore = new ThemeButton("Restaurar selecionado", ButtonKind.Primary)
        {
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(8, 0, 0, 0)
        };
        restore.Click += (_, _) =>
        {
            if (SelectedBackup == null) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = new ThemeButton("Cancelar", ButtonKind.Secondary)
        {
            AutoSize = true,
            Margin = new Padding(8, 0, 0, 0)
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        var actions = new FlowLayoutPanel
        {
            Location = new Point(24, 430),
            Size = new Size(692, 44),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.Background
        };
        actions.Controls.Add(cancel);
        actions.Controls.Add(restore);
        AcceptButton = restore;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { title, hint, backups, empty, actions });
    }
}
