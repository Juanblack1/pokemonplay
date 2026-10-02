using System.Drawing;
using System.Windows.Forms;

internal sealed class PokemonLegalityDialog : Form
{
    internal PokemonLegalityDialog(string pokemonName, PokemonLegalityResult result)
    {
        Text = "Análise de legalidade · PKHeX";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 440);
        Size = new Size(760, 560);
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;

        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(20, 12, 20, 4),
            Text = result.IsConsistent
                ? $"{pokemonName} · nenhuma inconsistência reportada pelo PKHeX"
                : $"{pokemonName} · {result.IssueCount} ponto(s) para revisar",
            ForeColor = result.IsConsistent ? AppTheme.Green : AppTheme.Text,
            Font = AppTheme.Section,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var report = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            TabStop = false,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = AppTheme.Surface,
            ForeColor = AppTheme.Text,
            Font = new Font("Consolas", 10f),
            Text = result.Report
        };
        var disclaimer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(20, 8, 20, 8),
            Text = "A análise é somente leitura. O relatório auxilia a revisão, mas não garante aceitação ou legalidade em jogos oficiais.",
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.Caption,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(report);
        Controls.Add(disclaimer);
        Controls.Add(heading);
    }
}
