using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

internal sealed partial class PokemonEditorDialog : Form
{
    private sealed record Choice(int Value, string Name) { public override string ToString()=>Name; }
    private sealed record Binding(PropertyInfo Property, Control Control, Func<object> Read, object Initial);
    private readonly List<Binding> bindings=new();
    private PKM draft;
    private readonly SaveFile save;
    private readonly PKHeX.Core.GameStrings strings=PKHeX.Core.GameInfo.GetStrings("en");
    private readonly EditorPageHost tabs=new() {Dock=DockStyle.Fill};
    private readonly Label summary=new(){Dock=DockStyle.Fill,ForeColor=AppTheme.Text,Font=AppTheme.Body,Padding=new Padding(16)};
    private readonly Label status=new(){Dock=DockStyle.Fill,ForeColor=AppTheme.TextMuted,Font=AppTheme.Caption,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
    private readonly PictureBox sprite=new(){Dock=DockStyle.Top,Height=160,SizeMode=PictureBoxSizeMode.Zoom,BackColor=AppTheme.SurfaceRaised};
    private readonly TextBox report=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Font=AppTheme.Body,BorderStyle=BorderStyle.FixedSingle};
    private readonly ListView encounters=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Font=AppTheme.Body};
    private readonly ThemeButton search=new("Buscar encontros",ButtonKind.Primary){AutoSize=true};
    private readonly ThemeButton useEncounter=new("Usar encontro selecionado",ButtonKind.Secondary){AutoSize=true,Enabled=false};
    private readonly Label encounterInfo=new(){Dock=DockStyle.Bottom,Height=56,ForeColor=AppTheme.TextMuted,Font=AppTheme.Body,Padding=new Padding(8)};
    private ComboBox species;
    private bool busy;
    private bool loading;
    private long revision;
    private readonly CheckBox shiny=new(){Text="Shiny (ajusta Secret ID)",ForeColor=AppTheme.Text,AutoSize=true};
    private bool initialShiny;
    private int spriteSequence;
    internal PKM Result { get; private set; }

    internal PokemonEditorDialog(PKM pokemon, SaveFile save = null, bool creating = false)
    {
        draft=pokemon.Clone();this.save=save;
        Text=(creating?"Criar Pokémon":"Editar Pokémon")+" · PK"+pokemon.Format;
        ClientSize=new Size(1180,780);MinimumSize=new Size(1000,700);StartPosition=FormStartPosition.CenterParent;
        BackColor=AppTheme.Background;Font=AppTheme.Body;MinimizeBox=false;
        var header=new PixelHeader(creating?"Criar Pokémon":"Editor Pokémon","Monte o conjunto · revise os atributos · acompanhe a legalidade",72);
        var side=new Panel{Dock=DockStyle.Left,Width=220,BackColor=AppTheme.Surface};side.Controls.Add(summary);side.Controls.Add(sprite);
        sprite.Paint+=(_,e)=>{if(sprite.Image==null)PaintTools.DrawPokeball(e.Graphics,new Rectangle(80,50,60,60),AppTheme.Focus,AppTheme.Background);};
        var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16),BackColor=AppTheme.Background};body.Controls.Add(tabs);body.Controls.Add(side);
        var analyze=new ThemeButton("Verificar legalidade",ButtonKind.Secondary){AutoSize=true};analyze.Click+=(_,_)=>Analyze();
        var database=new ThemeButton("Base de encontros",ButtonKind.Secondary){AutoSize=true};database.Click+=(_,_)=>ShowEncounters();
        apply=new ThemeButton(creating?"Criar Pokémon":"Aplicar alterações",ButtonKind.Primary){AutoSize=true};apply.Click+=(_,_)=>Apply();
        var cancel=new ThemeButton("Cancelar",ButtonKind.Secondary){AutoSize=true,DialogResult=DialogResult.Cancel};CancelButton=cancel;
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=62,Padding=new Padding(16,10,16,10),FlowDirection=FlowDirection.RightToLeft,WrapContents=false,BackColor=AppTheme.TopBar};footer.Controls.AddRange(new Control[]{apply,cancel,database,analyze});
        var note=new Panel{Dock=DockStyle.Bottom,Height=44,Padding=new Padding(20,0,20,0)};note.Controls.Add(status);
        Controls.Add(body);Controls.Add(note);Controls.Add(footer);Controls.Add(header);
        BuildFields();BuildEncounterPage();
        shiny.CheckedChanged+=(_,_)=>Changed(nameof(PKM.IsShiny));
        tabs.TabPages.Add(new Panel{Text="Legalidade",BackColor=AppTheme.Background,Padding=new Padding(12)});tabs.TabPages[^1].Controls.Add(report);
        InitializeLiveFeedback();LoadFields();
        FormClosed+=(_,_)=>{legalityTimer.Stop();legalityTimer.Dispose();spriteSequence++;sprite.Image?.Dispose();sprite.Image=null;};
    }

    private TableLayoutPanel Page(string title)
    {
        var page=new Panel{Text=title,BackColor=AppTheme.Background,Padding=new Padding(14),AutoScroll=true};tabs.TabPages.Add(page);
        var fields=FieldTable(172);page.Controls.Add(fields);return fields;
    }
    private void Row(TableLayoutPanel table,string label,Control control)
    {
        int row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption=new Label{Text=label,AutoSize=true,ForeColor=AppTheme.TextSecondary,Font=AppTheme.Body,Margin=new Padding(0,8,12,8)};
        control.AccessibleName=label;
        Control field=control is ComboBox or NumericUpDown or TextBox ? new EditorFieldFrame(control) : control;
        field.Dock=DockStyle.Top;field.Margin=new Padding(0,4,0,8);table.Controls.Add(caption,0,row);table.Controls.Add(field,1,row);
    }
    private void Bind(string property,Control control,Func<object> read)
    {
        var info=typeof(PKM).GetProperty(property);bindings.Add(new Binding(info,control,read,null));
        if(control is ComboBox combo)combo.SelectedIndexChanged+=(_,_)=>Changed(property);
        if(control is NumericUpDown numeric)numeric.ValueChanged+=(_,_)=>Changed(property);
        if(control is TextBox text)text.TextChanged+=(_,_)=>Changed(property);
        if(control is CheckBox check)check.CheckedChanged+=(_,_)=>Changed(property);
        if(control is DateTimePicker date)date.ValueChanged+=(_,_)=>Changed(property);
    }
    private ComboBox Pick(TableLayoutPanel table,string label,string property,IEnumerable<Choice> choices)
    {
        var combo=new ComboBox{DrawMode=DrawMode.OwnerDrawFixed,FlatStyle=FlatStyle.Flat,ItemHeight=24,DropDownStyle=ComboBoxStyle.DropDownList,Font=AppTheme.Body,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Height=32};
        combo.DrawItem+=(_,e)=>{if(e.Index<0)return;using var brush=new SolidBrush((e.State&DrawItemState.Selected)!=0?AppTheme.SurfaceHover:AppTheme.SurfaceRaised);e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,combo.Items[e.Index].ToString(),combo.Font,e.Bounds,AppTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
        combo.Items.AddRange(choices.Cast<object>().ToArray());combo.DropDownStyle=ComboBoxStyle.DropDown;combo.AutoCompleteMode=AutoCompleteMode.SuggestAppend;combo.AutoCompleteSource=AutoCompleteSource.ListItems;combo.TextChanged+=(_,_)=>Changed(property);Row(table,label,combo);Bind(property,combo,()=>((Choice)combo.SelectedItem)?.Value ?? 0);return combo;
    }
    private ComboBox Named(TableLayoutPanel table,string label,string property,IReadOnlyList<string> names,int max,int min=0)
        => Pick(table,label,property,Enumerable.Range(min,Math.Max(0,Math.Min(names.Count-1,max)-min+1)).Select(id=>new Choice(id,$"{id:D3} · {names[id]}")));
    private void Number(TableLayoutPanel table,string label,string property,decimal min,decimal max,bool hex=false)
    {
        var value=new NumericUpDown{Minimum=min,Maximum=max,Hexadecimal=hex,Font=AppTheme.Body,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,Height=32};Row(table,label,value);Bind(property,value,()=>value.Value);
    }
    private void TextField(TableLayoutPanel table,string label,string property,int max)
    {
        var text=new TextBox{MaxLength=max,Font=AppTheme.Body,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,BorderStyle=BorderStyle.FixedSingle};Row(table,label,text);Bind(property,text,()=>text.Text);
    }
    private void Check(TableLayoutPanel table,string label,string property)
    {
        var check=new CheckBox{Text=label,AutoSize=true,ForeColor=AppTheme.Text};Row(table,"",check);Bind(property,check,()=>check.Checked);
    }
    private void BuildFields()
    {
        var overview=new Panel{Text="Conjunto",BackColor=AppTheme.Background,Padding=new Padding(10),AutoScroll=true};tabs.TabPages.Add(overview);
        var columns=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));overview.Controls.Add(columns);
        var main=FieldTable(96);main.Margin=new Padding(0,0,18,0);columns.Controls.Add(main,0,0);
        species=Pick(main,"Espécie",nameof(PKM.Species),Enumerable.Range(1,draft.MaxSpeciesID).Select(id=>new Choice(id,$"{strings.Species[id]} · {id:D3}")));
        Number(main,"Nível",nameof(PKM.CurrentLevel),1,100);
        Named(main,"Natureza",nameof(PKM.Nature),strings.Natures,24);
        Named(main,"Habilidade",nameof(PKM.Ability),strings.Ability,draft.MaxAbilityID);
        Named(main,"Item",nameof(PKM.HeldItem),strings.Item,draft.MaxItemID);
        Row(main,"",new Label{Text="Golpes",Font=AppTheme.Section,ForeColor=AppTheme.Text,AutoSize=true,Margin=new Padding(0,14,0,8)});
        for(int i=1;i<=4;i++)Named(main,"Golpe "+i,"Move"+i,strings.Move,draft.MaxMoveID);
        var stats=BuildStatTable();columns.Controls.Add(stats,1,0);
        var identity=Page("Identidade");
        TextField(identity,"Apelido",nameof(PKM.Nickname),draft.MaxStringLengthNickname);Check(identity,"Usar apelido",nameof(PKM.IsNicknamed));
        Number(identity,"Forma (ID)",nameof(PKM.Form),0,255);
        Pick(identity,"Sexo",nameof(PKM.Gender),new[]{new Choice(0,"Macho"),new Choice(1,"Fêmea"),new Choice(2,"Sem sexo")});
        Pick(identity,"Slot de habilidade",nameof(PKM.AbilityNumber),new[]{new Choice(1,"1 · normal"),new Choice(2,"2 · normal"),new Choice(4,"Oculta")});
        Check(identity,"É ovo",nameof(PKM.IsEgg));Row(identity,"Cor",shiny);
        var met=Page("Encontro");
        var versions=PokemonEditorService.Versions(draft.Format);
        Pick(met,"Jogo de origem",nameof(PKM.Version),versions.Select(v=>new Choice((int)v,PKHeX.Core.GameInfo.GetVersionName(v))));
        Number(met,"Nível de captura",nameof(PKM.MetLevel),0,100);
        Number(met,"Local de captura (ID)",nameof(PKM.MetLocation),0,ushort.MaxValue);
        Number(met,"Local do ovo (ID)",nameof(PKM.EggLocation),0,ushort.MaxValue);
        Pick(met,"Poké Bola",nameof(PKM.Ball),Enumerable.Range(0,draft.MaxBallID+1).Select(id=>new Choice(id,((Ball)id).ToString())));
        Check(met,"Encontro fatídico",nameof(PKM.FatefulEncounter));
        if(draft.Format>=4)
        {
            var date=new DateTimePicker{Format=DateTimePickerFormat.Short,ShowCheckBox=true};Row(met,"Data de captura",date);Bind(nameof(PKM.MetDate),date,()=>date.Checked?(object)DateOnly.FromDateTime(date.Value):null);
        }
        var moves=Page("Golpes");
        for(int i=1;i<=4;i++)
        {
            if(draft.Format>=6)Named(moves,"Golpe de reaprendizado "+i,"RelearnMove"+i,strings.Move,draft.MaxMoveID);

            Number(moves,"PP / PP Ups · "+i,"Move"+i+"_PP",0,99);Number(moves,"PP Ups · "+i,"Move"+i+"_PPUps",0,3);
        }
        var trainer=Page("Treinador / outros");
        TextField(trainer,"Treinador original",nameof(PKM.OriginalTrainerName),draft.MaxStringLengthTrainer);
        Number(trainer,"Trainer ID",nameof(PKM.TID16),0,ushort.MaxValue);Number(trainer,"Secret ID",nameof(PKM.SID16),0,ushort.MaxValue);
        Pick(trainer,"Sexo do treinador",nameof(PKM.OriginalTrainerGender),new[]{new Choice(0,"Masculino"),new Choice(1,"Feminino")});
        Number(trainer,"Idioma (ID)",nameof(PKM.Language),1,10);Number(trainer,"Amizade",nameof(PKM.OriginalTrainerFriendship),0,255);
        Number(trainer,"PID (hexadecimal)",nameof(PKM.PID),0,uint.MaxValue,true);
        if(draft.Format>=6)Number(trainer,"Encryption Constant",nameof(PKM.EncryptionConstant),0,uint.MaxValue,true);
        var hint=new Label{Text="Shiny depende de PID e IDs do treinador. A base de encontros preserva as correlações exigidas pelo jogo; mudanças manuais podem invalidá-las.",AutoSize=true,MaximumSize=new Size(480,0),ForeColor=AppTheme.TextMuted};Row(trainer,"",hint);
    }

    private void LoadFields()
    {
        revision++;loading=true;
        for(int i=0;i<bindings.Count;i++)
        {
            var binding=bindings[i];object value=binding.Property.GetValue(draft);
            switch(binding.Control)
            {
                case ComboBox combo:
                    int id=Convert.ToInt32(value);var found=combo.Items.Cast<Choice>().FirstOrDefault(c=>c.Value==id);
                    if(found==null){found=new Choice(id,$"{id} · valor atual");combo.Items.Add(found);}combo.SelectedItem=found;combo.SelectionLength=0;break;
                case NumericUpDown number:number.Value=Math.Clamp(Convert.ToDecimal(value),number.Minimum,number.Maximum);break;
                case TextBox text:text.Text=Convert.ToString(value);break;
                case CheckBox check:check.Checked=(bool)value;break;
                case DateTimePicker date:date.Checked=value!=null;if(value is DateOnly day)date.Value=day.ToDateTime(TimeOnly.MinValue);break;
            }
            bindings[i]=binding with {Initial=binding.Read()};
        }
        initialShiny=draft.IsShiny;shiny.Checked=initialShiny;
        loading=false;RefreshSummary();RefreshValidation();QueueLegality();_ = UpdateSprite();
    }
    internal PKM ReadDraft(bool validate=true)
    {
        foreach(var binding in bindings.Where(b=>b.Control is ComboBox))
        {
            var combo=(ComboBox)binding.Control;
            if(combo.SelectedItem is not Choice || !string.Equals(combo.Text,combo.SelectedItem.ToString(),StringComparison.Ordinal))
                throw new InvalidOperationException("Escolha um valor da lista para "+combo.AccessibleName+".");
        }
        PKM edited=draft.Clone();
        foreach(var binding in bindings)
        {
            object value=binding.Read();if(Equals(value,binding.Initial))continue;
            Type type=Nullable.GetUnderlyingType(binding.Property.PropertyType)??binding.Property.PropertyType;
            object converted=value==null?null:type.IsEnum?Enum.ToObject(type,Convert.ToInt32(value)):type==typeof(DateOnly)?value:Convert.ChangeType(value,type,CultureInfo.InvariantCulture);
            binding.Property.SetValue(edited,converted);
        }
        bool Modified(string name)=>bindings.Any(b=>b.Property.Name==name&&!Equals(b.Read(),b.Initial));
        object Value(string name)=>bindings.Single(b=>b.Property.Name==name).Read();
        if(edited.Format<=5 && !Modified(nameof(PKM.PID)))
        {
            if(Modified(nameof(PKM.Gender)))edited.SetPIDGender((byte)Convert.ToInt32(Value(nameof(PKM.Gender))));
            if(Modified(nameof(PKM.Nature)))edited.SetPIDNature((Nature)Convert.ToInt32(Value(nameof(PKM.Nature))));
        }
        if(Modified(nameof(PKM.AbilityNumber))&&!Modified(nameof(PKM.Ability)))edited.RefreshAbility(edited.AbilityNumber==4?2:edited.AbilityNumber==2?1:0);
        bool changedSpecies=bindings.Any(b=>b.Property.Name==nameof(PKM.Species)&&!Equals(b.Read(),b.Initial));
        if(changedSpecies)
        {
            var levelBinding=bindings.Single(b=>b.Property.Name==nameof(PKM.CurrentLevel));
            edited.CurrentLevel=(byte)Convert.ToInt32(levelBinding.Read());
            if(!bindings.Any(b=>b.Property.Name==nameof(PKM.Ability)&&!Equals(b.Read(),b.Initial)))
                edited.RefreshAbility(edited.AbilityNumber==4?2:edited.AbilityNumber==2?1:0);
        }
        if(shiny.Checked!=initialShiny)
        {
            if(shiny.Checked)edited.SetShinySID(Shiny.Always);
            else if(edited.IsShiny)edited.SID16^=16;
        }
        if(!edited.IsNicknamed && (changedSpecies || bindings.Any(b=>b.Property.Name==nameof(PKM.IsNicknamed)&&!Equals(b.Read(),b.Initial))))edited.Nickname=SpeciesName.GetSpeciesNameGeneration(edited.Species,edited.Language,(byte)edited.Format);
        if(validate&&edited.EVTotal>510)throw new InvalidOperationException($"EVs: {edited.EVTotal}/510. Remova {edited.EVTotal-510} pontos em Conjunto antes de aplicar.");
        if(validate&&new[]{edited.EV_HP,edited.EV_ATK,edited.EV_DEF,edited.EV_SPA,edited.EV_SPD,edited.EV_SPE}.Any(ev=>ev>edited.MaxEV))
            throw new InvalidOperationException($"Cada atributo aceita no máximo {edited.MaxEV} EVs em PK{edited.Format}.");
        edited.RefreshChecksum();return edited;
    }
    private void Changed(string property)
    {
        if(loading)return;
        revision++;RefreshSummary();RefreshValidation();QueueLegality();
        if(property is nameof(PKM.Species) or nameof(PKM.Form) or nameof(PKM.Gender) or nameof(PKM.IsShiny))_ = UpdateSprite();
        if(property is nameof(PKM.Species) or nameof(PKM.Form) or nameof(PKM.Version))
        {encounters.Items.Clear();useEncounter.Enabled=false;encounterInfo.Text="Espécie, forma ou versão alterada. Busque os encontros novamente.";}
    }
    private void RefreshSummary()
    {
        if(species==null||bindings.Count==0)return;
        try{var pk=ReadDraft(false);summary.Text=$"{strings.Species[pk.Species]}\n\nPK{pk.Format} · Nível {pk.CurrentLevel}\n{(pk.IsShiny?"Shiny":"Não shiny")} · {(pk.IsEgg?"Ovo":"Pokémon")}\n\nNatureza: {strings.Natures[(int)pk.Nature]}\nIVs: {pk.IVTotal}\nEVs: {pk.EVTotal} / 510\n\nOT: {pk.OriginalTrainerName}\nTID: {pk.TID16} · SID: {pk.SID16}";}
        catch(Exception error){summary.Text=error.Message;}
    }
    private async Task UpdateSprite()
    {
        int request=++spriteSequence;PKM pk;try{pk=ReadDraft(false);}catch{return;}
        Image image=await PokemonSpriteService.LoadAsync(pk.Species,pk.IsShiny,pk.Gender==1);
        if(IsDisposed||request!=spriteSequence){image?.Dispose();return;}
        Image previous=sprite.Image;sprite.Image=image;previous?.Dispose();
    }
    private void Analyze()
    {
        legalityTimer.Stop();tabs.SelectedTab=tabs.TabPages[^1];_ = AnalyzeLive();
    }
    internal void ShowEncounters(){tabs.SelectedTab=tabs.TabPages.Cast<Panel>().Single(p=>p.Text=="Base de encontros");SearchEncounters();}
    private void BuildEncounterPage()
    {
        var page=new Panel{Text="Base de encontros",BackColor=AppTheme.Background,Padding=new Padding(12)};tabs.TabPages.Add(page);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(0,4,0,12)};
        actions.Controls.AddRange(new Control[]{search,useEncounter});search.Click+=(_,_)=>SearchEncounters();useEncounter.Click+=(_,_)=>UseEncounter();
        foreach(var column in new[]{("Tipo",200),("Espécie",140),("Nível",70),("Local",240)})encounters.Columns.Add(column.Item1,column.Item2);
        encounters.SelectedIndexChanged+=(_,_)=>useEncounter.Enabled=!busy&&encounters.SelectedItems.Count==1;
        encounters.DoubleClick+=(_,_)=>UseEncounter();
        encounterInfo.Text="Selecione espécie e jogo de origem nas abas Pokémon e Encontro. Buscar consulta a base real do PKHeX.";
        page.Controls.Add(encounters);page.Controls.Add(encounterInfo);page.Controls.Add(actions);
    }
    private async void SearchEncounters()
    {
        if(busy)return;PKM pk;try{pk=ReadDraft();}catch(Exception e){status.Text=e.Message;return;}
        long searchedRevision=revision;
        busy=true;search.Enabled=useEncounter.Enabled=false;encounters.Items.Clear();encounterInfo.Text="Consultando encontros…";
        try
        {
            var results=await Task.Run(()=>PokemonEditorService.Encounters(pk,save));if(IsDisposed)return;
            if(searchedRevision!=revision){encounterInfo.Text="O rascunho mudou durante a consulta. Busque novamente.";return;}
            foreach(var encounter in results)
            {
                string location=PKHeX.Core.GameInfo.GetLocationName(false,encounter.Location,encounter.Generation,encounter.Generation,encounter.Version);
                var item=new ListViewItem(new[]{encounter.LongName,strings.Species[encounter.Species],$"{encounter.LevelMin}–{encounter.LevelMax}",$"{location} ({encounter.Location})"}){Tag=encounter};encounters.Items.Add(item);
            }
            encounterInfo.Text=results.Count==0?"Nenhum encontro encontrado para essa espécie, forma e versão. Experimente outra versão compatível.":$"{results.Count} encontro(s) · {PKHeX.Core.GameInfo.GetVersionName(pk.Version)}. Selecione um modelo para carregar no rascunho.";
        }
        catch(Exception e){if(!IsDisposed)encounterInfo.Text="Falha na consulta: "+e.Message;}
        finally{busy=false;if(!IsDisposed)search.Enabled=true;}
    }
    private void UseEncounter()
    {
        if(busy||encounters.SelectedItems.Count!=1)return;
        try{draft=PokemonEditorService.FromEncounter((IEncounterable)encounters.SelectedItems[0].Tag,ReadDraft(),save);LoadFields();Analyze();status.Text="Modelo de encontro carregado. Revise a análise antes de aplicar; nenhum arquivo foi gravado.";}
        catch(Exception error){status.Text=error.Message;}
    }
    private void Apply()
    {
        try{Result=ReadDraft();DialogResult=DialogResult.OK;Close();}
        catch(Exception error){status.Text=error.Message;}
    }
}
