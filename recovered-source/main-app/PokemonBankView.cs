using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

internal sealed partial class PokemonBankView : BufferedPanel
{
    private readonly string root;
    private readonly ThemeSelect boxPicker = new ThemeSelect();
    private readonly FlowLayoutPanel slots = new();
    private readonly FlowLayoutPanel bankNavigation = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 8, 0, 8), BackColor = Color.Transparent, WrapContents = true, Visible = false };
    private readonly FlowLayoutPanel bankPaging = new() { Dock = DockStyle.Bottom, Height = 48, BackColor = AppTheme.Background, WrapContents = false, Visible = false };
    private readonly Label bankSpeciesLabel = new();
    private readonly TextBox bankSpeciesSearch = new();
    private readonly Dictionary<string, BankSearchEntry> bankSearchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Label bankSortLabel = new();
    private readonly ThemeSelect bankSortPicker = new();
    private readonly Label bankGenerationLabel = new();
    private readonly ThemeSelect bankGenerationPicker = new();
    private readonly ThemeSelect bankDexPicker = new();
    private readonly ThemeSelect bankFormatPicker = new();
    private readonly ThemeSelect bankTypePicker = new();
    private readonly ThemeSelect bankShinyPicker = new();
    private readonly ThemeSelect bankGenderPicker = new();
    private readonly ThemeSelect bankEggPicker = new();
    private readonly ThemeSelect bankDuplicatePicker = new();
    private ThemeButton bankMoreFilters;
    private readonly NumericUpDown bankMinLevel = new() { Minimum=1,Maximum=100,Value=1 };
    private readonly NumericUpDown bankMaxLevel = new() { Minimum=1,Maximum=100,Value=100 };
    private readonly FlowLayoutPanel bankExtraFilters = new() { Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=AppTheme.Surface,Margin=Padding.Empty,Visible=false };
    private bool updatingFilters;
    private readonly ThemeButton bankPreviousPage = new("Anterior", ButtonKind.Secondary);
    private readonly ThemeButton bankNextPage = new("Próxima", ButtonKind.Secondary);
    private readonly Label bankPageStatus = new();
    private readonly Label status = new();
    private readonly Label details = new();
    private readonly PictureBox selectedSpritePreview = new();
    private Image selectedSpritePreviewImage;
    private readonly Button editButton;
    private readonly Button moveButton;
    private readonly Button archiveButton;
    private readonly Button saveButton;
    private readonly Button createButton;
    private readonly Button exportBankButton;
    private readonly Button importBankButton;
    private readonly Button openBankFolderButton;
    private readonly Button importPokemonButton;
    private readonly Button exportPokemonButton;
    private readonly Button removePokemonButton;
    private readonly Button legalityButton;
    private readonly ThemeSelect creationFormat = new(){Width=138};
    private readonly Button restoreRemovedButton;
    private SaveFile save;
    private string savePath;
    private GameInfo loadedGame;
    private byte[] openedHash;
    private int selectedBox;
    private int selectedSlot = -1;
    private string[] bankFiles = Array.Empty<string>();
    private int[] bankViewIndices = Array.Empty<int>();
    private int bankPage;
    private const int BankPageSize = 30;
    internal const string PokemonImportFileFilter = "Pokémon suportados (*.pk3;*.pk4;*.pk5;*.pk6;*.pk7;*.pk8;*.pk9)|*.pk3;*.pk4;*.pk5;*.pk6;*.pk7;*.pk8;*.pk9|Pokémon Gen 3 (*.pk3)|*.pk3|Pokémon Gen 4 (*.pk4)|*.pk4|Pokémon Gen 5 (*.pk5)|*.pk5|Pokémon Gen 6 (*.pk6)|*.pk6|Pokémon Gen 7 (*.pk7)|*.pk7|Pokémon Gen 8 (*.pk8)|*.pk8|Pokémon Gen 9 (*.pk9)|*.pk9";
    private sealed record BankSearchEntry(long Length, long LastWriteTicks, ushort Species, string Nickname, string SpeciesSearchText, byte Level, int Format, bool Shiny, byte Gender, bool Egg, byte Type1, byte Type2, DateOnly? MetDate, bool Valid);
    private readonly ThemeSelect gamePicker = new();
    private readonly ThemeSelect profilePicker = new();
    private readonly ThemeSelect savePicker = new();
    private readonly Label openedContext = new();
    private readonly List<GameInfo> games;
    private SaveProfileState profileState;
    private List<ProfileSaveChoice> profileSaves = new();
    private bool hasUnsavedChanges;
    private readonly FirebaseCloudSaveService cloud;
    private readonly Label cloudStatus = new();
    internal const string CloudSignedOutStatus = "Banco local neste PC. Cópia manual ligada à conta Google no Firebase do app.\nNão fica no Google Drive; enviar/restaurar não é automático.";
    private readonly Label bankSummary = new();
    private readonly Button cloudLoginButton;
    private readonly Button cloudUploadButton;
    private readonly Button cloudRestoreButton;
    private readonly Button cloudRefreshButton;
    private readonly Button cloudSignOutButton;
    private bool cloudBusy;
    internal Func<DialogResult> ConfirmSaveChange { get; set; }

    public PokemonBankView(string root)
    {
        this.root=root;games=GameCatalog.SaveGames(root);cloud=new FirebaseCloudSaveService(root);Dock=DockStyle.Fill;BackColor=Color.Transparent;
        ConfirmSaveChange=()=>MessageBox.Show(this,"Há alterações pendentes no save aberto. Deseja salvá-las antes de abrir outro save?","Trocar save",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
        var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(24,8,24,8),BackColor=AppTheme.Background,WrapContents=true};
        var openButton=MakeButton("Abrir arquivo…",0,0,OpenSave);
        saveButton=MakeButton("Salvar com backup",0,0,SaveChanges);createButton=MakeButton("Criar Pokémon",0,0,CreatePokemon);((ThemeButton)createButton).Kind=ButtonKind.Primary;
        creationFormat.Items.AddRange(new object[]{"PK3 · GBA","PK4 · DS","PK5 · DS","PK6 · 3DS","PK7 · 3DS","PK8 · Switch","PK9 · Switch"});creationFormat.SelectedIndex=0;
        var encounterButton=MakeButton("Base de encontros",0,0,(_,_)=>OpenCreationEditor(true));
        exportBankButton=MakeButton("Exportar banco ZIP",0,0,ExportBank);importBankButton=MakeButton("Importar banco ZIP",0,0,ImportBank);
        openBankFolderButton=MakeButton("Abrir pasta do banco",0,0,OpenBankFolder);
        restoreRemovedButton=MakeButton("Restaurar removido…",0,0,RestoreRemovedPokemon);
        boxPicker.Width=210;boxPicker.Margin=new Padding(0,2,8,8);boxPicker.Items.Add("Banco global");boxPicker.SelectedIndexChanged+=(_,_)=>{if(boxPicker.SelectedIndex<0)return;if(save!=null&&boxPicker.SelectedIndex<save.BoxCount)selectedBox=boxPicker.SelectedIndex;selectedSlot=-1;bankPage=0;RenderSlots();};
        importPokemonButton=MakeButton("Importar Pokémon",0,0,ImportPokemonFile);
        exportPokemonButton=MakeButton("Exportar Pokémon",0,0,ExportPokemonFile);
        var toolsButton=MakeButton("Ferramentas do banco…",0,0,(_,_)=>{});
        var toolsMenu=new ContextMenuStrip{BackColor=AppTheme.Surface,ForeColor=AppTheme.Text,Font=AppTheme.Body};
        var toolCommands=new (Button Button,EventHandler Action)[]{(exportBankButton,ExportBank),(importBankButton,ImportBank),(openBankFolderButton,OpenBankFolder),(restoreRemovedButton,RestoreRemovedPokemon)};
        foreach(var command in toolCommands)
        {
            var item=new ToolStripMenuItem(command.Button.Text);item.Click+=command.Action;toolsMenu.Items.Add(item);
        }
        toolsMenu.Opening+=(_,_)=>{for(int i=0;i<toolCommands.Length;i++)toolsMenu.Items[i].Enabled=toolCommands[i].Button.Enabled&&(i!=3||IsBank);};
        toolsButton.Click+=(_,_)=>toolsMenu.Show(toolsButton,new Point(0,toolsButton.Height));
        Disposed+=(_,_)=>toolsMenu.Dispose();

        toolbar.Controls.AddRange(new Control[]{boxPicker,createButton,creationFormat,encounterButton,importPokemonButton,saveButton,toolsButton});
        var openFileItem=new ToolStripMenuItem("Abrir arquivo de save…");openFileItem.Click+=OpenSave;toolsMenu.Items.Add(openFileItem);
        var info=new Panel{Dock=DockStyle.Top,Height=48,BackColor=AppTheme.Background,Padding=new Padding(24,0,24,0)};
        openedContext.Dock=DockStyle.Top;openedContext.Height=26;openedContext.ForeColor=AppTheme.Text;openedContext.Font=AppTheme.CaptionBold;openedContext.Text="Nenhum save aberto";
        status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.ForeColor=AppTheme.TextMuted;status.Font=AppTheme.Caption;status.AutoEllipsis=true;info.Controls.Add(status);
        info.Controls.Add(openedContext);
        var body=new Panel{Dock=DockStyle.Fill,BackColor=AppTheme.Background,Padding=new Padding(24,0,24,24)};
        bankSpeciesLabel.Text="Buscar";bankSpeciesLabel.AutoSize=true;bankSpeciesLabel.ForeColor=AppTheme.TextMuted;bankSpeciesLabel.Font=AppTheme.Caption;bankSpeciesLabel.Margin=new Padding(0,8,8,0);
        bankSpeciesSearch.Width=240;bankSpeciesSearch.Height=32;bankSpeciesSearch.Margin=new Padding(0,0,12,8);bankSpeciesSearch.PlaceholderText="Número, espécie ou apelido";bankSpeciesSearch.AccessibleName="Filtrar banco por número, nome da espécie ou apelido";bankSpeciesSearch.BackColor=AppTheme.SurfaceRaised;bankSpeciesSearch.ForeColor=AppTheme.Text;bankSpeciesSearch.BorderStyle=BorderStyle.FixedSingle;bankSpeciesSearch.Font=AppTheme.Body;
        bankSortLabel.Text="Ordenar";bankSortLabel.AutoSize=true;bankSortLabel.ForeColor=AppTheme.TextMuted;bankSortLabel.Font=AppTheme.Caption;bankSortLabel.Margin=new Padding(8,8,8,0);
        bankSortPicker.Width=188;bankSortPicker.Height=32;bankSortPicker.Margin=new Padding(0,0,8,0);bankSortPicker.DropDownStyle=ComboBoxStyle.DropDownList;bankSortPicker.Items.AddRange(new object[]{"Mais recentes","Número da espécie","Menor nível","Geração da espécie","Nome da espécie A–Z","Maior nível","Número decrescente","Formato do arquivo","Captura · recentes"});bankSortPicker.SelectedIndex=0;
        bankSortPicker.SelectedIndexChanged+=(_,_)=>ApplyBankFilters();
        bankGenerationLabel.Text="Geração da espécie";bankGenerationLabel.AutoSize=true;bankGenerationLabel.ForeColor=AppTheme.TextMuted;bankGenerationLabel.Font=AppTheme.Caption;bankGenerationLabel.Margin=new Padding(8,8,8,0);
        bankGenerationPicker.Width=160;bankGenerationPicker.Height=32;bankGenerationPicker.Margin=new Padding(0,0,8,0);bankGenerationPicker.DropDownStyle=ComboBoxStyle.DropDownList;bankGenerationPicker.AccessibleName="Geração em que a espécie foi introduzida, independente do formato do arquivo";bankGenerationPicker.Items.AddRange(new object[]{"Todas","Gen 1 · Kanto","Gen 2 · Johto","Gen 3 · Hoenn","Gen 4 · Sinnoh","Gen 5 · Unova","Gen 6 · Kalos","Gen 7 · Alola","Gen 8 · Galar/Hisui","Gen 9 · Paldea"});bankGenerationPicker.SelectedIndex=0;
        bankGenerationPicker.SelectedIndexChanged+=(_,_)=>ApplyBankFilters();
        ConfigureBankFilter(bankDexPicker,"Pokédex regional",BankPokedexCatalog.Names,240);
        ConfigureBankFilter(bankFormatPicker,"Formato do arquivo",new[]{"Todos","PK3 · Gen 3","PK4 · Gen 4","PK5 · Gen 5","PK6 · Gen 6","PK7 · Gen 7","PK8 · Gen 8","PK9 · Gen 9"},160);
        ConfigureBankFilter(bankTypePicker,"Tipo",new[]{"Todos"}.Concat(PokemonTypeCatalog.Names).ToArray(),144);
        ConfigureBankFilter(bankShinyPicker,"Shiny",new[]{"Todos","Só shiny","Sem shiny"},132);
        ConfigureBankFilter(bankGenderPicker,"Sexo",new[]{"Todos","Macho","Fêmea","Sem sexo"},132);
        ConfigureBankFilter(bankEggPicker,"Ovos",new[]{"Todos","Só ovos","Sem ovos"},132);
        ConfigureBankFilter(bankDuplicatePicker,"Espécies repetidas",new[]{"Todas","Só repetidas","Uma por espécie"},160);
        bankDuplicatePicker.AccessibleDescription="A duplicidade é calculada entre os resultados depois dos outros filtros.";
        foreach(var level in new[]{bankMinLevel,bankMaxLevel}){level.Width=80;level.Font=AppTheme.Body;level.BackColor=AppTheme.SurfaceRaised;level.ForeColor=AppTheme.Text;level.ValueChanged+=(_,_)=>ApplyBankFilters();}
        bankMinLevel.AccessibleName="Nível mínimo";bankMaxLevel.AccessibleName="Nível máximo";
        bankExtraFilters.Controls.AddRange(new Control[]{FilterField("Formato do arquivo",bankFormatPicker),FilterField("Tipo",bankTypePicker),FilterField("Shiny",bankShinyPicker),FilterField("Sexo",bankGenderPicker),FilterField("Ovos",bankEggPicker),FilterField("Espécies repetidas",bankDuplicatePicker),FilterField("Nível mínimo",bankMinLevel),FilterField("Nível máximo",bankMaxLevel)});
        bankPreviousPage.AutoSize=true;bankNextPage.AutoSize=true;
        bankPreviousPage.AutoSize=true;bankPreviousPage.Margin=new Padding(0,0,8,0);bankPreviousPage.Click+=(_,_)=>ChangeBankPage(-1,false);
        bankNextPage.AutoSize=true;bankNextPage.Margin=new Padding(0,0,8,0);bankNextPage.Click+=(_,_)=>ChangeBankPage(1,false);
        bankPageStatus.AutoSize=true;bankPageStatus.ForeColor=AppTheme.TextMuted;bankPageStatus.Font=AppTheme.Caption;bankPageStatus.Margin=new Padding(4,9,8,0);
        bankSpeciesSearch.TextChanged+=(_,_)=>ApplyBankFilters();
        var clearFilters=MakeButton("Limpar filtros",0,0,(_,_)=>ClearBankFilters());
        var moreFilters=MakeButton("Mais filtros",0,0,(_,_)=>{});bankMoreFilters=(ThemeButton)moreFilters;
        bankNavigation.Controls.AddRange(new Control[]{FilterField("Buscar",bankSpeciesSearch),FilterField("Geração da espécie",bankGenerationPicker),FilterField("Pokédex regional",bankDexPicker),FilterField("Ordenar",bankSortPicker),moreFilters,clearFilters});
        bankPaging.Controls.AddRange(new Control[]{bankPreviousPage,bankPageStatus,bankNextPage});
        slots.Dock=DockStyle.Fill;slots.AutoScroll=true;slots.BackColor=Color.Transparent;slots.Padding=new Padding(0);slots.WrapContents=true;
        slots.Controls.Add(new EmptyStatePanel("Seu banco Pokémon","Escolha um jogo e um perfil à esquerda para abrir sua equipe e acessar a coleção global.","Abrir perfil",OpenProfileSave){Width=600,Height=180});
        var side=new Panel{Dock=DockStyle.Right,Width=272,BackColor=AppTheme.Surface,Padding=new Padding(12),AutoScroll=true};
        selectedSpritePreview.Height=112;selectedSpritePreview.Dock=DockStyle.Top;selectedSpritePreview.Margin=new Padding(0,0,0,8);selectedSpritePreview.SizeMode=PictureBoxSizeMode.Zoom;selectedSpritePreview.BackColor=AppTheme.SurfaceRaised;selectedSpritePreview.BorderStyle=BorderStyle.FixedSingle;selectedSpritePreview.Visible=false;selectedSpritePreview.AccessibleName="Prévia ampliada do Pokémon selecionado";selectedSpritePreview.AccessibleRole=AccessibleRole.Graphic;
        selectedSpritePreview.AccessibleDescription="A imagem do Pokémon selecionado aparece aqui em tamanho ampliado.";
        selectedSpritePreview.Paint+=(_,e)=>{if(selectedSpritePreview.Image==null)PaintTools.DrawPokeball(e.Graphics,new Rectangle((selectedSpritePreview.Width-42)/2,(selectedSpritePreview.Height-42)/2,42,42),AppTheme.Focus,AppTheme.Background);};
        Disposed+=(_,_)=>{selectedSpritePreviewImage?.Dispose();selectedSpritePreviewImage=null;};
        details.AutoSize=true;details.MaximumSize=new Size(228,0);details.Padding=new Padding(4,8,4,16);details.ForeColor=AppTheme.TextMuted;details.Font=AppTheme.Body;details.Text="Selecione um Pokémon para ver os detalhes.";
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=AppTheme.Surface,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
        editButton=MakeButton("Editar Pokémon",0,0,EditPokemon);moveButton=MakeButton("Mover Pokémon",0,0,MovePokemon);archiveButton=MakeButton("Copiar para banco",0,0,ArchivePokemon);
        removePokemonButton=MakeButton("Remover do banco",0,0,RemovePokemonFromBank);
        legalityButton=MakeButton("Verificar legalidade",0,0,AnalyzeSelectedPokemon);
        foreach(var action in new[]{editButton,legalityButton,moveButton,archiveButton,exportPokemonButton,removePokemonButton}){action.AutoSize=false;action.Width=240;action.Height=36;action.Margin=new Padding(0,0,0,4);}
        actions.Controls.AddRange(new Control[]{editButton,legalityButton,moveButton,archiveButton,exportPokemonButton,removePokemonButton});
        var selectionLayout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=3,BackColor=AppTheme.Surface,Margin=Padding.Empty};
        selectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));selectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));selectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));selectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actions.Dock=DockStyle.Top;selectionLayout.Controls.Add(selectedSpritePreview,0,0);selectionLayout.Controls.Add(details,0,1);selectionLayout.Controls.Add(actions,0,2);side.Controls.Add(selectionLayout);
        side.Controls.Add(bankExtraFilters);
        moreFilters.Click+=(_,_)=>{bankExtraFilters.Visible=!bankExtraFilters.Visible;selectionLayout.Visible=!bankExtraFilters.Visible;side.AutoScroll=!bankExtraFilters.Visible;UpdateExtraFilterCaption();};
        slots.ControlAdded+=(_,_)=>{if(!IsBank){bankExtraFilters.Visible=false;selectionLayout.Visible=true;side.AutoScroll=true;moreFilters.Text="Filtros";}};
        // Keep the search and paging strip outside the content area so the right-side
        // actions begin below it instead of being covered by the bank navigation row.
        var contentArea=new Panel{Dock=DockStyle.Fill,BackColor=AppTheme.Background};
        contentArea.Controls.Add(slots);contentArea.Controls.Add(bankPaging);contentArea.Controls.Add(side);
        body.Controls.Add(contentArea);body.Controls.Add(bankNavigation);
        slots.Resize+=(_,_)=>{foreach(var state in slots.Controls.OfType<EmptyStatePanel>())state.Width=Math.Max(280,slots.ClientSize.Width-20);};
        var profiles=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(24,8,24,0),BackColor=AppTheme.Background,WrapContents=true};
        gamePicker.Width=160;profilePicker.Width=170;savePicker.Width=280;
        foreach(var picker in new[]{gamePicker,profilePicker,savePicker})picker.Margin=new Padding(0,0,8,8);
        Label Caption(string text)=>new Label{Text=text,AutoSize=true,ForeColor=AppTheme.TextMuted,Font=AppTheme.Caption,Margin=new Padding(0,12,8,0)};
        var openProfile=MakeButton("Abrir perfil",0,0,OpenProfileSave);((ThemeButton)openProfile).Kind=ButtonKind.Primary;
        profiles.Controls.AddRange(new Control[]{Caption("Jogo"),gamePicker,Caption("Perfil"),profilePicker,savePicker,openProfile,MakeButton("Atualizar saves",0,0,(_,_)=>RefreshProfileSaves())});
        var cloudBar=new Panel{Dock=DockStyle.Top,Height=82,Padding=new Padding(24,8,24,6),BackColor=AppTheme.Surface};
        profiles.Visible=true;cloudBar.Visible=false;

        var cloudItem=new ToolStripMenuItem("Conta e cópia na nuvem…");cloudItem.Click+=(_,_)=>cloudBar.Visible=!cloudBar.Visible;toolsMenu.Items.Add(cloudItem);
        var cloudActions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=530,Height=48,AutoSize=false,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,BackColor=AppTheme.Surface};
        cloudLoginButton=MakeButton("Google",0,0,CloudLogin);cloudLoginButton.AutoSize=false;cloudLoginButton.Width=112;
        cloudRefreshButton=MakeButton("Atualizar",0,0,CloudRefresh);cloudRefreshButton.AutoSize=false;cloudRefreshButton.Width=106;
        cloudUploadButton=MakeButton("Enviar",0,0,CloudUpload);cloudUploadButton.AutoSize=false;cloudUploadButton.Width=86;
        cloudRestoreButton=MakeButton("Restaurar",0,0,CloudRestore);cloudRestoreButton.AutoSize=false;cloudRestoreButton.Width=120;
        cloudSignOutButton=MakeButton("Sair",0,0,CloudSignOut);cloudSignOutButton.AutoSize=false;cloudSignOutButton.Width=64;
        var cloudTips=new ToolTip();cloudTips.SetToolTip(cloudLoginButton,"Conectar ou trocar a conta Google do banco global");cloudTips.SetToolTip(cloudRefreshButton,"Verificar a cópia global desta conta");cloudTips.SetToolTip(cloudUploadButton,"Enviar a coleção local para esta conta");cloudTips.SetToolTip(cloudRestoreButton,"Restaurar a coleção salva nesta conta");cloudTips.SetToolTip(cloudSignOutButton,"Desconectar esta conta neste computador");cloudTips.SetToolTip(cloudStatus,"O login Google identifica a conta. O arquivo fica no Firebase/Firestore do Pokemons Play, não no Google Drive. O banco local é independente dos perfis; enviar e restaurar são ações manuais. A restauração guarda primeiro uma cópia local.");cloudTips.SetToolTip(bankSummary,"Banco local salvo em " + Path.Combine(root,"Pokemon Bank") + ". Pokémon conta cada arquivo válido; espécies conta identidades distintas, então repetidos não aumentam esse número. A coleção é compartilhada por todos os perfis de jogo deste computador.");
        cloudTips.SetToolTip(restoreRemovedButton,"A contagem inclui somente arquivos Pokémon válidos. Arquivos corrompidos continuam preservados na pasta de removidos.");
        cloudActions.Controls.AddRange(new Control[]{cloudLoginButton,cloudRefreshButton,cloudUploadButton,cloudRestoreButton,cloudSignOutButton});
        var cloudCopy=new Panel{Dock=DockStyle.Fill,BackColor=AppTheme.Surface};
        bankSummary.Dock=DockStyle.Top;bankSummary.Height=23;bankSummary.ForeColor=AppTheme.Text;bankSummary.Font=AppTheme.CaptionBold;bankSummary.AutoEllipsis=true;
        cloudStatus.Dock=DockStyle.Fill;cloudStatus.ForeColor=AppTheme.TextMuted;cloudStatus.Font=AppTheme.Caption;cloudStatus.AutoEllipsis=false;cloudStatus.Text=CloudSignedOutStatus;
        cloudCopy.Controls.Add(cloudStatus);cloudCopy.Controls.Add(bankSummary);cloudBar.Controls.Add(cloudCopy);cloudBar.Controls.Add(cloudActions);
        foreach(var game in games)gamePicker.Items.Add(game.Title);
        gamePicker.SelectedIndexChanged+=(_,_)=>RefreshProfiles();
        profilePicker.SelectedIndexChanged+=(_,_)=>RefreshProfileSaves();
        Controls.Add(body);Controls.Add(info);Controls.Add(toolbar);Controls.Add(profiles);Controls.Add(cloudBar);Controls.Add(new PixelHeader("Banco Pokémon","Sua equipe, seus perfis e sua coleção global.",100));SetLoadedState(false);RefreshCloudControls();RefreshBankSummary();boxPicker.SelectedIndex=0;
        BuildBankWorkspace(toolbar, profiles, info, body, contentArea, side, cloudBar, openProfile);
        if(games.Count>0)gamePicker.SelectedIndex=0;
    }
    private Button MakeButton(string text,int x,int y,EventHandler action)
    {
        var button=new ThemeButton(text,ButtonKind.Secondary){AutoSize=true,Margin=new Padding(0,0,8,8)};button.Click+=action;return button;
    }

    private static Control FilterField(string title,Control input)
    {
        var group=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent,Margin=new Padding(0,0,12,8)};
        group.Controls.Add(new Label{Text=title,AutoSize=true,Font=AppTheme.Caption,ForeColor=AppTheme.TextMuted,Margin=new Padding(0,0,0,4)});
        input.Margin=Padding.Empty;group.Controls.Add(input);return group;
    }

    private void ConfigureBankFilter(ThemeSelect picker,string title,string[] choices,int width)
    {
        picker.Width=width;picker.AccessibleName=title;picker.DropDownStyle=ComboBoxStyle.DropDownList;picker.Items.AddRange(choices);picker.SelectedIndex=0;
        picker.SelectedIndexChanged+=(_,_)=>ApplyBankFilters();
    }

    private void ApplyBankFilters()
    {
        if(updatingFilters)return;
        bankPage=0;selectedSlot=-1;RenderSlots();
    }

    private void UpdateExtraFilterCaption()
    {
        int count=new[]{bankFormatPicker,bankTypePicker,bankShinyPicker,bankGenderPicker,bankEggPicker,bankDuplicatePicker}.Count(p=>p.SelectedIndex>0)+(bankMinLevel.Value!=1||bankMaxLevel.Value!=100?1:0);
        bankMoreFilters.Text=bankExtraFilters.Visible?"Ver detalhes":count>0?$"Filtros ({count})":"Filtros";
    }

    private void ClearBankFilters()
    {
        updatingFilters=true;
        try{bankSpeciesSearch.Clear();foreach(var picker in new[]{bankGenerationPicker,bankDexPicker,bankFormatPicker,bankTypePicker,bankShinyPicker,bankGenderPicker,bankEggPicker,bankDuplicatePicker})picker.SelectedIndex=0;bankMinLevel.Value=1;bankMaxLevel.Value=100;}
        finally{updatingFilters=false;}
        ApplyBankFilters();
    }

    internal static int SpeciesGeneration(ushort species) => species switch
    {
        0=>0, <=151=>1, <=251=>2, <=386=>3, <=493=>4, <=649=>5, <=721=>6, <=809=>7, <=905=>8, _=>9
    };

    private void OpenSave(object sender, EventArgs e)
    {
        string savesPath = Path.Combine(root, "Saves");
        using var dialog = new OpenFileDialog
        {
            Title = "Selecione o save normal do jogo",
            InitialDirectory = Directory.Exists(savesPath) ? savesPath : root,
            Filter = "Arquivos de save|*.sav;*.srm;*.dsv;*.dat;*.bin|Todos os arquivos|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadSave(dialog.FileName, Path.GetExtension(dialog.FileName).Equals(".srm",StringComparison.OrdinalIgnoreCase)&&gamePicker.SelectedIndex>=0 ? games[gamePicker.SelectedIndex] : null);
    }

    private void RefreshProfiles()
    {
        profilePicker.Items.Clear();
        if(gamePicker.SelectedIndex<0)return;
        try
        {
            profileState=SaveProfileService.Load(root,games[gamePicker.SelectedIndex].SaveFolderName);
            foreach(var profile in profileState.Profiles)profilePicker.Items.Add(profile);
            int index=profileState.Profiles.FindIndex(p=>p.Id==profileState.ActiveId);
            // Force refresh when both games have the same selected index.
            profilePicker.SelectedIndex=-1;
            profilePicker.SelectedIndex=index;
        }
        catch(Exception ex){profileState=null;profilePicker.SelectedIndex=-1;profileSaves.Clear();savePicker.Items.Clear();savePicker.SelectedIndex=-1;status.Text=ex.Message;}
    }

    private void RefreshProfileSaves()
    {
        savePicker.Items.Clear();profileSaves.Clear();savePicker.SelectedIndex=-1;
        if(profileState==null||profilePicker.SelectedIndex<0)return;
        try
        {
            profileSaves=ProfileSaveLocator.Find(root,games[gamePicker.SelectedIndex],profileState.Profiles[profilePicker.SelectedIndex].Id);
            foreach(var file in profileSaves)savePicker.Items.Add(file);
            savePicker.Enabled=profileSaves.Count>0;
            if(profileSaves.Count>0)savePicker.SelectedIndex=0;
            else { savePicker.Items.Add("Nenhum save neste perfil"); savePicker.SelectedIndex=0; }
        }
        catch(Exception ex){status.Text="Não foi possível localizar o save: "+ex.Message;}
    }

    private void OpenProfileSave(object sender, EventArgs e)
    {
        try
        {
        if(gamePicker.SelectedIndex<0||profilePicker.SelectedIndex<0||profileState==null)return;
        SaveProfileService.EnsureEmulatorsClosed();
        string previous=savePicker.SelectedIndex>=0&&savePicker.SelectedIndex<profileSaves.Count?profileSaves[savePicker.SelectedIndex].Path:null;
        RefreshProfileSaves();
        int index=profileSaves.FindIndex(p=>string.Equals(p.Path,previous,StringComparison.OrdinalIgnoreCase));
        if(index>=0)savePicker.SelectedIndex=index;
        if(profileSaves.Count==0)
        {
            status.Text="Este perfil ainda não tem um save compatível. Abra o jogo com esse perfil e salve pelo menu do jogo.";
            return;
        }
        string path=profileSaves[savePicker.SelectedIndex].Path;
        LoadSave(path, games[gamePicker.SelectedIndex]);
        if(string.Equals(savePath,path,StringComparison.OrdinalIgnoreCase))
            openedContext.Text="Save aberto: "+games[gamePicker.SelectedIndex].Title+" · "+profileState.Profiles[profilePicker.SelectedIndex].Name;
        }
        catch(Exception ex){status.Text=ex.Message;}
    }

    private bool CanReplaceSave()
    {
        if(!hasUnsavedChanges)return true;
        var choice=ConfirmSaveChange();
        if(choice==DialogResult.Cancel)return false;
        if(choice==DialogResult.Yes){SaveChanges(this,EventArgs.Empty);return !hasUnsavedChanges;}
        return true;
    }
    internal bool PrepareForUpdate()=>CanReplaceSave();

    private void LoadSave(string path, GameInfo game = null)
    {
        try
        {
            SaveProfileService.EnsureEmulatorsClosed();
            var loaded = game == null ? SaveUtil.GetSaveFile(path) : ProfileSaveLocator.ReadForGame(path, game);
            if (loaded == null || loaded.Generation is < 3 or > 5 || !loaded.HasBox)
            {
                MessageBox.Show(this, "Este arquivo não foi reconhecido como save compatível de Pokémon das gerações 3–5. O arquivo não foi alterado.", "Save não suportado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if(!CanReplaceSave())return;
            loaded=game == null ? SaveUtil.GetSaveFile(path) : ProfileSaveLocator.ReadForGame(path, game);
            save = loaded;
            savePath = path;
            loadedGame = game;
            openedHash = HashFile(path);
            hasUnsavedChanges=false;
            openedContext.Text="Save aberto: arquivo manual · "+Path.GetFileName(path);
            selectedBox = 0;
            selectedSlot = -1;
            boxPicker.Items.Clear();
            foreach (var name in BoxUtil.GetBoxNames(save)) boxPicker.Items.Add(name);
            boxPicker.Items.Add("Equipe");
            boxPicker.Items.Add("Banco global");
            boxPicker.SelectedIndex = save.PartyCount>0 ? save.BoxCount : 0;
            status.Text = $"{save.Version} · Geração {save.Generation} · {Path.GetFileName(path)} · {save.BoxCount} caixas × {save.BoxSlotCount} espaços · {(save.ChecksumsValid ? "checksums OK" : "checksum requer atenção")}";
            SetLoadedState(true);
            RenderSlots();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível abrir o save. Nenhuma alteração foi gravada.\n\n" + ex.Message, "Erro ao ler save", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetLoadedState(bool loaded)
    {
        saveButton.Enabled = loaded;
        saveButton.Visible = loaded;
        createButton.Enabled = true;
        creationFormat.Visible = !loaded;
        boxPicker.Enabled = true;
        editButton.Enabled = moveButton.Enabled = archiveButton.Enabled = false;
        if (!loaded) status.Text = "O banco global pode ser consultado sem abrir um save. Abra um jogo para transferir Pokémon.";
    }

    private void RefreshCloudControls()
    {
        bool signedIn = cloud.IsSignedIn;
        cloudLoginButton.Text = signedIn ? "Trocar" : "Google";
        cloudLoginButton.Enabled = !cloudBusy;
        cloudSignOutButton.Enabled = signedIn && !cloudBusy;
        cloudRefreshButton.Enabled = signedIn && !cloudBusy;
        cloudUploadButton.Enabled = signedIn && !cloudBusy && PokemonBankCloudArchive.CountValid(Path.Combine(root, "Pokemon Bank")) > 0;
        cloudRestoreButton.Enabled = signedIn && !cloudBusy;
        if (!cloudBusy && cloudStatus.Text.StartsWith("Banco local neste PC.", StringComparison.Ordinal))
            cloudStatus.Text = signedIn ? "Conta conectada · verifique se há uma cópia online." : CloudSignedOutStatus;
    }

    private void RefreshBankSummary()
    {
        string folder = Path.Combine(root, "Pokemon Bank");
        int valid = 0;
        int supported = 0;
        var uniqueSpecies = new HashSet<ushort>();
        if (Directory.Exists(folder))
        {
            foreach (string path in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                if (!PokemonBankFileService.IsSupportedPokemonFile(path)) continue;
                supported++;
                BankSearchEntry entry = GetBankSearchEntry(path);
                if (!entry.Valid) continue;
                valid++;
                uniqueSpecies.Add(entry.Species);
            }
        }
        int invalid = Math.Max(0, supported - valid);
        int recoverable = PokemonBankFileService.RecoverableRemovedFiles(Path.Combine(root, "Pokemon Bank")).Length;
        restoreRemovedButton.Enabled = recoverable > 0;
        restoreRemovedButton.Text = recoverable > 0 ? $"Restaurar removido ({recoverable})" : "Restaurar removido…";
        restoreRemovedButton.AccessibleName = recoverable > 0 ? $"Restaurar um Pokémon removido; {recoverable} disponíveis" : "Nenhum Pokémon removido disponível para restauração";
        string account = cloud.IsSignedIn ? " · Conta: " + (string.IsNullOrWhiteSpace(cloud.GoogleEmail) ? "Google conectada" : cloud.GoogleEmail) : string.Empty;
        string speciesCount = uniqueSpecies.Count == 1 ? "1 espécie" : $"{uniqueSpecies.Count} espécies";
        bankSummary.Text = $"Coleção local independente · {valid} Pokémon · {speciesCount}" + (invalid > 0 ? $" · {invalid} inválido(s), não enviado(s)" : string.Empty) + account;
        cloudUploadButton.Enabled = cloud.IsSignedIn && !cloudBusy && valid > 0;
        exportBankButton.Enabled = valid > 0;
    }

    private void ExportBank(object sender, EventArgs e)
    {
        string folder = Path.Combine(root, "Pokemon Bank");
        using var dialog = new SaveFileDialog
        {
            Title = "Exportar banco global",
            InitialDirectory = Directory.Exists(folder) ? folder : root,
            FileName = "Pokemon-Bank-" + DateTime.Now.ToString("yyyyMMdd") + ".zip",
            Filter = "Backup do banco Pokémon (*.zip)|*.zip",
            DefaultExt = "zip",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string temporary = dialog.FileName + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            PokemonBankCloudArchive.CreateVerifiedArchive(folder, temporary);
            File.Move(temporary, dialog.FileName, true);
            status.Text = "Backup ZIP do Banco global exportado com validação.";
        }
        catch (Exception ex)
        {
            status.Text = "Falha ao exportar o Banco global.";
            MessageBox.Show(this, "Não foi possível criar o backup ZIP. O banco local não foi alterado.\n\n" + ex.Message, "Falha na exportação", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void ImportBank(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Importar banco global",
            InitialDirectory = root,
            Filter = "Backup do banco Pokémon (*.zip)|*.zip",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        bool restored = false;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length <= 0 || info.Length > 64L * 1024 * 1024)
                throw new InvalidDataException("O arquivo ZIP está vazio ou excede 64 MB.");
            PokemonBankCloudArchive.ValidateArchive(dialog.FileName);
            int count = PokemonBankCloudArchive.CountPokemonInArchive(dialog.FileName);
            string generations = PokemonBankCloudArchive.GenerationSummary(dialog.FileName);
            if (MessageBox.Show(this, BuildBankImportConfirmation(dialog.FileName, count, generations), "Importar Banco global", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            PokemonBankCloudArchive.Restore(File.ReadAllBytes(dialog.FileName), Path.Combine(root, "Pokemon Bank"));
            restored = true;
            RefreshAfterBankRestore($"Banco global restaurado · {count} Pokémon · cópia anterior em Backups/Automaticos.");
        }
        catch (Exception ex)
        {
            status.Text = "Falha ao importar o Banco global.";
            string outcome = restored ? "O backup foi aplicado, mas a tela não pôde ser atualizada." : "O backup não foi aplicado; a coleção local foi preservada.";
            MessageBox.Show(this, outcome + "\n\n" + ex.Message, "Falha na importação", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal static string BuildBankImportConfirmation(string archivePath, int count)
        => BuildBankImportConfirmation(archivePath, count, string.Empty);

    internal static string BuildBankImportConfirmation(string archivePath, int count, string generationSummary)
        => $"Importar {Path.GetFileName(archivePath)} com {count} Pokémon" + (string.IsNullOrWhiteSpace(generationSummary) ? "" : $" ({generationSummary})") + "? A coleção local será substituída. Antes, o app guardará uma cópia integral em Backups/Automaticos.";

    private void ImportPokemonFile(object sender, EventArgs e)
    {
        if (!IsBank) return;
        string folder = Path.Combine(root, "Pokemon Bank");
        using var dialog = new OpenFileDialog
        {
            Title = "Importar Pokémon para o banco global",
            InitialDirectory = Directory.Exists(folder) ? folder : root,
            Filter = PokemonImportFileFilter,
            FilterIndex = 1,
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string[] imported = PokemonBankFileService.ImportFiles(folder, dialog.FileNames);
            if (imported.Length == 0) return;
            bankSpeciesSearch.Clear();
            RefreshBankFiles();
            if (imported.Length == 1)
            {
                int bankIndex = Array.IndexOf(bankFiles, imported[0]);
                int viewIndex = Array.IndexOf(bankViewIndices, bankIndex);
                if (viewIndex >= 0)
                {
                    bankPage = viewIndex / BankPageSize;
                    SelectSlot(bankIndex);
                }
                else RenderSlots();
            }
            else RenderSlots();
            RefreshBankSummary();
            status.Text = BuildBankImportSummary(imported);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "O banco local não foi alterado. Confira se os arquivos PK3 a PK9 são válidos e se cada extensão corresponde à geração do Pokémon.\n\n" + ex.Message, "Falha ao importar Pokémon", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal static string BuildBankImportSummary(IEnumerable<string> importedFiles)
    {
        string[] files = importedFiles?.ToArray() ?? Array.Empty<string>();
        if (files.Length == 0) return "Nenhum Pokémon importado.";
        var counts = files.Select(path => PokemonBankFileService.Read(path).Format)
            .GroupBy(format => format)
            .OrderBy(group => group.Key)
            .Select(group => $"Gen {group.Key}: {group.Count()}");
        if (files.Length == 1)
            return $"Pokémon importado para o banco global · {Path.GetFileName(files[0])} · {counts.Single()}.";
        return $"{files.Length} Pokémon importados para o banco global · " + string.Join(" · ", counts) + ".";
    }

    private void ExportPokemonFile(object sender, EventArgs e)
    {
        if (!IsBank || selectedSlot < 0 || selectedSlot >= bankFiles.Length) return;
        try
        {
            PKM pokemon = GetSlot(selectedSlot);
            string extension = "." + pokemon.Extension.ToLowerInvariant();
            int filterIndex = extension switch { ".pk3" => 1, ".pk4" => 2, ".pk5" => 3, ".pk6" => 4, ".pk7" => 5, ".pk8" => 6, ".pk9" => 7, _ => throw new InvalidDataException("Formato de Pokémon não exportável.") };
            using var dialog = new SaveFileDialog
            {
                Title = "Exportar Pokémon selecionado",
                InitialDirectory = Path.Combine(root, "Pokemon Bank"),
                FileName = Path.GetFileNameWithoutExtension(bankFiles[selectedSlot]),
                Filter = "Pokémon Gen 3 (*.pk3)|*.pk3|Pokémon Gen 4 (*.pk4)|*.pk4|Pokémon Gen 5 (*.pk5)|*.pk5|Pokémon Gen 6 (*.pk6)|*.pk6|Pokémon Gen 7 (*.pk7)|*.pk7|Pokémon Gen 8 (*.pk8)|*.pk8|Pokémon Gen 9 (*.pk9)|*.pk9",
                FilterIndex = filterIndex,
                DefaultExt = extension.TrimStart('.'),
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            PokemonBankFileService.ExportFile(bankFiles[selectedSlot], dialog.FileName);
            status.Text = "Pokémon exportado · " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível exportar o Pokémon selecionado. O arquivo do banco permanece intacto.\n\n" + ex.Message, "Falha ao exportar Pokémon", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void CloudLogin(object sender, EventArgs e)
    {
        await RunCloud(async () =>
        {
            cloudStatus.Text = "Aguardando autenticação da conta Google…";
            await cloud.SignInAsync();
            cloudStatus.Text = "Conta conectada. Verificando a cópia global…";
            await RefreshRemoteBankStatus();
        });
    }

    private void CloudSignOut(object sender, EventArgs e)
    {
        cloud.SignOut();
        cloudStatus.Text = "Conta desconectada. O banco local continua neste computador.";
        RefreshCloudControls();
        RefreshBankSummary();
    }

    private async void CloudRefresh(object sender, EventArgs e)
    {
        RefreshBankSummary();
        await RunCloud(RefreshRemoteBankStatus);
    }

    private async Task RefreshRemoteBankStatus()
    {
        CloudBankInfo info = await cloud.ReadGlobalPokemonBankInfoAsync();
        if (!info.Exists)
        {
            cloudStatus.Text = "Esta conta ainda não tem uma cópia do banco global.";
            return;
        }
        string date = info.UpdatedAt.HasValue ? info.UpdatedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "data indisponível";
        cloudStatus.Text = $"Cópia na conta · {info.PokemonCount} Pokémon · atualizada em {date}.";
    }

    private async void CloudUpload(object sender, EventArgs e)
    {
        await RunCloud(async () =>
        {
            CloudBankInfo remote = await cloud.ReadGlobalPokemonBankInfoAsync();
            string account = string.IsNullOrWhiteSpace(cloud.GoogleEmail) ? "esta conta Google" : cloud.GoogleEmail;
            string folder = Path.Combine(root, "Pokemon Bank");
            string archive = Path.Combine(Path.GetTempPath(), "pokemons-play-bank-preflight-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                cloudStatus.Text = "Validando e compactando a coleção…";
                await Task.Run(() => PokemonBankCloudArchive.CreateVerifiedArchive(folder, archive));
                long archiveBytes = new FileInfo(archive).Length;
                string size = $"{archiveBytes / 1024d / 1024d:0.00} MB de {FirebaseCloudSaveService.MaxCloudArchiveBytes / 1024 / 1024} MB";
                if (!FirebaseCloudSaveService.IsCloudArchiveSizeAllowed(archiveBytes))
                {
                    cloudStatus.Text = $"Backup ZIP local: {size} · acima do limite da nuvem.";
                    MessageBox.Show(this, $"Este banco compactado tem {size} e não cabe no limite atual da nuvem. O banco local continua intacto; use Exportar banco ZIP para guardar ou transferir a coleção.", "Banco acima do limite da nuvem", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int localCount = PokemonBankCloudArchive.CountPokemonInArchive(archive);
                string target = BuildCloudUploadConfirmation(remote, account, localCount, size);
                if (MessageBox.Show(this, target, "Enviar banco global", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                cloudStatus.Text = "Enviando a cópia validada…";
                await cloud.UploadGlobalPokemonBankArchiveAsync(folder, archive, remote);
                cloudStatus.Text = "Banco global enviado para " + account + ".";
                await RefreshRemoteBankStatus();
            }
            finally
            {
                if (File.Exists(archive)) File.Delete(archive);
            }
        });
    }

    private async void CloudRestore(object sender, EventArgs e)
    {
        await RunCloud(async () =>
        {
            CloudBankInfo remote = await cloud.ReadGlobalPokemonBankInfoAsync();
            if (!remote.Exists)
            {
                cloudStatus.Text = "Esta conta ainda não tem um backup para restaurar.";
                return;
            }
            string account = string.IsNullOrWhiteSpace(cloud.GoogleEmail) ? "sua conta Google" : cloud.GoogleEmail;
            string warning = BuildCloudRestoreConfirmation(remote, account);
            if (MessageBox.Show(this, warning, "Restaurar banco global", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            cloudStatus.Text = "Baixando e validando a coleção…";
            await cloud.DownloadGlobalPokemonBankAsync(Path.Combine(root, "Pokemon Bank"), remote);
            RefreshAfterBankRestore($"Banco global restaurado da nuvem · {remote.PokemonCount} Pokémon.");
            cloudStatus.Text = "Coleção restaurada. A versão anterior foi preservada em Backups\\Automaticos.";
        });
    }

    internal static string BuildCloudRestoreConfirmation(CloudBankInfo remote, string account)
    {
        string updated = remote.UpdatedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "data indisponível";
        string size = remote.CompressedBytes > 0 ? $"{remote.CompressedBytes / 1024d / 1024d:0.00} MB" : "tamanho indisponível";
        return $"Restaurar os {remote.PokemonCount} Pokémon da cópia de {account}, atualizada em {updated} ({size})? A coleção deste computador será substituída; uma cópia local completa será criada antes.";
    }

    internal static string BuildCloudUploadConfirmation(CloudBankInfo remote, string account, int localCount, string localSize)
    {
        if (!remote.Exists)
            return $"Enviar {localCount} Pokémon ({localSize}) para {account}? A cópia ficará vinculada a essa conta Google, separada dos saves e perfis dos jogos.";

        string updated = remote.UpdatedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "data indisponível";
        string remoteSize = remote.CompressedBytes > 0 ? $"{remote.CompressedBytes / 1024d / 1024d:0.00} MB" : "tamanho indisponível";
        return $"Enviar {localCount} Pokémon ({localSize}) para {account}? Isso substituirá a cópia atual de {remote.PokemonCount} Pokémon, atualizada em {updated} ({remoteSize}).";
    }

    private void RefreshAfterBankRestore(string message)
    {
        ClearBankFilters();
        selectedSlot = -1;
        bankPage = 0;
        RefreshBankFiles();
        RenderSlots();
        RefreshBankSummary();
        status.Text = message;
    }

    private async Task RunCloud(Func<Task> operation)
    {
        if (cloudBusy) return;
        cloudBusy = true;
        RefreshCloudControls();
        try { await operation(); }
        catch (Exception ex) { cloudStatus.Text = ex.Message; }
        finally { cloudBusy = false; RefreshCloudControls(); RefreshBankSummary(); }
    }

    private bool IsParty => save != null && boxPicker.SelectedIndex == save.BoxCount;
    private bool IsBank => save == null ? boxPicker.SelectedIndex == 0 : boxPicker.SelectedIndex == save.BoxCount + 1;
    private int SlotCount => IsBank ? bankViewIndices.Length : IsParty ? 6 : save?.BoxSlotCount ?? 0;
    private PKM GetSlot(int index) => IsBank ? ReadBankPokemon(bankFiles[index]) : IsParty ? save.GetPartySlotAtIndex(index) : save.GetBoxSlotAtIndex(selectedBox, index);

    private PKM ReadBankPokemon(string path)
        => PokemonBankFileService.Read(path);

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == Keys.Escape && IsBank && bankSpeciesSearch.ContainsFocus && bankSpeciesSearch.Text.Length > 0)
        {
            bankSpeciesSearch.Clear();
            return true;
        }
        if (keyData == (Keys.Control | Keys.F) && IsBank && bankSpeciesSearch.Visible)
        {
            bankSpeciesSearch.Focus();
            return true;
        }
        if(keyData==(Keys.Control|Keys.S)&&saveButton.Enabled){SaveChanges(this,EventArgs.Empty);return true;}
        if(keyData==Keys.F2&&editButton.Enabled){EditPokemon(this,EventArgs.Empty);return true;}
        return base.ProcessCmdKey(ref message, keyData);
    }

    private void RefreshBankFiles()
    {
        string folder = Path.Combine(root, "Pokemon Bank");
        bankFiles = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pk?", SearchOption.TopDirectoryOnly) : Array.Empty<string>();
        Array.Sort(bankFiles, StringComparer.OrdinalIgnoreCase);
        var currentFiles = new HashSet<string>(bankFiles, StringComparer.OrdinalIgnoreCase);
        foreach (string cachedPath in bankSearchCache.Keys.Where(path => !currentFiles.Contains(path)).ToArray())
            bankSearchCache.Remove(cachedPath);
        string query = bankSpeciesSearch.Text.Trim();
        IEnumerable<int> filtered = Enumerable.Range(0, bankFiles.Length).Where(i => GetBankSearchEntry(bankFiles[i]).Valid);
        if (bankGenerationPicker.SelectedIndex > 0)
        {
            int generation = bankGenerationPicker.SelectedIndex;
            filtered = filtered.Where(i => SpeciesGeneration(GetBankSearchEntry(bankFiles[i]).Species) == generation);
        }
        if(bankDexPicker.SelectedIndex==BankPokedexCatalog.NationalSaveIndex)filtered=filtered.Where(i=>save!=null&&GetBankSearchEntry(bankFiles[i]).Species<=save.MaxSpeciesID);
        else if(bankDexPicker.SelectedIndex>0)filtered=filtered.Where(i=>BankPokedexCatalog.Contains(bankDexPicker.SelectedIndex,GetBankSearchEntry(bankFiles[i]).Species));
        if(bankFormatPicker.SelectedIndex>0)filtered=filtered.Where(i=>GetBankSearchEntry(bankFiles[i]).Format==bankFormatPicker.SelectedIndex+2);
        if(bankTypePicker.SelectedIndex>0)filtered=filtered.Where(i=>{var entry=GetBankSearchEntry(bankFiles[i]);int type=bankTypePicker.SelectedIndex-1;return entry.Type1==type||entry.Type2==type;});
        if(bankShinyPicker.SelectedIndex>0)filtered=filtered.Where(i=>GetBankSearchEntry(bankFiles[i]).Shiny==(bankShinyPicker.SelectedIndex==1));
        if(bankGenderPicker.SelectedIndex>0)filtered=filtered.Where(i=>GetBankSearchEntry(bankFiles[i]).Gender==bankGenderPicker.SelectedIndex-1);
        if(bankEggPicker.SelectedIndex>0)filtered=filtered.Where(i=>GetBankSearchEntry(bankFiles[i]).Egg==(bankEggPicker.SelectedIndex==1));
        filtered=filtered.Where(i=>{var level=GetBankSearchEntry(bankFiles[i]).Level;return level>=bankMinLevel.Value&&level<=bankMaxLevel.Value;});
        if (query.Length > 0 && int.TryParse(query, out int species) && species > 0)
        {
            filtered = filtered.Where(i => GetBankSearchEntry(bankFiles[i]).Species == species);
        }
        else if (query.Length > 0)
        {
            filtered = filtered.Where(i =>
            {
                BankSearchEntry entry = GetBankSearchEntry(bankFiles[i]);
                return entry.Valid && (ContainsSearchText(entry.Nickname, query) || ContainsSearchText(entry.SpeciesSearchText, query) || ContainsSearchText(Path.GetFileName(bankFiles[i]), query));
            });
        }
        var repeatedSpecies = new HashSet<ushort>();
        if (bankDuplicatePicker.SelectedIndex > 0)
        {
            repeatedSpecies = filtered
                .Select(i => GetBankSearchEntry(bankFiles[i]).Species)
                .GroupBy(species => species)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet();
            if (bankDuplicatePicker.SelectedIndex == 1)
                filtered = filtered.Where(i => repeatedSpecies.Contains(GetBankSearchEntry(bankFiles[i]).Species));
        }

        int[] sortedIndices = bankSortPicker.SelectedIndex switch
        {
            0 => filtered.OrderByDescending(i => File.GetLastWriteTimeUtc(bankFiles[i])).ThenBy(i => bankFiles[i], StringComparer.OrdinalIgnoreCase).ToArray(),
            1 => SortBankEntries(filtered, i => GetBankSearchEntry(bankFiles[i]).Species),
            2 => SortBankEntries(filtered, i => GetBankSearchEntry(bankFiles[i]).Level),
            3 => SortBankEntries(filtered, i => SpeciesGeneration(GetBankSearchEntry(bankFiles[i]).Species)),
            4 => filtered.OrderBy(i=>GetBankSearchEntry(bankFiles[i]).SpeciesSearchText,StringComparer.CurrentCultureIgnoreCase).ThenBy(i=>bankFiles[i],StringComparer.OrdinalIgnoreCase).ToArray(),
            5 => SortBankEntries(filtered,i=>-GetBankSearchEntry(bankFiles[i]).Level),
            6 => SortBankEntries(filtered,i=>-GetBankSearchEntry(bankFiles[i]).Species),
            7 => SortBankEntries(filtered,i=>GetBankSearchEntry(bankFiles[i]).Format),
            8 => filtered.OrderBy(i => GetBankSearchEntry(bankFiles[i]).MetDate.HasValue ? 0 : 1)
                .ThenByDescending(i => GetBankSearchEntry(bankFiles[i]).MetDate)
                .ThenBy(i => GetBankSearchEntry(bankFiles[i]).Species)
                .ThenBy(i => bankFiles[i], StringComparer.OrdinalIgnoreCase).ToArray(),
            _ => Array.Empty<int>()
        };
        if (bankDuplicatePicker.SelectedIndex == 2)
        {
            var includedSpecies = new HashSet<ushort>();
            sortedIndices = sortedIndices.Where(i => includedSpecies.Add(GetBankSearchEntry(bankFiles[i]).Species)).ToArray();
        }
        bankViewIndices = sortedIndices;
        int maxPage = Math.Max(0, (bankViewIndices.Length - 1) / BankPageSize);
        bankPage = Math.Clamp(bankPage, 0, maxPage);
    }

    private int[] SortBankEntries(IEnumerable<int> indices, Func<int, int> key)
    {
        return indices.Where(i => GetBankSearchEntry(bankFiles[i]).Valid)
            .OrderBy(key)
            .ThenBy(i => GetBankSearchEntry(bankFiles[i]).Species)
            .ThenBy(i => bankFiles[i], StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private BankSearchEntry GetBankSearchEntry(string path)
    {
        var info = new FileInfo(path);
        long modified = info.LastWriteTimeUtc.Ticks;
        if (bankSearchCache.TryGetValue(path, out BankSearchEntry cached) && cached.Length == info.Length && cached.LastWriteTicks == modified)
            return cached;

        BankSearchEntry entry;
        try
        {
            PKM pokemon = ReadBankPokemon(path);
            string localizedSpecies = GetSpeciesName(pokemon);
            string englishSpecies = SpeciesName.GetSpeciesNameGeneration(pokemon.Species, 2, (byte)pokemon.Format) ?? string.Empty;
            string speciesSearchText = string.Equals(localizedSpecies, englishSpecies, StringComparison.OrdinalIgnoreCase)
                ? localizedSpecies
                : localizedSpecies + " " + englishSpecies;
            entry = new BankSearchEntry(info.Length, modified, pokemon.Species, pokemon.Nickname ?? string.Empty, speciesSearchText, pokemon.CurrentLevel, pokemon.Format, pokemon.IsShiny,pokemon.Gender,pokemon.IsEgg,pokemon.PersonalInfo.Type1,pokemon.PersonalInfo.Type2,pokemon.MetDate,true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            entry = new BankSearchEntry(info.Length, modified, 0, string.Empty, string.Empty, 0, 0, false,0,false,0,0,null,false);
        }
        bankSearchCache[path] = entry;
        return entry;
    }

    private void RenderSlots()
    {
        SyncWorkspace();
        UpdateExtraFilterCaption();
        moveButton.Text = IsBank ? "Copiar para caixa…" : "Mover / copiar…";
        moveButton.AccessibleName = IsBank ? "Copiar Pokémon do Banco global para o save aberto" : "Mover Pokémon entre slots do save";
        removePokemonButton.Enabled = IsBank && selectedSlot >= 0 && selectedSlot < bankFiles.Length;
        restoreRemovedButton.Visible = IsBank;
        archiveButton.Visible = !IsBank;
        removePokemonButton.Visible = IsBank;
        legalityButton.Enabled = selectedSlot >= 0 && (IsBank ? selectedSlot < bankFiles.Length : selectedSlot < SlotCount && GetSlot(selectedSlot).Species != 0);
        slots.SuspendLayout();
        selectedSpritePreview.Image=null;selectedSpritePreviewImage?.Dispose();selectedSpritePreviewImage=null;selectedSpritePreview.Visible=false;
        while (slots.Controls.Count > 0)
        {
            Control previous = slots.Controls[0];
            slots.Controls.RemoveAt(0);
            previous.Dispose();
        }
        if (IsBank) RefreshBankFiles();
        if(IsBank&&selectedSlot>=0&&!bankViewIndices.Contains(selectedSlot))selectedSlot=-1;
        legalityButton.Enabled=selectedSlot>=0&&(IsBank||GetSlot(selectedSlot).Species!=0);
        removePokemonButton.Enabled=IsBank&&selectedSlot>=0&&selectedSlot<bankFiles.Length;
        importPokemonButton.Enabled = IsBank;
        exportPokemonButton.Enabled = IsBank && selectedSlot >= 0 && selectedSlot < bankFiles.Length;
        bankNavigation.Visible = IsBank;
        bankPaging.Visible = false;
        exportPokemonButton.Visible = IsBank;
        if(selectedSlot < 0){editButton.Enabled=moveButton.Enabled=archiveButton.Enabled=false;details.Text="Selecione um Pokémon\n\nClique em um sprite para ver atributos e ações.\n\nDuplo clique para editar. Espaços vazios abrem a criação.";}
        bankSpeciesLabel.Visible = bankSpeciesSearch.Visible = bankGenerationLabel.Visible = bankGenerationPicker.Visible = bankSortLabel.Visible = bankSortPicker.Visible = bankPreviousPage.Visible = bankNextPage.Visible = bankPageStatus.Visible = IsBank;
        int pageCount = Math.Max(1, (bankViewIndices.Length + BankPageSize - 1) / BankPageSize);
        bankPageStatus.Text = bankViewIndices.Length == 0 ? "0 Pokémon" : $"Página {bankPage + 1} de {pageCount} · {bankPage * BankPageSize + 1}–{Math.Min((bankPage + 1) * BankPageSize, bankViewIndices.Length)} de {bankViewIndices.Length} Pokémon";
        bankPreviousPage.Enabled = IsBank && bankPage > 0;
        bankNextPage.Enabled = IsBank && (bankPage + 1) * BankPageSize < bankViewIndices.Length;
        if (save == null && !IsBank) { slots.ResumeLayout(); return; }
        if (IsBank && bankFiles.Length == 0)
        {
            details.Text = "Banco global\n\nUse Criar Pokémon, Base de encontros ou Importar Pokémon para começar.\n\nSelecione um jogo e perfil para abrir sua equipe e caixas.";
            editButton.Enabled = moveButton.Enabled = archiveButton.Enabled = false;
            slots.Controls.Add(new EmptyStatePanel("Comece sua coleção", "Crie um Pokémon no editor, escolha um modelo na base de encontros ou importe um arquivo. Para acessar sua equipe, abra o jogo e perfil à esquerda.", "Criar Pokémon", CreatePokemon) { Width = Math.Max(280, slots.ClientSize.Width - 20), Height = 180 });
        }
        if (IsBank && bankFiles.Length > 0 && bankViewIndices.Length == 0)
        {
            if (!bankFiles.Any(path=>GetBankSearchEntry(path).Valid))
                slots.Controls.Add(new EmptyStatePanel("Nenhum Pokémon válido", $"{bankFiles.Length} arquivo(s) foram mantidos, mas não passaram na validação. Confira a pasta do banco.", "Abrir pasta", OpenBankFolder) { Width = Math.Max(280, slots.ClientSize.Width - 20), Height = 180 });
            else
            {
                bool noRepeatedSpecies = bankDuplicatePicker.SelectedIndex == 1;
                string title = noRepeatedSpecies
                    ? "Nenhuma espécie repetida"
                    : bankGenerationPicker.SelectedIndex > 0 ? $"Nenhum Pokémon da Gen {bankGenerationPicker.SelectedIndex}" : "Nenhum Pokémon encontrado";
                string description = noRepeatedSpecies
                    ? "Nenhuma espécie aparece mais de uma vez nos resultados atuais. Limpe outros filtros ou selecione Todas para voltar à coleção completa."
                    : bankDexPicker.SelectedIndex == BankPokedexCatalog.NationalSaveIndex && save == null
                        ? "Abra um save para consultar sua Pokédex nacional ou selecione Toda a coleção."
                        : bankMinLevel.Value > bankMaxLevel.Value
                            ? "O nível mínimo está acima do máximo. Ajuste o intervalo ou limpe os filtros."
                            : "Nenhum Pokémon corresponde à combinação de busca e filtros. Ajuste as opções ou limpe os filtros.";
                slots.Controls.Add(new EmptyStatePanel(title, description, "Limpar filtros", (_, _) => ClearBankFilters()) { Width = Math.Max(280, slots.ClientSize.Width - 20), Height = 180 });
            }
        }
        int firstItem = IsBank ? bankPage * BankPageSize : 0;
        int visibleCount = IsBank ? Math.Min(BankPageSize, Math.Max(0, SlotCount - firstItem)) : SlotCount;
        for (int i = 0; i < visibleCount; i++)
        {
            int slotIndex = IsBank ? bankViewIndices[firstItem + i] : i;
            int bankPosition = firstItem + i + 1;
            PKM pk;
            try { pk = GetSlot(slotIndex); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
            {
                status.Text = "Arquivo do banco ignorado: " + ex.Message;
                continue;
            }
            bool occupied = pk.Species != 0;
            var button = new PokemonSlotButton
            {
                Width = IsBank ? 192 : 136, Height = IsBank ? 118 : 96, Margin = new Padding(0,0,8,8), FlatStyle = FlatStyle.Flat,
                BackColor = slotIndex == selectedSlot ? AppTheme.SurfaceSelected : occupied ? AppTheme.SurfaceRaised : AppTheme.Surface,
                ForeColor = occupied ? AppTheme.Text : AppTheme.TextMuted,
                BoxMode = true, Font = AppTheme.Body, TextAlign = ContentAlignment.MiddleCenter,
                Text = IsBank ? $"#{bankPosition} · {PokemonLabel(pk)}\nNv. {pk.CurrentLevel} · Gen {pk.Format}" : occupied ? $"#{i + 1}\n{PokemonLabel(pk)}\nNv. {pk.CurrentLevel}" : $"#{i + 1}\n— vazio —",
                Cursor = Cursors.Hand
            };
            button.Tag=slotIndex;
            if(IsBank)
            {
                button.Title=PokemonLabel(pk);button.Metadata=$"Nº {pk.Species:D4} · Espécie G{SpeciesGeneration(pk.Species)}";
                button.Facts=$"Nv. {pk.CurrentLevel} · PK{pk.Format}"+(pk.IsEgg?" · Ovo":pk.IsShiny?" · Shiny":"");
                button.PrimaryType=pk.PersonalInfo.Type1;button.SecondaryType=pk.PersonalInfo.Type2;
                string types=string.Join(" · ",new[]{PokemonTypeCatalog.GetName(button.PrimaryType),PokemonTypeCatalog.GetName(button.SecondaryType)}.Where(name=>!string.IsNullOrEmpty(name)).Distinct());
                button.AccessibleName=$"{button.Title}, geração da espécie {SpeciesGeneration(pk.Species)}, tipos {types}, formato PK{pk.Format}, {button.Facts}, {GenderLabel(pk.Gender)}";
                button.AccessibleDescription="Use as setas para navegar pelos cartões vizinhos; PageUp e PageDown mudam de página; Enter ou Espaço seleciona o Pokémon.";
            }
            else if (occupied)
            {
                button.PokemonName=PokemonLabel(pk);
                button.Facts=$"Nv. {pk.CurrentLevel}"+(pk.IsEgg?" · Ovo":pk.IsShiny?" · Shiny":"");
                button.AccessibleName=$"Slot {i+1}, {button.PokemonName}, {button.Facts}, {GenderLabel(pk.Gender)}";
                button.AccessibleDescription="O sprite aparece à esquerda do nome; selecione o slot para ver os detalhes e as ações.";
            }
            if (occupied)
                button.HandleCreated += (_, _) => _ = LoadPokemonSpriteAsync(button, pk.Species, pk.IsShiny, pk.Gender == 1);
            button.FlatAppearance.BorderColor = slotIndex == selectedSlot ? AppTheme.Focus : AppTheme.BorderSoft;
            button.Click += (_, _) => SelectSlot(slotIndex);
            button.DoubleClick += (_, _) => {SelectSlot(slotIndex);if(occupied)EditPokemon(button,EventArgs.Empty);else CreatePokemon(button,EventArgs.Empty);};
            button.KeyDown += NavigateBankCards;
            workspaceTips.SetToolTip(button,occupied?$"{PokemonLabel(pk)} · Nv. {pk.CurrentLevel} · PK{pk.Format}\nDuplo clique ou F2 para editar":$"Espaço {i+1} disponível · duplo clique para criar");
            slots.Controls.Add(button);
        }
        slots.ResumeLayout();
        LayoutBoxCells();
        UpdateSelectedSpritePreview();SyncWorkspace();RefreshSelectionLegality();
    }

    private void NavigateBankCards(object sender, KeyEventArgs e)
    {
        if (e.Modifiers != Keys.None || sender is not PokemonSlotButton current)
            return;
        if (e.KeyCode is Keys.PageUp or Keys.PageDown)
        {
            if (!ChangeBankPage(e.KeyCode == Keys.PageDown ? 1 : -1, true))
                return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        PokemonSlotButton next = FindDirectionalBankCard(current, e.KeyCode);
        if (next == null)
            return;

        e.Handled = true;
        e.SuppressKeyPress = true;
        slots.ScrollControlIntoView(next);
        SelectSlot((int)next.Tag);
        next.Focus();
    }

    private bool ChangeBankPage(int offset, bool focusFirstPokemon)
    {
        if (!IsBank || offset == 0)
            return false;

        int maxPage = Math.Max(0, (bankViewIndices.Length - 1) / BankPageSize);
        int nextPage = Math.Clamp(bankPage + Math.Sign(offset), 0, maxPage);
        if (nextPage == bankPage)
            return false;

        bankPage = nextPage;
        selectedSlot = -1;
        RenderSlots();
        if (focusFirstPokemon && slots.Controls.OfType<PokemonSlotButton>().FirstOrDefault() is PokemonSlotButton firstPokemon)
        {
            slots.ScrollControlIntoView(firstPokemon);
            SelectSlot((int)firstPokemon.Tag);
            firstPokemon.Focus();
        }
        return true;
    }

    private static PokemonSlotButton FindDirectionalBankCard(PokemonSlotButton current, Keys direction)
    {
        if (current?.Parent == null || direction is not (Keys.Left or Keys.Right or Keys.Up or Keys.Down))
            return null;

        int centerX = current.Left + current.Width / 2;
        int centerY = current.Top + current.Height / 2;
        bool horizontal = direction is Keys.Left or Keys.Right;
        var candidates = current.Parent.Controls.OfType<PokemonSlotButton>()
            .Where(candidate => candidate != current)
            .Select(candidate => new
            {
                Button = candidate,
                DeltaX = candidate.Left + candidate.Width / 2 - centerX,
                DeltaY = candidate.Top + candidate.Height / 2 - centerY
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
        return candidates.Select(candidate => candidate.Button).FirstOrDefault();
    }

    private async Task LoadPokemonSpriteAsync(PokemonSlotButton button, int species, bool shiny, bool female)
    {
        Image sprite = await PokemonSpriteService.LoadAsync(species, shiny, female);
        if (sprite == null)
            return;
        if (button.IsDisposed)
        {
            sprite.Dispose();
            return;
        }
        button.Sprite = sprite;
        button.Invalidate();
        if (button.Tag is int index && index == selectedSlot)
            UpdateSelectedSpritePreview();
    }

    private static string PokemonLabel(PKM pk)
    {
        string speciesName = GetSpeciesName(pk);
        return pk.IsNicknamed && !string.IsNullOrWhiteSpace(pk.Nickname) && !string.Equals(pk.Nickname, speciesName, StringComparison.OrdinalIgnoreCase)
            ? $"{pk.Nickname} ({speciesName})"
            : speciesName;
    }

    private static string GetSpeciesName(PKM pokemon)
    {
        try
        {
            string name = SpeciesName.GetSpeciesNameGeneration(pokemon.Species, pokemon.Language, (byte)pokemon.Format);
            return string.IsNullOrWhiteSpace(name) ? $"Espécie {pokemon.Species}" : name;
        }
        catch (ArgumentOutOfRangeException)
        {
            return $"Espécie {pokemon.Species}";
        }
    }

    private void AnalyzeSelectedPokemon(object sender, EventArgs e)
    {
        if (selectedSlot < 0 || selectedSlot >= SlotCount) return;
        PKM pokemon = GetSlot(selectedSlot);
        if (pokemon.Species == 0) return;
        try
        {
            PokemonLegalityResult result = PokemonLegalityService.Analyze(pokemon);
            using var dialog = new PokemonLegalityDialog(PokemonLabel(pokemon), result);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "O PKHeX não conseguiu concluir a análise deste Pokémon. O arquivo não foi alterado.\n\n" + ex.Message, "Falha na análise", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenBankFolder(object sender, EventArgs e)
    {
        string folder = Path.Combine(root, "Pokemon Bank");
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
            status.ForeColor = AppTheme.Green;
            status.Text = "Pasta do banco aberta no Explorador.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            status.ForeColor = AppTheme.Red;
            status.Text = "Não foi possível abrir a pasta do banco: " + ex.Message;
        }
    }

    internal static string BuildBankRemoveConfirmation(string fileName, string backupFolder)
        => $"Remover {fileName} do Banco global? O Pokémon e os dados de origem serão movidos para {backupFolder}, onde poderão ser recuperados.";

    internal static string BuildRemovedRestoreConfirmation(string fileName)
        => $"Restaurar {fileName} para o Banco global? O backup recuperável será mantido se o nome original já estiver ocupado.";

    private void RestoreRemovedPokemon(object sender, EventArgs e)
    {
        if (!IsBank) return;
        string bankFolder = Path.Combine(root, "Pokemon Bank");
        string removedFolder = PokemonBankFileService.RemovedFilesFolder(bankFolder);
        using var dialog = new OpenFileDialog
        {
            Title = "Restaurar Pokémon removido",
            InitialDirectory = Directory.Exists(removedFolder) ? removedFolder : root,
            Filter = PokemonImportFileFilter,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (MessageBox.Show(this, BuildRemovedRestoreConfirmation(Path.GetFileName(dialog.FileName)), "Restaurar Pokémon", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string restored = PokemonBankFileService.RestoreRemovedFile(bankFolder, dialog.FileName);
            bankSpeciesSearch.Clear();
            RefreshBankFiles();
            selectedSlot = Array.IndexOf(bankFiles, restored);
            RenderSlots();
            if (selectedSlot >= 0) SelectSlot(selectedSlot);
            RefreshBankSummary();
            status.Text = "Pokémon restaurado no Banco global · " + Path.GetFileName(restored);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível restaurar este Pokémon. O arquivo recuperável permanece na pasta de removidos.\n\n" + ex.Message, "Falha ao restaurar Pokémon", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemovePokemonFromBank(object sender, EventArgs e)
    {
        if (!IsBank || selectedSlot < 0 || selectedSlot >= bankFiles.Length) return;
        string source = bankFiles[selectedSlot];
        string backupFolder = Path.Combine(root, "Backups", "Automaticos", "Pokemon-Banco-Removidos");
        if (MessageBox.Show(this, BuildBankRemoveConfirmation(Path.GetFileName(source), backupFolder), "Remover Pokémon", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            string backup = PokemonBankFileService.RemoveFile(Path.Combine(root, "Pokemon Bank"), source);
            selectedSlot = -1;
            RefreshBankFiles();
            RenderSlots();
            RefreshBankSummary();
            status.Text = "Pokémon removido do Banco · cópia recuperável em " + Path.GetRelativePath(root, backup);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível remover o Pokémon. O Banco local foi preservado.\n\n" + ex.Message, "Falha ao remover Pokémon", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SelectSlot(int index)
    {
        selectedSlot = index;
        RefreshSelectionLegality();
        var pk = GetSlot(index);
        if (IsBank)
        {
            details.Text = InspectorText(pk);
            editButton.Enabled = true;
            moveButton.Enabled = save != null && pk.GetType() == save.PKMType;
            archiveButton.Enabled = false;
            UpdateSelection();
            return;
        }
        editButton.Enabled = moveButton.Enabled = archiveButton.Enabled = pk.Species != 0;
        details.Text = pk.Species == 0 ? $"Espaço {index+1} disponível\n\nUse Criar Pokémon para preencher este espaço.\nDuplo clique também abre a criação." : InspectorText(pk);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        removePokemonButton.Enabled=exportPokemonButton.Enabled=IsBank&&selectedSlot>=0&&selectedSlot<bankFiles.Length;
        legalityButton.Enabled=selectedSlot>=0&&(IsBank||GetSlot(selectedSlot).Species!=0);
        foreach(var button in slots.Controls.OfType<PokemonSlotButton>())
        {
            bool selected=button.Tag is int index&&index==selectedSlot;
            button.BackColor=selected?AppTheme.SurfaceSelected:button.ForeColor==AppTheme.Text?AppTheme.SurfaceRaised:AppTheme.Surface;
            button.FlatAppearance.BorderColor=selected?AppTheme.Focus:AppTheme.BorderSoft;
            button.Invalidate();
        }
        UpdateSelectedSpritePreview();
    }

    private void UpdateSelectedSpritePreview()
    {
        PokemonSlotButton selectedButton = selectedSlot >= 0 && GetSlot(selectedSlot).Species!=0
            ? slots.Controls.OfType<PokemonSlotButton>().FirstOrDefault(button => button.Tag is int index && index == selectedSlot)
            : null;
        Image nextImage = selectedButton?.Sprite == null ? null : selectedButton.Sprite is Bitmap bitmap ? bitmap.Clone(selectedButton.SpriteInk,bitmap.PixelFormat) : new Bitmap(selectedButton.Sprite);
        Image previousImage = selectedSpritePreviewImage;
        selectedSpritePreviewImage = nextImage;
        selectedSpritePreview.Image = nextImage;
        selectedSpritePreview.Visible = selectedButton != null;
        selectedSpritePreview.AccessibleDescription = selectedButton == null
            ? "Selecione um Pokémon do Banco global para ver sua imagem ampliada."
            : selectedButton.Sprite == null
                ? $"Prévia ampliada de {selectedButton.Title}; o sprite ainda está carregando."
                : selectedButton.AccessibleName;
        previousImage?.Dispose();
    }

    private static string GenderLabel(byte gender) => gender switch { 0 => "macho", 1 => "fêmea", _ => "sem sexo" };

    internal static string BuildBankPokemonDetails(PKM pokemon, int targetGeneration, string fileName)
    {
        string destination = targetGeneration > 0 ? $"Save aberto: Gen {targetGeneration}." : "Abra um save para conferir a compatibilidade de transferência.";
        string captureDate = pokemon.MetDate is DateOnly metDate ? $"Data de captura: {metDate:dd/MM/yyyy}\n" : string.Empty;
        string compatibility = targetGeneration <= 0
            ? "A transferência ficará disponível quando um save compatível estiver aberto."
            : pokemon.Format == targetGeneration
                ? "Formato compatível com o save aberto."
                : $"Transferência bloqueada: o save usa Gen {targetGeneration}. O original continuará no Banco global.";
        return $"Banco local\n\n{PokemonLabel(pokemon)}\nEspécie: Nº {pokemon.Species}\nGeração da espécie: {SpeciesGeneration(pokemon.Species)}\nNível: {pokemon.CurrentLevel}\nSexo: {GenderLabel(pokemon.Gender)}\n{captureDate}Shiny: {(pokemon.IsShiny?"sim":"não")}\nOvo: {(pokemon.IsEgg?"sim":"não")}\nFormato: Gen {pokemon.Format} (PK{pokemon.Format})\nArquivo: {fileName}\n{destination}\n{compatibility}";
    }

    private void EditPokemon(object sender, EventArgs e)
    {
        if ((save == null && !IsBank) || selectedSlot < 0) return;
        PKM original = GetSlot(selectedSlot);
        if (original.Species == 0) return;
        PKM edited = original.Clone();
        using var form = new PokemonEditorDialog(edited, save);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        edited = form.Result;
        edited.RefreshChecksum();
        if (IsBank) { WriteBankPokemon(bankFiles[selectedSlot], edited); RefreshBankSummary(); }
        else SetSlot(edited, selectedSlot);
        status.Text = IsBank ? "Pokémon atualizado no arquivo do banco local" : "Alteração pendente · edição manual não confirma legalidade de encontro";
        RenderSlots();
        if(selectedSlot>=0)SelectSlot(selectedSlot);
    }

    private void MovePokemon(object sender, EventArgs e) => ChooseTransferDestination();

    private int FindEmptyBoxSlot()
    {
        for (int i = 0; i < save.SlotCount; i++)
        {
            save.GetBoxSlotFromIndex(i, out int box, out int slot);
            if (!save.IsBoxSlotOverwriteProtected(box, slot) && save.GetBoxSlotAtIndex(box, slot).Species == 0) return i;
        }
        return -1;
    }

    private int FindEmptySlot()
    {
        if (IsBank) return 0;
        for (int i = 0; i < SlotCount; i++) if (GetSlot(i).Species == 0) return i;
        return -1;
    }

    private static bool ContainsSearchText(string value, string query)
        => CultureInfo.CurrentCulture.CompareInfo.IndexOf(value ?? string.Empty, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private void ArchivePokemon(object sender, EventArgs e)
    {
        if (save == null || selectedSlot < 0) return;
        PKM pokemon = GetSlot(selectedSlot);
        if (pokemon.Species == 0) return;
        try
        {
            string folder = Path.Combine(root, "Pokemon Bank");
            Directory.CreateDirectory(folder);
            string path = NewBankPath(pokemon);
            string name = Path.GetFileName(path);
            WriteBankPokemon(path, pokemon);
            File.WriteAllText(path + ".origin.txt", $"Source={savePath}\nGame={save.Version}\nGeneration={save.Generation}\nBox={(IsParty ? "Team" : selectedBox.ToString())}\nSlot={selectedSlot}\nArchived={DateTimeOffset.Now:O}\n");
            status.Text = $"Cópia guardada no banco local · {name}";
            RefreshBankSummary();
        }

        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível criar uma cópia no banco local.\n\n" + ex.Message, "Falha ao arquivar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string NewBankPath(PKM pokemon) => Path.Combine(root, "Pokemon Bank", $"{pokemon.Species:D4}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.{pokemon.Extension}");

    private static void WriteBankPokemon(string path, PKM pokemon)
    {
        byte[] data = new byte[pokemon.SIZE_PARTY];
        pokemon.RefreshChecksum();
        pokemon.WriteDecryptedDataParty(data);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, data);
        if (File.Exists(path))
        {
            File.Copy(path, SaveBackupService.UniqueBackupPath(path), false);
            File.Replace(temp, path, null);
        }
        else File.Move(temp, path);
    }

    private void CreatePokemon(object sender, EventArgs e) => OpenCreationEditor(false);

    private void OpenCreationEditor(bool fromEncounter)
    {
        int slot = save == null || IsBank ? 0 : selectedSlot >= 0 && GetSlot(selectedSlot).Species == 0 ? selectedSlot : FindEmptySlot();
        if (slot < 0)
        {
            status.Text = "Esta caixa/equipe não tem espaço vazio. Escolha outra caixa.";
            return;
        }
        try
        {
            PKM blank = PokemonEditorService.Blank(save?.Generation ?? creationFormat.SelectedIndex + 3, save,
                loadedGame == null ? null : SaveProfileService.GameVersionFor(loadedGame.Title));
            using var form = new PokemonEditorDialog(blank, save, creating:true);
            if (fromEncounter) form.Shown += (_,_) => form.ShowEncounters();
            if (form.ShowDialog(this) != DialogResult.OK) return;
            PKM pokemon = form.Result;
            if (save == null || IsBank)
            {
                Directory.CreateDirectory(Path.Combine(root, "Pokemon Bank"));
                string path = NewBankPath(pokemon);
                WriteBankPokemon(path, pokemon);
                RefreshBankFiles();RefreshBankSummary();
                selectedSlot = Array.FindIndex(bankFiles,file=>string.Equals(file,path,StringComparison.OrdinalIgnoreCase));
            }
            else { SetSlot(pokemon, slot);selectedSlot = slot; }
            status.Text = save == null || IsBank ? "Pokémon criado no Banco global. A análise está disponível no editor e na seleção." : "Pokémon criado no rascunho do save · use Salvar com backup para gravar.";
            RenderSlots();if(selectedSlot>=0)SelectSlot(selectedSlot);
        }
        catch (Exception ex) { status.Text = "Não foi possível criar o Pokémon: " + ex.Message; }
    }

    private void SetSlot(PKM pk, int slot)
    {
        if (IsParty) save.SetPartySlotAtIndex(pk, slot);
        else save.SetBoxSlotAtIndex(pk, selectedBox, slot);
        hasUnsavedChanges=true;
    }

    private void SaveChanges(object sender, EventArgs e)
    {
        if (save == null || string.IsNullOrEmpty(savePath)) return;
        string temp = savePath + ".pokemonplay.tmp";
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(openedHash, HashFile(savePath)))
            {
                MessageBox.Show(this, "O arquivo mudou desde que foi aberto. Feche o emulador e reabra o save antes de gravar para não sobrescrever progresso recente.", "Save alterado no disco", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var output = save.Write().ToArray();
            var check = SaveUtil.GetSaveFile(output);
            if (check == null || check.Generation != save.Generation || !check.ChecksumsValid)
                throw new InvalidDataException("A validação do save temporário falhou; o original foi preservado.");

            string backup = SaveBackupService.UniqueBackupPath(savePath);
            File.Copy(savePath, backup, false);
            File.WriteAllBytes(temp, output);
            File.Replace(temp, savePath, null);
            openedHash = HashFile(savePath);
            save = loadedGame == null ? SaveUtil.GetSaveFile(savePath) : ProfileSaveLocator.ReadForGame(savePath, loadedGame);
            hasUnsavedChanges=false;
            status.Text = $"Salvo e validado · backup: {Path.GetFileName(backup)}";
            RenderSlots();
        }
        catch (Exception ex)
        {
            if (File.Exists(temp)) File.Delete(temp);
            MessageBox.Show(this, "Não foi possível gravar com segurança. O save original e o backup permanecem disponíveis.\n\n" + ex.Message, "Falha ao salvar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static byte[] HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return sha.ComputeHash(stream);
    }
}

internal sealed class EditPokemonDialog : Form
{
 private readonly TextBox nickname=new(){BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,BorderStyle=BorderStyle.FixedSingle,Font=AppTheme.Body};
 private readonly NumericUpDown level=new(){Minimum=1,Maximum=100,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Font=AppTheme.Body};
 public string Nickname=>nickname.Text.Trim();public int Level=>(int)level.Value;
 public EditPokemonDialog(PKM pokemon)
 {
  Text="Editar Pokémon";ClientSize=new Size(440,334);FormBorderStyle=FormBorderStyle.FixedDialog;StartPosition=FormStartPosition.CenterParent;MinimizeBox=MaximizeBox=false;BackColor=AppTheme.Background;Font=AppTheme.Body;
  var notice=new Label{Text="Edição direta do save. Os campos alterados não passam por validação de encontro.",Location=new Point(24,24),Size=new Size(392,56),ForeColor=AppTheme.TextMuted};
  var name=new Label{Text="Apelido (vazio = padrão)",Location=new Point(24,100),AutoSize=true,ForeColor=AppTheme.Text};nickname.SetBounds(24,128,392,32);if(pokemon.IsNicknamed)nickname.Text=pokemon.Nickname;
  var levelLabel=new Label{Text="Nível",Location=new Point(24,178),AutoSize=true,ForeColor=AppTheme.Text};level.SetBounds(24,206,140,32);level.Value=Math.Clamp((int)pokemon.CurrentLevel,1,100);
  var cancel=new ThemeButton("Cancelar",ButtonKind.Secondary){DialogResult=DialogResult.Cancel,Location=new Point(188,270),Width=104};var apply=new ThemeButton("Aplicar",ButtonKind.Primary){DialogResult=DialogResult.OK,Location=new Point(304,270),Width=112};
  Controls.AddRange(new Control[]{notice,name,nickname,levelLabel,level,cancel,apply});AcceptButton=apply;CancelButton=cancel;
 }
}
internal sealed class CreatePokemonDialog : Form
{
 private readonly NumericUpDown species=new(){Minimum=1,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Font=AppTheme.Body};
 private readonly NumericUpDown level=new(){Minimum=1,Maximum=100,Value=5,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Font=AppTheme.Body};
 public int Species=>(int)species.Value;public int Level=>(int)level.Value;
 public CreatePokemonDialog(int maxSpecies)
 {
  Text="Novo Pokémon · sandbox";ClientSize=new Size(440,334);FormBorderStyle=FormBorderStyle.FixedDialog;StartPosition=FormStartPosition.CenterParent;MinimizeBox=MaximizeBox=false;BackColor=AppTheme.Background;Font=AppTheme.Body;
  var notice=new Label{Text="Criação livre para sandbox. Os valores não são validados contra encontros do jogo e podem ser incompatíveis.",Location=new Point(24,24),Size=new Size(392,64),ForeColor=AppTheme.TextMuted};
  var speciesLabel=new Label{Text="ID da espécie",Location=new Point(24,110),AutoSize=true,ForeColor=AppTheme.Text};species.SetBounds(24,140,168,32);species.Maximum=Math.Max(1,maxSpecies);
  var levelLabel=new Label{Text="Nível",Location=new Point(216,110),AutoSize=true,ForeColor=AppTheme.Text};level.SetBounds(216,140,200,32);
  var cancel=new ThemeButton("Cancelar",ButtonKind.Secondary){DialogResult=DialogResult.Cancel,Location=new Point(168,270),Width=104};var create=new ThemeButton("Criar Pokémon",ButtonKind.Primary){DialogResult=DialogResult.OK,Location=new Point(284,270),Width=132};
  Controls.AddRange(new Control[]{notice,speciesLabel,species,levelLabel,level,cancel,create});AcceptButton=create;CancelButton=cancel;
 }
}
internal sealed class PokemonSlotButton : Button
{
 public Rectangle SpriteInk=>spriteInk.IsEmpty&&sprite!=null?new Rectangle(Point.Empty,sprite.Size):spriteInk;
 public bool BoxMode { get; set; }
 public string Title { get; set; }
 public string PokemonName { get; set; }
 public string Metadata { get; set; }
 public string Facts { get; set; }
 private Image sprite;
 private Rectangle spriteInk;
 public Image Sprite { get=>sprite; set {sprite=value;spriteInk=Rectangle.Empty;if(value is Bitmap bitmap){int left=bitmap.Width,top=bitmap.Height,right=0,bottom=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y).A>16){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}if(left<=right&&top<=bottom)spriteInk=Rectangle.FromLTRB(left,top,right+1,bottom+1);}} }
 public int PrimaryType { get; set; } = -1;
 public int SecondaryType { get; set; } = -1;
 public PokemonSlotButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.StandardClick|ControlStyles.StandardDoubleClick,true);}
 protected override bool IsInputKey(Keys keyData)
 {
  Keys key=keyData&Keys.KeyCode;
  return key is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown||base.IsInputKey(keyData);
 }
 protected override void OnPaint(PaintEventArgs e)
 {
  if(BoxMode){PaintBoxCell(e);return;}
  e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;using var brush=new SolidBrush(BackColor);var rect=new Rectangle(1,1,Width-3,Height-3);PaintTools.FillRounded(e.Graphics,brush,rect,8);using var pen=new Pen(Focused?AppTheme.Focus:FlatAppearance.BorderColor);PaintTools.DrawRounded(e.Graphics,pen,rect,8);
  if(Title!=null)
  {
   var flags=TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding;
   Rectangle spriteBounds = new(10,29,48,48);
   PaintTools.DrawPokeball(e.Graphics,new Rectangle(19,37,30,30),AppTheme.Focus,AppTheme.Background);
   if(Sprite!=null)e.Graphics.DrawImage(Sprite,PaintTools.FitImage(Sprite,spriteBounds));
   TextRenderer.DrawText(e.Graphics,Metadata,AppTheme.Caption,new Rectangle(66,10,Width-76,20),AppTheme.TextMuted,flags);
   TextRenderer.DrawText(e.Graphics,Title,AppTheme.CaptionBold,new Rectangle(66,32,Width-76,40),ForeColor,flags|TextFormatFlags.WordBreak);
   DrawTypeBadge(e.Graphics,PrimaryType,12,79);
   if(SecondaryType!=PrimaryType)DrawTypeBadge(e.Graphics,SecondaryType,12+TypeBadgeWidth(PrimaryType)+5,79);
   TextRenderer.DrawText(e.Graphics,Facts,AppTheme.Caption,new Rectangle(12,99,Width-24,16),AppTheme.TextMuted,flags);
  }
  else if(PokemonName!=null)
  {
   Rectangle spriteBounds=new(7,25,42,42);PaintTools.DrawPokeball(e.Graphics,new Rectangle(14,32,28,28),AppTheme.Focus,AppTheme.Background);
   if(Sprite!=null)e.Graphics.DrawImage(Sprite,PaintTools.FitImage(Sprite,spriteBounds));
   string slotText=(Text??string.Empty).Split('\n').FirstOrDefault()??string.Empty;
   TextRenderer.DrawText(e.Graphics,slotText,AppTheme.Caption,new Rectangle(8,7,Width-16,16),AppTheme.TextMuted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
   TextRenderer.DrawText(e.Graphics,PokemonName,AppTheme.CaptionBold,new Rectangle(56,28,Width-64,38),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
   TextRenderer.DrawText(e.Graphics,Facts,AppTheme.Caption,new Rectangle(56,67,Width-64,17),AppTheme.TextMuted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
  }
  else TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(8,8,Width-16,Height-16),ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
 }
 private void PaintBoxCell(PaintEventArgs e)
 {
  var g=e.Graphics;g.Clear(BackColor);
  var bounds=new Rectangle(1,1,Width-3,Height-3);using var pen=new Pen(Focused?AppTheme.Focus:FlatAppearance.BorderColor);g.DrawRectangle(pen,bounds);
  string position=(Text??"").Split('\n')[0].Split('·')[0].Trim();TextRenderer.DrawText(g,position,AppTheme.Caption,new Rectangle(6,3,Width-12,16),AppTheme.TextMuted,TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
  bool showName=Width>=90&&Height>=88;
  int art=Math.Max(8,Math.Min(64,Math.Min(Width-12,Height-(showName?56:38))));var artBounds=new Rectangle((Width-art)/2,18,art,art);
  if(Sprite!=null){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;var ink=spriteInk.IsEmpty?new Rectangle(Point.Empty,Sprite.Size):spriteInk;double scale=Math.Min((double)artBounds.Width/ink.Width,(double)artBounds.Height/ink.Height);var draw=new Rectangle(artBounds.X+(artBounds.Width-(int)(ink.Width*scale))/2,artBounds.Y+(artBounds.Height-(int)(ink.Height*scale))/2,(int)(ink.Width*scale),(int)(ink.Height*scale));g.DrawImage(Sprite,draw,ink,GraphicsUnit.Pixel);}
  else if(PokemonName!=null||Title!=null)PaintTools.DrawPokeball(g,new Rectangle(Width/2-12,Height/2-8,24,24),AppTheme.Focus,AppTheme.Background);
  else {using var empty=new SolidBrush(AppTheme.BorderSoft);g.FillEllipse(empty,Width/2-3,Height/2-3,6,6);}
  if(showName&&(PokemonName!=null||Title!=null))TextRenderer.DrawText(g,PokemonName??Title,AppTheme.Caption,new Rectangle(4,Height-34,Width-8,16),AppTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
  if(PokemonName!=null||Title!=null)TextRenderer.DrawText(g,Width<95?(Facts??"").Split('·')[0]:Facts,AppTheme.Caption,new Rectangle(4,Height-17,Width-8,15),AppTheme.TextSecondary,TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
 }
 private static int TypeBadgeWidth(int type)
 {
  string name=PokemonTypeCatalog.GetName(type);
  return string.IsNullOrEmpty(name)?0:TextRenderer.MeasureText(name,AppTheme.Caption).Width+12;
 }
 private void DrawTypeBadge(Graphics graphics,int type,int x,int y)
 {
  string name=PokemonTypeCatalog.GetName(type);
  if(string.IsNullOrEmpty(name)||x>=Width-10)return;
  int width=Math.Min(TypeBadgeWidth(type),Width-10-x);
  var rectangle=new Rectangle(x,y,width,17);Color accent=PokemonTypeCatalog.GetAccent(type);
  using var fill=new SolidBrush(Color.FromArgb(42,accent));PaintTools.FillRounded(graphics,fill,rectangle,4);
  using var border=new Pen(Color.FromArgb(180,accent));PaintTools.DrawRounded(graphics,border,rectangle,4);
  TextRenderer.DrawText(graphics,name,AppTheme.Caption,new Rectangle(x+4,y+1,width-8,15),AppTheme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
 }
 protected override void Dispose(bool disposing){if(disposing)Sprite?.Dispose();base.Dispose(disposing);}
}
