using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed class RomImportDialog : Form
{
	private static readonly (string Name, int Generation)[] Bases =
	{
		("FireRed", 3), ("LeafGreen", 3), ("Emerald", 3), ("Ruby", 3), ("Sapphire", 3), ("HeartGold", 4), ("SoulSilver", 4),
		("Platinum", 4), ("Diamond", 4), ("Pearl", 4), ("Black", 5), ("White", 5), ("Black 2", 5), ("White 2", 5),
		("X", 6), ("Y", 6), ("Omega Ruby", 6), ("Alpha Sapphire", 6), ("Sun", 6), ("Moon", 6), ("Ultra Sun", 6), ("Ultra Moon", 6)
	};
	private readonly TextBox titleInput;
	private readonly ComboBox basePicker;
	private readonly CheckBox confirmContent;
	private readonly CheckBox hackCheck;
	private readonly RomIdentity identity;
	private readonly string romPath;
	private readonly ImportedPokemonGame existing;

	internal ImportedPokemonGame Result { get; private set; }

	internal RomImportDialog(string romPath, RomIdentity identity, ImportedPokemonGame existing)
	{
		this.romPath = Path.GetFullPath(romPath);
		this.identity = identity;
		this.existing = existing;
		Text = existing == null ? "Adicionar jogo Pokémon" : "Editar jogo local";
		ClientSize = new Size(520, 430);
		BackColor = AppTheme.Background;
		ForeColor = AppTheme.Text;
		Font = AppTheme.Body;
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MinimizeBox = MaximizeBox = false;

		var heading = new Label { Text = "Identifique esta ROM", Location = new Point(24, 22), Size = new Size(460, 32), Font = AppTheme.BodyBold, ForeColor = AppTheme.Text };
		string extension = Path.GetExtension(this.romPath);
		bool requiresConfirmation = !identity.RecognizedPokemon || IsThreeDs(extension);
		var fileInfo = new Label { Text = Path.GetFileName(this.romPath) + "\n" + (identity.RecognizedPokemon && !IsThreeDs(extension) ? "Cabeçalho Pokémon reconhecido" : "Confirme o conteúdo deste arquivo"), Location = new Point(24, 60), Size = new Size(460, 38), ForeColor = identity.RecognizedPokemon && !IsThreeDs(extension) ? AppTheme.TextSecondary : AppTheme.TextMuted, AutoEllipsis = true };
		confirmContent = new CheckBox { Text = "Confirmo que este arquivo contém um jogo Pokémon", Location = new Point(24, 101), Size = new Size(460, 26), AutoSize = false, Checked = !requiresConfirmation, ForeColor = AppTheme.Text };
		var titleLabel = new Label { Text = "Nome na biblioteca", Location = new Point(24, 136), Size = new Size(460, 22), ForeColor = AppTheme.TextSecondary };
		titleInput = new TextBox { Location = new Point(24, 160), Size = new Size(460, 32), Text = existing?.Title ?? GuessTitle(this.romPath, identity) };
		var baseLabel = new Label { Text = "Jogo-base", Location = new Point(24, 205), Size = new Size(460, 22), ForeColor = AppTheme.TextSecondary };
		basePicker = new ComboBox { Location = new Point(24, 229), Size = new Size(460, 34), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = AppTheme.Surface, ForeColor = AppTheme.Text, Font = AppTheme.Body };
		foreach (var item in Bases.Where(item => MatchesSystem(extension, item.Generation))) basePicker.Items.Add(item.Name);
		string selectedBase = existing?.BaseGame ?? identity.BaseGame;
		int selectedIndex = basePicker.Items.IndexOf(selectedBase);
		basePicker.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
		titleInput.MaxLength = 80;
		titleInput.AccessibleName = "Nome do jogo na biblioteca";
		basePicker.AccessibleName = "Jogo-base compatível com o sistema da ROM";
		basePicker.AccessibleDescription = "Mostra somente jogos do mesmo console que o arquivo selecionado.";
		hackCheck = new CheckBox { Text = "Esta é uma hack ROM de Pokémon", Location = new Point(24, 274), Size = new Size(460, 28), AutoSize = false, Checked = existing?.IsHackRom ?? false, ForeColor = AppTheme.Text };
		var helper = new Label { Text = "Marque para exibir o selo HACK ROM e o jogo-base no cartão. A capa usa a do jogo-base como referência. O arquivo não é copiado.", Location = new Point(24, 306), Size = new Size(460, 42), ForeColor = AppTheme.TextMuted };
		var cancel = new ThemeButton("Cancelar", ButtonKind.Secondary) { Location = new Point(272, 376), Size = new Size(100, 36), AutoSize = false, DialogResult = DialogResult.Cancel };
		var add = new ThemeButton(existing == null ? "Adicionar" : "Salvar", ButtonKind.Primary) { Location = new Point(384, 376), Size = new Size(100, 36), AutoSize = false };
		add.Click += (_, _) => SaveResult();
		Controls.AddRange(new Control[] { heading, fileInfo, confirmContent, titleLabel, titleInput, baseLabel, basePicker, hackCheck, helper, cancel, add });
		AcceptButton = add;
		CancelButton = cancel;
	}

	private void SaveResult()
	{
		string title = titleInput.Text.Trim();
		if (title.Length is < 1 or > 80 || title.IndexOfAny(new[] { '/', '\\' }) >= 0 || title.Any(char.IsControl))
		{
			MessageBox.Show(this, "Use um nome entre 1 e 80 caracteres, sem barras ou controles.", "Nome inválido", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (!confirmContent.Checked)
		{
			MessageBox.Show(this, "Confirme que o arquivo contém um jogo Pokémon antes de adicioná-lo à biblioteca.", "Confirme o conteúdo", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		string baseName = basePicker.SelectedItem?.ToString();
		int generation = Bases.First(item => item.Name == baseName).Generation;
		string extension = Path.GetExtension(romPath);
		if ((generation == 3 && !extension.Equals(".gba", StringComparison.OrdinalIgnoreCase)) ||
			(generation is 4 or 5 && !extension.Equals(".nds", StringComparison.OrdinalIgnoreCase)) ||
			(generation == 6 && !IsThreeDs(extension)) || (generation < 6 && IsThreeDs(extension)))
		{
			MessageBox.Show(this, "O sistema da ROM não corresponde ao jogo-base selecionado. Escolha o jogo-base correto.", "Sistema incompatível", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		Result = new ImportedPokemonGame
		{
			Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
			RomPath = romPath,
			Title = title,
			BaseGame = baseName,
			Generation = generation,
			IsHackRom = hackCheck.Checked
		};
		DialogResult = DialogResult.OK;
		Close();
	}

	private static string GuessTitle(string path, RomIdentity identity)
	{
		string fromFile = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Trim();
		if (fromFile.Length == 0) fromFile = identity.HeaderTitle.Trim();
		if (fromFile.Length > 80)
		{
			fromFile = fromFile[..80];
			if (char.IsHighSurrogate(fromFile[^1])) fromFile = fromFile[..^1];
		}
		return fromFile;
	}

	private static bool MatchesSystem(string extension, int generation) => generation == 3
		? extension.Equals(".gba", StringComparison.OrdinalIgnoreCase)
		: generation is 4 or 5 ? extension.Equals(".nds", StringComparison.OrdinalIgnoreCase) : IsThreeDs(extension);

	private static bool IsThreeDs(string extension) => new[] { ".3ds", ".cci", ".cxi", ".3dsx", ".zcci" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
}
