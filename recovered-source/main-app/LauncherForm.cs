using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class LauncherForm : Form
{
	private readonly string root;

	private Panel contentHost;

	private Label pageTitle;

	private Label pageHint;

	private NavItem libraryNav;

	private NavItem savesNav;

	private NavItem pokemonNav;

	private NavItem settingsNav;
	private ThemeButton resumeGameButton;
	private ThemeButton endGameButton;
	private GameHostForm gameSession;
    private Control pageUnderGame;
    private string pageTitleUnderGame;
    private string pageHintUnderGame;
    private bool finishCloseAfterSession;
    private Action layoutTopbar;
    private readonly AppUpdateService updates;
    private readonly UpdateNoticeButton updateNotice=new();
    private readonly System.Windows.Forms.Timer updateTimer=new(){Interval=6*60*60*1000};
    private AppUpdate availableUpdate;
    private bool checkingUpdate;
    internal bool UpdatesEnabled {get;set;}=true;
    internal string UpdateReadyStage {get;set;}

	public LauncherForm(string root)
	{
		LauncherForm launcherForm = this;
		this.root = root;
        updates=new AppUpdateService(root);
		Text = "Pokemons Play · "+AppRelease.Tag+" · Pixel";
		Width = 1280;
		Height = 820;
		MinimumSize = new Size(1000, 720);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = AppTheme.Background;
		FormBorderStyle = FormBorderStyle.Sizable;
		MaximizeBox = true;
		string text = Path.Combine(root, "Pokemons Play.ico");
		if (File.Exists(text))
		{
			Icon = new Icon(text);
		}
		BuildShell();
		Shown += (object param0, EventArgs param1) =>
		{
			string path = Path.Combine(root, "Settings", "input-presets.txt");
			launcherForm.Navigate(File.Exists(path) ? "library" : "settings");
		};
        Shown+=async(_,_)=>
        {
            if(UpdateReadyStage!=null)File.WriteAllText(Path.Combine(UpdateReadyStage,"ready"),AppRelease.Tag);
            if(UpdatesEnabled){await CheckForUpdates();updateTimer.Start();await System.Threading.Tasks.Task.Delay(5000);CleanCompletedUpdates();}
        };
        updateTimer.Tick+=async(_,_)=>await CheckForUpdates();
        FormClosed+=(_,_)=>{pageUnderGame?.Dispose();pageUnderGame=null;updateTimer.Dispose();updates.Dispose();};
		FormClosing += async (_, e) =>
		{
			if (finishCloseAfterSession) return;
			if (gameSession == null || gameSession.IsDisposed) return;
			e.Cancel = true;
			GameHostForm session = gameSession;
			if (await session.RequestCloseAsync() && !IsDisposed)
			{
				finishCloseAfterSession = true;
				Close();
			}
		};
		Deactivate += (_, _) => { if (gameSession?.Visible == true) gameSession.NotifyApplicationDeactivated(); };
		Activated += (_, _) => { if (gameSession?.Visible == true) gameSession.NotifyApplicationActivated(); };
	}

 private void BuildShell()
 {
  var topbar=new BufferedPanel{Dock=DockStyle.Top,Height=60,BackColor=AppTheme.TopBar};
  var brand=new TopBrandPanel{Dock=DockStyle.Left,Width=238};
  pageTitle=new Label{Text="Biblioteca",Font=AppTheme.CaptionBold,ForeColor=AppTheme.TextSecondary,Location=new Point(264,20),AutoSize=false,AutoEllipsis=true,Height=22};pageHint=new Label{Visible=false};
  var folder=new ThemeButton("Pasta de jogos 3DS",ButtonKind.Secondary){AutoSize=true,Anchor=AnchorStyles.Top|AnchorStyles.Right};updateNotice.AutoSize=true;updateNotice.Anchor=AnchorStyles.Top|AnchorStyles.Right;resumeGameButton=new ThemeButton("Jogo pausado · Retomar",ButtonKind.Primary){AutoSize=false,Width=180,Anchor=AnchorStyles.Top|AnchorStyles.Right,Visible=false,AccessibleName="Retomar o jogo pausado"};endGameButton=new ThemeButton("Encerrar jogo",ButtonKind.Secondary){AutoSize=true,Anchor=AnchorStyles.Top|AnchorStyles.Right,Visible=false,AccessibleName="Encerrar a sessão pausada"};resumeGameButton.Click+=(_,_)=>ResumeGameSession();endGameButton.Click+=(_,_)=>EndGameSession();topbar.Controls.AddRange(new Control[]{pageTitle,folder,updateNotice,resumeGameButton,endGameButton,brand});folder.Click+=(_,_)=>OpenThreeDsFolder();updateNotice.Click+=(_,_)=>OpenUpdates();layoutTopbar=()=>{int right=topbar.Width-24;folder.Left=right-folder.Width;folder.Top=10;right=folder.Left;if(endGameButton.Visible){right-=endGameButton.Width+8;endGameButton.Left=right;endGameButton.Top=10;}if(resumeGameButton.Visible){right-=resumeGameButton.Width+8;resumeGameButton.Left=right;resumeGameButton.Top=10;}updateNotice.Left=right-updateNotice.Width-8;updateNotice.Top=10;pageTitle.Width=Math.Max(1,updateNotice.Left-pageTitle.Left-16);pageTitle.Visible=pageTitle.Width>=80;};EventHandler toolbarLayout=(_,_)=>layoutTopbar();topbar.Resize+=toolbarLayout;updateNotice.SizeChanged+=toolbarLayout;resumeGameButton.SizeChanged+=toolbarLayout;endGameButton.SizeChanged+=toolbarLayout;layoutTopbar();
  libraryNav=new NavItem("Biblioteca",NavIcon.Library,(_,_)=>Navigate("library"));savesNav=new NavItem("Meus saves",NavIcon.Saves,(_,_)=>Navigate("saves"));pokemonNav=new NavItem("Banco Pokémon",NavIcon.Bank,(_,_)=>Navigate("pokemon"));settingsNav=new NavItem("Configurações",NavIcon.Settings,(_,_)=>Navigate("settings"));
  var navbar=new BufferedPanel{Dock=DockStyle.Bottom,Height=80,BackColor=AppTheme.TopBar};var items=new[]{libraryNav,savesNav,pokemonNav,settingsNav};navbar.Controls.AddRange(items);
  EventHandler layout=(_,_)=>{int itemWidth=(navbar.Width-32-24)/4;for(int i=0;i<4;i++)items[i].SetBounds(16+i*(itemWidth+8),8,itemWidth,64);};navbar.Resize+=layout;
  contentHost=new PixelGridPanel{Dock=DockStyle.Fill};Controls.Add(contentHost);Controls.Add(navbar);Controls.Add(topbar);layout(this,EventArgs.Empty);
 }

    internal void RegisterGameSession(GameHostForm session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (gameSession != null && !gameSession.IsDisposed)
            throw new InvalidOperationException("Já existe uma sessão de jogo aberta no launcher.");
        gameSession = session;
        session.SessionHidden = hidden =>
        {
            if (!ReferenceEquals(gameSession, hidden)) return;
            contentHost.Controls.Remove(hidden);
            RestorePageUnderGame();
            resumeGameButton.Text = "Retomar · " + hidden.GameTitle;
            resumeGameButton.AccessibleName = "Retomar " + hidden.GameTitle + ", perfil " + hidden.ProfileTitle;
            endGameButton.AccessibleName = "Encerrar " + hidden.GameTitle + ", perfil " + hidden.ProfileTitle;
            resumeGameButton.Visible = true;
            endGameButton.Visible = true;
            layoutTopbar?.Invoke();
            Activate();
            resumeGameButton.Focus();
        };
        session.SessionClosed = closed =>
        {
            if (!ReferenceEquals(gameSession, closed)) return;
            contentHost.Controls.Remove(closed);
            gameSession = null;
            resumeGameButton.Text = "Jogo pausado · Retomar";
            resumeGameButton.AccessibleName = "Retomar o jogo pausado";
            endGameButton.AccessibleName = "Encerrar a sessão pausada";
            resumeGameButton.Visible = false;
            endGameButton.Visible = false;
            layoutTopbar?.Invoke();
            RestorePageUnderGame();
        };
        PresentGameSession(session);
    }

    private void PresentGameSession(GameHostForm session)
    {
        if (pageUnderGame != null)
        {
            pageUnderGame.Dispose();
            pageUnderGame = null;
        }
        if (contentHost.Controls.Count > 0)
        {
            pageUnderGame = contentHost.Controls[0];
            contentHost.Controls.Remove(pageUnderGame);
            pageTitleUnderGame = pageTitle.Text;
            pageHintUnderGame = pageHint.Text;
        }
        session.TopLevel = false;
        session.FormBorderStyle = FormBorderStyle.None;
        session.Dock = DockStyle.Fill;
        if (!ReferenceEquals(session.Parent, contentHost)) contentHost.Controls.Add(session);
        pageTitle.Text = session.GameTitle;
        pageHint.Text = "Perfil " + session.ProfileTitle + " · a navegação do Pokemon Play continua disponível";
        session.Show();
        session.BringToFront();
    }

    private void RestorePageUnderGame()
    {
        Control page = pageUnderGame;
        if (page == null || IsDisposed || Disposing) return;
        pageUnderGame = null;
        pageTitle.Text = pageTitleUnderGame;
        pageHint.Text = pageHintUnderGame;
        if (page is LibraryView library) library.RefreshAfterGameSession();
        contentHost.Controls.Add(page);
        page.BringToFront();
    }

    private void ResumeGameSession()
    {
        GameHostForm session = gameSession;
        if (session == null || session.IsDisposed)
        {
            gameSession = null;
            resumeGameButton.Text = "Jogo pausado · Retomar";
            resumeGameButton.AccessibleName = "Retomar o jogo pausado";
            endGameButton.AccessibleName = "Encerrar a sessão pausada";
            resumeGameButton.Visible = false;
            endGameButton.Visible = false;
            layoutTopbar?.Invoke();
            return;
        }
        resumeGameButton.Visible = false;
        endGameButton.Visible = false;
        layoutTopbar?.Invoke();
        PresentGameSession(session);
        session.ResumeSession();
    }

    private void EndGameSession()
    {
        GameHostForm session = gameSession;
        if (session == null || session.IsDisposed)
        {
            gameSession = null;
            resumeGameButton.Text = "Jogo pausado · Retomar";
            resumeGameButton.AccessibleName = "Retomar o jogo pausado";
            endGameButton.AccessibleName = "Encerrar a sessão pausada";
            resumeGameButton.Visible = false;
            endGameButton.Visible = false;
            layoutTopbar?.Invoke();
            return;
        }
        session.RequestClose();
    }

    private async System.Threading.Tasks.Task CheckForUpdates()
    {
        if(checkingUpdate||updates.Repository.Length==0||IsDisposed)return;
        checkingUpdate=true;
        try{var result=await updates.CheckAsync();if(!IsDisposed){availableUpdate=result;ShowUpdateNotice();}}
        catch(Exception){/* Offline checks leave the app usable; the manual dialog reports errors. */}
        finally{checkingUpdate=false;}
    }
    private void ShowUpdateNotice()
    {
        updateNotice.Available=availableUpdate!=null;updateNotice.Kind=availableUpdate!=null?ButtonKind.Primary:ButtonKind.Secondary;
        updateNotice.Text=availableUpdate!=null?"Atualização disponível":"Atualizações";updateNotice.AccessibleName=availableUpdate!=null?"Atualização "+availableUpdate.Tag+" disponível; escolher se deseja atualizar":"Verificar atualizações do aplicativo";updateNotice.Invalidate();
    }
    private void OpenUpdates()
    {
        using var dialog=new AppUpdateDialog(updates,availableUpdate){BeforeRestart=()=>
        {
            try
            {
                SaveProfileService.EnsureEmulatorsClosed();
                foreach(Form form in Application.OpenForms)if(form is GameHostForm){MessageBox.Show(this,"Feche o jogo antes de atualizar.","Atualização",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
                foreach(Control control in contentHost.Controls)if(control is PokemonBankView bank&&!bank.PrepareForUpdate())return false;
                return true;
            }
            catch(Exception e){MessageBox.Show(this,e.Message,"Atualização",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
        }};
        if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.PreparedStage==null){availableUpdate=dialog.CurrentUpdate;ShowUpdateNotice();return;}
        string stage=dialog.PreparedStage;
        try
        {
            var start=new ProcessStartInfo(Path.Combine(stage,"PokemonPlayUpdater.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=stage};
            start.ArgumentList.Add(Environment.ProcessId.ToString());start.ArgumentList.Add(Path.GetFullPath(root));start.ArgumentList.Add(stage);
            if(Process.Start(start)==null)throw new IOException("Não foi possível iniciar o atualizador.");Close();
        }
        catch(Exception e){AppUpdateService.TryClean(stage);MessageBox.Show(this,e.Message,"Falha na atualização",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    private void CleanCompletedUpdates()
    {
        if(IsDisposed)return;
        try{foreach(string stage in Directory.GetDirectories(root,".pokemonplay-update-*")){string name=Path.GetFileName(stage);if(Guid.TryParseExact(name.Substring(20),"N",out _)&&(File.GetAttributes(stage)&FileAttributes.ReparsePoint)==0&&(File.Exists(Path.Combine(stage,"success"))||File.Exists(Path.Combine(stage,"failed"))))AppUpdateService.TryClean(stage);}}
        catch(IOException){}catch(UnauthorizedAccessException){}
    }

	public void OpenThreeDsFolder()
	{
		string text = Path.Combine(root, "Pokemon 3DS - Arquivos", "Roms");
		Directory.CreateDirectory(text);
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = "explorer.exe";
		processStartInfo.Arguments = "\"" + text.Replace("\"", "\\\"") + "\"";
		processStartInfo.UseShellExecute = true;
		Process.Start(processStartInfo);
	}

	public void Navigate(string page)
	{
		GameHostForm session = gameSession;
		if (session != null && !session.IsDisposed && session.Visible)
		{
			if (session.CanPauseToMenu) session.ReturnToMenu();
			else return;
		}
		pageTitle.Text = page == "saves" ? "Meus saves" : page == "pokemon" ? "Banco Pokémon" : page == "settings" ? "Configurações" : "Biblioteca";
		pageHint.Text = "Seus jogos e saves, em um só lugar";
		libraryNav.Active = page == "library";
		savesNav.Active = page == "saves";
		pokemonNav.Active = page == "pokemon";
		settingsNav.Active = page == "settings";
		foreach (Control control3 in contentHost.Controls)
		{
			control3.Dispose();
		}
		contentHost.Controls.Clear();
		Control control2;
		if (page == "pokemon")
		{
			control2 = new PokemonBankView(root);
		}
		else if (page == "saves")
		{
			control2 = new SaveManagerView(root);
		}
		else if (page == "settings")
		{
			control2 = new SettingsView(Path.Combine(root, "Settings", "input-presets.txt"));
		}
		else
		{
			control2 = new LibraryView(this, root);
		}
		control2.Dock = DockStyle.Fill;
		contentHost.Controls.Add(control2);
	}
}
