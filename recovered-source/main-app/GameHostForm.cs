using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GameHostForm : Form
{
	private const int GWL_STYLE = -16;

	private const int WS_CHILD = 1073741824;

	private const int HOTKEY_ID = 9002;

	private readonly string launcher;

	private readonly string processName;

	private readonly string arguments;

	private readonly string historyRoot;

	private readonly string historyTitle;
	private readonly string profileTitle;
	private readonly string temporaryConfigPath;
	private readonly int configuredConsoleModel;
	private readonly bool canPauseToMenu;

	private Process emulator;

	private Timer timer;

	private Panel gamePanel;

	private bool closing;
	private bool closeInProgress;
	private bool emulatorEmbedded;
	private IntPtr embeddedWindowHandle;
	private bool launchFailed;
	private bool azaharPausedForMenu;
	private bool azaharResumePending;
	private Stopwatch sessionClock;
	private long recordedSessionSeconds;
	internal Func<ProcessStartInfo, Process> ProcessStarter { get; set; } = startInfo => Process.Start(startInfo);
	internal Action<GameHostForm> SessionHidden { get; set; }
	internal Action<GameHostForm> SessionClosed { get; set; }

    private GameInputBridge inputBridge;
    private ControllerVisualizer virtualPad;
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

	[DllImport("user32.dll")]
	private static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(IntPtr handle, int index, int value);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr handle, int index);

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(IntPtr handle, int id);

	[DllImport("user32.dll", EntryPoint = "PostMessageW", CharSet = CharSet.Unicode)]
	private static extern bool PostWindowMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

	public GameHostForm(string launcher, string processName, string arguments)
		: this(launcher, processName, arguments, "Jogo", "Principal")
	{
	}

	public GameHostForm(string launcher, string processName, string arguments, string gameTitle, string profileName)
		: this(launcher, processName, arguments, gameTitle, profileName, null)
	{
	}

	public GameHostForm(string launcher, string processName, string arguments, string gameTitle, string profileName, string historyRoot)
		: this(launcher, processName, arguments, gameTitle, profileName, historyRoot, null, -1)
	{
	}

	internal GameHostForm(string launcher, string processName, string arguments, string gameTitle, string profileName, string historyRoot, string temporaryConfigPath, int consoleModel)
	{
		this.launcher = launcher;
		this.processName = processName;
		this.arguments = arguments ?? string.Empty;
		gameTitle = string.IsNullOrWhiteSpace(gameTitle) ? "Jogo" : gameTitle.Trim();
		profileName = string.IsNullOrWhiteSpace(profileName) ? "Principal" : profileName.Trim();
		this.historyRoot = historyRoot;
		historyTitle = gameTitle;
		profileTitle = profileName;
		this.temporaryConfigPath = temporaryConfigPath;
		canPauseToMenu = !string.IsNullOrWhiteSpace(temporaryConfigPath);
		configuredConsoleModel = consoleModel;
		Text = $"Pokemons Play · {gameTitle} · Perfil {profileName}";
		Width = 1180;
		Height = 800;
		MinimumSize = new Size(820, 580);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.Black;
		KeyPreview = true;
		Panel toolbar = new BufferedPanel
		{
			Dock = DockStyle.Top,
			Height = 56,
			BackColor = AppTheme.TopBar
		};
		Label value = new Label
		{
			Text = $"{gameTitle} · Perfil {profileName}",
			Font = AppTheme.Section,
			ForeColor = AppTheme.Text,
			Location = new Point(22, 10),
			AutoSize = false,
			AutoEllipsis = true,
			Width = 920,
			Height = 22
		};
		Label value2 = new Label
		{
			Text = canPauseToMenu
				? "F12 ou PAUSAR · MENU volta à Biblioteca e mantém o jogo pausado; use Retomar no topo para continuar."
				: "F12 ou VOLTAR AO MENU encerra o emulador após confirmação.",
			Font = AppTheme.Caption,
			ForeColor = AppTheme.TextMuted,
			Location = new Point(23, 31),
			AutoSize = false,
			AutoEllipsis = true,
			Width = 920,
			Height = 18
		};
		ThemeButton back = new ThemeButton(canPauseToMenu ? "PAUSAR · MENU" : "VOLTAR AO MENU", ButtonKind.Secondary)
		{
			Width = 158,
			Height = 34,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right)
		};
		back.Click += (object param0, EventArgs param1) =>
		{
			if (canPauseToMenu) ReturnToMenu(); else CloseWithConfirmation();
		};
		toolbar.Controls.Add(value);
		toolbar.Controls.Add(value2);
		toolbar.Controls.Add(back);
		toolbar.Resize += (object param0, EventArgs param1) =>
		{
			back.Left = toolbar.ClientSize.Width - back.Width - 20;
			back.Top = 11;
			value.Width = value2.Width = Math.Max(120, toolbar.ClientSize.Width - back.Width - 72);
		};
		gamePanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.Black
		};
		Controls.Add(gamePanel);
		Controls.Add(toolbar);
        if(InputDeviceProfile.Load(Path.Combine(AppPaths.Root,"Settings","input-device.json")).Mode==3)
        {
            virtualPad=new ControllerVisualizer {Dock=DockStyle.Bottom,Height=260,VirtualInput=true,Testing=true,ConsoleModel=configuredConsoleModel is >= 0 and <= 2 ? configuredConsoleModel : processName=="visualboyadvance-m"?0:processName=="azahar"?2:1};
            Controls.Add(virtualPad);virtualPad.BringToFront();
            Deactivate+=(_,_)=>virtualPad.ReleaseVirtual();
        }
		if (canPauseToMenu)
		{
			Deactivate += (_, _) =>
			{
				RecordSessionPlayTime();
				if (string.Equals(processName, "azahar", StringComparison.OrdinalIgnoreCase)) azaharPausedForMenu = true;
			};
			Activated += (_, _) =>
			{
				if (!closing && !launchFailed) sessionClock?.Start();
				if (azaharPausedForMenu) ResumeAzaharAfterFocus();
			};
		}
		Shown += (object param0, EventArgs param1) =>
		{
			StartGame();
		};
		FormClosed += (_, _) => { GameSessionSettingsService.Cleanup(this.temporaryConfigPath); SessionClosed?.Invoke(this); };
		FormClosing += (object sender, FormClosingEventArgs e) =>
		{
			if (!closing)
			{
				e.Cancel = true;
				CloseWithConfirmation();
			}
		};
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		RegisterHotKey(Handle, HOTKEY_ID, 0u, 123u);
	}

	protected override void OnHandleDestroyed(EventArgs e)
	{
		UnregisterHotKey(Handle, HOTKEY_ID);
		base.OnHandleDestroyed(e);
	}

	protected override void WndProc(ref Message message)
	{
		if (message.Msg == 786 && message.WParam.ToInt32() == HOTKEY_ID)
		{
			if (canPauseToMenu) ReturnToMenu(); else CloseWithConfirmation();
		}
		base.WndProc(ref message);
	}

	private void StartGame()
	{
		string baseDirectory = AppPaths.Root;
		string text = Path.Combine(baseDirectory, launcher);
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = text;
		processStartInfo.WorkingDirectory = Path.GetDirectoryName(text);
		processStartInfo.Arguments = arguments;
		processStartInfo.UseShellExecute = true;
		try
		{
			emulator = ProcessStarter(processStartInfo);
		}
		catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
		{
			ShowLaunchFailure(ex);
			return;
		}

		if (emulator != null && !string.IsNullOrWhiteSpace(historyRoot))
			GameLaunchHistoryService.TryRecordLaunch(historyRoot, historyTitle, DateTimeOffset.UtcNow);
		if (emulator != null)
		sessionClock = Stopwatch.StartNew();
		timer = new Timer
		{
			Interval = 250
		};
		timer.Tick += (object param0, EventArgs param1) =>
		{
			TryEmbed();
		};
		timer.Start();
	}

	internal bool CanPauseToMenu => canPauseToMenu;
	internal string GameTitle => historyTitle;
	internal string ProfileTitle => profileTitle;

	internal void ReturnToMenu()
	{
		if (!canPauseToMenu || closing || closeInProgress || IsDisposed)
			return;
		RecordSessionPlayTime();
		if (string.Equals(processName, "azahar", StringComparison.OrdinalIgnoreCase)) azaharPausedForMenu = true;
		Hide();
		SessionHidden?.Invoke(this);
	}

	internal void ResumeSession()
	{
		if (!canPauseToMenu || closing || IsDisposed)
			return;
		Show();
		WindowState = FormWindowState.Normal;
		Activate();
		sessionClock?.Start();
	}

	private async void ResumeAzaharAfterFocus()
	{
		if (!azaharPausedForMenu || !string.Equals(processName, "azahar", StringComparison.OrdinalIgnoreCase)) return;
		if (azaharResumePending) return;
		azaharResumePending = true;
		try
		{
			for (int attempt = 0; attempt < 8; attempt++)
			{
				await Task.Delay(250);
				if (closing || IsDisposed || !Visible || IsProcessExited(emulator)) return;
				IntPtr handle = embeddedWindowHandle;
				if (handle == IntPtr.Zero && !TryGetMainWindowHandle(emulator, out handle)) continue;
				FocusEmulator();
				if (!PostWindowMessage(handle, 0x0100, (IntPtr)0x73, IntPtr.Zero)) return; // Azahar's default Continue/Pause shortcut is F4.
				PostWindowMessage(handle, 0x0101, (IntPtr)0x73, IntPtr.Zero);
				azaharPausedForMenu = false;
				return;
			}
		}
		finally { azaharResumePending = false; }
	}

	internal void NotifyApplicationDeactivated() => RecordSessionPlayTime();

	internal void NotifyApplicationActivated()
	{
		if (!closing && !launchFailed && Visible) sessionClock?.Start();
	}

	internal void RequestClose() => CloseWithConfirmation();

	private void ShowLaunchFailure(Exception error)
	{
		launchFailed = true;
		string description = "Verifique se o emulador está instalado e se você tem permissão para executá-lo.\n\n" + error.Message;
		gamePanel.Controls.Clear();
		gamePanel.Controls.Add(new EmptyStatePanel("Não foi possível iniciar o jogo", description, "VOLTAR AO MENU", (_, _) => CloseWithConfirmation())
		{
			Dock = DockStyle.Fill,
			Width = gamePanel.ClientSize.Width
		});
	}

	private void TryEmbed()
	{
		if (emulator != null && IsProcessExited(emulator))
		{
			if (closeInProgress)
				return;
			RecordSessionPlayTime();
			closing = true;
			timer?.Stop();
			Close();
			return;
		}
		if (emulatorEmbedded)
		{
			ResizeEmbedded();
			return;
		}
		if (emulator == null)
			emulator = FindEmulator();
		if (TryGetMainWindowHandle(emulator, out IntPtr mainWindowHandle))
		{
			embeddedWindowHandle = mainWindowHandle;
			inputBridge = new GameInputBridge(AppPaths.Root, GetEmulatorProcessId, () => Form.ActiveForm == this && ContainsFocus, () => virtualPad?.VirtualActions ?? new bool[12], FocusEmulator);
			SetParent(mainWindowHandle, gamePanel.Handle);
			SetWindowLong(mainWindowHandle, -16, GetWindowLong(mainWindowHandle, -16) | 0x40000000);
			emulatorEmbedded = true;
			ResizeEmbedded();
			Resize += (object param0, EventArgs param1) =>
			{
				ResizeEmbedded();
			};
		}
	}

	private Process FindEmulator()
	{
		Process[] candidates = Process.GetProcessesByName(processName);
		Process selected = null;
		try
		{
			for (int index = candidates.Length - 1; index >= 0; index--)
			{
				if (TryGetMainWindowHandle(candidates[index], out _))
				{
					selected = candidates[index];
					break;
				}
			}
			return selected;
		}
		finally
		{
			foreach (Process candidate in candidates)
				if (!ReferenceEquals(candidate, selected))
					candidate.Dispose();
		}
	}

	private static bool IsProcessExited(Process process)
	{
		if (process == null)
			return true;
		try { return process.HasExited; }
		catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return true; }
	}

	private static bool TryGetMainWindowHandle(Process process, out IntPtr handle)
	{
		handle = IntPtr.Zero;
		if (IsProcessExited(process))
			return false;
		try
		{
			handle = process.MainWindowHandle;
			return handle != IntPtr.Zero;
		}
		catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
		{
			handle = IntPtr.Zero;
			return false;
		}
	}

	private int GetEmulatorProcessId()
	{
		if (IsProcessExited(emulator))
			return 0;
		try { return emulator.Id; }
		catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return 0; }
	}

	private void ResizeEmbedded()
	{
		if (embeddedWindowHandle != IntPtr.Zero && !IsProcessExited(emulator))
			MoveWindow(embeddedWindowHandle, 0, 0, gamePanel.ClientSize.Width, gamePanel.ClientSize.Height, repaint: true);
	}

	private async void CloseWithConfirmation()
	{
		await RequestCloseAsync();
	}

	internal async Task<bool> RequestCloseAsync()
	{
		if (closing || closeInProgress)
			return closing;
		if (launchFailed)
		{
			closing = true;
			timer?.Stop();
			Close();
			return true;
		}
		if (MessageBox.Show("Deseja fechar o jogo e voltar ao menu? O emulador tentará salvar o progresso ao encerrar.", "Voltar ao menu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			return false;

		closeInProgress = true;
		timer?.Stop();
		if (emulator == null)
			emulator = FindEmulator();

		if (emulator != null && !IsProcessExited(emulator))
		{
			try
			{
				if (emulator.CloseMainWindow())
				{
					try { await emulator.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
					catch (TimeoutException) { }
				}
			}
			catch (InvalidOperationException) { }

			if (!IsProcessExited(emulator))
			{
				DialogResult force = MessageBox.Show(
					"O emulador ainda está aberto. Forçar o encerramento pode perder o progresso desde a última gravação. Deseja forçar?",
					"Encerrar emulador",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning,
					MessageBoxDefaultButton.Button2);
				if (force != DialogResult.Yes)
				{
					closeInProgress = false;
					timer?.Start();
					return false;
				}

				try
				{
					emulator.Kill();
					await emulator.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
				}
				catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
				{
					if (!IsProcessExited(emulator))
					{
						MessageBox.Show("Não consegui confirmar o encerramento do emulador:\n\n" + ex.Message + "\n\nO jogo continuará aberto.", "Pokemons Play", MessageBoxButtons.OK, MessageBoxIcon.Error);
						closeInProgress = false;
						timer?.Start();
							return false;
					}
				}
			}
		}

		closing = true;
		RecordSessionPlayTime();
		closeInProgress = false;
		timer?.Stop();
		Close();
		return true;
	}

	private void RecordSessionPlayTime()
	{
		if (sessionClock == null || string.IsNullOrWhiteSpace(historyRoot))
			return;

		sessionClock.Stop();
		long elapsedSeconds = (long)sessionClock.Elapsed.TotalSeconds;
		long newSeconds = elapsedSeconds - recordedSessionSeconds;
		if (newSeconds > 0 && GameLaunchHistoryService.TryAddPlayTime(historyRoot, historyTitle, TimeSpan.FromSeconds(newSeconds)))
			recordedSessionSeconds = elapsedSeconds;
	}

    protected override void Dispose(bool disposing)
    {
        if(disposing){inputBridge?.Dispose();timer?.Stop();timer?.Dispose();emulator?.Dispose();}
        base.Dispose(disposing);
    }
    private void FocusEmulator()
    {
        IntPtr hwnd=embeddedWindowHandle;
        if(hwnd==IntPtr.Zero&&!TryGetMainWindowHandle(emulator,out hwnd))return;
        uint target=InputReader.GetWindowThreadProcessId(hwnd,out _),current=GetCurrentThreadId();
        bool attached=target!=current&&AttachThreadInput(current,target,true);
        try{SetFocus(hwnd);}finally{if(attached)AttachThreadInput(current,target,false);}
    }
}
