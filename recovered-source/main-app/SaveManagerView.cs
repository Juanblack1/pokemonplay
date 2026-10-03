using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class SaveManagerView : BufferedPanel
{
	private readonly string root;

	private readonly string savesDir;

	private readonly SectionCard listCard;

	private readonly SectionCard detailCard;

	private readonly FlowLayoutPanel gameList;

	private readonly FlowLayoutPanel fileList;

	private readonly Label status;

	private readonly List<GameInfo> games;

	private readonly FirebaseCloudSaveService cloud;

	private GameInfo selected;

	private bool cloudBusy;

	private Label cloudMessage;

	private ThemeButton cloudLogin;

	private ThemeButton cloudUpload;

	private ThemeButton cloudDownload;

 public SaveManagerView(string root)
 {
  this.root=root;savesDir=Path.Combine(root,"Saves");games=GameCatalog.SaveGames(root);selected=games.Count==0?null:games[0];cloud=new FirebaseCloudSaveService(root);Dock=DockStyle.Fill;BackColor=Color.Transparent;
  var canvas=new PixelGridPanel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=AppTheme.Background};
  listCard=new SectionCard("Seus jogos","Escolha uma aventura."){Width=248,Height=680,Location=new Point(24,16)};
  detailCard=new SectionCard("Progresso do jogo","Backup automático ao iniciar jogos · nuvem opcional."){Width=700,Height=810,Location=new Point(288,16)};
  gameList=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,BackColor=AppTheme.Surface,Location=new Point(12,88),Size=new Size(224,570),Padding=new Padding(4)};
  listCard.Controls.Add(gameList);
  fileList=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,BackColor=AppTheme.Surface,Location=new Point(24,444),Height=136,Width=640,Padding=new Padding(0)};
  status=new Label{ForeColor=AppTheme.TextMuted,Font=AppTheme.Caption,AutoSize=false,AutoEllipsis=true,Location=new Point(24,770),Height=24,Width=620};
  canvas.Controls.AddRange(new Control[]{listCard,detailCard});Controls.Add(canvas);Controls.Add(new PixelHeader("Meus saves","Organize seu progresso e mantenha uma cópia segura.",100));
  PopulateGameList();BuildDetail();canvas.Resize+=(_,_)=>LayoutCanvas(canvas.ClientSize.Width);LayoutCanvas(1000);
 }
 private void LayoutCanvas(int width)
 {
  detailCard.Width=Math.Max(430,width-332);listCard.Width=248;detailCard.Left=288;fileList.Width=detailCard.Width-48;status.Width=detailCard.Width-48;
  foreach(Control control in fileList.Controls)control.Width=Math.Max(150,fileList.Width-24);
  foreach(Control control in detailCard.Controls)
  {
   if((string)control.Tag=="detail-text")control.Width=Math.Max(180,detailCard.Width-172);
   if((string)control.Tag=="detail-actions")control.Width=Math.Max(180,detailCard.Width-172);
   if((string)control.Tag=="cloud-actions")control.Width=detailCard.Width-48;
   if((string)control.Tag=="cloud-message")control.Width=detailCard.Width-48;
			if((string)control.Tag=="backup-info")control.Width=detailCard.Width-48;
  }
  if(profileSummary!=null&&!profileSummary.IsDisposed)profileSummary.Width=detailCard.Width-56;
 }


	private void PopulateGameList()
	{
		while(gameList.Controls.Count>0)gameList.Controls[0].Dispose();
        gameList.Controls.Clear();
		foreach (GameInfo game in games)
		{
			GameInfo game2 = game;
			EventHandler click = (object param0, EventArgs param1) =>
			{
				SelectGame(game);
			};
			SaveListItem saveListItem = new SaveListItem(game2, root, click);
			saveListItem.Selected = game == selected;
			gameList.Controls.Add(saveListItem);
		}
	}

	private void SelectGame(GameInfo game)
	{
		selected = game;
		PopulateGameList();
		BuildDetail();
	}

	private void ClearDetail()
	{
		List<Control> list = new List<Control>();
		foreach (Control control2 in detailCard.Controls)
		{
			if (control2 != fileList && control2 != status && (string)control2.Tag != "card-header")
			{
				list.Add(control2);
			}
		}
		foreach (Control item in list)
		{
			detailCard.Controls.Remove(item);
			if (item is PictureBox { Image: not null } pictureBox)
			{
				pictureBox.Image.Dispose();
			}
			item.Dispose();
		}
		while(fileList.Controls.Count>0)fileList.Controls[0].Dispose();
        fileList.Controls.Clear();
	}

 private void BuildDetail()
 {
  try { BuildDetailCore(); }
  catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or System.Security.SecurityException) {
   ClearDetail();profileControls=null;profileSummary=null;cloudLogin=null;cloudUpload=null;cloudDownload=null;cloudMessage=null;
   status.Text="Leitura interrompida. Os arquivos existentes foram preservados.";
   var title=new Label {Text=selected.Title,Font=AppTheme.Section,ForeColor=AppTheme.Text,Location=new Point(24,88),Size=new Size(detailCard.Width-48,48),Tag="cloud-message"};
   string guidance="Não foi possível conferir os saves de "+selected.Title+". Verifique o acesso à pasta Saves e ao arquivo Settings/SaveProfiles/"+selected.SaveFolderName+".json. Depois, tente novamente. Você pode selecionar outro jogo. Nenhum perfil foi redefinido.";
   var message=new Label {Text=guidance,AccessibleName="Falha na leitura dos saves de "+selected.Title,AccessibleDescription=guidance,Font=AppTheme.Body,ForeColor=AppTheme.Red,Location=new Point(24,148),Size=new Size(detailCard.Width-48,132),Tag="cloud-message"};
   var retry=new ThemeButton("Tentar novamente",ButtonKind.Secondary){Location=new Point(24,296),AutoSize=true,AccessibleDescription="Conferir novamente os perfis e saves deste jogo sem redefinir seus arquivos."};
   retry.Click+=(_,_)=>BuildDetail();
   detailCard.Controls.AddRange(new Control[]{title,message,retry,status});
   LayoutCanvas(detailCard.Parent?.ClientSize.Width??1000);
  }
 }
 private void BuildDetailCore()
 {
  ClearDetail();if(selected==null)return;
  var cover=new PictureBox{Location=new Point(24,88),Size=new Size(108,148),SizeMode=PictureBoxSizeMode.Zoom,BackColor=AppTheme.Surface};
  cover.Image=GameCoverService.Load(root,selected.Cover);
  Label LabelAt(string text,Font font,Color color,int x,int y,int height,string tag="detail-text")=>new Label{Text=text,Font=font,ForeColor=color,BackColor=AppTheme.Surface,Location=new Point(x,y),Size=new Size(Math.Max(180,detailCard.Width-x-24),height),AutoEllipsis=true,Tag=tag};
  var title=LabelAt(selected.Title,AppTheme.Section,AppTheme.Text,152,88,30);
  var subtitle=LabelAt(selected.Subtitle,AppTheme.Body,AppTheme.TextMuted,152,126,24);
  var folder=LabelAt("Pasta: "+selected.SaveFolderName,AppTheme.Caption,AppTheme.TextMuted,152,156,24);
  var actions=new FlowLayoutPanel{Location=new Point(152,188),Width=detailCard.Width-176,Height=104,BackColor=AppTheme.Surface,Tag="detail-actions"};
  ThemeButton Action(string text,ButtonKind kind,EventHandler click){var b=new ThemeButton(text,kind){AutoSize=true,Margin=new Padding(0,0,8,8)};b.Click+=click;return b;}
  actions.Controls.Add(Action("Abrir pasta",ButtonKind.Secondary,(_,_)=>OpenSelected()));actions.Controls.Add(Action("Backup ZIP",ButtonKind.Primary,(_,_)=>BackupSelected()));actions.Controls.Add(Action("Restaurar backup",ButtonKind.Secondary,(_,_)=>RestoreSelected()));actions.Controls.Add(Action("Todos os saves",ButtonKind.Ghost,(_,_)=>OpenAll()));
  string latestBackup=SaveBackupService.LatestAutomaticBackup(root,selected.SaveFolderName);
  string backupText=latestBackup==null?"Backup automático deste perfil ainda não criado · será feito ao iniciar o jogo.":"Último backup automático · "+File.GetLastWriteTime(latestBackup).ToString("dd/MM/yyyy HH:mm");
  var backupInfo=LabelAt(backupText,AppTheme.Caption,latestBackup==null?AppTheme.TextMuted:AppTheme.Green,24,384,22,"backup-info");
  var filesTitle=LabelAt("Arquivos de save",AppTheme.BodyBold,AppTheme.Text,24,412,24,"files-heading");
  detailCard.Controls.AddRange(new Control[]{cover,title,subtitle,folder,actions,backupInfo,filesTitle,fileList,status});
  BuildProfileControls();
  string[] files=Directory.GetFiles(SelectedFolder(),"*",SearchOption.AllDirectories);
  if(files.Length==0)fileList.Controls.Add(new Label{Text="Nenhum save nesta pasta. Seu progresso aparecerá aqui depois de salvar dentro do jogo.",Font=AppTheme.Body,ForeColor=AppTheme.TextMuted,AutoSize=false,Size=new Size(400,64),Padding=new Padding(0,12,0,0)});
  else {Array.Sort(files,StringComparer.OrdinalIgnoreCase);foreach(string file in files)fileList.Controls.Add(new SaveFileRow(new FileInfo(file)));}
  status.ForeColor=AppTheme.TextMuted;
  status.Text=files.Length==0?"Pasta pronta para receber seu progresso.":files.Length+" arquivo(s) nesta pasta.";
  BuildCloudControls();LayoutCanvas(detailCard.Parent?.ClientSize.Width??1000);
 }
 private void BuildCloudControls()
 {
  var heading=new Label{Text="Backup na nuvem",Font=AppTheme.BodyBold,ForeColor=AppTheme.Text,Location=new Point(24,590),AutoSize=true};
  cloudMessage=new Label{Text=cloud.IsSignedIn?SignedInCloudMessage(cloud.GoogleEmail,selected.Title,ActiveProfileName()):"Conecte sua conta Google para acessar seus saves em outro computador.",Font=AppTheme.Body,ForeColor=AppTheme.TextMuted,Location=new Point(24,622),Size=new Size(detailCard.Width-48,42),Tag="cloud-message"};
  var actions=new FlowLayoutPanel{Location=new Point(24,678),Size=new Size(detailCard.Width-48,88),BackColor=AppTheme.Surface,Tag="cloud-actions"};
  cloudLogin=new ThemeButton(cloud.IsSignedIn?"Sair da conta":"Entrar com Google",ButtonKind.Secondary){AutoSize=true,Margin=new Padding(0,0,8,8)};
  cloudUpload=new ThemeButton("Enviar save",ButtonKind.Primary){AutoSize=true,Margin=new Padding(0,0,8,8),Enabled=cloud.IsSignedIn};
  cloudDownload=new ThemeButton("Restaurar save",ButtonKind.Secondary){AutoSize=true,Margin=new Padding(0,0,8,8),Enabled=cloud.IsSignedIn};
  cloudLogin.Click+=async(_,_)=>await RunCloudAction("login");
  cloudUpload.Click+=async(_,_)=>await RunCloudAction("upload");
  cloudDownload.Click+=async(_,_)=>await RunCloudAction("download");
  actions.Controls.AddRange(new Control[]{cloudLogin,cloudUpload,cloudDownload});detailCard.Controls.AddRange(new Control[]{heading,cloudMessage,actions});
 }


	internal static string CloudAccountName(string email)
		=> string.IsNullOrWhiteSpace(email) ? "sua conta Google" : email;

	internal static string SignedInCloudMessage(string email, string gameName, string profileName)
		=> $"Google: {CloudAccountName(email)} · Save: {gameName} / perfil {profileName}. Cópias por perfil no Firebase privado.";

	internal static string BuildCloudActionConfirmation(bool restore, string gameName, string profileName, string email)
	{
		string context = $"{gameName} · perfil {profileName}";
		return restore
			? $"Restaurar o save de {context} da conta {CloudAccountName(email)}? O arquivo será validado para este jogo antes da troca, e o progresso local será guardado como backup."
			: $"Enviar o save de {context} para {CloudAccountName(email)}? O ZIP será validado para este jogo antes do envio e substituirá a cópia existente na nuvem.";
	}

	internal static string BuildCloudSaveUploadConfirmation(CloudBankInfo remote, string gameName, string profileName, string email)
	{
		string context = $"{gameName} · perfil {profileName}";
		if (remote == null || !remote.Exists)
			return $"Enviar o save de {context} para {CloudAccountName(email)}? Esta conta ainda não tem uma cópia deste perfil. O ZIP será validado para este jogo antes do envio.";

		string updated = remote.UpdatedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "data indisponível";
		string size = remote.CompressedBytes > 0 ? $"{remote.CompressedBytes / 1024d / 1024d:0.00} MB" : "tamanho indisponível";
		return $"Enviar o save de {context} para {CloudAccountName(email)}? Isso substituirá a cópia confirmada, atualizada em {updated} ({size}), somente se ela continuar igual. O ZIP será validado para este jogo.";
	}

	internal static string BuildCloudSaveRestoreConfirmation(CloudBankInfo remote, string gameName, string profileName, string email)
	{
		string context = $"{gameName} · perfil {profileName}";
		if (remote == null || !remote.Exists)
			return $"Esta conta não tem um save de {context} para restaurar.";

		string updated = remote.UpdatedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "data indisponível";
		string size = remote.CompressedBytes > 0 ? $"{remote.CompressedBytes / 1024d / 1024d:0.00} MB" : "tamanho indisponível";
		return $"Restaurar o save de {context} da conta {CloudAccountName(email)}? A cópia confirmada foi atualizada em {updated} ({size}). Ela será validada para este jogo e só será aplicada se continuar igual; o save local será guardado como backup.";
	}

	private string ActiveProfileName()
	{
		if (selected.Generation > 5) return "Principal";
		SaveProfileState state = SaveProfileService.Load(root, selected.SaveFolderName);
		return state.Profiles.FirstOrDefault(profile => profile.Id == state.ActiveId)?.Name ?? "Principal";
	}

	private async Task RunCloudAction(string action)
	{
		if (cloudBusy)
		{
			return;
		}
		cloudBusy = true;
		gameList.Enabled = false;
        if(profileControls != null) profileControls.Enabled = false;
		ThemeButton themeButton = cloudLogin;
		ThemeButton themeButton2 = cloudUpload;
		bool flag = (cloudDownload.Enabled = false);
		flag = (themeButton2.Enabled = flag);
		themeButton.Enabled = flag;
		CloudBankInfo expectedSnapshot = null;
		try
		{
			string gameId = SaveProfileService.CloudId(root, selected.SaveFolderName);
			string folder = SelectedFolder();
			if (action == "upload")
			{
				cloudMessage.Text = "Consultando a cópia atual antes do envio...";
				expectedSnapshot = await cloud.ReadCloudSaveInfoAsync(gameId);
				string confirmation = BuildCloudSaveUploadConfirmation(expectedSnapshot, selected.Title, ActiveProfileName(), cloud.GoogleEmail);
				if (MessageBox.Show(FindForm(), confirmation, "Enviar save", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
				{
					cloudMessage.Text = "Envio cancelado. Nenhuma cópia na nuvem foi alterada.";
					return;
				}
			}
			else if (action == "download")
			{
				cloudMessage.Text = "Consultando a cópia atual antes da restauração...";
				expectedSnapshot = await cloud.ReadCloudSaveInfoAsync(gameId);
				if (!expectedSnapshot.Exists)
				{
					cloudMessage.Text = "Esta conta ainda não tem um save deste jogo e perfil para restaurar.";
					cloudMessage.ForeColor = AppTheme.TextMuted;
					return;
				}
				string confirmation = BuildCloudSaveRestoreConfirmation(expectedSnapshot, selected.Title, ActiveProfileName(), cloud.GoogleEmail);
				if (MessageBox.Show(FindForm(), confirmation, "Restaurar save", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
				{
					cloudMessage.Text = "Restauração cancelada. O save local continua intacto.";
					return;
				}
			}

			if (action == "login")
			{
				if (cloud.IsSignedIn)
				{
					cloud.SignOut();
					cloudMessage.Text = "Conta desconectada. Seus saves locais continuam disponíveis.";
				}
				else
				{
					cloudMessage.Text = "Conclua o login no navegador em até 3 minutos.";
					await cloud.SignInAsync();
					if (!IsDisposed)
					{
						cloudMessage.Text = SignedInCloudMessage(cloud.GoogleEmail, selected.Title, ActiveProfileName());
					}
				}
			}
			else if (action == "upload")
			{
				cloudMessage.Text = "Enviando o save...";
				await cloud.UploadFolderAsync(gameId, folder, selected, expectedSnapshot);
				if (!IsDisposed)
				{
					cloudMessage.Text = $"Save enviado para {CloudAccountName(cloud.GoogleEmail)} no Firebase. Use a mesma conta para restaurá-lo em outro computador.";
				}
			}
			else
			{
				cloudMessage.Text = "Validando e restaurando o save...";
				await cloud.DownloadFolderAsync(gameId, folder, selected, expectedSnapshot);
				if (!IsDisposed)
				{
					cloudMessage.Text = "Save restaurado. Se havia arquivos locais, a cópia anterior está em Saves/Backups/Automaticos.";
				}
			}
			if (!IsDisposed)
			{
				cloudMessage.ForeColor = AppTheme.Green;
			}
		}
		catch (Exception ex)
		{
			if (!IsDisposed)
			{
				cloudMessage.Text = ex.Message;
				cloudMessage.ForeColor = AppTheme.Red;
			}
		}
		finally
		{
			cloudBusy = false;
			if (!IsDisposed)
			{
				gameList.Enabled = true;
                if(profileControls != null) profileControls.Enabled = true;
				cloudLogin.Enabled = true;
				cloudLogin.Text = (cloud.IsSignedIn ? "SAIR DA CONTA" : "ENTRAR COM GOOGLE");
				ThemeButton themeButton3 = cloudUpload;
				flag = (cloudDownload.Enabled = cloud.IsSignedIn);
				themeButton3.Enabled = flag;
			}
		}
	}

    private FlowLayoutPanel profileControls;
    private Label profileSummary;
    private void BuildProfileControls()
    {
        profileSummary = null;
        profileControls = new FlowLayoutPanel { Location = new Point(24, 300), Width = detailCard.Width - 48, Height = 82, Tag = "cloud-actions" };
        detailCard.Controls.Add(profileControls);
        if (selected.Generation > 5)
        {
            profileControls.Controls.Add(new Label { Text = "Perfis disponíveis para GBA e DS.", AutoSize = true, ForeColor = AppTheme.TextMuted });
            return;
        }
        var state = SaveProfileService.Load(root, selected.SaveFolderName);
        var label = new Label { Text = "Perfil ativo", AutoSize = true, ForeColor = AppTheme.Text, Margin = new Padding(0, 10, 8, 0) };
        var picker = new ThemeSelect { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var profile in state.Profiles) picker.Items.Add(profile);
        picker.SelectedIndex = state.Profiles.FindIndex(p => p.Id == state.ActiveId);
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0) return;
            SaveProfile profile = state.Profiles[picker.SelectedIndex];
            profileSummary.Text = BuildProfileSummary(root, selected, profile.Id);
            try { SaveProfileService.Activate(root, selected.SaveFolderName, profile.Id); BuildDetail(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Trocar perfil"); BuildDetail(); }
        };
        var create = new ThemeButton("Novo perfil", ButtonKind.Primary) { AutoSize = true };
        create.Click += (_, _) =>
        {
            try
            {
                SaveProfileService.EnsureEmulatorsClosed();
                using var dialog = new SaveProfileDialog();
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string import = null;
                if (dialog.Mode == 2)
                {
                    using var file = new OpenFileDialog { Title = "Importar save deste jogo", Filter = "Save normal|*.sav;*.srm;*.dsv;*.bin|Todos os arquivos|*.*" };
                    if (file.ShowDialog(this) != DialogResult.OK) return;
                    import = file.FileName;
                }
                SaveProfileService.Create(root, selected, dialog.ProfileName, dialog.Mode == 1, import);
                BuildDetail();
                status.Text = "Perfil criado e selecionado. Use Jogar na Biblioteca para iniciar esta aventura.";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Novo perfil"); }
        };
        var rename = new ThemeButton("Renomear perfil", ButtonKind.Secondary) { AutoSize = true };
        rename.Click += (_, _) =>
        {
            if (picker.SelectedIndex < 0) return;
            SaveProfile profile = state.Profiles[picker.SelectedIndex];
            using var dialog = new RenameSaveProfileDialog(profile.Name);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                SaveProfileService.Rename(root, selected.SaveFolderName, profile.Id, dialog.ProfileName);
                BuildDetail();
                status.Text = "Perfil renomeado. Os arquivos de save e o ID da conta na nuvem foram mantidos.";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Renomear perfil"); }
        };
        profileSummary = new Label { AutoSize = false, AutoEllipsis = true, Width = detailCard.Width - 56, Height = 24, ForeColor = AppTheme.TextMuted, Font = AppTheme.Caption };
        profileSummary.Text = BuildProfileSummary(root, selected, state.Profiles[picker.SelectedIndex].Id);
        profileControls.Controls.AddRange(new Control[] { label, picker, create, rename, profileSummary });
    }

    internal static string BuildProfileSummary(string root, GameInfo game, string profileId)
    {
        try
        {
            List<ProfileSaveChoice> saves = ProfileSaveLocator.Find(root, game, profileId);
            if (saves.Count == 0) return "Sem save compatível neste perfil.";
            DateTime latest = saves.Max(save => File.GetLastWriteTimeUtc(save.Path)).ToLocalTime();
            string count = saves.Count == 1 ? "1 save compatível" : $"{saves.Count} saves compatíveis";
            return $"{count} · mais recente: {latest:dd/MM/yyyy HH:mm}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return "Não foi possível conferir os saves deste perfil.";
        }
    }

	private string SelectedFolder()
	{
		string text = SaveProfileService.ActiveFolder(root, selected.SaveFolderName);
		Directory.CreateDirectory(text);
		return text;
	}

	private void OpenSelected()
		=> RunLocalSaveAction(OpenSelectedCore);

	private void RunLocalSaveAction(Action action)
	{
		try { action(); }
		catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
		{
			BuildDetail();
			status.ForeColor=AppTheme.Red;
			status.Text="Ação interrompida. Confira o acesso aos arquivos e tente novamente.";
		}
	}

	private void OpenSelectedCore()
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = SelectedFolder();
		processStartInfo.UseShellExecute = true;
		Process.Start(processStartInfo);
	}

	private void OpenAll()
	{
		Directory.CreateDirectory(savesDir);
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = savesDir;
		processStartInfo.UseShellExecute = true;
		Process.Start(processStartInfo);
	}

	private void BackupSelected()
		=> RunLocalSaveAction(BackupSelectedCore);

	private void BackupSelectedCore()
	{
		string text = SelectedFolder();
		string[] files = Directory.GetFiles(text, "*", SearchOption.AllDirectories);
		if (files.Length == 0)
		{
			status.Text = "Não há arquivos para incluir no backup.";
			return;
		}
		string text2 = Path.Combine(savesDir, "Backups");
		Directory.CreateDirectory(text2);
		string text3 = Path.Combine(text2, SaveProfileService.CloudId(root, selected.SaveFolderName) + " - " + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".zip");
		try
		{
			SaveBackupService.CreateVerifiedArchive(text, text3);
			status.ForeColor = AppTheme.Green;
			status.Text = BuildManualBackupSuccessStatus(Path.GetFileName(text3), files.Length, new FileInfo(text3).Length);
		}
		catch (Exception ex)
		{
			status.ForeColor = AppTheme.Red;
			status.Text = "Backup não concluído; os saves originais continuam intactos.";
			MessageBox.Show(FindForm(), "Não foi possível criar um backup ZIP válido. Nenhum arquivo de save foi substituído.\n\n" + ex.Message, "Backup não concluído", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	internal static string BuildManualBackupSuccessStatus(string fileName, int fileCount, long archiveBytes)
	{
		string count = fileCount == 1 ? "1 arquivo" : fileCount + " arquivos";
		string size = archiveBytes >= 1024 * 1024
			? (archiveBytes / (1024d * 1024d)).ToString("0.00") + " MB"
			: (archiveBytes / 1024d).ToString("0.0") + " KB";
		return $"Backup validado · {count} · {size} · {Path.GetFileName(fileName)}";
	}

	private void RestoreSelected()
		=> RunLocalSaveAction(RestoreSelectedCore);

	private void RestoreSelectedCore()
	{
		string processName = selected.EmulatorProcess;
		if (string.IsNullOrWhiteSpace(processName))
			processName = selected.Generation >= 6 ? "azahar" : selected.Generation == 3 ? "visualboyadvance-m" : "melonDS";
		if (IsEmulatorRunning(processName))
		{
			status.ForeColor = AppTheme.TextMuted;
			status.Text = "Feche o emulador antes de restaurar um backup.";
			return;
		}

		List<BackupArchiveChoice> choices = SaveBackupService.BackupsForActiveProfile(root, selected.SaveFolderName);
		string activeProfile = selected.Generation is >= 3 and <= 5
			? SaveProfileService.Load(root, selected.SaveFolderName).Profiles.First(p => p.Id == SaveProfileService.Load(root, selected.SaveFolderName).ActiveId).Name
			: "Principal";
		using BackupRestoreDialog picker = new BackupRestoreDialog(activeProfile, choices);
		if (picker.ShowDialog(FindForm()) != DialogResult.OK || picker.SelectedBackup == null)
			return;
		if (MessageBox.Show(
			BackupRestoreDialog.BuildRestoreConfirmation(activeProfile, picker.SelectedChoice),
			"Restaurar backup",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Warning,
			MessageBoxDefaultButton.Button2) != DialogResult.Yes)
			return;

		string destination = SelectedFolder();
		string staging = Path.Combine(savesDir, ".restore-" + Guid.NewGuid().ToString("N"));
		string previous = Path.Combine(savesDir, ".restore-previous-" + Guid.NewGuid().ToString("N"));
		try
		{
			SaveBackupService.ExtractLocalArchive(picker.SelectedBackup, staging);
			if (!Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Any())
				throw new InvalidDataException("O arquivo ZIP não contém saves.");
			SaveBackupService.ValidateForGame(staging, selected);
			if (IsEmulatorRunning(processName))
			{
				status.ForeColor = AppTheme.TextMuted;
				status.Text = "O emulador foi aberto durante a restauração. Feche-o e tente novamente.";
				return;
			}

			SaveBackupService.CreateIfChanged(root, selected.SaveFolderName);
			if (IsEmulatorRunning(processName))
			{
				status.ForeColor = AppTheme.TextMuted;
				status.Text = "O emulador foi aberto durante a criação do backup. Feche-o e tente novamente.";
				return;
			}

			SaveBackupService.ReplaceFolderFromStaging(staging, destination, previous);
			BuildDetail();
			status.ForeColor = AppTheme.Green;
			status.Text = "Backup restaurado. O save local anterior foi salvo em Backups/Automaticos, se havia arquivos.";
		}
		catch (Exception ex)
		{
			status.ForeColor = AppTheme.TextMuted;
			MessageBox.Show("Não foi possível restaurar o backup:\n\n" + ex.Message, "Restaurar backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		finally
		{
			if (Directory.Exists(staging))
			{
				try { Directory.Delete(staging, recursive: true); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
			}
		}
	}

	private static bool IsEmulatorRunning(string processName)
	{
		foreach (Process process in Process.GetProcessesByName(processName))
		{
			using (process)
			{
				if (!process.HasExited)
					return true;
			}
		}
		return false;
	}
}
