using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GameHostForm : Form
{
	private const int GWL_STYLE = -16;

	private const int WS_CHILD = 1073741824;
    private const int WS_POPUP = unchecked((int)0x80000000);
    internal EmbeddingAttemptTrace LastEmbeddingAttempt { get; private set; }

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
	private bool emulatorTopLevel;
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
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool AttachThreadInput(uint from,uint to,bool attach);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

	[DllImport("user32.dll")]
	private static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll",SetLastError=true)]
    private static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool GetClientRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool ClientToScreen(IntPtr window,ref NativePoint point);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int SetWindowLong(IntPtr handle, int index, int value);

	[DllImport("user32.dll", SetLastError = true)]
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
        if(string.Equals(processName,"retroarch",StringComparison.OrdinalIgnoreCase)&&File.Exists(temporaryConfigPath)) {
            try {
                string shortcut=File.ReadLines(temporaryConfigPath).FirstOrDefault(line=>line.StartsWith("input_enable_hotkey = ",StringComparison.Ordinal));
                if(shortcut!=null) {
                    string modifier=shortcut.Split('=',2)[1].Trim().Trim('"').ToUpperInvariant();
                    value2.Text=$"F12 pausa e volta à Biblioteca · Menu do RetroArch: segure {modifier} e pressione F1.";
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
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
            // Dock layout reserves Bottom controls before sizing the Fill viewport.
            // Keeping this z-order prevents the virtual pad from covering the game.
            Controls.Add(virtualPad);
            Deactivate+=(_,_)=>{if(!IsEmulatorForeground())virtualPad.ReleaseVirtual();};
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
        if (!canPauseToMenu || launchFailed || closing || closeInProgress || IsDisposed)
			return;
		RecordSessionPlayTime();
		if (string.Equals(processName, "azahar", StringComparison.OrdinalIgnoreCase)) azaharPausedForMenu = true;
		Hide();
		SessionHidden?.Invoke(this);
	}

	internal void ResumeSession()
	{
		if (!canPauseToMenu || launchFailed || closing || IsDisposed)
			return;
		Show();
		WindowState = FormWindowState.Normal;
		Activate();
		if(emulatorTopLevel)BeginInvoke((Action)FocusEmulator);
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

    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    internal sealed record WindowDpiTrace(long Context, int Awareness, uint Dpi);
    internal sealed record EmbeddingAttemptTrace(DateTimeOffset AtUtc, long ChildHwnd, long PanelHwnd, uint WindowPid, int ExpectedPid,
        uint OriginalStyle, uint RequestedStyle, int StyleReturn, int StyleError, long ParentReturn, int ParentError,
        long ParentBefore, long ParentAfter, long RootAfter, uint FinalStyle, bool IsPanelChild,
        WindowDpiTrace ChildDpiBefore, WindowDpiTrace PanelDpiBefore, WindowDpiTrace ChildDpiAfter, WindowDpiTrace PanelDpiAfter,
        bool Success, string Failure, bool RestoreAttempted, int RestoreReturn, int RestoreError, bool RestoreVerified, string RestoreReason);
    private static WindowDpiTrace ReadWindowDpi(IntPtr window){IntPtr context=GetWindowDpiAwarenessContext(window);return new(context.ToInt64(),GetAwarenessFromDpiAwarenessContext(context),GetDpiForWindow(window));}
    private void TryEmbed()
    {
        if (launchFailed) return;
        if (emulator != null && IsProcessExited(emulator))
        {
            if (closeInProgress) return;
            RecordSessionPlayTime();closing=true;timer?.Stop();Close();return;
        }
        if (emulatorEmbedded){ResizeEmbedded();return;}
        if(emulator==null){timer?.Stop();ShowLaunchFailure(new InvalidOperationException("O processo iniciado não forneceu uma identidade para incorporar a janela."));return;}
        if(!TryGetMainWindowHandle(emulator,out IntPtr mainWindowHandle))return;
        bool keepForegroundWindow=string.Equals(processName,"retroarch",StringComparison.OrdinalIgnoreCase);
        bool prepared=keepForegroundWindow
            ?TryPrepareForegroundWindow(mainWindowHandle,out Exception failure)
            :TryEmbedOwnedWindow(mainWindowHandle,out failure);
        if(!prepared){
            timer?.Stop();embeddedWindowHandle=IntPtr.Zero;emulatorEmbedded=false;ShowLaunchFailure(failure);return;
        }
        embeddedWindowHandle=mainWindowHandle;
        emulatorTopLevel=keepForegroundWindow;
        inputBridge=new GameInputBridge(AppPaths.Root,GetEmulatorProcessId,()=>IsInputHostFocused,
            ()=>virtualPad?.VirtualActions??new bool[12],FocusEmulator);
        if(emulatorTopLevel&&virtualPad!=null)virtualPad.MouseDown+=(_,eventArgs)=>{if(eventArgs.Button==MouseButtons.Left)FocusEmulator();};
        emulatorEmbedded=true;ResizeEmbedded();Resize+=(_,_)=>ResizeEmbedded();Move+=(_,_)=>ResizeEmbedded();VisibleChanged+=(_,_)=>ResizeEmbedded();
        if(emulatorTopLevel&&Visible&&WindowState!=FormWindowState.Minimized)SetForegroundWindow(mainWindowHandle);
    }
    private bool TryPrepareForegroundWindow(IntPtr window,out Exception failure)
    {
        const int WS_CHILD_STYLE=0x40000000,WS_POPUP_STYLE=unchecked((int)0x80000000),WS_CAPTION_STYLE=0x00C00000,
            WS_THICKFRAME_STYLE=0x00040000,WS_SYSMENU_STYLE=0x00080000,WS_MINIMIZEBOX_STYLE=0x00020000,WS_MAXIMIZEBOX_STYLE=0x00010000;
        GetWindowThreadProcessId(window,out uint pid);int expectedPid=GetEmulatorProcessId();failure=null;int original=0;bool styleChanged=false;
        try{
            if(expectedPid<=0||pid!=expectedPid)throw new InvalidOperationException("A identidade da janela não corresponde ao processo iniciado.");
            if(GetAncestor(window,1)!=GetDesktopWindow())throw new InvalidOperationException("A janela do RetroArch não tem a área de trabalho como parent antes do posicionamento.");
            original=GetWindowLong(window,GWL_STYLE);int readError=Marshal.GetLastPInvokeError();
            if(original==0&&readError!=0)throw new System.ComponentModel.Win32Exception(readError,"Não foi possível ler o estilo da janela do RetroArch.");
            int requested=(original&~(WS_CHILD_STYLE|WS_CAPTION_STYLE|WS_THICKFRAME_STYLE|WS_SYSMENU_STYLE|WS_MINIMIZEBOX_STYLE|WS_MAXIMIZEBOX_STYLE))|WS_POPUP_STYLE;
            int styleReturn=SetWindowLong(window,GWL_STYLE,requested);int styleError=Marshal.GetLastPInvokeError();
            if(styleReturn==0&&styleError!=0)throw new System.ComponentModel.Win32Exception(styleError,"Não foi possível preparar a janela sem borda do RetroArch.");
            styleChanged=true;
            if(GetWindowLong(window,GWL_STYLE)!=requested)throw new InvalidOperationException("O estilo superior sem borda não foi confirmado.");
            if(!SetWindowPos(window,IntPtr.Zero,0,0,0,0,0x27))throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(),"Não foi possível atualizar os limites sem borda do RetroArch.");
            if(!MoveTopLevelToGamePanel(window))throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(),"Não foi possível posicionar o RetroArch na área do jogo.");
            return true;
        }catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception or EntryPointNotFoundException){
            failure=error;
            GetWindowThreadProcessId(window,out uint currentPid);
            if(styleChanged&&currentPid==expectedPid&&GetEmulatorProcessId()==expectedPid)SetWindowLong(window,GWL_STYLE,original);
            return false;
        }
    }
    private bool TryEmbedOwnedWindow(IntPtr window,out Exception failure)
    {
        IntPtr panel=gamePanel.Handle;int expectedPid=GetEmulatorProcessId();GetWindowThreadProcessId(window,out uint pid);
        int original=0,requested=0,styleReturn=0,styleError=0,parentError=0,restoreReturn=0,restoreError=0;
        IntPtr parentReturn=IntPtr.Zero,parentBefore=GetAncestor(window,1),parentAfter=parentBefore,rootAfter=IntPtr.Zero;
        uint finalStyle=0;bool success=false,styleChanged=false,restoreAttempted=false,isPanelChild=false;
        WindowDpiTrace childBefore=null,panelBefore=null,childAfter=null,panelAfter=null;failure=null;
        try{
            GetWindowThreadProcessId(panel,out uint panelPid);
            if(expectedPid<=0||pid!=expectedPid||panelPid!=Environment.ProcessId)throw new InvalidOperationException("A identidade da janela não corresponde ao processo iniciado e ao painel deste aplicativo.");
            childBefore=ReadWindowDpi(window);panelBefore=ReadWindowDpi(panel);
            original=GetWindowLong(window,GWL_STYLE);int readError=Marshal.GetLastPInvokeError();
            if(original==0&&readError!=0)throw new System.ComponentModel.Win32Exception(readError,"Não foi possível ler o estilo da janela.");
            requested=(original&~WS_POPUP)|WS_CHILD;
            styleReturn=SetWindowLong(window,GWL_STYLE,requested);styleError=Marshal.GetLastPInvokeError();
            if(styleReturn==0&&styleError!=0)throw new System.ComponentModel.Win32Exception(styleError,"Não foi possível preparar o estilo da janela.");
            styleChanged=true;
            if(GetWindowLong(window,GWL_STYLE)!=requested)throw new InvalidOperationException("O estilo da janela não confirmou a preparação para incorporação.");
            parentReturn=SetParent(window,panel);parentError=Marshal.GetLastPInvokeError();
            parentAfter=GetAncestor(window,1);rootAfter=GetAncestor(window,2);isPanelChild=IsChild(panel,window);
            finalStyle=unchecked((uint)GetWindowLong(window,GWL_STYLE));GetWindowThreadProcessId(window,out uint finalPid);
            if(parentError!=0&&parentReturn==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(parentError,"Não foi possível incorporar a janela ao painel.");
            if(parentAfter!=panel||rootAfter!=GetAncestor(panel,2)||!isPanelChild||finalPid!=expectedPid||(finalStyle&0x40000000)==0||(finalStyle&0x80000000)!=0)
                throw new InvalidOperationException("A janela não confirmou vínculo, identidade e estilo de incorporação.");
            success=true;return true;
        }catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception or EntryPointNotFoundException){
            failure=error;
            // Restore only the same owned HWND if parenting did not take effect; never touch third-party windows.
            GetWindowThreadProcessId(window,out uint restorePid);parentAfter=GetAncestor(window,1);
            if(styleChanged&&!IsProcessExited(emulator)&&GetEmulatorProcessId()==expectedPid&&restorePid==expectedPid&&parentAfter!=panel){
                restoreAttempted=true;restoreReturn=SetWindowLong(window,GWL_STYLE,original);restoreError=Marshal.GetLastPInvokeError();
            }
            return false;
        }finally{
            parentAfter=GetAncestor(window,1);rootAfter=GetAncestor(window,2);isPanelChild=IsChild(panel,window);finalStyle=unchecked((uint)GetWindowLong(window,GWL_STYLE));
            try{childAfter=ReadWindowDpi(window);panelAfter=ReadWindowDpi(panel);}catch(EntryPointNotFoundException){}
            LastEmbeddingAttempt=new(DateTimeOffset.UtcNow,window.ToInt64(),panel.ToInt64(),pid,expectedPid,unchecked((uint)original),unchecked((uint)requested),styleReturn,styleError,parentReturn.ToInt64(),parentError,parentBefore.ToInt64(),parentAfter.ToInt64(),rootAfter.ToInt64(),finalStyle,isPanelChild,childBefore,panelBefore,childAfter,panelAfter,success,failure?.Message,restoreAttempted,restoreReturn,restoreError,restoreAttempted&&finalStyle==unchecked((uint)original),restoreAttempted?"Parenting did not take effect; restore original style of same owned HWND":"No restoration: no style change, ownership lost, or window already parented to panel");
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
		if (embeddedWindowHandle == IntPtr.Zero || IsProcessExited(emulator))return;
		if(emulatorTopLevel){
			bool shouldShow=Visible&&WindowState!=FormWindowState.Minimized;
			if(!shouldShow){ShowWindow(embeddedWindowHandle,0);return;}
            if(!MoveTopLevelToGamePanel(embeddedWindowHandle))return;
			ShowWindow(embeddedWindowHandle,8);
			return;
		}
		MoveWindow(embeddedWindowHandle,0,0,gamePanel.ClientSize.Width,gamePanel.ClientSize.Height,repaint:true);
	}
    private bool MoveTopLevelToGamePanel(IntPtr window)
    {
        Rectangle bounds=gamePanel.RectangleToScreen(gamePanel.ClientRectangle);
        if(bounds.Width<=0||bounds.Height<=0||!GetWindowRect(window,out NativeRect outer)||!GetClientRect(window,out NativeRect client))return false;
        NativePoint clientOrigin=default;if(!ClientToScreen(window,ref clientOrigin))return false;
        int clientWidth=client.Right-client.Left,clientHeight=client.Bottom-client.Top;
        int outerWidth=outer.Right-outer.Left,outerHeight=outer.Bottom-outer.Top;
        int insetLeft=clientOrigin.X-outer.Left,insetTop=clientOrigin.Y-outer.Top;
        int nonClientWidth=outerWidth-clientWidth,nonClientHeight=outerHeight-clientHeight;
        if(clientWidth<=0||clientHeight<=0||nonClientWidth<0||nonClientHeight<0)return false;
        int targetLeft=bounds.Left-insetLeft,targetTop=bounds.Top-insetTop,targetWidth=bounds.Width+nonClientWidth,targetHeight=bounds.Height+nonClientHeight;
        if(outer.Left==targetLeft&&outer.Top==targetTop&&outerWidth==targetWidth&&outerHeight==targetHeight)return true;
        return MoveWindow(window,targetLeft,targetTop,targetWidth,targetHeight,repaint:true);
    }

	private async void CloseWithConfirmation()
	{
		await RequestCloseAsync();
	}

	internal async Task<bool> RequestCloseAsync()
	{
		if (closing || closeInProgress)
			return closing;
		if (launchFailed && (emulator == null || IsProcessExited(emulator)))
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
					if (!launchFailed) timer?.Start();
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
							if (!launchFailed) timer?.Start();
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
    internal bool IsInputHostFocused
    {
        get
        {
            if (!Visible || closing || launchFailed) return false;
            InputReader.GetWindowThreadProcessId(InputReader.GetForegroundWindow(), out uint foregroundPid);
            return foregroundPid == Environment.ProcessId &&
                ((Form.ActiveForm == this && ContainsFocus) || virtualPad?.Capture == true);
        }
    }
    private void FocusEmulator()
    {
        IntPtr hwnd=embeddedWindowHandle;
        if(hwnd==IntPtr.Zero&&!TryGetMainWindowHandle(emulator,out hwnd))return;
        if(emulatorTopLevel)SetForegroundWindow(hwnd);
        uint target=InputReader.GetWindowThreadProcessId(hwnd,out _),current=GetCurrentThreadId();
        bool attached=target!=current&&AttachThreadInput(current,target,true);
        try{SetFocus(hwnd);}finally{if(attached)AttachThreadInput(current,target,false);}
    }
    private bool IsEmulatorForeground()
    {
        if(!emulatorTopLevel||embeddedWindowHandle==IntPtr.Zero)return false;
        InputReader.GetWindowThreadProcessId(InputReader.GetForegroundWindow(),out uint foregroundPid);
        return foregroundPid==(uint)GetEmulatorProcessId();
    }
}
