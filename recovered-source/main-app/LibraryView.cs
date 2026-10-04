using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed class LibraryView : BufferedPanel
{
	private readonly string root;

	private readonly LauncherForm host;

	private readonly List<GameInfo> games;
    private ImportedGameAvailability importedAvailability;

	private readonly ThemeInput search;
    private readonly Timer searchDelay = new(){Interval=150};
    private bool searchPending;

	private readonly ThemeSelect systemFilter;
	private readonly ThemeButton favoriteFilter = new("☆ Favoritos", ButtonKind.Secondary);
	private readonly ThemeButton refreshButton = new("Atualizar", ButtonKind.Secondary);
	private readonly ThemeButton importButton = new("Adicionar jogos", ButtonKind.Primary);

	private ThemeButton clearRecentButton;
	private HeroBanner heroBanner;

	private readonly Panel chips;

	private Panel scroll;

	private FlowLayoutPanel content;

	private Label sortHelper;

	private Action layoutToolbar;

	private readonly List<ChipButton> chipButtons = new List<ChipButton>();

	private int generationFilter;
	private bool favoritesOnly;
	private bool recentOnly;
	private bool alphabeticalOnly;
    private bool mostPlayedOnly;

	public LibraryView(LauncherForm host, string root)
	{
		this.host = host;
		this.root = root;
		games = GameCatalog.Build(root);
        importedAvailability=ImportedGameAvailability.Inspect(root);
		Dock = DockStyle.Fill;
		BackColor = Color.Transparent;
		Controls.Add(CreateContentArea());
		chips = new BufferedPanel
		{
			Dock = DockStyle.Top,
			Height = 48,
			BackColor = Color.Transparent
		};
		AddChip("Todos", 0);
		AddChip("Geração 3", 3);
		AddChip("Geração 4", 4);
		AddChip("Geração 5", 5);
		AddChip("Geração 6", 6);
		AddChip("A–Z", -2);
		AddChip("Recentes", -1);
        AddChip("Mais jogados", -3);
		refreshButton.AutoSize = false;
		refreshButton.AccessibleName = "Atualizar a biblioteca · F5";
		refreshButton.AccessibleDescription = "Redescobre jogos e capas disponíveis nas pastas locais.";
		refreshButton.Click += (_, _) => RefreshCatalog();
		chips.Controls.Add(refreshButton);
		var importMenu = new ContextMenuStrip();
		importMenu.Items.Add("Selecionar arquivo(s) de ROM…", null, (_, _) => SelectRomFiles());
		importMenu.Items.Add("Examinar uma pasta…", null, (_, _) => SelectRomFolder());
		importMenu.Items.Add("Gerenciar jogos importados…", null, (_, _) => ManageImportedGames());
		importButton.ContextMenuStrip = importMenu;
		importButton.Tag = "rom-import";
		importButton.AccessibleName = "Adicionar jogos Pokémon de arquivos ou de uma pasta";
		importButton.AccessibleDescription = "Adiciona ROMs GBA, Nintendo DS e Nintendo 3DS. O aplicativo não copia os arquivos.";
		importButton.Click += (_, _) => importMenu.Show(importButton, new Point(0, importButton.Height));
		chips.Controls.Add(importButton);
		Controls.Add(chips);
		Controls.Add(CreateToolbar(out search, out systemFilter));
		heroBanner = new HeroBanner(games.Count);
		Controls.Add(heroBanner);
		Panel panel = chips;
		EventHandler value = (object param0, EventArgs param1) =>
		{
			LayoutGenerationChips();
		};
		panel.Resize += value;
        searchDelay.Tick+=(_,_)=>Rebuild();
        search.TextChanged+=(_,_)=>ScheduleSearch();
		systemFilter.SelectedIndexChanged += (object param0, EventArgs param1) =>
		{
			Rebuild();
		};
		favoriteFilter.Click += (_, _) =>
		{
			favoritesOnly = !favoritesOnly;
			UpdateFavoriteFilter();
			Rebuild();
		};
		LayoutGenerationChips();
		Rebuild();
		VisibleChanged += (_, _) =>
		{
            if(!Visible)searchDelay.Stop();
			if (Visible && (searchPending || recentOnly || mostPlayedOnly) && IsHandleCreated)
				BeginInvoke((MethodInvoker)(()=>{if(!IsDisposed&&!Disposing)Rebuild();}));
		};
	}
    private void ScheduleSearch()
    {
        if(!IsHandleCreated||!search.ContainsFocus||string.IsNullOrWhiteSpace(search.Text)){Rebuild();return;}
        searchPending=true;searchDelay.Stop();if(Visible)searchDelay.Start();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing)searchDelay.Dispose();
        base.Dispose(disposing);
    }

	private Panel CreateContentArea()
	{
		scroll = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Color.Transparent
		};
		content = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			Padding = new Padding(24, 18, 24, 32),
			BackColor = Color.Transparent
		};
		scroll.Controls.Add(content);
		scroll.Resize += (object param0, EventArgs param1) =>
		{
			UpdateContentWidth();
		};
		return scroll;
	}

	private Control CreateToolbar(out ThemeInput searchBox, out ThemeSelect filter)
	{
		BufferedPanel toolbar = new BufferedPanel
		{
			Dock = DockStyle.Top,
			Height = 60,
			BackColor = Color.Transparent
		};
        ThemeInput box = new ThemeInput{Location=new Point(24,8),Width=330,Height=40};
        box.SetAccessibleMetadata("Buscar jogos", "Combine palavras do título e plataforma, como Ruby GBA. Ignora diferenças de acentuação. Ctrl+F ou Ctrl+E foca a busca; Esc limpa.");
		searchBox = box;
		ThemeSelect comboBox = new ThemeSelect();
		comboBox.Location = new Point(370, 8);
		comboBox.Height = 40;
		comboBox.Width = 230;
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.FlatStyle = FlatStyle.Flat;
		comboBox.BackColor = AppTheme.Surface;
		comboBox.ForeColor = AppTheme.Text;
		comboBox.Font = AppTheme.Body;
		ThemeSelect comboBox2 = comboBox;
		comboBox2.Items.AddRange(new object[4] { "Todos os sistemas", "Game Boy Advance", "Nintendo DS", "Nintendo 3DS" });
		comboBox2.SelectedIndex = 0;
		comboBox2.AccessibleContext = "Filtrar por sistema";
		comboBox2.AccessibleDescription = "Combina com a busca, os favoritos e a ordenação da Biblioteca.";
		filter = comboBox2;
		ThemeInput searchControl = searchBox;
		ThemeSelect filterControl = filter;
		favoriteFilter.Location = new Point(612, 8);
		favoriteFilter.Size = new Size(154, 40);
		favoriteFilter.AutoSize = false;
		favoriteFilter.AccessibleName = "Mostrar somente jogos favoritos";
		clearRecentButton = new ThemeButton("Limpar recentes", ButtonKind.Secondary)
		{
			Location = new Point(774, 8),
			Size = new Size(160, 40),
			AutoSize = false,
			Visible = false,
			AccessibleName = "Limpar histórico local de jogos recentes"
		};
		clearRecentButton.Click += (_, _) =>
		{
			if (MessageBox.Show(this, "Remover o histórico local dos jogos? Isso também apaga as datas de acesso e os tempos totais jogados.", "Limpar histórico", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				return;
			if (GameLaunchHistoryService.Clear(root))
				Rebuild();
			else
				MessageBox.Show(this, "Não foi possível limpar o histórico local.", "Limpar histórico", MessageBoxButtons.OK, MessageBoxIcon.Error);
		};
		Label helper = new Label
		{
			Text = "Ordenados por geração de lançamento",
			ForeColor = AppTheme.TextMuted,
			Font = AppTheme.Caption,
			AutoSize = true,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right)
		};
		sortHelper = helper;
		toolbar.Controls.Add(searchBox);
		toolbar.Controls.Add(filter);
		toolbar.Controls.Add(favoriteFilter);
		toolbar.Controls.Add(clearRecentButton);
		toolbar.Controls.Add(helper);
		void LayoutToolbar()
		{
			if (toolbar.ClientSize.Width < 1000)
			{
				int searchWidth = 190 + Math.Min(138, Math.Max(0, toolbar.ClientSize.Width - 762));
				searchControl.SetBounds(24, 8, searchWidth, 40);
				filterControl.SetBounds(searchControl.Right + 8, 8, 172, 40);
				favoriteFilter.SetBounds(filterControl.Right + 8, 8, 132, 40);
				clearRecentButton.SetBounds(favoriteFilter.Right + 8, 8, 160, 40);
			}
		else
			{
				searchControl.SetBounds(24, 8, 330, 40);
				filterControl.SetBounds(370, 8, 230, 40);
				favoriteFilter.SetBounds(612, 8, 154, 40);
				clearRecentButton.SetBounds(774, 8, 160, 40);
			}
			helper.Left = toolbar.ClientSize.Width - helper.Width - 26;
			helper.Top = 22;
			int lastActionRight = clearRecentButton.Visible ? clearRecentButton.Right : favoriteFilter.Right;
			helper.Visible = toolbar.ClientSize.Width >= 1100 && helper.Left >= lastActionRight + 16;
		}
		layoutToolbar = LayoutToolbar;
		toolbar.Resize += (object param0, EventArgs param1) => LayoutToolbar();
		LayoutToolbar();
		return toolbar;
	}

	private void AddChip(string text, int generation)
	{
		ChipButton chip = new ChipButton(text);
		chip.Selected = generation == 0;
		chip.Margin = Padding.Empty;
		chip.Click += (object param0, EventArgs param1) =>
		{
			recentOnly = generation == -1;
			alphabeticalOnly = generation == -2;
			mostPlayedOnly = generation == -3;
			generationFilter = generation > 0 ? generation : 0;
			if (sortHelper != null)
			{
				sortHelper.Text = recentOnly ? "Ordenados pela abertura mais recente" : alphabeticalOnly ? "Ordenados de A a Z" : mostPlayedOnly ? "Ordenados pelo tempo total jogado" : "Ordenados por geração de lançamento";
				layoutToolbar?.Invoke();
			}
			foreach (ChipButton chipButton in chipButtons)
			{
				chipButton.Selected = chipButton == chip;
			}
			Rebuild();
		};
		chipButtons.Add(chip);
		chips.Controls.Add(chip);
	}

	private void LayoutGenerationChips()
	{
		if (chips == null || chipButtons.Count == 0)
		{
			return;
		}
		const int spacing = 8;
		const int refreshWidth = 120;
		const int importWidth = 150;
		const int minimumChipWidth = 92;
		bool showImport = chips.ClientSize.Width >= 762;
		bool showRefresh = chips.ClientSize.Width >= 762;
		bool actionsOnOwnRow = chips.ClientSize.Width < 1000 && (showImport || showRefresh);
		importButton.Visible = showImport;
		refreshButton.Visible = showRefresh;
		int left = 24;
		int reservedActions = (showImport ? importWidth + 8 : 0) + (showRefresh ? refreshWidth + 16 : 0);
		int rightLimit = Math.Max(left + minimumChipWidth, chips.ClientSize.Width - 24 - (actionsOnOwnRow ? 0 : reservedActions));
		int firstChipTop = actionsOnOwnRow ? 50 : 9;
		int row = 0;
		int rows = 1;
		foreach (ChipButton chip in chipButtons)
		{
			int chipWidth = Math.Max(minimumChipWidth, chip.GetPreferredSize(Size.Empty).Width);
			if (left > 24 && left + chipWidth > rightLimit)
			{
				row++;
				rows++;
				left = 24;
			}
			chip.SetBounds(left, firstChipTop + row * (36 + spacing), chipWidth, 36);
			left += chipWidth + spacing;
		}
		int height = firstChipTop + rows * 36 + (rows - 1) * spacing + 3;
		if (chips.Height != height)
			chips.Height = height;
		if (showRefresh)
			refreshButton.SetBounds(chips.ClientSize.Width - refreshWidth - 24, 6, refreshWidth, 36);
		if (showImport)
			importButton.SetBounds(chips.ClientSize.Width - 24 - (showRefresh ? refreshWidth + 8 : 0) - importWidth, 6, importWidth, 36);
	}

	private void ManageImportedGames()
	{
		using var dialog = new ImportedGameManagerDialog(root);
		dialog.ShowDialog(this);
		if (dialog.Changed) RefreshCatalog();
	}

	private void SelectRomFiles()
	{
		using var dialog = new OpenFileDialog
		{
			Title = "Adicionar ROMs de Pokémon",
			Filter = "ROMs Pokémon compatíveis|*.gba;*.nds;*.3ds;*.cci;*.cxi;*.3dsx;*.zcci|Game Boy Advance|*.gba|Nintendo DS|*.nds|Nintendo 3DS|*.3ds;*.cci;*.cxi;*.3dsx;*.zcci",
			Multiselect = true,
			CheckFileExists = true
		};
		if (dialog.ShowDialog(this) == DialogResult.OK) ImportSelectedRoms(dialog.FileNames);
	}

	private void SelectRomFolder()
	{
		using var dialog = new FolderBrowserDialog { Description = "Escolha uma pasta para examinar ROMs GBA, Nintendo DS e Nintendo 3DS. Nenhum arquivo será copiado." };
		if (dialog.ShowDialog(this) != DialogResult.OK) return;
		try
		{
			var discovered = RomFileDiscovery.Scan(dialog.SelectedPath, ImportedGameCatalog.IsSupportedRom, 501);
			string[] files = discovered.Files.ToArray();
			if (files.Length == 0)
			{
				string detail=discovered.SkippedLocations>0||discovered.LimitReached ? " O exame foi parcial: há locais inacessíveis, links ou limites de leitura. Escolha a pasta da ROM diretamente ou selecione seus arquivos." : string.Empty;
				MessageBox.Show(this, "Não encontrei arquivos GBA, Nintendo DS ou Nintendo 3DS compatíveis nesta pasta."+detail, "Examinar pasta", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if (files.Length > 500)
			{
				MessageBox.Show(this, "A pasta contém mais de 500 ROMs compatíveis. Escolha uma pasta menor para revisar os arquivos com segurança.", "Limite de arquivos", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if(discovered.SkippedLocations>0||discovered.LimitReached)
				MessageBox.Show(this,"O exame encontrou "+files.Length+" ROM(s), mas foi parcial: há locais inacessíveis, links ou limites de leitura. Você pode adicionar os arquivos encontrados; para os demais, selecione a pasta ou o arquivo diretamente.","Exame parcial",MessageBoxButtons.OK,MessageBoxIcon.Information);
			ImportSelectedRoms(files);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Text.Json.JsonException)
		{
			MessageBox.Show(this, "Não foi possível examinar esta pasta. Confira as permissões e tente novamente.\n\n" + ex.Message, "Examinar pasta", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void ImportSelectedRoms(IEnumerable<string> files)
	{
		try
		{
			int imported = ImportedGameCatalog.ImportFiles(this, root, files);
			if (imported > 0) RefreshCatalog();
			else MessageBox.Show(this, "Nenhum jogo foi adicionado. ROMs sem identificação Pokémon só entram após a confirmação de que são hacks Pokémon.", "Adicionar jogos", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
		{
			MessageBox.Show(this, "Não foi possível salvar o catálogo local de jogos. Os arquivos de ROM não foram alterados.\n\n" + ex.Message, "Adicionar jogos", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void UpdateContentWidth()
	{
		if (content == null || scroll == null)
		{
			return;
		}
		content.MaximumSize = new Size(Math.Max(400, scroll.ClientSize.Width - 20), 0);
        content.Width = content.MaximumSize.Width;
		foreach (Control control in content.Controls)
		{
			if (control is FlowLayoutPanel flowLayoutPanel)
			{
				flowLayoutPanel.Width = Math.Max(340, content.ClientSize.Width - content.Padding.Horizontal);
				int columns = Math.Max(1, flowLayoutPanel.Width / 242);
				int rowHeight = (flowLayoutPanel.Controls.Count > 0 ? flowLayoutPanel.Controls[0].Height : 344) + 16;
				flowLayoutPanel.Height = ((flowLayoutPanel.Controls.Count + columns - 1) / columns) * rowHeight;
			}
			if (control is Label label)
			{
				label.Width = Math.Max(340, content.ClientSize.Width - content.Padding.Horizontal);
			}
		}
	}

	private static IEnumerable<Control> GetChildren(Control parent)
	{
		foreach (Control child in parent.Controls) yield return child;
	}

	protected override bool ProcessCmdKey(ref Message message, Keys keyData)
	{
        if(keyData==Keys.Enter&&search.ContainsFocus){Rebuild();return true;}
		if (keyData == Keys.Escape && search.ContainsFocus && search.Text.Length > 0)
		{
			search.Text = string.Empty;
			return true;
		}
		if (keyData == Keys.F5)
		{
			RefreshCatalog();
			return true;
		}
		if (keyData == (Keys.Control | Keys.F) || keyData == (Keys.Control | Keys.E))
		{
			search.Focus();
			return true;
		}
		return base.ProcessCmdKey(ref message, keyData);
	}

	private void RefreshCatalog()
	{
		try
		{
			List<GameInfo> refreshedGames = GameCatalog.Build(root);
			games.Clear();
			games.AddRange(refreshedGames);
            importedAvailability=ImportedGameAvailability.Inspect(root);
			heroBanner.UpdateGameCount(games.Count, DateTimeOffset.Now);
			Rebuild();
			if (refreshButton.Visible)
				refreshButton.Focus();
			else
				search.Focus();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
		{
			MessageBox.Show(this, "Não foi possível atualizar a biblioteca. Os jogos atuais continuam disponíveis.\n\n" + ex.Message, "Atualizar biblioteca", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	internal void RefreshAfterGameSession() => Rebuild();

	private void Rebuild()
	{
        searchDelay.Stop();searchPending=false;
		if (content == null)
		{
			return;
		}
		string text = ((search == null || string.IsNullOrWhiteSpace(search.Text)) ? string.Empty : search.Text.Trim());
		int num = ((systemFilter != null) ? systemFilter.SelectedIndex : 0);
		HashSet<string> favorites = GameFavoriteService.Load(root);
		Dictionary<string, GameLaunchHistoryEntry> recentGames = recentOnly || mostPlayedOnly
			? GameLaunchHistoryService.Load(root).ToDictionary(entry => entry.Title, entry => entry, StringComparer.OrdinalIgnoreCase)
			: new Dictionary<string, GameLaunchHistoryEntry>(StringComparer.OrdinalIgnoreCase);
		bool historyView = recentOnly || mostPlayedOnly;
		clearRecentButton.Text = mostPlayedOnly ? "Limpar histórico" : "Limpar recentes";
		clearRecentButton.AccessibleName = mostPlayedOnly
			? "Limpar histórico local, incluindo datas recentes e tempos totais jogados"
			: "Limpar histórico local de jogos recentes, incluindo os tempos totais jogados";
		clearRecentButton.Visible = historyView && recentGames.Count > 0;
		layoutToolbar?.Invoke();
		content.SuspendLayout();
		foreach (Control old in new List<Control>(GetChildren(content))) old.Dispose();
		content.Controls.Clear();
        if(importedAvailability.CatalogUnreadable||importedAvailability.UnavailableCount>0) {
            string title=importedAvailability.CatalogUnreadable?"Não foi possível ler os jogos importados":importedAvailability.UnavailableCount==1?"Uma ROM importada não está acessível":$"{importedAvailability.UnavailableCount} ROMs importadas não estão acessíveis";
            string description=importedAvailability.CatalogUnreadable?"O catálogo local precisa ser verificado. Seus arquivos de ROM e saves foram preservados. Abra o gerenciador para ver o problema.":"Reconecte a unidade ou localize o arquivo no gerenciador. A associação do jogo com seus saves será mantida.";
            var notice=new EmptyStatePanel(title,description,"GERENCIAR JOGOS",(_,_)=>ManageImportedGames()) {Tag="imported-rom-notice",AccessibleName=title,AccessibleDescription=description};
            content.Controls.Add(notice);
        }
		if (generationFilter == 0 && !favoritesOnly && !recentOnly && !alphabeticalOnly && !mostPlayedOnly && num == 0 && text.Length == 0 &&
			!games.Any(game => game.IsImported ? File.Exists(game.RomPath) : !string.IsNullOrEmpty(game.Launcher) && File.Exists(Path.Combine(root, game.Launcher))))
		{
			var gettingStarted = new EmptyStatePanel("Comece adicionando um jogo", "Adicione ROMs Pokémon GBA, DS ou 3DS. Depois configure o emulador em Configurações. Login é opcional.", "ADICIONAR JOGOS", (sender, _) =>
			{
				if (sender is Control action) importButton.ContextMenuStrip.Show(action, new Point(0, action.Height));
			});
			gettingStarted.Tag = "library-getting-started";
			gettingStarted.AccessibleName = "Primeiros passos para jogar Pokémon";
			content.Controls.Add(gettingStarted);
		}
		bool flag = false;
		int matchingGameCount = 0;
		for (int i = 3; i <= 6; i++)
		{
			if (recentOnly || alphabeticalOnly || mostPlayedOnly ? i != 3 : generationFilter != 0 && generationFilter != i)
			{
				continue;
			}
			List<GameInfo> list = new List<GameInfo>();
			foreach (GameInfo game in games)
			{
				bool sectionMatches = recentOnly
					? recentGames.ContainsKey(game.Title)
					: mostPlayedOnly
						? recentGames.TryGetValue(game.Title, out GameLaunchHistoryEntry entry) && entry.TotalPlayTimeSeconds > 0
						: alphabeticalOnly || game.Generation == i;
				if (sectionMatches && (!favoritesOnly || favorites.Contains(game.Title)) && (num != 1 || game.Subtitle.IndexOf("Game Boy", StringComparison.OrdinalIgnoreCase) >= 0) && (num != 2 || game.Subtitle.IndexOf("Nintendo DS", StringComparison.OrdinalIgnoreCase) >= 0) && (num != 3 || game.Subtitle.IndexOf("Nintendo 3DS", StringComparison.OrdinalIgnoreCase) >= 0) && GameSearch.Matches(game,text))
				{
					list.Add(game);
				}
			}
			if (recentOnly)
				list = list.OrderByDescending(game => recentGames[game.Title].PlayedAt).ThenBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
			else if (mostPlayedOnly)
				list = list.OrderByDescending(game => recentGames[game.Title].TotalPlayTimeSeconds)
					.ThenByDescending(game => recentGames[game.Title].PlayedAt)
					.ThenBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
			else if (alphabeticalOnly)
				list = list.OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
			matchingGameCount += list.Count;
			if (!favoritesOnly && list.Count == 0 && i == 6 && generationFilter != 6 && string.IsNullOrEmpty(text) && num == 0 && GameCatalog.HasAzahar(root))
			{
				if (recentOnly) AddRecentHeader(); else if (alphabeticalOnly) AddAlphabeticalHeader(); else if (mostPlayedOnly) AddMostPlayedHeader(); else AddGenerationHeader(i);
				content.Controls.Add(new EmptyStatePanel("Adicione seus jogos de Nintendo 3DS", "Abra a pasta de ROMs e adicione um jogo para vê-lo na biblioteca.", "ABRIR PASTA 3DS", (object param0, EventArgs param1) =>
				{
					host.OpenThreeDsFolder();
				}));
				flag = true;
			}
			else
			{
				if (list.Count == 0)
				{
					continue;
				}
				flag = true;
				if (recentOnly) AddRecentHeader(); else if (alphabeticalOnly) AddAlphabeticalHeader(); else if (mostPlayedOnly) AddMostPlayedHeader(); else AddGenerationHeader(i);
				FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
				flowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
				flowLayoutPanel.WrapContents = true;
				flowLayoutPanel.AutoScroll = false;
				flowLayoutPanel.AutoSize = false;
				flowLayoutPanel.Width = 820;
				flowLayoutPanel.BackColor = Color.Transparent;
				flowLayoutPanel.Margin = new Padding(0, 0, 0, 10);
				flowLayoutPanel.Padding = new Padding(0);
				FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
				foreach (GameInfo item in list)
				{
					DateTimeOffset? lastPlayedAt = recentOnly || mostPlayedOnly ? recentGames[item.Title].PlayedAt : null;
					long totalPlayTimeSeconds = recentOnly || mostPlayedOnly ? recentGames[item.Title].TotalPlayTimeSeconds : 0;
					var card = new GameCard(item, root, lastPlayedAt, totalPlayTimeSeconds);
					card.FavoriteChanged += (_, _) => HandleFavoriteChanged(card);
					flowLayoutPanel2.Controls.Add(card);
				}
				content.Controls.Add(flowLayoutPanel2);
			}
		}
		bool hasActiveFilters = generationFilter != 0 || favoritesOnly || recentOnly || alphabeticalOnly || mostPlayedOnly || num != 0 || text.Length > 0;
		heroBanner.UpdateVisibleGameCount(matchingGameCount, hasActiveFilters);
		if (!flag)
		{
			bool hasMeasuredPlayTime = recentGames.Values.Any(entry => entry.TotalPlayTimeSeconds > 0);
			string emptyTitle = recentOnly ? "Nenhum jogo recente" : favoritesOnly ? "Nenhum favorito ainda" : mostPlayedOnly && !hasMeasuredPlayTime ? "Ainda sem tempo jogado" : "Nenhum jogo encontrado";
			string emptyDescription = recentOnly ? "Os jogos que você abrir aparecerão aqui, começando pelo mais recente." : favoritesOnly ? "Marque a estrela no cartão de um jogo para encontrá-lo aqui." : mostPlayedOnly && !hasMeasuredPlayTime ? "Jogue um título e encerre o emulador para começar a registrar o tempo total." : "Tente outra busca ou selecione Todos os sistemas para ver a coleção completa.";
			string emptyAction = recentOnly || favoritesOnly || alphabeticalOnly || mostPlayedOnly ? "MOSTRAR TODOS" : "LIMPAR BUSCA";
			content.Controls.Add(new EmptyStatePanel(emptyTitle, emptyDescription, emptyAction, (object param0, EventArgs param1) =>
			{
				favoritesOnly = false;
				recentOnly = false;
			alphabeticalOnly = false;
			mostPlayedOnly = false;
			clearRecentButton.Visible = false;
			sortHelper.Text = "Ordenados por geração de lançamento";
			layoutToolbar?.Invoke();
				UpdateFavoriteFilter();
				search.Text = string.Empty;
				systemFilter.SelectedIndex = 0;
				generationFilter = 0;
				foreach (ChipButton chipButton in chipButtons)
				{
					chipButton.Selected = chipButton == chipButtons[0];
				}
				Rebuild();
			}));
		}
		content.ResumeLayout();
		UpdateContentWidth();
	}

	private void HandleFavoriteChanged(GameCard card)
	{
		if (!favoritesOnly)
			return;

		bool restoreFocus = card.ContainsFocus;
		int previousIndex = EnumerateGameCards().ToList().IndexOf(card);
		Rebuild();
		if (!restoreFocus)
			return;

		GameCard[] remainingCards = EnumerateGameCards().ToArray();
		if (remainingCards.Length > 0)
		{
			remainingCards[Math.Clamp(previousIndex, 0, remainingCards.Length - 1)].FocusFavoriteButton();
			return;
		}

		EmptyStatePanel emptyState = content.Controls.OfType<EmptyStatePanel>().FirstOrDefault();
		Button action = emptyState?.Controls.OfType<Button>().FirstOrDefault();
		if (action != null)
			action.Focus();
		else
			favoriteFilter.Focus();
	}

	private IEnumerable<GameCard> EnumerateGameCards()
	{
		foreach (Control child in content.Controls)
		{
			if (child is FlowLayoutPanel cards)
				foreach (GameCard card in cards.Controls.OfType<GameCard>())
					yield return card;
		}
	}

	private void UpdateFavoriteFilter()
	{
		favoriteFilter.Text = favoritesOnly ? "★ Favoritos" : "☆ Favoritos";
		favoriteFilter.Kind = favoritesOnly ? ButtonKind.Primary : ButtonKind.Secondary;
		favoriteFilter.AccessibleName = favoritesOnly ? "Mostrar todos os jogos" : "Mostrar somente jogos favoritos";
	}

	private void AddGenerationHeader(int generation)
	{
		Label label = new Label();
		label.Text = "Geração " + generation;
		label.Height = 28;
		label.Width = 820;
		label.Font = AppTheme.Section;
		label.ForeColor = ((generation == 6) ? AppTheme.BlueSoft : AppTheme.Gold);
		label.Margin = new Padding(0, 6, 0, 7);
		label.Padding = new Padding(0, 3, 0, 0);
		Label value = label;
		content.Controls.Add(value);
	}

	private void AddRecentHeader()
	{
		Label label = new Label
		{
			Text = "Jogados recentemente",
			Height = 28,
			Width = 820,
			Font = AppTheme.Section,
			ForeColor = AppTheme.Gold,
			Margin = new Padding(0, 6, 0, 7),
			Padding = new Padding(0, 3, 0, 0)
		};
		content.Controls.Add(label);
	}

	private void AddAlphabeticalHeader()
	{
		Label label = new Label
		{
			Text = "Todos os jogos · A–Z",
			Height = 28,
			Width = 820,
			Font = AppTheme.Section,
			ForeColor = AppTheme.Gold,
			Margin = new Padding(0, 6, 0, 7),
			Padding = new Padding(0, 3, 0, 0)
		};
		content.Controls.Add(label);
	}

    private void AddMostPlayedHeader()
    {
        Label label = new Label
        {
            Text = "Mais jogados · tempo total",
            Height = 28,
            Width = 820,
            Font = AppTheme.Section,
            ForeColor = AppTheme.Gold,
            Margin = new Padding(0, 6, 0, 7),
            Padding = new Padding(0, 3, 0, 0)
        };
        content.Controls.Add(label);
    }
}
