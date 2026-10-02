using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class AppUpdateDialog : Form
{
    private readonly AppUpdateService service;
    private readonly TextBox repository=new();
    private readonly Label status=new();
    private readonly TextBox notes=new();
    private readonly ProgressBar progress=new();
    private readonly ThemeButton check=new("Verificar",ButtonKind.Secondary);
    private readonly ThemeButton install=new("Atualizar e reabrir",ButtonKind.Primary);
    private readonly ThemeButton later=new("Agora não",ButtonKind.Secondary);
    private readonly ThemeButton release=new("Ver release no GitHub",ButtonKind.Secondary);
    private AppUpdate update;
    private CancellationTokenSource cancellation;
    internal Func<bool> BeforeRestart {get;set;}
    internal string PreparedStage {get;private set;}
    internal AppUpdate CurrentUpdate=>update;
    internal AppUpdateDialog(AppUpdateService service,AppUpdate available)
    {
        this.service=service;update=available;Text="Atualizações do Pokemons Play";ClientSize=new Size(680,520);MinimumSize=new Size(640,520);StartPosition=FormStartPosition.CenterParent;BackColor=AppTheme.Background;ForeColor=AppTheme.Text;Font=AppTheme.Body;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=7};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int height in new[]{32,24,40,52})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        layout.Controls.Add(new Label{Text="Sua versão: "+AppRelease.Tag,AutoSize=true,Font=AppTheme.BodyBold},0,0);
        layout.Controls.Add(new Label{Text="Repositório público de atualizações no GitHub",AutoSize=true,ForeColor=AppTheme.TextMuted},0,1);
        var source=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));
        repository.Text=service.Repository;repository.Dock=DockStyle.Fill;repository.PlaceholderText="https://github.com/usuario/pokemon-play";repository.BackColor=AppTheme.SurfaceRaised;repository.ForeColor=AppTheme.Text;repository.AccessibleName="Repositório público das atualizações";source.Controls.Add(repository,0,0);check.Dock=DockStyle.Fill;source.Controls.Add(check,1,0);layout.Controls.Add(source,0,2);
        status.Dock=DockStyle.Fill;status.ForeColor=AppTheme.TextSecondary;layout.Controls.Add(status,0,3);
        notes.Multiline=true;notes.ReadOnly=true;notes.Dock=DockStyle.Fill;notes.ScrollBars=ScrollBars.Vertical;notes.BackColor=AppTheme.Surface;notes.ForeColor=AppTheme.Text;notes.AccessibleName="Novidades da release";layout.Controls.Add(notes,0,4);
        progress.Dock=DockStyle.Fill;progress.Visible=false;layout.Controls.Add(progress,0,5);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,FlowDirection=FlowDirection.RightToLeft};actions.Controls.AddRange(new Control[]{later,install,release});foreach(var button in new[]{later,install,release}){button.AutoSize=true;button.Margin=new Padding(8,8,0,0);}layout.Controls.Add(actions,0,6);Controls.Add(layout);
        check.Click+=async(_,_)=>await Check();install.Click+=async(_,_)=>await Download();release.Click+=(_,_)=>{if(update!=null)Process.Start(new ProcessStartInfo(update.ReleaseUrl){UseShellExecute=true});};later.Click+=(_,_)=>{if(cancellation!=null){cancellation.Cancel();status.Text="Cancelando download…";}else Close();};
        repository.TextChanged+=(_,_)=>{if(cancellation==null){update=null;ShowState();}};
        FormClosing+=(_,e)=>{if(cancellation!=null){cancellation.Cancel();e.Cancel=true;status.Text="Cancelando download…";}};
        ShowState();
    }
    private void ShowState()
    {
        install.Enabled=release.Enabled=update!=null;
        status.Text=update!=null?$"{update.Tag} disponível · {update.Size/1024d/1024d:0.0} MB. Você decide quando atualizar.":service.Repository.Length==0?"Informe o repositório para ativar a verificação de atualizações.":"Clique em Verificar para consultar a última release estável.";
        notes.Text=update?.Notes??"A atualização substitui o aplicativo e preserva seus saves, banco Pokémon e configurações.";
    }
    private async Task Check()
    {
        check.Enabled=install.Enabled=release.Enabled=repository.Enabled=false;cancellation=new CancellationTokenSource();later.Text="Cancelar verificação";
        try{service.SetRepository(repository.Text);repository.Text=service.Repository;update=await service.CheckAsync(cancellation.Token);ShowState();if(update==null)status.Text="Você já está usando a versão mais recente publicada.";}
        catch(Exception e){update=null;ShowState();status.Text=e is TaskCanceledException?cancellation.IsCancellationRequested?"Verificação cancelada.":"A verificação demorou demais. Tente novamente.":e.Message;}
        finally{cancellation.Dispose();cancellation=null;check.Enabled=repository.Enabled=true;later.Text="Agora não";}
    }
    private async Task Download()
    {
        if(update==null)return;repository.Enabled=check.Enabled=install.Enabled=release.Enabled=false;progress.Visible=true;progress.Value=0;later.Text="Cancelar download";cancellation=new CancellationTokenSource();
        string stage=null;
        try
        {
            status.Text="Baixando "+update.Tag+"… Você pode continuar usando a versão atual se cancelar.";
            stage=await service.DownloadAsync(update,new Progress<int>(value=>{progress.Value=value;status.Text=$"Baixando {update.Tag} · {value}%";}),cancellation.Token);
            if(BeforeRestart!=null&&!BeforeRestart()){AppUpdateService.TryClean(stage);stage=null;status.Text="Atualização adiada. A versão atual continua aberta.";return;}
            PreparedStage=stage;stage=null;
        }
        catch(OperationCanceledException){status.Text="Download cancelado. A versão atual foi preservada.";}
        catch(Exception e){status.Text=e.Message;}
        finally{if(stage!=null)AppUpdateService.TryClean(stage);cancellation.Dispose();cancellation=null;repository.Enabled=check.Enabled=true;install.Enabled=release.Enabled=update!=null;later.Text="Agora não";progress.Visible=false;}
        if(PreparedStage!=null){DialogResult=DialogResult.OK;Close();}
    }
}
