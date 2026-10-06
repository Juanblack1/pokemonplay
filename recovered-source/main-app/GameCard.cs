using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed class GameCard : BufferedPanel
{
	private readonly GameInfo game;
	private readonly string root;

	private readonly Image cover;
	private readonly DateTimeOffset? lastPlayedAt;
    private readonly long totalPlayTimeSeconds;
    private string playLabel;
    private string profileProblem;
	private readonly ThemeButton favoriteButton;
	private readonly ThemeButton openCoverFolderButton;
	private bool isFavorite;
	public event EventHandler FavoriteChanged;

	internal void FocusFavoriteButton() => favoriteButton.Focus();

	private bool hover;

	public GameCard(GameInfo game, string root)
		: this(game, root, null, 0)
	{
	}

	public GameCard(GameInfo game, string root, DateTimeOffset? lastPlayedAt)
		: this(game, root, lastPlayedAt, 0)
	{
	}

	public GameCard(GameInfo game, string root, DateTimeOffset? lastPlayedAt, long totalPlayTimeSeconds)
	{
		this.game = game;
		this.root = root;
		this.lastPlayedAt = lastPlayedAt;
        this.totalPlayTimeSeconds = Math.Clamp(totalPlayTimeSeconds, 0, TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond);
        var presentation=GameProfilePresentation.Read(root,game);
        playLabel=presentation.Label;profileProblem=presentation.Problem;
		isFavorite = GameFavoriteService.Load(root).Contains(game.Title);
		Width = 228;
		BackColor = AppTheme.Background;
		Height = game.IsHackRom ? (lastPlayedAt.HasValue ? 398 : 366) : lastPlayedAt.HasValue ? 376 : 344;
		Margin = new Padding(0, 0, 14, 16);
		Cursor = Cursors.Hand;
		TabStop = true;
		AccessibleRole = AccessibleRole.PushButton;
		AccessibleName = game.Title + ", " + (game.IsHackRom ? "HACK ROM, " : string.Empty) + game.Subtitle + ", " + playLabel;
		if (lastPlayedAt.HasValue)
			AccessibleDescription = "Jogado em " + lastPlayedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ". Tempo total: " + FormatPlayTime(this.totalPlayTimeSeconds);
        if(profileProblem!=null)AccessibleDescription=(AccessibleDescription+" "+profileProblem).Trim();
		favoriteButton = new GameFavoriteButton(isFavorite) { Size = new Size(36, 36), Location = new Point(Width - 48, 12), Margin = Padding.Empty, TabStop = true, AccessibleName = isFavorite ? "Remover dos favoritos" : "Adicionar aos favoritos", AccessibleDescription = game.Title };
		favoriteButton.Click += (_, _) => ToggleFavorite();
		Controls.Add(favoriteButton);
		cover = GameCoverService.Load(root, game.Cover);
		if (cover == null)
		{
			string hint = string.IsNullOrWhiteSpace(game.Cover)
				? "Adicione a capa da ROM em Pokemon 3DS - Capas."
				: "Arquivo esperado: " + game.Cover;
			AccessibleDescription = string.IsNullOrWhiteSpace(AccessibleDescription)
				? "Capa não encontrada. " + hint
				: AccessibleDescription + ". Capa não encontrada. " + hint;
			openCoverFolderButton = new ThemeButton("Abrir pasta da capa", ButtonKind.Ghost)
			{
				AutoSize = false,
				Size = new Size(196, 25),
				Location = new Point((Width - 196) / 2, 186),
				AccessibleName = "Abrir pasta da capa",
				AccessibleDescription = "Abre " + GetCoverDirectoryPath(root, game.Cover) + ". A pasta será criada se ainda não existir."
			};
			openCoverFolderButton.Click += (_, _) => OpenCoverFolder();
			Controls.Add(openCoverFolderButton);
		}
		else openCoverFolderButton = null;
		Click += (object param0, EventArgs param1) =>
		{
			Launch();
		};
	}

	internal static string GetCoverDirectoryPath(string root, string coverRelativePath)
	{
		string installRoot = Path.GetFullPath(root);
		string directory = string.IsNullOrWhiteSpace(coverRelativePath)
			? Path.Combine(installRoot, "Pokemon 3DS - Capas")
			: Path.GetDirectoryName(Path.GetFullPath(Path.Combine(installRoot, coverRelativePath))) ?? installRoot;
		string rootPrefix = Path.TrimEndingDirectorySeparator(installRoot) + Path.DirectorySeparatorChar;
		if (!directory.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException("A pasta da capa precisa ficar dentro da instalação.", nameof(coverRelativePath));
		return directory;
	}

    internal static string FormatPlayTime(long totalSeconds)
    {
        if (totalSeconds <= 0) return "ainda não medido";
        TimeSpan total = TimeSpan.FromSeconds(Math.Min(totalSeconds, TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond));
        if (total.TotalDays >= 1)
            return (int)total.TotalDays + " d" + (total.Hours > 0 ? " " + total.Hours + " h" : string.Empty);
        if (total.TotalHours >= 1)
            return (int)total.TotalHours + " h" + (total.Minutes > 0 ? " " + total.Minutes + " min" : string.Empty);
        if (total.TotalMinutes >= 1)
            return (int)total.TotalMinutes + " min";
        return "menos de 1 min";
    }

	private void OpenCoverFolder()
	{
		try
		{
			string directory = GetCoverDirectoryPath(root, game.Cover);
			Directory.CreateDirectory(directory);
			Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "Não foi possível abrir a pasta da capa.\n\n" + ex.Message, "Capas dos jogos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private void ToggleFavorite()
	{
		try
		{
			GameFavoriteService.Set(root, game.Title, !isFavorite);
			isFavorite = !isFavorite;
			favoriteButton.Text = isFavorite ? "★" : "☆";
			favoriteButton.AccessibleName = isFavorite ? "Remover dos favoritos" : "Adicionar aos favoritos";
			favoriteButton.AccessibleDescription = game.Title;
			FavoriteChanged?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) { MessageBox.Show(this, "Não foi possível atualizar os favoritos.\n\n" + ex.Message, "Favoritos", MessageBoxButtons.OK, MessageBoxIcon.Error); }
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		hover = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnGotFocus(EventArgs e)
	{
		Invalidate();
		base.OnGotFocus(e);
	}

	protected override void OnLostFocus(EventArgs e)
	{
		Invalidate();
		base.OnLostFocus(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Modifiers == Keys.None && e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
		{
			GameCard next = FindDirectionalGameCard(this, e.KeyCode);
			if (next != null)
			{
				for (Control ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
				{
					if (ancestor is ScrollableControl scrollable && scrollable.AutoScroll)
					{
						scrollable.ScrollControlIntoView(next);
						break;
					}
				}
				next.Focus();
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}
		if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
		{
			Launch();
			e.Handled = true;
			e.SuppressKeyPress = true;
		}
		base.OnKeyDown(e);
	}

	protected override bool IsInputKey(Keys keyData)
	{
		Keys key = keyData & Keys.KeyCode;
		return key is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Return or Keys.Space || base.IsInputKey(keyData);
	}

	private static GameCard FindDirectionalGameCard(GameCard current, Keys direction)
	{
		if (current?.Parent == null || direction is not (Keys.Left or Keys.Right or Keys.Up or Keys.Down))
			return null;

		Control libraryContent = current.Parent.Parent;
		IEnumerable<GameCard> cards = libraryContent is FlowLayoutPanel content
			? content.Controls.OfType<FlowLayoutPanel>().SelectMany(section => section.Controls.OfType<GameCard>())
			: current.Parent.Controls.OfType<GameCard>();
		Point center = current.PointToScreen(new Point(current.Width / 2, current.Height / 2));
		bool horizontal = direction is Keys.Left or Keys.Right;
		var candidates = cards
			.Where(candidate => candidate != current && candidate.Visible)
			.Select(candidate => new
			{
				Card = candidate,
				Center = candidate.PointToScreen(new Point(candidate.Width / 2, candidate.Height / 2))
			})
			.Select(candidate => new
			{
				candidate.Card,
				DeltaX = candidate.Center.X - center.X,
				DeltaY = candidate.Center.Y - center.Y
			})
			.Where(candidate => direction switch
			{
				Keys.Left => candidate.DeltaX < 0,
				Keys.Right => candidate.DeltaX > 0,
				Keys.Up => candidate.DeltaY < 0,
				Keys.Down => candidate.DeltaY > 0,
				_ => false
			})
			.OrderBy(candidate => horizontal ? Math.Abs(candidate.DeltaX) : Math.Abs(candidate.DeltaY))
			.ThenBy(candidate => horizontal ? Math.Abs(candidate.DeltaY) : Math.Abs(candidate.DeltaX));
		return candidates.Select(candidate => candidate.Card).FirstOrDefault();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.SmoothingMode = SmoothingMode.None;
		e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		Rectangle rectangle = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
		Color color = (hover ? AppTheme.SurfaceHover : AppTheme.Surface);
		using (SolidBrush brush = new SolidBrush(color))
		{
			PaintTools.FillRounded(e.Graphics, brush, rectangle, 12);
		}
		using (Pen pen = new Pen((hover || Focused) ? AppTheme.BlueSoft : AppTheme.BorderSoft, (hover || Focused) ? 1.8f : 1.1f))
		{
			PaintTools.DrawRounded(e.Graphics, pen, rectangle, 12);
		}
		Rectangle rectangle2 = new Rectangle(10, 10, Width - 21, 204);
		using (SolidBrush brush2 = new SolidBrush(AppTheme.Background))
		{
			PaintTools.FillRounded(e.Graphics, brush2, rectangle2, 4);
		}
		if (cover != null)
		{
			Rectangle rect = PaintTools.FitImage(cover, new Rectangle(14, 14, Width - 29, 196));
			e.Graphics.DrawImage(cover, rect);
		}
		else
		{
			PaintTools.DrawPokeball(e.Graphics, new Rectangle(75, 65, 66, 66), game.Accent, Color.FromArgb(9, 14, 38));
			TextRenderer.DrawText(e.Graphics, "Capa não encontrada", AppTheme.Caption, new Rectangle(18, 139, Width - 36, 22), AppTheme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
			string hint = string.IsNullOrWhiteSpace(game.Cover) ? "Pokemon 3DS - Capas" : game.Cover;
			TextRenderer.DrawText(e.Graphics, hint, AppTheme.Caption, new Rectangle(18, 162, Width - 36, 22), AppTheme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
		}
		TextRenderer.DrawText(e.Graphics, game.Title, AppTheme.BodyBold, new Rectangle(14, 226, Width - 28, 25), AppTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
		int actionTop = 292;
		if (game.IsHackRom)
		{
			Rectangle badge = new Rectangle(14, 252, 80, 20);
			using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(65, 52, 124))) PaintTools.FillRounded(e.Graphics, badgeBrush, badge, 4);
			using (Pen badgePen = new Pen(Color.FromArgb(137, 111, 220), 1f)) PaintTools.DrawRounded(e.Graphics, badgePen, badge, 4);
			TextRenderer.DrawText(e.Graphics, "HACK ROM", AppTheme.CaptionBold, badge, Color.FromArgb(224, 213, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
			TextRenderer.DrawText(e.Graphics, game.Subtitle, AppTheme.Caption, new Rectangle(14, 275, Width - 28, 18), AppTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
			actionTop = 314;
		}
		else
			TextRenderer.DrawText(e.Graphics, game.Subtitle, AppTheme.Caption, new Rectangle(14, 254, Width - 28, 22), AppTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
		if (lastPlayedAt.HasValue)
		{
			int historyTop = game.IsHackRom ? 300 : 278;
			TextRenderer.DrawText(e.Graphics, "Jogado em " + lastPlayedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), AppTheme.Caption, new Rectangle(14, historyTop, Width - 28, 18), AppTheme.TextMuted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
			TextRenderer.DrawText(e.Graphics, "Tempo total: " + FormatPlayTime(totalPlayTimeSeconds), AppTheme.Caption, new Rectangle(14, historyTop + 20, Width - 28, 18), AppTheme.TextMuted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
			actionTop = game.IsHackRom ? 346 : 324;
		}
		using (SolidBrush brush3 = new SolidBrush(game.Accent))
		{
			e.Graphics.FillRectangle(brush3, new Rectangle(14, actionTop - 11, 28, 2));
		}
		using (SolidBrush brush4 = new SolidBrush(profileProblem!=null?(hover?AppTheme.SurfaceRaised:AppTheme.Surface):hover ? Color.FromArgb(59,122,232) : AppTheme.Blue))
		{
			PaintTools.FillRounded(e.Graphics, brush4, new Rectangle(14, actionTop, Width - 28, 40), 8);
		}
		using (Pen pen2 = new Pen(profileProblem!=null?AppTheme.Red:hover ? AppTheme.BlueSoft : AppTheme.Border, 1f))
		{
			PaintTools.DrawRounded(e.Graphics, pen2, new Rectangle(14, actionTop, Width - 28, 40), 8);
		}
		TextRenderer.DrawText(e.Graphics, playLabel, AppTheme.BodyBold, new Rectangle(14, actionTop, Width - 28, 40), profileProblem==null?AppTheme.Text:AppTheme.Red, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
	}
    internal bool RefreshProfileStatus()
    {
        var presentation=GameProfilePresentation.Read(root,game);
        if(profileProblem!=null)AccessibleDescription=(AccessibleDescription??string.Empty).Replace(profileProblem,string.Empty,StringComparison.Ordinal).Trim();
        playLabel=presentation.Label;profileProblem=presentation.Problem;
        AccessibleName=game.Title+", "+(game.IsHackRom?"HACK ROM, ":string.Empty)+game.Subtitle+", "+playLabel;
        if(profileProblem!=null)AccessibleDescription=(AccessibleDescription+" "+profileProblem).Trim();
        Invalidate();return profileProblem==null;
    }

	private void Launch()
	{
		string baseDirectory = root;
		string path = Path.Combine(baseDirectory, game.Launcher ?? string.Empty);
		Form form = FindForm();
		string processName = game.EmulatorProcess ?? (game.Generation == 3 ? "visualboyadvance-m" : game.Generation >= 6 ? "azahar" : "melonDS");
		string temporaryConfigPath = null;
		bool managedLauncherSession = false;
		try
		{
            SaveProfileService.EnsureEmulatorsClosed();
            if(!RefreshProfileStatus()) {
                MessageBox.Show(form,profileProblem+"\n\nO jogo não foi iniciado. Após corrigir a configuração, tente novamente.","Verificar perfis",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return;
            }
            if (!ChooseSaveProfileDialog.ChooseForLaunch(form, root, game)) return;
			string launchProfileName = game.IsImported ? "Padrão" : "Principal";
			if (game.Generation <= 5 && !game.IsImported)
            {
                var profiles = SaveProfileService.Load(root, game.SaveFolderName);
                launchProfileName = profiles.Profiles.Find(p => p.Id == profiles.ActiveId)?.Name ?? "Principal";
                playLabel = "Jogar · " + launchProfileName;
                AccessibleName = game.Title + ", " + game.Subtitle + ", " + playLabel;
                Invalidate();
            }
            string arguments=game.Arguments;
			LauncherSettings.PrepareGame(root,game,ref path,ref arguments,out processName,out temporaryConfigPath);
			if (!File.Exists(path)) throw new FileNotFoundException("Não encontrei o executável configurado para este jogo.", path);
			bool emulatorRunning = IsProcessRunning(processName);
			if (!emulatorRunning)
			{
				try
				{
					SaveBackupService.CreateIfChanged(root, game.SaveFolderName);
				}
				catch (Exception backupError)
				{
					DialogResult choice = MessageBox.Show(
						"Não foi possível proteger o save antes de abrir o jogo:\n\n" + backupError.Message +
						"\n\nDeseja continuar sem o backup?",
						"Backup automático",
						MessageBoxButtons.YesNo,
						MessageBoxIcon.Warning,
						MessageBoxDefaultButton.Button2);
					if (choice != DialogResult.Yes)
						return;
				}
				emulatorRunning = IsProcessRunning(processName);
			}
			if (emulatorRunning)
			{
				DialogResult choice = MessageBox.Show(
					"Já existe um emulador aberto. Abrir outro jogo pode causar conflito e o backup talvez não inclua as alterações mais recentes. Deseja continuar?",
					"Save em uso",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning,
					MessageBoxDefaultButton.Button2);
				if (choice != DialogResult.Yes)
					return;
			}
			int consoleModel = game.Generation == 3 ? 0 : game.Generation >= 6 ? 2 : 1;
			if (temporaryConfigPath != null && form is LauncherForm launcherForm)
			{
				GameHostForm session = new GameHostForm(path, processName, arguments, game.Title, launchProfileName, root, temporaryConfigPath, consoleModel);
				launcherForm.RegisterGameSession(session);
				managedLauncherSession = true;
				return;
			}
			form.Hide();
			using GameHostForm gameHostForm = new GameHostForm(path, processName, arguments, game.Title, launchProfileName, root, temporaryConfigPath, consoleModel);
			gameHostForm.ShowDialog();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Não foi possível iniciar o jogo:\n\n" + ex.Message, "Pokemons Play", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			if (!managedLauncherSession)
			{
				GameSessionSettingsService.Cleanup(temporaryConfigPath);
				form.Show();
				form.Activate();
			}
		}
	}

	private static bool IsProcessRunning(string processName)
	{
		Process[] processes = Process.GetProcessesByName(processName);
		try
		{
			foreach (Process process in processes)
			{
				try
				{
					if (!process.HasExited)
						return true;
				}
				catch (InvalidOperationException)
				{
				}
			}
			return false;
		}
		finally
		{
			foreach (Process process in processes)
				process.Dispose();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && cover != null)
		{
			cover.Dispose();
		}
		base.Dispose(disposing);
	}
}
