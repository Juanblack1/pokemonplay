using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

internal sealed partial class PokemonEditorDialog
{
    private ThemeButton apply;
    private readonly Label evBudget=new(){AutoSize=true,Font=AppTheme.Body,ForeColor=AppTheme.Text};
    private readonly Label legalityState=new(){Dock=DockStyle.Bottom,Height=96,Padding=new Padding(16,8,16,8),Font=AppTheme.Body,ForeColor=AppTheme.TextMuted,BackColor=AppTheme.SurfaceRaised,AccessibleName="Legalidade do rascunho"};
    private readonly System.Windows.Forms.Timer legalityTimer=new(){Interval=350};
    private readonly Dictionary<string,ThemeSlider> evSliders=new();
    private bool syncingStats;
    internal bool CanApply => apply.Enabled;
    internal string EvBudgetText => evBudget.Text;
    internal string LegalityStateText => legalityState.Text;
    internal Color ValidationColor => evBudget.ForeColor;
    private static readonly (string Name,string Key)[] StatNames={ ("HP","HP"),("Ataque","ATK"),("Defesa","DEF"),("At. especial","SPA"),("Def. especial","SPD"),("Velocidade","SPE") };

    private TableLayoutPanel FieldTable(int captionWidth)
    {
        var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(0,6,0,6)};
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,captionWidth));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return fields;
    }
    private Control BuildStatTable()
    {
        var stack=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
        stack.Controls.Add(new Label{Text="Atributos",Font=AppTheme.Section,ForeColor=AppTheme.Text,AutoSize=true,Margin=new Padding(0,8,0,12)});
        stack.Controls.Add(evBudget);
        stack.Controls.Add(new Label{Text=$"EVs: até {draft.MaxEV} por atributo · IVs: 0–{draft.MaxIV}",Font=AppTheme.Caption,ForeColor=AppTheme.TextMuted,AutoSize=true,Margin=new Padding(0,4,0,12)});
        var table=new TableLayoutPanel{AutoSize=true,ColumnCount=4,Margin=Padding.Empty};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,62));table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,62));
        foreach(var entry in new[]{("Atributo",0),("Distribuição de EVs",1),("EV",2),("IV",3)})table.Controls.Add(new Label{Text=entry.Item1,Font=AppTheme.Caption,ForeColor=AppTheme.TextMuted,AutoSize=true},entry.Item2,0);
        int row=1;
        foreach(var stat in StatNames)
        {
            var caption=new Label{Text=stat.Name,Font=AppTheme.Body,ForeColor=AppTheme.TextSecondary,AutoSize=true,Margin=new Padding(0,8,0,8)};
            table.Controls.Add(caption,0,row);
            var slider=new ThemeSlider{Minimum=0,Maximum=draft.MaxEV,SmallChange=4,LargeChange=32,Dock=DockStyle.Fill,Height=36,Margin=new Padding(2,2,10,6),AccessibleName=stat.Name+" distribuição de EVs"};
            evSliders.Add(stat.Key,slider);table.Controls.Add(slider,1,row);
            foreach(var prefix in new[]{"EV_","IV_"})
            {
                string property=prefix+stat.Key;
                int limit=prefix=="EV_"?draft.MaxEV:draft.MaxIV;
                // Keep an invalid imported value visible so it can be corrected, never silently truncate it.
                int initial=Convert.ToInt32(typeof(PKM).GetProperty(property).GetValue(draft));
                var number=new NumericUpDown{Minimum=0,Maximum=Math.Max(initial,limit),Width=56,Font=AppTheme.Body,BackColor=AppTheme.SurfaceRaised,ForeColor=AppTheme.Text,BorderStyle=BorderStyle.None,AccessibleName=stat.Name+" "+prefix[..2]};
                Bind(property,number,()=>number.Value);var frame=new EditorFieldFrame(number){Dock=DockStyle.Top,Margin=new Padding(0,2,6,6)};table.Controls.Add(frame,prefix=="EV_"?2:3,row);
                if(prefix=="EV_")slider.ValueChanged+=(_,_)=>{if(!loading&&!syncingStats)number.Value=Math.Min(number.Maximum,slider.Value);};
            }
            row++;
        }
        stack.Controls.Add(table);
        var tools=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=new Padding(0,8,0,4)};
        var reset=new ThemeButton("Zerar EVs",ButtonKind.Secondary){AutoSize=true,Font=AppTheme.Body};reset.Click+=(_,_)=>SetStats("EV_",0);
        var ivs=new ThemeButton("IVs no máximo",ButtonKind.Secondary){AutoSize=true,Font=AppTheme.Body};ivs.Click+=(_,_)=>SetStats("IV_",draft.MaxIV);
        tools.Controls.AddRange(new Control[]{reset,ivs});stack.Controls.Add(tools);
        stack.Controls.Add(new Label{Text="Excesso de EVs bloqueia a aplicação.",Font=AppTheme.Caption,ForeColor=AppTheme.TextMuted,AutoSize=true,Margin=new Padding(0,8,0,8)});
        stack.SizeChanged+=(_,_)=>table.Width=Math.Max(300,stack.Width-2);
        stack.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        return stack;
    }
    private void SetStats(string prefix,int value)
    {
        loading=true;
        foreach(var b in bindings.Where(b=>b.Property.Name.StartsWith(prefix,StringComparison.Ordinal)))((NumericUpDown)b.Control).Value=value;
        loading=false;Changed(prefix);
    }
    private void InitializeLiveFeedback()
    {
        summary.Parent.Controls.Add(legalityState);legalityState.SendToBack();
        legalityTimer.Tick+=(_,_)=>{legalityTimer.Stop();_ = AnalyzeLive();};
        legalityState.Cursor=Cursors.Hand;legalityState.Click+=(_,_)=>Analyze();
        summary.Text="";
    }
    private void RefreshValidation()
    {
        var evs=bindings.Where(b=>b.Property.Name.StartsWith("EV_",StringComparison.Ordinal)).ToArray();
        int total=evs.Sum(b=>Convert.ToInt32(b.Read()));bool invalid=total>510||evs.Any(b=>Convert.ToInt32(b.Read())>draft.MaxEV);
        evBudget.Text=total>510?$"EVs {total}/510 · remova {total-510} pontos":$"EVs {total}/510 · {510-total} disponíveis";
        evBudget.ForeColor=invalid?AppTheme.Red:AppTheme.Green;evBudget.AccessibleName=evBudget.Text;
        syncingStats=true;
        foreach(var b in evs)
        {
            var number=(NumericUpDown)b.Control;bool error=total>510||number.Value>draft.MaxEV;
            number.ForeColor=error?AppTheme.Red:AppTheme.Text;
            if(number.Parent is EditorFieldFrame frame){frame.HasError=error;frame.Invalidate();}
            evSliders[b.Property.Name[3..]].Value=(int)number.Value;
        }
        syncingStats=false;
        try{ReadDraft();apply.Enabled=true;status.ForeColor=AppTheme.TextMuted;}
        catch(Exception error){apply.Enabled=false;status.Text=error.Message;status.ForeColor=AppTheme.Red;}
    }
    private void QueueLegality()
    {
        legalityTimer.Stop();
        if(!apply.Enabled){legalityState.Text="Não é possível aplicar\nCorrija os campos em vermelho.";legalityState.ForeColor=AppTheme.Red;report.Text=status.Text;return;}
        legalityState.Text="Analisando rascunho…\nVerificação automática pelo PKHeX";legalityState.ForeColor=AppTheme.TextMuted;
        status.Text="Rascunho alterado · verificando legalidade…";report.Text="Verificando o rascunho atual…";
        legalityTimer.Start();
    }
    internal async Task AnalyzeLive()
    {
        long analyzedRevision=revision;PKM pk;
        try{pk=ReadDraft();}catch{RefreshValidation();return;}
        try
        {
            var result=await Task.Run(()=>PokemonLegalityService.Analyze(pk));
            if(IsDisposed||Disposing||analyzedRevision!=revision)return;
            legalityState.Text=result.IsConsistent?"Legal segundo o PKHeX\nNenhuma inconsistência encontrada.":$"Revisar legalidade\n{result.IssueCount} inconsistência(s). Clique para detalhes.";
            legalityState.ForeColor=result.IsConsistent?AppTheme.Green:AppTheme.Red;
            report.Text=(result.IsConsistent?"Nenhuma inconsistência encontrada.":$"{result.IssueCount} inconsistência(s) encontrada(s).")+"\r\n\r\n"+result.Report+"\r\n\r\nA análise verifica regras conhecidas. Não comprova captura real nem garante aceitação online.";
            status.ForeColor=result.IsConsistent?AppTheme.Green:AppTheme.Red;
            status.Text=result.IsConsistent?"Rascunho compatível com as verificações do PKHeX · pronto para aplicar.":"O PKHeX encontrou inconsistências. Revise Legalidade ou use um modelo da Base de encontros.";
        }
        catch(Exception error)
        {
            if(IsDisposed||Disposing||analyzedRevision!=revision)return;
            legalityState.Text="Análise indisponível\nUse Verificar legalidade para tentar novamente.";legalityState.ForeColor=AppTheme.Gold;report.Text=error.Message;status.Text="Não foi possível verificar: "+error.Message;status.ForeColor=AppTheme.Gold;
        }
    }
}

