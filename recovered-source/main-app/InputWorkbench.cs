using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

internal sealed class InputWorkbench : BufferedPanel, IMessageFilter
{
    public InputDeviceProfile Profile { get; private set; }
    public readonly List<KeyBindingRow> Rows = new();
    private readonly ThemeSelect mode, slot, console;
    private readonly Label connection, hint, live, deadZoneLabel, deadZoneValue;
    private readonly ThemeSlider deadZone;
    private readonly ThemeButton test, cancel;
    private readonly ControllerVisualizer visual;
    private readonly Timer timer;
    private readonly Func<string[]> keyboard;
    private readonly Action<int> captureKeyboard;
    private readonly ThemeSelect keyboardPreset;
    private readonly Panel mappingPanel;
    private readonly Label presetLabel;
    private int capturing = -1;
    private bool testing, armed;
    private HashSet<string> previous = new();
    private static readonly string[] Names = { "Cima", "Baixo", "Esquerda", "Direita", "Ação A", "Ação B", "Ombro L", "Ombro R", "Start", "Select", "Ação X", "Ação Y" };
    public InputWorkbench(InputDeviceProfile profile, ThemeSelect preset, Func<string[]> keyboard, Action<int> captureKeyboard)
    {
        Profile = profile; this.keyboard = keyboard; this.captureKeyboard = captureKeyboard; keyboardPreset = preset;
        BackColor = AppTheme.Surface; Height = 700;
        var title = MakeLabel("Controles e teste", AppTheme.Section); title.SetBounds(24,20,600,28); Controls.Add(title);
        hint = MakeLabel("Escolha como jogar. Clique em uma atribuição para personalizar.",AppTheme.Caption); hint.SetBounds(24,54,750,30); Controls.Add(hint);
        mode = new ThemeSelect { Location = new Point(24,100), Width = 290 }; mode.Items.AddRange(new object[] { "Só teclado", "Teclado + mouse", "Controle", "Touchpad" }); mode.SelectedIndex = Profile.Mode;
        slot = new ThemeSelect { Location = new Point(334,100), Width = 230 }; slot.Items.AddRange(new object[]{"Controle 1 · XInput","Controle 2 · XInput","Controle 3 · XInput","Controle 4 · XInput"}); slot.SelectedIndex = Profile.ControllerSlot;
        connection = MakeLabel("",AppTheme.Caption); connection.SetBounds(24,150,800,28);
        deadZoneLabel = MakeLabel("Zona morta",AppTheme.Caption);
        deadZoneValue = MakeLabel(Profile.DeadZone + "%",AppTheme.Caption);
        deadZone = new ThemeSlider { Minimum=10, Maximum=60, Value=Profile.DeadZone, SmallChange=1, LargeChange=5, TickFrequency=10, AccessibleName="Zona morta do controle" };
        deadZone.ValueChanged += (_,_) =>
        {
            Profile.DeadZone = deadZone.Value;
            deadZoneValue.Text = Profile.DeadZone + "%";
            visual.DeadZone = Profile.DeadZone;
            visual.Invalidate();
            live.Text = "Zona morta alterada. Salve as configurações.";
        };
        visual = new ControllerVisualizer { Location = new Point(24,210), Size = new Size(520,300), DeadZone=Profile.DeadZone };
        console=new ThemeSelect{Location=new Point(650,20),Width=250};console.Items.AddRange(new object[]{"Teste · Game Boy Advance","Teste · Nintendo DS","Teste · Nintendo 3DS"});console.SelectedIndex=Profile.TestConsole;visual.ConsoleModel=Profile.TestConsole;
        presetLabel = MakeLabel("Perfil de teclado",AppTheme.BodyBold); presetLabel.SetBounds(580,174,320,24); preset.SetBounds(580,204,320,40);
        mappingPanel=new Panel{AutoScroll=true,BackColor=AppTheme.Surface};Controls.Add(mappingPanel);
        for(int i=0;i<12;i++) { int index=i; var row=new KeyBindingRow(Names[i],""){Height=30}; row.RowClick+=(_,_)=>Capture(index); Rows.Add(row); mappingPanel.Controls.Add(row); }
        live = MakeLabel("Teste parado",AppTheme.Body); live.SetBounds(24,520,500,30);
        test = new ThemeButton("Iniciar teste",ButtonKind.Primary) { AutoSize=true, Location=new Point(24,570) }; test.Click+=(_,_)=>OpenConsoleTest();
        cancel = new ThemeButton("Cancelar captura",ButtonKind.Secondary) { AutoSize=true,Location=new Point(24,640),Visible=false }; cancel.Click+=(_,_)=>CancelCapture();
        Controls.AddRange(new Control[]{mode,slot,connection,deadZoneLabel,deadZone,deadZoneValue,visual,presetLabel,preset,live,test,cancel,console});
        console.SelectedIndexChanged+=(_,_)=>{SetTesting(false);Profile.TestConsole=console.SelectedIndex;visual.ConsoleModel=console.SelectedIndex;UpdateRows();};
        mode.SelectedIndexChanged+=(_,_)=>{SetTesting(false);CancelCapture();Profile.Mode=mode.SelectedIndex;UpdateRows();};
        slot.SelectedIndexChanged+=(_,_)=>{CancelCapture();Profile.ControllerSlot=slot.SelectedIndex;};
        preset.SelectedIndexChanged+=(_,_)=>UpdateRows(); Resize+=(_,_)=>Arrange();
        timer=new Timer { Interval=16 }; timer.Tick+=(_,_)=>Poll(); timer.Start(); Application.AddMessageFilter(this); UpdateRows();
    }
    private Label MakeLabel(string text,Font font) => new(){Text=text,Font=font,ForeColor=AppTheme.TextSecondary,BackColor=Color.Transparent,AutoSize=false};
    private void Arrange()
    {
        int leftWidth=Math.Max(320,(Width-72)*55/100), rightX=leftWidth+48, rightWidth=Width-rightX-24;
        mode.Width=Math.Min(290,Width-48); slot.Left=334; slot.Width=Math.Max(150,Math.Min(250,Width-358));
        visual.SetBounds(24,174,leftWidth,Math.Max(120,Height-234)); presetLabel.SetBounds(rightX,174,rightWidth,24); keyboardPreset.SetBounds(rightX,204,rightWidth,40);
        mappingPanel.SetBounds(rightX,254,rightWidth,Math.Max(90,Height-278));
        for(int i=0;i<Rows.Count;i++) Rows[i].SetBounds(0,i*34,rightWidth-20,32);
        live.SetBounds(24,Height-54,leftWidth,40);cancel.Location=new Point(24,Height-100);
        int connectionWidth=Math.Min(520,Math.Max(250,Width-336));
        connection.SetBounds(24,145,connectionWidth,28);deadZoneLabel.SetBounds(32+connectionWidth,145,76,28);
        int sliderX=deadZoneLabel.Right+4,valueX=Width-76;deadZone.SetBounds(sliderX,140,Math.Max(80,valueX-sliderX-8),30);deadZoneValue.SetBounds(valueX,145,52,28);
        hint.Width=Width-48;test.Location=new Point(Width-test.Width-24,100);console.Location=new Point(Width-console.Width-24,20);
    }
    public void UpdateRows()
    {
        bool pad=Profile.Mode==2;slot.Visible=pad;keyboardPreset.Visible=!pad;presetLabel.Text=pad?"Atribuições do controle":"Perfil de teclado";
        deadZoneLabel.Visible=deadZone.Visible=deadZoneValue.Visible=pad;
        if(deadZone.Value!=Profile.DeadZone)deadZone.Value=Profile.DeadZone;
        deadZoneValue.Text=Profile.DeadZone+"%";visual.DeadZone=Profile.DeadZone;
        var keys=keyboard(); for(int i=0;i<Rows.Count;i++){Rows[i].SetKey(pad?InputReader.Label(Profile.Bindings[i]):keys[i]);Rows[i].Editable=true;}
        for(int i=0;i<Rows.Count;i++)Rows[i].Visible=i<10||Profile.TestConsole>0;
        hint.Text=Profile.Mode switch { 0=>"Teclas acionam os botões. Clique em uma atribuição para editar.",1=>"Teclado para os botões; mouse como caneta na tela do DS.",2=>"Controle XInput: personalize os botões e ajuste a zona morta do analógico.",_=>"Botões virtuais na tela do jogo. Inicie o teste e toque ou clique no desenho." };
        visual.Actions=new bool[12];visual.Snapshot=new();visual.Invalidate();
    }
    private void Capture(int index)
    {
        SetTesting(false);
        if(Profile.Mode!=2){captureKeyboard(index);UpdateRows();return;}
        if(!InputReader.ReadPad(Profile.ControllerSlot,Profile.DeadZone).Connected){live.Text="Conecte um controle XInput para personalizar.";return;}
        capturing=index;armed=false;cancel.Visible=true;live.Text="Solte os botões; depois pressione para: "+Names[index];
    }
    private void CancelCapture(){capturing=-1;cancel.Visible=false;live.Text="Teste parado";}
    private void SetTesting(bool value)
    {
        CancelCapture();testing=value;test.Text=value?"Parar teste":"Iniciar teste";visual.ReleaseVirtual();visual.VirtualInput=value&&Profile.Mode==3;visual.Testing=value;live.Text=value?"Pressione teclas ou botões. Esc encerra o teste.":"Teste parado";visual.Actions=new bool[12];visual.Snapshot=new();visual.Invalidate();foreach(var row in Rows)row.Active=false;Arrange();
    }
    private void Poll()
    {
        if(IsDisposed || FindForm()==null)return;
        var pad=InputReader.ReadPad(Profile.ControllerSlot,Profile.DeadZone);
        connection.Text=Profile.Mode==2?(pad.Connected?$"Controle conectado · zona morta {Profile.DeadZone}%":"Nenhum controle neste slot. Conecte um controle XInput ou escolha outro slot."):"";
        connection.ForeColor=pad.Connected?AppTheme.Green:AppTheme.TextMuted;
        bool focused=Form.ActiveForm==FindForm()&&FindForm().ContainsFocus;
        if(!focused){ if(testing||capturing>=0){SetTesting(false);live.Text="Teste pausado ao sair da janela. Clique em Iniciar teste.";} previous.Clear();return; }
        if(capturing>=0)
        {
            if(!pad.Connected){CancelCapture();live.Text="Controle desconectado. Reconecte para personalizar.";return;}
            if(pad.Pressed.Count==0)armed=true;
            string token=armed?pad.Pressed.Except(previous).FirstOrDefault():null;
            if(token!=null){int index=capturing;CancelCapture();if(Profile.Bindings.Where((_,i)=>i!=index).Contains(token))live.Text="Esse botão já está atribuído. Escolha outro.";else{Profile.Bindings[index]=token;UpdateRows();live.Text="Botão alterado. Salve as configurações.";}}
            previous=new(pad.Pressed);return;
        }
        if(!testing)return;
        var keys=keyboard();var active=new bool[12];for(int i=0;i<12;i++){active[i]=Profile.Mode==2?pad.Pressed.Contains(Profile.Bindings[i]):Profile.Mode==3?visual.VirtualActions[i]:InputReader.Down(InputReader.ParseKey(keys[i]));if(Profile.Mode==2&&i<4&&Profile.Bindings[i]==InputReader.PadInputs[i])active[i]|=pad.Pressed.Contains(new[]{"StickUp","StickDown","StickLeft","StickRight"}[i]);}
        visual.Actions=active;visual.Snapshot=Profile.Mode==2?pad:new InputSnapshot();visual.Invalidate();
        for(int i=0;i<12;i++)Rows[i].Active=active[i];
        var pressed=Names.Where((_,i)=>active[i]).ToArray();live.Text=pressed.Length>0?"Ativo: "+string.Join(" + ",pressed):"Nenhuma ação pressionada";
        if(Profile.Mode==1){bool pointer=InputReader.Down(Keys.LButton)||InputReader.Down(Keys.RButton);if(pointer)live.Text+=" · Caneta pressionada";}
    }
    public bool PreFilterMessage(ref Message m)
    {
        if(!testing&&capturing<0)return false;
        if(m.Msg is 0x100 or 0x101 or 0x104 or 0x105)
        {
            if((Keys)m.WParam.ToInt32()==Keys.Escape && (m.Msg==0x100||m.Msg==0x104)){SetTesting(false);return true;}
            // While testing, keys drive the visualizer instead of activating focused buttons.
            return true;
        }
        return false;
    }
    private void OpenConsoleTest()
    {
        SetTesting(false);using var dialog=new ConsoleTestForm(Profile,keyboard(),console.SelectedIndex);dialog.ShowDialog(FindForm());Profile.TestConsole=dialog.SelectedConsole;console.SelectedIndex=dialog.SelectedConsole;visual.ConsoleModel=dialog.SelectedConsole;UpdateRows();
    }
    public void ResetProfile(){SetTesting(false);Profile=new();mode.SelectedIndex=0;slot.SelectedIndex=0;console.SelectedIndex=0;visual.ConsoleModel=0;UpdateRows();}
    public void RenderPreviewState(int previewMode,bool feedback)
    {
        SetTesting(false);mode.SelectedIndex=previewMode;UpdateRows();visual.VirtualInput=previewMode==3;
        if(feedback){visual.Testing=true;visual.Actions[0]=visual.Actions[4]=visual.Actions[6]=true;visual.Snapshot=new InputSnapshot{LX=-.6f,LY=.4f,LT=.7f};Rows[0].Active=Rows[4].Active=Rows[6].Active=true;live.Text="Prévia ilustrativa de ações pressionadas";visual.Invalidate();}
    }
    protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();Application.RemoveMessageFilter(this);}base.Dispose(disposing);}
}
