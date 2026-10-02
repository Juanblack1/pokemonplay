using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

internal sealed class ImportedGameManagerDialog : Form
{
    private readonly string root;
    private readonly ListBox gameList;
    private readonly TextBox details;
    private readonly Label status;
    private readonly ThemeButton locateButton;
    private readonly ToolTip statusHint = new();
    internal bool Changed { get; private set; }

    internal ImportedGameManagerDialog(string root)
    {
        this.root = root;
        Text = "Gerenciar jogos importados";
        ClientSize = new Size(700, 500);
        MinimumSize = new Size(620, 480);
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;
        Font = AppTheme.Body;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (float height in new[] { 36f, 48f }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (float height in new[] { 92f, 48f, 48f }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var heading = new Label { Text = "Jogos importados", Font = AppTheme.Section, ForeColor = AppTheme.Text, Dock = DockStyle.Fill };
        var help = new Label { Text = "Sua ROM mudou de pasta? Localize o mesmo arquivo para manter o jogo e seus saves associados. Nenhum arquivo será copiado.", ForeColor = AppTheme.TextSecondary, Dock = DockStyle.Fill };
        gameList = new ListBox { Dock = DockStyle.Fill, BackColor = AppTheme.Surface, ForeColor = AppTheme.Text, Font = AppTheme.Body, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = "Jogos importados e disponibilidade dos arquivos" };
        details = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = AppTheme.Surface, ForeColor = AppTheme.TextSecondary, AccessibleName = "Sistema e caminho da ROM selecionada" };
        status = new Label { Dock = DockStyle.Fill, ForeColor = AppTheme.TextSecondary, AccessibleName = "Resultado do gerenciamento de ROMs", AutoEllipsis = true };
        locateButton = new ThemeButton("Localizar ROM", ButtonKind.Primary) { AccessibleName = "Localizar novamente a ROM do jogo selecionado", Enabled = false };
        locateButton.Click += (_, _) => ChooseReplacement();
        var close = new ThemeButton("Fechar", ButtonKind.Secondary) { DialogResult = DialogResult.Cancel };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.AddRange(new Control[] { locateButton, close });
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(help, 0, 1);
        layout.Controls.Add(gameList, 0, 2);
        layout.Controls.Add(details, 0, 3);
        layout.Controls.Add(status, 0, 4);
        layout.Controls.Add(actions, 0, 5);
        Controls.Add(layout);
        CancelButton = close;
        Disposed += (_, _) => statusHint.Dispose();
        gameList.SelectedIndexChanged += (_, _) => UpdateSelection();
        LoadGames(null);
    }

    private void LoadGames(string selectedId)
    {
        gameList.Items.Clear();
        try
        {
            foreach (var entry in ImportedGameCatalog.Entries(root).OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase))
                gameList.Items.Add(new GameChoice(entry));
            int index = gameList.Items.Cast<GameChoice>().ToList().FindIndex(choice => choice.Entry.Id == selectedId);
            if (gameList.Items.Count > 0) gameList.SelectedIndex = index < 0 ? 0 : index;
            SetStatus(gameList.Items.Count == 0 ? "Nenhum jogo importado. Use Adicionar jogos para selecionar uma ROM ou pasta." : "Escolha o mesmo nome de arquivo no novo local para preservar a associação com o save.");
        }
        catch (Exception ex) when (IsCatalogError(ex))
        {
            SetStatus("Não foi possível ler o catálogo. Confira Settings/ImportedPokemonGames.json. Os arquivos foram preservados. " + ex.Message, true);
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var choice = gameList.SelectedItem as GameChoice;
        locateButton.Enabled = choice != null;
        details.Text = choice == null ? "Selecione um jogo para ver o caminho da ROM." :
            choice.Entry.Title + " · " + (choice.Entry.Generation == 3 ? "GBA" : choice.Entry.Generation >= 6 ? "Nintendo 3DS" : "Nintendo DS") +
            (choice.Entry.IsHackRom ? " · Hack de " : " · ") + choice.Entry.BaseGame + Environment.NewLine + choice.Entry.RomPath;
    }

    private void ChooseReplacement()
    {
        if (gameList.SelectedItem is not GameChoice choice) return;
        string extension = Path.GetExtension(choice.Entry.RomPath);
        using var picker = new OpenFileDialog { Title = "Localizar " + Path.GetFileName(choice.Entry.RomPath), Filter = "ROM do mesmo sistema|*" + extension, FileName = Path.GetFileName(choice.Entry.RomPath), CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, "Este arquivo é a mesma ROM de " + choice.Entry.Title + "?\n\n" + picker.FileName + "\n\nO jogo manterá sua pasta de saves. O arquivo não será copiado.", "Confirmar ROM localizada", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        RecoverSelected(picker.FileName);
    }

    internal void RecoverSelected(string path)
    {
        if (gameList.SelectedItem is not GameChoice choice) return;
        try
        {
            ImportedGameCatalog.Relocate(root, choice.Entry.Id, path);
            Changed = true;
            LoadGames(choice.Entry.Id);
            SetStatus("ROM localizada. Jogo e pasta de saves mantidos; nenhum arquivo foi copiado.");
        }
        catch (Exception ex) when (IsCatalogError(ex))
        {
            SetStatus("Não foi possível localizar a ROM. " + ex.Message, true);
        }
    }

    private static bool IsCatalogError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or System.Security.SecurityException;

    private void SetStatus(string text, bool error = false)
    {
        status.Text = status.AccessibleDescription = text;
        status.ForeColor = error ? AppTheme.Red : AppTheme.TextSecondary;
        statusHint.SetToolTip(status, text);
    }

    private sealed record GameChoice(ImportedPokemonGame Entry)
    {
        public override string ToString() => Entry.Title + " · " + (File.Exists(Entry.RomPath) ? "Disponível" : "Não encontrado");
    }
}
