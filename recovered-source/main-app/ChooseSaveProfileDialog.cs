using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal sealed class ChooseSaveProfileDialog : Form
{
    private readonly string root;
    private readonly GameInfo game;
    private readonly ThemeSelect picker = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label profileSummary = new() { AutoSize = false, ForeColor = AppTheme.TextMuted, Font = AppTheme.Caption, AutoEllipsis = true };
    private readonly ThemeButton openFolder = new("Abrir pasta do perfil", ButtonKind.Secondary) { Location = new Point(24, 220), Size = new Size(184, 32), Enabled = false };
    private SaveProfileState profiles;
    public string SelectedProfileId => profiles.Profiles[picker.SelectedIndex].Id;

    public ChooseSaveProfileDialog(string root, GameInfo game)
    {
        this.root = root; this.game = game;
        Text = "Escolher perfil · " + game.Title;
        ClientSize = new Size(480, 350); BackColor = AppTheme.Background; ForeColor = AppTheme.Text;
        Font = AppTheme.Body; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = MaximizeBox = false;
        var title = new Label { Text = "Com qual perfil você quer jogar?", Location = new Point(24, 24), Size = new Size(432, 32), Font = AppTheme.BodyBold };
        picker.SetBounds(24, 76, 432, 40);
        profileSummary.SetBounds(24, 126, 432, 28);
        var help = new Label { Text = game.Generation <= 5 ? "Cada perfil tem seu próprio progresso. Escolha sua aventura antes de abrir o jogo." : "Este jogo 3DS usa o perfil Principal. Perfis adicionais estão disponíveis para GBA e DS.", Location = new Point(24, 160), Size = new Size(432, 54), ForeColor = AppTheme.TextMuted };
        var create = new ThemeButton("Novo perfil", ButtonKind.Secondary) { AutoSize = true, Location = new Point(220, 220), Enabled = game.Generation <= 5 };
        create.Click += (_, _) => CreateProfile();
        openFolder.Visible = game.Generation <= 5;
        openFolder.Click += (_, _) => OpenProfileFolder();
        var cancel = new ThemeButton("Cancelar", ButtonKind.Secondary) { DialogResult = DialogResult.Cancel, Location = new Point(224, 284), Width = 104 };
        var play = new ThemeButton("Jogar", ButtonKind.Primary) { DialogResult = DialogResult.OK, Location = new Point(340, 284), Width = 116 };
        Controls.AddRange(new Control[] { title, picker, profileSummary, help, openFolder, create, cancel, play });
        AcceptButton = play; CancelButton = cancel;
        picker.SelectedIndexChanged += (_, _) => RefreshProfileSummary();
        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        profiles = game.Generation <= 5 ? SaveProfileService.Load(root, game.SaveFolderName) : new SaveProfileState();
        picker.Items.Clear();
        foreach (var profile in profiles.Profiles) picker.Items.Add(profile);
        picker.SelectedIndex = profiles.Profiles.FindIndex(p => p.Id == profiles.ActiveId);
        RefreshProfileSummary();
    }

    private void RefreshProfileSummary()
    {
        if (picker.SelectedIndex < 0)
        {
            openFolder.Enabled = false;
            profileSummary.Text = "Selecione um perfil para conferir o progresso.";
            return;
        }
        if (game.Generation > 5)
        {
            openFolder.Enabled = false;
            profileSummary.Text = "Perfil Principal selecionado.";
            return;
        }

        try
        {
            SaveProfile profile = profiles.Profiles[picker.SelectedIndex];
            string profileFolder = SaveProfileService.Folder(root, game.SaveFolderName, profile.Id);
            openFolder.Enabled = Directory.Exists(profileFolder);
            var saves = ProfileSaveLocator.Find(root, game, profile.Id);
            if (saves.Count == 0)
            {
                bool hasFiles = Directory.Exists(profileFolder) && Directory.EnumerateFiles(profileFolder, "*", SearchOption.AllDirectories).Any();
                profileSummary.Text = hasFiles
                    ? "Há arquivos nesta pasta, mas nenhum save compatível foi reconhecido."
                    : "Sem save neste perfil. Salve no jogo para iniciar.";
                return;
            }
            DateTime latest = saves.Max(save => File.GetLastWriteTimeUtc(save.Path)).ToLocalTime();
            string count = saves.Count == 1 ? "1 save compatível" : $"{saves.Count} saves compatíveis";
            profileSummary.Text = $"{count} · mais recente: {latest:dd/MM/yyyy HH:mm}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            profileSummary.Text = "Não foi possível conferir os saves deste perfil.";
            openFolder.Enabled = false;
        }
    }

    private void OpenProfileFolder()
    {
        if (picker.SelectedIndex < 0 || game.Generation > 5) return;
        string folder = SaveProfileService.Folder(root, game.SaveFolderName, profiles.Profiles[picker.SelectedIndex].Id);
        if (!Directory.Exists(folder)) return;
        try { Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { MessageBox.Show(this, "Não foi possível abrir a pasta do perfil.\n\n" + ex.Message, "Pasta do perfil", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    private void CreateProfile()
    {
        try
        {
            SaveProfileService.EnsureEmulatorsClosed();
            using var dialog = new SaveProfileDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string import = null;
            if (dialog.Mode == 2)
            {
                using var file = new OpenFileDialog { Title = "Importar save deste jogo", Filter = "Save normal|*.sav;*.dsv;*.bin|Todos os arquivos|*.*" };
                if (file.ShowDialog(this) != DialogResult.OK) return;
                import = file.FileName;
            }
            SaveProfileService.Create(root, game, dialog.ProfileName, dialog.Mode == 1, import);
            RefreshProfiles();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Novo perfil", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    public static bool ChooseForLaunch(IWin32Window owner, string root, GameInfo game)
    {
        SaveProfileService.EnsureEmulatorsClosed();
        using var dialog = new ChooseSaveProfileDialog(root, game);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;
        if (game.Generation <= 5) SaveProfileService.Activate(root, game.SaveFolderName, dialog.SelectedProfileId);
        return true;
    }
}