// Native input semantics with the same pixel border and focus/error vocabulary as the app.
internal sealed class EditorFieldFrame : BufferedPanel
{
    internal bool HasError {get;set;}
    internal EditorFieldFrame(Control input)
    {
        Height=36;Padding=new Padding(8,6,8,5);BackColor=AppTheme.SurfaceRaised;
        if(input is TextBox text)text.BorderStyle=BorderStyle.None;
        if(input is NumericUpDown number)number.BorderStyle=BorderStyle.None;
        input.Dock=DockStyle.Fill;Controls.Add(input);
        input.GotFocus+=(_,_)=>Invalidate();input.LostFocus+=(_,_)=>Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);using var pen=new Pen(HasError?AppTheme.Red:ContainsFocus?AppTheme.Focus:AppTheme.Border);
        PaintTools.DrawRounded(e.Graphics,pen,new Rectangle(1,1,Width-3,Height-3),AppTheme.Radius);
    }
}

// Themed navigation replaces white native tab headers while preserving keyboard focus.
internal sealed class EditorPageHost : Panel
{
    private readonly FlowLayoutPanel navigation=new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(0,0,0,8)};
    private readonly Panel content=new(){Dock=DockStyle.Fill};
    private readonly Dictionary<Panel,ThemeButton> buttons=new();
    internal Collection<Panel> TabPages {get;}
    private Panel selected;
    internal Panel SelectedTab
    {
        get=>selected;
        set
        {
            if(value==null||!TabPages.Contains(value))return;
            selected=value;
            foreach(var pair in buttons){pair.Key.Visible=pair.Key==value;pair.Value.Kind=pair.Key==value?ButtonKind.Primary:ButtonKind.Secondary;}
            value.BringToFront();
        }
    }
    internal EditorPageHost()
    {
        TabPages=new PageCollection(this);Controls.Add(content);Controls.Add(navigation);
    }
    private sealed class PageCollection(EditorPageHost owner) : Collection<Panel>
    {
        protected override void InsertItem(int index,Panel page)
        {
            base.InsertItem(index,page);page.Dock=DockStyle.Fill;page.Visible=false;contentAdd(page);
            var button=new ThemeButton(page.Text,ButtonKind.Secondary){AutoSize=true,Font=AppTheme.Body,Height=36,Margin=new Padding(0,0,6,6),AccessibleName="Seção "+page.Text};
            button.Click+=(_,_)=>owner.SelectedTab=page;owner.buttons.Add(page,button);owner.navigation.Controls.Add(button);
            if(owner.selected==null)owner.SelectedTab=page;
        }
        private void contentAdd(Panel page)=>owner.content.Controls.Add(page);
    }
}
