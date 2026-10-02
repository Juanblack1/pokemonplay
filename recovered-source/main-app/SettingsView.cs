using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed class SettingsView : BufferedPanel
{
	private readonly string settingsFile;

	private readonly Panel canvas;

	private readonly InputWorkbench controlsCard;

	private readonly SectionCard dsCard;

	private readonly SectionCard audioCard;

	private readonly SectionCard retroArchCard;

	private readonly ThemeSelect preset;

	private readonly ThemeSelect screens;

	private readonly ThemeSlider volume;

	private readonly Label volumeValue;

	private readonly CheckBox mute;

	private readonly CheckBox audioSync;

	private readonly CheckBox interpolation;

	private readonly CheckBox retroArchGba;

	private readonly CheckBox retroArchDs;

	private readonly Label retroArchExecutablePath;

	private readonly Label retroArchGbaCorePath;

	private readonly Label retroArchDsCorePath;

	private readonly ThemeButton retroArchAccountButton;

	private readonly ThemeButton retroArchSupportButton;

	private RetroArchSettings retroArchSettings;
	internal Func<ProcessStartInfo, Process> RetroArchProcessStarter { get; set; } = startInfo => Process.Start(startInfo);
	internal Action<string> RetroArchBrowserOpener { get; set; } = url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
	private const string RetroArchSupportUrl = "https://docs.retroachievements.org/general/emulator-support-and-issues.html";

	private readonly string root;


	private readonly Label status;

	private readonly ThemeButton restoreButton;

	private readonly ThemeButton saveButton;

	private readonly string[] customKeys = new string[10] { "W", "S", "A", "D", "Z", "X", "Q", "E", "Enter", "Backspace" };

	private static readonly string[] Actions = new string[12] { "Movimento - cima", "Movimento - baixo", "Movimento - esquerda", "Movimento - direita", "Ação A", "Ação B", "Ombro L", "Ombro R", "Start", "Select", "Ação X", "Ação Y" };

 public SettingsView(string settingsFile)
 {
  this.settingsFile=settingsFile;root=Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(settingsFile))); Dock=DockStyle.Fill; BackColor=Color.Transparent;
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=AppTheme.Background};
  canvas=new PixelGridPanel{Width=900,Height=604,BackColor=AppTheme.Background};scroll.Controls.Add(canvas);

  dsCard=new SectionCard("Telas do Nintendo DS","Organize as duas telas do emulador."){Height=228};
  audioCard=new SectionCard("Áudio","Ajustes aplicados ao abrir o próximo jogo."){Height=320};
  retroArchCard=new SectionCard("RetroAchievements","Use RetroArch nos jogos GBA/DS. Configure sua conta no RetroArch; o Pokemon Play não guarda credenciais."){Height=380};

  preset=new ThemeSelect{Width=360};
  preset.Items.AddRange(new object[]{"Clássico · Setas + Z/X", "WASD · Z/X + Q/E", "Numpad · 8/5/4/6", "ESDF · J/K + A/G", "WASD · J/K + Q/E", "Personalizado"});preset.SelectedIndex=1;
  controlsCard=new InputWorkbench(InputDeviceProfile.Load(InputDeviceProfile.PathFor(settingsFile)),preset,()=>InputDeviceProfile.KeyboardKeys(preset.SelectedIndex,customKeys).Concat(controlsCard?.Profile.ExtraKeys ?? new[]{"C","V"}).ToArray(),CaptureKey);
  canvas.Controls.AddRange(new Control[]{controlsCard,dsCard,audioCard});
  var screenLabel=MakeLabel("Layout das telas",AppTheme.Text,AppTheme.BodyBold);screenLabel.Location=new Point(24,88);
  screens=new ThemeSelect{Location=new Point(24,116),Width=360};screens.Items.AddRange(new object[]{"Natural · cima / baixo","Horizontal · lado a lado","Vertical · telas alinhadas","Híbrido · foco na tela de cima","Híbrido · foco na tela de baixo"});screens.SelectedIndex=0;
  var touch=MakeLabel("Use o mouse como caneta na tela do DS.",AppTheme.TextMuted,AppTheme.Caption);touch.Location=new Point(24,166);touch.AutoSize=false;touch.Size=new Size(340,38);touch.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  dsCard.Controls.AddRange(new Control[]{screenLabel,screens,touch});
  var volumeLabel=MakeLabel("Volume",AppTheme.Text,AppTheme.BodyBold);volumeLabel.Location=new Point(24,88);
  volume=new ThemeSlider{Location=new Point(24,114),Width=280,Height=36};volumeValue=MakeLabel("100%",AppTheme.Text,AppTheme.BodyBold);volumeValue.Location=new Point(320,122);volumeValue.Width=54;
  mute=MakeCheck("Silenciar som",24,160);audioSync=MakeCheck("Sincronizar áudio (DS)",24,194);interpolation=MakeCheck("Suavizar áudio",24,228);
  var audioHint=MakeLabel("Válido para VBA-M e melonDS.",AppTheme.TextMuted,AppTheme.Caption);audioHint.Location=new Point(24,270);
  audioCard.Controls.AddRange(new Control[]{volumeLabel,volume,volumeValue,mute,audioSync,interpolation,audioHint});
  retroArchGba=MakeCheck("Usar RetroArch para GBA",24,88);retroArchGba.AccessibleDescription="Abre jogos de Game Boy Advance com o core Libretro selecionado.";
  retroArchDs=MakeCheck("Usar RetroArch para Nintendo DS",264,88);retroArchDs.AccessibleDescription="Abre jogos de Nintendo DS com o core Libretro selecionado.";
  var retroArchBrowse=BrowseButton("Localizar RetroArch…",24,128,192);retroArchBrowse.Click+=(_,_)=>BrowseRetroArchExecutable();
  retroArchExecutablePath=PathLabel("Executável ainda não selecionado");retroArchExecutablePath.SetBounds(228,132,640,24);
  var gbaCoreBrowse=BrowseButton("Core Libretro GBA…",24,176,192);gbaCoreBrowse.Click+=(_,_)=>BrowseRetroArchCore(true);
  retroArchGbaCorePath=PathLabel("Core GBA ainda não selecionado");retroArchGbaCorePath.SetBounds(228,180,640,24);
  var dsCoreBrowse=BrowseButton("Core Libretro DS…",24,224,192);dsCoreBrowse.Click+=(_,_)=>BrowseRetroArchCore(false);
  retroArchDsCorePath=PathLabel("Core DS ainda não selecionado");retroArchDsCorePath.SetBounds(228,228,640,24);
  var retroArchHint=MakeLabel("Ao localizar o executável, procuramos cores compatíveis na pasta cores ao lado dele. O RetroArch exibe notificações; o perfil ativo recebe saves .srm.",AppTheme.TextMuted,AppTheme.Caption);retroArchHint.SetBounds(24,270,800,48);retroArchHint.AutoSize=false;retroArchHint.AccessibleName="Detecção de cores, notificações e saves do RetroArch";
  retroArchAccountButton=new ThemeButton("Abrir RetroArch · configurar conta",ButtonKind.Secondary);
  retroArchAccountButton.SetBounds(24,324,280,34);retroArchAccountButton.AutoSize=false;retroArchAccountButton.Enabled=false;retroArchAccountButton.AccessibleName="Abrir RetroArch para configurar conquistas";retroArchAccountButton.AccessibleDescription="Abre o RetroArch; configure sua conta em Configurações > Conquistas. O Pokemon Play não armazena a senha.";retroArchAccountButton.Click+=(_,_)=>OpenRetroArchForAccount();
  retroArchSupportButton=new ThemeButton("Ver compatibilidade dos cores",ButtonKind.Secondary);retroArchSupportButton.SetBounds(316,324,250,34);retroArchSupportButton.AutoSize=false;retroArchSupportButton.AccessibleName="Ver compatibilidade de cores com RetroAchievements";retroArchSupportButton.AccessibleDescription="Abre a lista oficial de sistemas e cores compatíveis com RetroAchievements.";retroArchSupportButton.Click+=(_,_)=>OpenRetroArchSupport();
  retroArchCard.Controls.AddRange(new Control[]{retroArchGba,retroArchDs,retroArchBrowse,retroArchExecutablePath,gbaCoreBrowse,retroArchGbaCorePath,dsCoreBrowse,retroArchDsCorePath,retroArchHint,retroArchAccountButton,retroArchSupportButton});
  canvas.Controls.Add(retroArchCard);
  status=MakeLabel("Suas preferências ficam salvas neste computador.",AppTheme.TextMuted,AppTheme.Caption);status.AutoSize=false;status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.AutoEllipsis=true;
  restoreButton=new ThemeButton("Restaurar padrão",ButtonKind.Secondary){AutoSize=true,Margin=new Padding(8,0,0,0)};
  saveButton=new ThemeButton("Salvar configurações",ButtonKind.Primary){AutoSize=true,Margin=new Padding(8,0,0,0)};
  restoreButton.Click+=(_,_)=>RestoreDefaults();saveButton.Click+=(_,_)=>Save();
  var footer=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=72,BackColor=AppTheme.Background,Padding=new Padding(24,16,24,16),ColumnCount=3,RowCount=1};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));footer.Controls.Add(status,0,0);footer.Controls.Add(restoreButton,1,0);footer.Controls.Add(saveButton,2,0);
  Controls.Add(scroll);Controls.Add(footer);Controls.Add(new PixelHeader("Configurações","Deixe os controles, o som e as conquistas do seu jeito.",100));
  preset.SelectedIndexChanged+=(_,_)=>UpdateMapping();volume.ValueChanged+=(_,_)=>UpdateVolumeLabel();mute.CheckedChanged+=(_,_)=>UpdateVolumeLabel();scroll.Resize+=(_,_)=>LayoutCards(scroll.ClientSize.Width,scroll.ClientSize.Height);
  LoadSettings();LoadRetroArchSettings();UpdateMapping();UpdateVolumeLabel();LayoutCards(1000,450);
 }

 private ThemeButton BrowseButton(string text,int x,int y,int width)=>new ThemeButton(text,ButtonKind.Secondary){Location=new Point(x,y),Size=new Size(width,34),AutoSize=false,AccessibleName=text.TrimEnd('…')};
 private Label PathLabel(string empty)=>new Label{Text=empty,ForeColor=AppTheme.TextMuted,Font=AppTheme.Caption,BackColor=AppTheme.Surface,AutoSize=false,AutoEllipsis=true,AccessibleRole=AccessibleRole.StaticText};

	private Label MakeLabel(string text, Color color, Font font)
	{
		Label label = new Label();
		label.Text = text;
		label.ForeColor = color;
		label.Font = font;
		label.AutoSize = true;
		label.BackColor = Color.Transparent;
		return label;
	}

	private CheckBox MakeCheck(string text, int x, int y)
	{
		CheckBox checkBox = new CheckBox();
		checkBox.Text = text;
		checkBox.ForeColor = AppTheme.TextSecondary;
		checkBox.BackColor = Color.Transparent;
		checkBox.AutoSize = true;
		checkBox.FlatStyle = FlatStyle.Flat;
		checkBox.Location = new Point(x, y);
		checkBox.Font = AppTheme.Body;
		return checkBox;
	}

 private void LayoutCards(int available,int height)
 {
  int width=Math.Max(850,available-20);canvas.Width=width;
  int cardWidth=(width-72)/2;
  int controlHeight=Math.Max(370,height-32);
  controlsCard.SetBounds(24,16,width-48,controlHeight);
  dsCard.SetBounds(24,controlHeight+40,cardWidth,320);
  audioCard.SetBounds(48+cardWidth,controlHeight+40,cardWidth,320);
  retroArchCard.SetBounds(24,controlHeight+376,width-48,380);
  canvas.Height=controlHeight+852;
  screens.Width=cardWidth-48;
  volume.Width=Math.Max(150,cardWidth-116);volumeValue.Left=cardWidth-74;
  retroArchExecutablePath.Width=retroArchGbaCorePath.Width=retroArchDsCorePath.Width=Math.Max(200,retroArchCard.ClientSize.Width-272);
  foreach(Control c in dsCard.Controls)if(c is Label && c.Top==166)c.Width=cardWidth-48;
 }
 private void UpdateMapping(){controlsCard?.UpdateRows();}

	private void UpdateVolumeLabel()
	{
		volumeValue.Text = (mute.Checked ? "MUDO" : (volume.Value + "%"));
	}

	private void LoadSettings()
	{
		if (!File.Exists(settingsFile))
		{
			return;
		}
		try
		{
			string[] array = File.ReadAllLines(settingsFile);
			if (array.Length > 0 && int.TryParse(array[0], out var result) && result >= 0 && result < preset.Items.Count)
			{
				preset.SelectedIndex = result;
			}
			if (array.Length > 1 && int.TryParse(array[1], out result) && result >= 0 && result < screens.Items.Count)
			{
				screens.SelectedIndex = result;
			}
			if (array.Length > 2 && int.TryParse(array[2], out result))
			{
				volume.Value = Math.Max(volume.Minimum, Math.Min(volume.Maximum, result));
			}
			if (array.Length > 3)
			{
				mute.Checked = IsTrue(array[3]);
			}
			if (array.Length > 4)
			{
				audioSync.Checked = IsTrue(array[4]);
			}
			if (array.Length > 5)
			{
				interpolation.Checked = IsTrue(array[5]);
			}
			for (int i = 0; i < customKeys.Length && i + 6 < array.Length; i++)
			{
				if (!string.IsNullOrWhiteSpace(array[i + 6]))
				{
					customKeys[i] = array[i + 6].Trim();
				}
			}
		}
		catch
		{
		}
	}

 private void LoadRetroArchSettings()
 {
  retroArchSettings=RetroArchSettingsService.Load(root);
  retroArchGba.Checked=retroArchSettings.UseForGba;retroArchDs.Checked=retroArchSettings.UseForDs;
  SetPathLabel(retroArchExecutablePath,retroArchSettings.ExecutablePath,"Executável ainda não selecionado");
  SetPathLabel(retroArchGbaCorePath,retroArchSettings.GbaCorePath,"Core GBA ainda não selecionado");
  SetPathLabel(retroArchDsCorePath,retroArchSettings.DsCorePath,"Core DS ainda não selecionado");
  UpdateRetroArchAccountButton();
 }

 private void UpdateRetroArchAccountButton()=>retroArchAccountButton.Enabled=File.Exists(retroArchSettings?.ExecutablePath);

 private void OpenRetroArchForAccount()
 {
  string executable=retroArchSettings?.ExecutablePath;
  if(!File.Exists(executable)){status.ForeColor=AppTheme.Red;status.Text="Localize e salve o executável do RetroArch primeiro.";return;}
  try
  {
   string accountArguments=string.Equals(Path.GetFullPath(executable),BundledEmulators.RetroArch(root),StringComparison.OrdinalIgnoreCase)?"--config \""+BundledEmulators.RetroArchConfig(root)+"\"":string.Empty;
   RetroArchProcessStarter(new ProcessStartInfo{FileName=Path.GetFullPath(executable),Arguments=accountArguments,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable)),UseShellExecute=true});
   status.ForeColor=AppTheme.Green;status.Text="RetroArch aberto. Configure a conta em Configurações > Conquistas.";
  }
  catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
  {
   status.ForeColor=AppTheme.Red;status.Text="Não foi possível abrir o RetroArch: "+ex.Message;
  }
 }

 private void OpenRetroArchSupport()
 {
  try
  {
   RetroArchBrowserOpener(RetroArchSupportUrl);
   status.ForeColor=AppTheme.TextMuted;status.Text="Lista oficial de compatibilidade aberta no navegador.";
  }
  catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException)
  {
   status.ForeColor=AppTheme.Red;status.Text="Não foi possível abrir a lista de compatibilidade: "+ex.Message;
  }
 }

 private static void SetPathLabel(Label label,string path,string empty)
 {
  label.Text=string.IsNullOrWhiteSpace(path)?empty:path;label.AccessibleDescription=string.IsNullOrWhiteSpace(path)?empty:path;
 }

 private void BrowseRetroArchExecutable()
 {
  using var dialog=new OpenFileDialog{Title="Selecione o RetroArch",Filter="RetroArch|retroarch.exe|Executável|*.exe|Todos os arquivos|*.*",CheckFileExists=true};
  if(dialog.ShowDialog(FindForm())!=DialogResult.OK)return;
  retroArchSettings.ExecutablePath=dialog.FileName;SetPathLabel(retroArchExecutablePath,dialog.FileName,"Executável ainda não selecionado");
  UpdateRetroArchAccountButton();
  bool foundGba=false,foundDs=false;
  if(!File.Exists(retroArchSettings.GbaCorePath)){string core=RetroArchSettingsService.DiscoverCore(dialog.FileName,true);if(core.Length>0){retroArchSettings.GbaCorePath=core;SetPathLabel(retroArchGbaCorePath,core,"Core GBA ainda não selecionado");foundGba=true;}}
  if(!File.Exists(retroArchSettings.DsCorePath)){string core=RetroArchSettingsService.DiscoverCore(dialog.FileName,false);if(core.Length>0){retroArchSettings.DsCorePath=core;SetPathLabel(retroArchDsCorePath,core,"Core DS ainda não selecionado");foundDs=true;}}
  status.Text=foundGba||foundDs?"RetroArch selecionado; cores compatíveis encontrados. Confira e salve.":"RetroArch selecionado. Clique em salvar para manter.";
 }

 private void BrowseRetroArchCore(bool gba)
 {
  using var dialog=new OpenFileDialog{Title=gba?"Selecione o core de Game Boy Advance":"Selecione o core de Nintendo DS",Filter="Cores Libretro|*.dll|Todos os arquivos|*.*",CheckFileExists=true};
  if(dialog.ShowDialog(FindForm())!=DialogResult.OK)return;
  if(gba){retroArchSettings.GbaCorePath=dialog.FileName;SetPathLabel(retroArchGbaCorePath,dialog.FileName,"Core GBA ainda não selecionado");}
  else{retroArchSettings.DsCorePath=dialog.FileName;SetPathLabel(retroArchDsCorePath,dialog.FileName,"Core DS ainda não selecionado");}
  status.Text="Core selecionado. Clique em salvar para manter.";
 }

	private static bool IsTrue(string value)
	{
		return value.Trim() == "1" || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
	}

	private void RestoreDefaults()
	{
		preset.SelectedIndex = 1;
        controlsCard.ResetProfile();
		for (int i = 0; i < customKeys.Length; i++)
		{
			customKeys[i] = (new string[10] { "W", "S", "A", "D", "Z", "X", "Q", "E", "Enter", "Backspace" })[i];
		}
		screens.SelectedIndex = 0;
		volume.Value = 100;
		mute.Checked = false;
		audioSync.Checked = false;
		interpolation.Checked = true;
		retroArchGba.Checked = false;
		retroArchDs.Checked = false;
		UpdateMapping();
		UpdateVolumeLabel();
		status.Text = "Padrão restaurado. Salve para manter as alterações.";
	}

	private void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
			List<string> list = new List<string>();
			list.Add(preset.SelectedIndex.ToString());
			list.Add(screens.SelectedIndex.ToString());
			list.Add(volume.Value.ToString());
			list.Add(mute.Checked ? "1" : "0");
			list.Add(audioSync.Checked ? "1" : "0");
			list.Add(interpolation.Checked ? "1" : "0");
			List<string> list2 = list;
			list2.AddRange(customKeys);
			controlsCard.Profile.Save(InputDeviceProfile.PathFor(settingsFile));
            File.WriteAllLines(settingsFile, list2.ToArray());
			retroArchSettings.UseForGba=retroArchGba.Checked;retroArchSettings.UseForDs=retroArchDs.Checked;
			string retroArchError=RetroArchSettingsService.ValidationError(retroArchSettings);
			if(retroArchError==null)RetroArchSettingsService.Save(root,retroArchSettings);
			status.ForeColor = AppTheme.Green;
			status.Text = retroArchError==null?"Configurações salvas. Válidas no próximo jogo.":"Controles salvos. Para ativar RetroArch: "+retroArchError;
			if(retroArchError!=null)status.ForeColor=AppTheme.Red;
            UpdateRetroArchAccountButton();
		}
		catch (Exception ex)
		{
			status.ForeColor = AppTheme.Red;
			status.Text = "Não foi possível salvar: " + ex.Message;
		}
	}

	private void CaptureKey(int index)
	{
        if(index>=10){using var dialog=new KeyCaptureDialog(Actions[index]);if(dialog.ShowDialog(FindForm())==DialogResult.OK){var used=InputDeviceProfile.KeyboardKeys(preset.SelectedIndex,customKeys).Concat(controlsCard.Profile.ExtraKeys.Where((_,i)=>i!=index-10));if(used.Contains(dialog.CapturedKey)){status.Text="Essa tecla já está atribuída.";return;}controlsCard.Profile.ExtraKeys[index-10]=dialog.CapturedKey;UpdateMapping();status.Text="Tecla alterada. Salve as configurações.";}return;}
		if (preset.SelectedIndex != 5)
		{
			var current=InputDeviceProfile.KeyboardKeys(preset.SelectedIndex,customKeys);
            Array.Copy(current,customKeys,10);
            preset.SelectedIndex=5;
		}
		using KeyCaptureDialog keyCaptureDialog = new KeyCaptureDialog(Actions[index]);
		if (keyCaptureDialog.ShowDialog(FindForm()) != DialogResult.OK)
		{
			return;
		}
		for (int i = 0; i < customKeys.Length; i++)
		{
			if ((i != index && string.Equals(customKeys[i], keyCaptureDialog.CapturedKey, StringComparison.OrdinalIgnoreCase)) || controlsCard.Profile.ExtraKeys.Contains(keyCaptureDialog.CapturedKey))
			{
				status.Text = "Essa tecla já está atribuída a outra ação.";
				return;
			}
		}
		customKeys[index] = keyCaptureDialog.CapturedKey;
		UpdateMapping();
		status.Text = "Tecla alterada. Clique em salvar para manter.";
	}
}
