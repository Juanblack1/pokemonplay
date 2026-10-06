using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

internal sealed partial class PokemonBankView
{
    private readonly ListBox boxList=new(){Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=32,BackColor=AppTheme.Surface,ForeColor=AppTheme.Text,Font=AppTheme.Body};
    private readonly Label boxHeading=new(){Dock=DockStyle.Fill,Font=AppTheme.Section,ForeColor=AppTheme.Text,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    private readonly Label workspaceHint=new(){Dock=DockStyle.Bottom,Height=28,Font=AppTheme.Caption,ForeColor=AppTheme.TextMuted,TextAlign=ContentAlignment.MiddleLeft};
    private ThemeButton teamScope,bankScope;
    private bool syncingWorkspace;
    private void BuildBankWorkspace(FlowLayoutPanel toolbar,FlowLayoutPanel profiles,Panel info,Panel body,Panel content,Panel side,Panel cloudBar,Button openProfile)
    {
        Controls.Clear();
        BackColor=AppTheme.Background;
        var rail=new Panel{Dock=DockStyle.Left,Width=198,BackColor=AppTheme.Surface,Padding=new Padding(14)};
        var source=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        Label Caption(string text)=>new(){Text=text,AutoSize=true,Font=AppTheme.Body,ForeColor=AppTheme.TextSecondary,Margin=new Padding(0,4,0,3)};
        source.Controls.Add(new Label{Text="Seu save",Font=AppTheme.Section,AutoSize=true,ForeColor=AppTheme.Text,Margin=new Padding(0,2,0,8)});
        foreach(var pair in new[]{("Jogo",gamePicker),("Perfil",profilePicker),("Arquivo de save",savePicker)}){source.Controls.Add(Caption(pair.Item1));pair.Item2.Width=168;pair.Item2.Height=30;pair.Item2.Font=AppTheme.Body;pair.Item2.Margin=new Padding(0,0,0,2);source.Controls.Add(pair.Item2);}
        openProfile.Text="Carregar save";openProfile.Width=168;openProfile.AutoSize=false;openProfile.Font=AppTheme.Body;openProfile.Height=32;openProfile.Margin=new Padding(0,8,0,4);source.Controls.Add(openProfile);
        var refresh=new ThemeButton("Atualizar saves",ButtonKind.Secondary){Width=168,Height=32,Font=AppTheme.Body};refresh.Click+=(_,_)=>RefreshProfileSaves();source.Controls.Add(refresh);
        teamScope=new ThemeButton("Equipe",ButtonKind.Secondary){Width=168,Height=32,Font=AppTheme.Body,Margin=new Padding(0,10,0,4)};teamScope.Click+=(_,_)=>{if(save!=null)boxPicker.SelectedIndex=save.BoxCount;};source.Controls.Add(teamScope);
        bankScope=new ThemeButton("Coleção global",ButtonKind.Secondary){Width=168,Height=32,Font=AppTheme.Body};bankScope.Click+=(_,_)=>boxPicker.SelectedIndex=save==null?0:save.BoxCount+1;source.Controls.Add(bankScope);
        source.Controls.Add(Caption("Caixas do save"));
        boxList.SelectedIndexChanged+=(_,_)=>{if(!syncingWorkspace&&boxList.SelectedIndex>=0)boxPicker.SelectedIndex=boxList.SelectedIndex;};
        boxList.DrawItem+=(_,e)=>{if(e.Index<0)return;bool active=(e.State&DrawItemState.Selected)!=0;using var fill=new SolidBrush(active?AppTheme.SurfaceSelected:AppTheme.Surface);e.Graphics.FillRectangle(fill,e.Bounds);TextRenderer.DrawText(e.Graphics,boxList.Items[e.Index].ToString(),AppTheme.Body,e.Bounds,active?AppTheme.Focus:AppTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
        rail.Controls.Add(boxList);rail.Controls.Add(source);
        toolbar.Controls.Remove(boxPicker);boxPicker.Visible=false;
        toolbar.Padding=new Padding(16,8,16,6);toolbar.BackColor=AppTheme.Background;
        foreach(Control action in toolbar.Controls){action.Font=AppTheme.Body;action.Margin=new Padding(0,0,8,4);}
        createButton.Text="Criar Pokémon";saveButton.Text="Salvar alterações";
        info.Dock=DockStyle.Bottom;info.Height=48;info.Padding=new Padding(16,4,16,0);status.Font=AppTheme.Caption;openedContext.Font=AppTheme.Caption;
        body.Padding=new Padding(14,0,14,4);body.BackColor=AppTheme.Background;
        side.Width=256;side.BackColor=AppTheme.Surface;side.Padding=new Padding(12);details.Font=AppTheme.Body;details.MaximumSize=new Size(216,0);
        foreach(var action in new[]{editButton,moveButton,archiveButton,legalityButton,exportPokemonButton,removePokemonButton}){action.Width=220;action.Font=AppTheme.Body;}
        selectedSpritePreview.Height=88;
        foreach(var picker in new[]{bankGenerationPicker,bankDexPicker}){var field=picker.Parent;bankNavigation.Controls.Remove(field);bankExtraFilters.Controls.Add(field);}
        bankSpeciesSearch.Width=144;bankSpeciesSearch.Parent.Width=156;bankSortPicker.Width=128;bankSortPicker.Parent.Width=140;
        foreach(Control action in bankNavigation.Controls){action.Font=AppTheme.Body;}
        bankMoreFilters.Font=AppTheme.Body;bankMoreFilters.Text="Filtros";bankMoreFilters.AutoSize=false;bankMoreFilters.Width=86;
        var clear=bankNavigation.Controls.OfType<ThemeButton>().First(b=>b!=bankMoreFilters);clear.Text="Limpar";clear.AutoSize=false;clear.Width=66;
        
        var navigation=new Panel{Dock=DockStyle.Top,Height=54,BackColor=AppTheme.SurfaceRaised,Padding=new Padding(12,8,12,8)};
        var previous=new ThemeButton("Anterior",ButtonKind.Secondary){Dock=DockStyle.Left,Width=88,Font=AppTheme.Body};previous.Click+=(_,_)=>ChangeWorkspaceBox(-1);
        var next=new ThemeButton("Próxima",ButtonKind.Secondary){Dock=DockStyle.Right,Width=88,Font=AppTheme.Body};next.Click+=(_,_)=>ChangeWorkspaceBox(1);
        boxHeading.Padding=new Padding(14,0,14,0);navigation.Controls.Add(boxHeading);navigation.Controls.Add(next);navigation.Controls.Add(previous);
        content.Controls.Remove(side);body.Controls.Add(side);
        content.Controls.Add(workspaceHint);content.Controls.Add(navigation);
        bankPaging.Visible=false;bankPaging.Height=0;
        slots.AutoScroll=false;slots.Padding=new Padding(0,10,0,0);slots.BackColor=AppTheme.Background;slots.Resize+=(_,_)=>LayoutBoxCells();
        var workspace=new Panel{Dock=DockStyle.Fill,BackColor=AppTheme.Background};workspace.Controls.Add(body);workspace.Controls.Add(toolbar);workspace.Controls.Add(cloudBar);workspace.Controls.Add(info);
        Controls.Add(workspace);Controls.Add(rail);
        var header=new Panel{Dock=DockStyle.Top,Height=52,BackColor=AppTheme.TopBar,Padding=new Padding(18,10,18,8)};
        header.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Banco Pokémon",Font=AppTheme.PageTitle,ForeColor=AppTheme.Text,TextAlign=ContentAlignment.MiddleLeft});Controls.Add(header);
        profiles.Dispose();
        boxPicker.SelectedIndexChanged+=(_,_)=>SyncWorkspace();
        SyncWorkspace();
    }
    private void SyncWorkspace()
    {
        if(teamScope==null)return;
        syncingWorkspace=true;
        teamScope.Enabled=save!=null;
        teamScope.Kind=IsParty?ButtonKind.Primary:ButtonKind.Secondary;bankScope.Kind=IsBank?ButtonKind.Primary:ButtonKind.Secondary;
        var names=save==null?Array.Empty<string>():BoxUtil.GetBoxNames(save).ToArray();
        boxList.Items.Clear();
        for(int i=0;i<names.Length;i++){int occupied=Enumerable.Range(0,save.BoxSlotCount).Count(slot=>save.GetBoxSlotAtIndex(i,slot).Species!=0);boxList.Items.Add($"{names[i]}   {occupied}/{save.BoxSlotCount}");}
        boxList.SelectedIndex=save!=null&&!IsBank&&!IsParty?selectedBox:-1;
        syncingWorkspace=false;
        boxHeading.Text=IsBank?$"Coleção · {bankPage+1}/{Math.Max(1,(bankViewIndices.Length+29)/30)} · {bankViewIndices.Length} Pokémon":IsParty?$"Equipe · {save.PartyCount}/6":save==null?"Coleção global":$"{names[selectedBox]} · {Enumerable.Range(0,save.BoxSlotCount).Count(i=>save.GetBoxSlotAtIndex(selectedBox,i).Species!=0)}/{save.BoxSlotCount}";
        workspaceHint.Text=IsBank?"Selecione para inspecionar · Duplo clique para editar · Setas para navegar":"Selecione um espaço · Duplo clique para editar/criar · Mover / copiar escolhe a caixa de destino";
        LayoutBoxCells();
    }
    private void ChangeWorkspaceBox(int direction)
    {
        if(IsBank){ChangeBankPage(direction,false);SyncWorkspace();return;}
        if(save!=null)boxPicker.SelectedIndex=(boxPicker.SelectedIndex+direction+save.BoxCount+1)%(save.BoxCount+1);
    }
    private void LayoutBoxCells()
    {
        if(slots.ClientSize.Width<60)return;
        int width=Math.Max(40,(slots.ClientSize.Width-18)/6-8);
        int height=Math.Clamp((slots.ClientSize.Height-20)/5-8,24,112);
        foreach(var cell in slots.Controls.OfType<PokemonSlotButton>()){cell.Size=new Size(width,height);cell.Margin=new Padding(0,0,8,8);}
    }
    private string InspectorText(PKM pk)
    {
        var strings=PKHeX.Core.GameInfo.GetStrings("en");
        string destination=IsBank?(save==null?"Carregue um save para copiar para uma caixa.":pk.GetType()==save.PKMType?"Pode copiar para o save aberto.":"Formato diferente do save aberto."):"Mudanças ficam pendentes até Salvar alterações.";
        string captureDate=pk.MetDate is DateOnly metDate?$"Data de captura: {metDate:dd/MM/yyyy}\n":string.Empty;
        return $"{PokemonLabel(pk)} · Nv. {pk.CurrentLevel}\n{GenderLabel(pk.Gender)}{(pk.IsShiny?" · Shiny":"")} · PK{pk.Format}\n\nNatureza  {strings.Natures[(int)pk.Nature]}\nHabilidade  {strings.Ability[pk.Ability]}\nItem  {strings.Item[pk.HeldItem]}\n\nIVs  {pk.IV_HP} / {pk.IV_ATK} / {pk.IV_DEF}\n        {pk.IV_SPA} / {pk.IV_SPD} / {pk.IV_SPE}\nEVs  {pk.EVTotal} / 510\n\nTreinador  {pk.OriginalTrainerName}\nOrigem  {PKHeX.Core.GameInfo.GetVersionName(pk.Version)}\n{captureDate}\n{destination}";
    }
    private void ChooseTransferDestination()
    {
        if(save==null||selectedSlot<0||GetSlot(selectedSlot).Species==0)return;
        using var dialog=new Form{Text=IsBank?"Copiar para caixa":"Mover ou copiar Pokémon",ClientSize=new Size(480,286),StartPosition=FormStartPosition.CenterParent,BackColor=AppTheme.Background,Font=AppTheme.Body,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var title=new Label{Text=PokemonLabel(GetSlot(selectedSlot)),ForeColor=AppTheme.Text,Font=AppTheme.Section,AutoSize=true,Location=new Point(20,16)};
        var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,62),Width=438,Font=AppTheme.Body};box.Items.AddRange(BoxUtil.GetBoxNames(save).Cast<object>().ToArray());
        if(!IsBank&&!IsParty)box.Items.Add("Equipe");
        var position=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,112),Width=438,Font=AppTheme.Body};
        box.SelectedIndexChanged+=(_,_)=>{
            position.Items.Clear();
            if(box.SelectedIndex==save.BoxCount){position.Items.Add(save.PartyCount>=6?"Equipe cheia":$"Próximo espaço disponível · {save.PartyCount+1}");position.SelectedIndex=0;return;}
            for(int i=0;i<save.BoxSlotCount;i++){var pk=save.GetBoxSlotAtIndex(box.SelectedIndex,i);position.Items.Add($"Espaço {i+1} · {(pk.Species==0?"disponível":PokemonLabel(pk))}");}
            position.SelectedIndex=Enumerable.Range(0,save.BoxSlotCount).FirstOrDefault(i=>save.GetBoxSlotAtIndex(box.SelectedIndex,i).Species==0);
        };box.SelectedIndex=IsBank||IsParty?0:selectedBox;
        var copy=new CheckBox{Text="Criar cópia (manter no local de origem)",Checked=IsBank,Enabled=!IsBank,ForeColor=AppTheme.Text,AutoSize=true,Location=new Point(20,158)};
        var result=new Label{Text="Escolha caixa e espaço. A gravação usa Salvar alterações.",ForeColor=AppTheme.TextMuted,AutoSize=false,Size=new Size(438,36),Location=new Point(20,190)};
        var apply=new ThemeButton("Confirmar destino",ButtonKind.Primary){Location=new Point(284,238),Width=174,Font=AppTheme.Body};apply.Click+=(_,_)=>{try{if(box.SelectedIndex==save.BoxCount)TransferToParty(copy.Checked);else TransferToBox(box.SelectedIndex,position.SelectedIndex,copy.Checked);dialog.DialogResult=DialogResult.OK;}catch(Exception error){result.Text=error.Message;}};
        var cancel=new ThemeButton("Cancelar",ButtonKind.Secondary){Location=new Point(174,238),Width=100,Font=AppTheme.Body,DialogResult=DialogResult.Cancel};dialog.CancelButton=cancel;dialog.Controls.AddRange(new Control[]{title,box,position,copy,result,apply,cancel});
        if(dialog.ShowDialog(this)==DialogResult.OK){RenderSlots();SyncWorkspace();}
    }
    internal void TransferToBox(int box,int slot,bool copy)
    {
        if(save==null||selectedSlot<0)throw new InvalidOperationException("Carregue um save e selecione um Pokémon.");
        if(box<0||box>=save.BoxCount||slot<0||slot>=save.BoxSlotCount)throw new InvalidOperationException("Selecione um destino válido.");
        var pokemon=GetSlot(selectedSlot);
        if(pokemon.Species==0||pokemon.GetType()!=save.PKMType)throw new InvalidOperationException("Formato incompatível com o save.");
        if(save.IsBoxSlotOverwriteProtected(box,slot)||save.GetBoxSlotAtIndex(box,slot).Species!=0)throw new InvalidOperationException("Este espaço está ocupado ou protegido. Escolha um espaço disponível.");
        if(!copy&&!IsBank&&IsParty&&save.PartyCount<=1)throw new InvalidOperationException("Mantenha um Pokémon na equipe ou escolha Criar cópia.");
        save.SetBoxSlotAtIndex(pokemon.Clone(),box,slot);
        if(!copy&&!IsBank){if(IsParty)save.DeletePartySlot(selectedSlot);else save.SetBoxSlotAtIndex(save.BlankPKM,selectedBox,selectedSlot);}
        hasUnsavedChanges=true;status.Text=$"{(copy||IsBank?"Cópia":"Movimento")} para {BoxUtil.GetBoxNames(save)[box]} · espaço {slot+1} · pendente de salvar";
        selectedSlot=-1;
    }

    internal void TransferToParty(bool copy)
    {
        if(save==null||IsBank||IsParty||selectedSlot<0)throw new InvalidOperationException("Selecione um Pokémon em uma caixa do save.");
        if(save.PartyCount>=6)throw new InvalidOperationException("A equipe está cheia. Escolha uma caixa de destino.");
        var pokemon=GetSlot(selectedSlot);
        if(pokemon.Species==0)throw new InvalidOperationException("Selecione um Pokémon.");
        save.SetPartySlotAtIndex(pokemon.Clone(),save.PartyCount);
        if(!copy)save.SetBoxSlotAtIndex(save.BlankPKM,selectedBox,selectedSlot);
        hasUnsavedChanges=true;selectedSlot=-1;status.Text="Pokémon movido para a equipe · pendente de Salvar alterações";
    }
}


