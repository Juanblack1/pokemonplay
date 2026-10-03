using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class ConsoleTestForm : Form, IMessageFilter
{
    private readonly InputDeviceProfile profile;
    private readonly string[] keys;
    private readonly ThemeSelect console;
    private readonly ControllerVisualizer visual;
    private readonly Label status, instructions;
    private readonly ThemeButton toggle;
    private readonly Timer timer;
    private bool running;
    private bool illustrative;
    public int SelectedConsole => console.SelectedIndex;
    public ConsoleTestForm(InputDeviceProfile profile,string[] keys,int model)
    {
        this.profile=profile;this.keys=keys;
        Text="Teste de comandos · Pokemons Play";ClientSize=new Size(940,720);MinimumSize=new Size(800,640);StartPosition=FormStartPosition.CenterParent;BackColor=AppTheme.Background;
        var top=new BufferedPanel{Dock=DockStyle.Top,Height=154,BackColor=AppTheme.Surface};
        var title=new Label{Text="Teste de comandos",Font=AppTheme.Section,ForeColor=AppTheme.Text,AutoSize=true,Location=new Point(24,18)};
        console=new ThemeSelect{Location=new Point(24,58),Width=230};console.Items.AddRange(new object[]{"Game Boy Advance","Nintendo DS","Nintendo 3DS"});console.SelectedIndex=Math.Clamp(model,0,2);
        toggle=new ThemeButton("Pausar teste",ButtonKind.Secondary){AutoSize=true,Location=new Point(274,58)};toggle.Click+=(_,_)=>SetRunning(!running);
        var close=new ThemeButton("Concluir teste",ButtonKind.Primary){AutoSize=true};close.Click+=(_,_)=>Close();top.Resize+=(_,_)=>close.Location=new Point(top.Width-close.Width-24,58);
        instructions=new Label{Font=AppTheme.Caption,ForeColor=AppTheme.TextSecondary,Location=new Point(24,106),Height=20,AutoSize=false,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
        var explanation=new Label{Text="Visualização de comandos e toque; não abre uma ROM nem comprova que o jogo roda.",Font=AppTheme.Caption,ForeColor=AppTheme.TextSecondary,Location=new Point(24,130),Height=20,AutoSize=false,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
        top.Controls.AddRange(new Control[]{title,console,toggle,close,instructions,explanation});
        status=new Label{Dock=DockStyle.Bottom,Height=64,Padding=new Padding(24,10,24,8),Font=AppTheme.Body,ForeColor=AppTheme.Text,BackColor=AppTheme.Surface,AutoSize=false};
        visual=new ControllerVisualizer{Dock=DockStyle.Fill,ConsoleModel=console.SelectedIndex,DeadZone=profile.DeadZone,BackColor=AppTheme.Background};
        Controls.Add(visual);Controls.Add(status);Controls.Add(top);
        console.SelectedIndexChanged+=(_,_)=>{visual.ReleaseVirtual();visual.ConsoleModel=console.SelectedIndex;SetRunning(running);};
        timer=new Timer{Interval=16};timer.Tick+=(_,_)=>Poll();Application.AddMessageFilter(this);SetRunning(true);timer.Start();
        Deactivate+=(_,_)=>{if(running&&!illustrative){SetRunning(false);status.Text="Teste pausado ao sair da janela. Clique em Retomar teste.";}};
        top.Width=ClientSize.Width;instructions.Width=explanation.Width=ClientSize.Width-48;
    }
    private void SetRunning(bool value)
    {
        running=value;visual.ReleaseVirtual();visual.Actions=new bool[12];visual.Snapshot=new();visual.Testing=value;visual.VirtualInput=value&&profile.Mode==3;visual.TouchTesting=value&&console.SelectedIndex>0;
        toggle.Text=value?"Pausar teste":"Retomar teste";
        instructions.Text=console.SelectedIndex==0?"Teste direcional, A/B, L/R, Start e Select. Esc conclui o teste.":"Pressione os botões. Clique ou toque; arraste na tela inferior. Esc encerra.";
        status.Text=value?"Nenhuma ação pressionada":"Teste pausado";visual.Invalidate();
    }
    private void Poll()
    {
        if(!running||illustrative||Form.ActiveForm!=this)return;
        var pad=profile.Mode==2?InputReader.ReadPad(profile.ControllerSlot,profile.DeadZone):new InputSnapshot();
        var actions=new bool[12];int count=console.SelectedIndex==0?10:12;
        for(int i=0;i<count;i++)
        {
            actions[i]=profile.Mode==2?pad.Pressed.Contains(profile.Bindings[i]):profile.Mode==3?visual.VirtualActions[i]:InputReader.Down(InputReader.ParseKey(keys[i]));
            if(profile.Mode==2&&i<4&&profile.Bindings[i]==InputReader.PadInputs[i])actions[i]|=pad.Pressed.Contains(new[]{"StickUp","StickDown","StickLeft","StickRight"}[i]);
        }
        visual.Actions=actions;visual.Snapshot=pad;visual.Invalidate();
        string[] names={"Cima","Baixo","Esquerda","Direita","A","B","L","R","Start","Select","X","Y"};
        var pressed=names.Where((_,i)=>actions[i]).ToArray();
        string text=pressed.Length>0?"Ativo: "+string.Join(" + ",pressed):"Nenhuma ação pressionada";
        if(profile.Mode==2&&!pad.Connected)text="Controle XInput desconectado. Reconecte ou escolha outro slot nas configurações.";
        if(console.SelectedIndex>0)text+="\nTela inferior: "+(visual.TouchPressed?$"pressionada · X {visual.TouchPosition.X} / Y {visual.TouchPosition.Y} · contatos {visual.TouchContacts}":"solta · clique ou toque para testar");
        status.Text=text;
    }
    public bool PreFilterMessage(ref Message m)
    {
        if(!running||Form.ActiveForm!=this)return false;
        if(m.Msg is 0x100 or 0x101 or 0x104 or 0x105){if((Keys)m.WParam.ToInt32()==Keys.Escape&&(m.Msg==0x100||m.Msg==0x104))Close();return true;}return false;
    }
    public void RenderIllustrativeState(int model)
    {
        illustrative=true;console.SelectedIndex=model;SetRunning(false);visual.Testing=true;visual.Actions[0]=visual.Actions[4]=visual.Actions[6]=true;visual.Snapshot=new InputSnapshot{LX=-.6f,LY=.4f};
        if(model>0)visual.PreviewTouch(.65f,.4f);
        status.Text="Prévia ilustrativa: direcional, A e L pressionados"+(model>0?"\nMarca de toque na tela inferior":"");visual.Invalidate();
    }
    protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();Application.RemoveMessageFilter(this);}base.Dispose(disposing);}
}
