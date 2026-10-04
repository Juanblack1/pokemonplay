using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;

internal static class GameControlsCheck
{
    internal static void Run(string root)
    {
        string install=Path.Combine(root,"game-controls");
        string executable=BundledEmulators.RetroArch(install);
        string core=Path.Combine(Path.GetDirectoryName(executable),"cores","mgba_libretro.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(core));
        File.WriteAllText(executable,"synthetic executable");File.WriteAllText(core,"synthetic core");
        Directory.CreateDirectory(Path.Combine(install,"Settings"));
        string[] keys={"NumPad8","S","A","D","Z","X","Q","E","Enter","Backspace"};
        File.WriteAllLines(Path.Combine(install,"Settings","input-presets.txt"),new[]{"5","","","","",""}.Concat(keys));
        string previous=Environment.GetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP");
        Environment.SetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP",Path.Combine(install,"sessions"));
        try {
            var game=new GameInfo{Generation=3,SaveFolderName="Synthetic GBA",Title="FireRed"};
            var plan=RetroArchSettingsService.CreateLaunch(install,game,Path.Combine(install,"diagnostic.gba"));
            string config=File.ReadAllText(plan.TemporaryConfigPath);
            RetroArchSettingsService.DeleteSessionConfig(plan.TemporaryConfigPath);
            if(!config.Contains("input_player1_up = \"keypad8\""))throw new Exception("Captured NumPad8 must reach RetroArch as keypad8, not an unknown Windows key name.");
            Console.WriteLine("PASS captured numeric keypad key uses a recognized RetroArch session binding");
            void Assert(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS "+text);}
            foreach(var example in new[]{("8","num8"),("D0","num0"),("NumPad1","keypad1"),("PageDown","pagedown"),("Delete","del"),("RShiftKey","rshift"),("ControlKey","ctrl"),("OemQuestion","slash"),("Decimal","kp_period")}){
                keys[0]=example.Item1;
                File.WriteAllLines(Path.Combine(install,"Settings","input-presets.txt"),new[]{"5","","","","",""}.Concat(keys));
                plan=RetroArchSettingsService.CreateLaunch(install,game,Path.Combine(install,"diagnostic.gba"));
                config=File.ReadAllText(plan.TemporaryConfigPath);RetroArchSettingsService.DeleteSessionConfig(plan.TemporaryConfigPath);
                Assert(config.Contains("input_player1_up = \""+example.Item2+"\""),"captured "+example.Item1+" reaches RetroArch using its recognized key name");
            }
            var profile=new InputDeviceProfile{Mode=2};
            profile.Save(Path.Combine(install,"Settings","input-device.json"));
            keys[0]="F11";
            File.WriteAllLines(Path.Combine(install,"Settings","input-presets.txt"),new[]{"5","","","","",""}.Concat(keys));
            string persistent=BundledEmulators.RetroArchConfig(install);
            File.WriteAllText(persistent,"input_player1_a_btn = \"0\"\ninput_enable_hotkey = \"alt\"\n");
            string persistentBefore=File.ReadAllText(persistent);
            plan=RetroArchSettingsService.CreateLaunch(install,game,Path.Combine(install,"diagnostic.gba"));
            config=File.ReadAllText(plan.TemporaryConfigPath);RetroArchSettingsService.DeleteSessionConfig(plan.TemporaryConfigPath);
            Assert(config.Contains("input_player1_a_btn = \"nul\"")&&config.Contains("input_player1_up_axis = \"nul\"")&&config.Contains("input_player1_l_x_plus_axis = \"nul\""),"launcher-managed controller disables duplicate native button and analog input for this session");
            Assert(config.Contains("input_enable_hotkey = \"f10\""),"RetroArch hotkey modifier avoids F11 when it is assigned to a game action");
            Assert(File.ReadAllText(persistent)==persistentBefore,"session input overrides preserve persistent RetroArch configuration");
            var settings=RetroArchSettingsService.Load(install);
            string external=Path.Combine(install,"external","retroarch.exe");Directory.CreateDirectory(Path.GetDirectoryName(external));File.WriteAllText(external,"synthetic external executable");
            settings.ExecutablePath=external;RetroArchSettingsService.Save(install,settings);
            plan=RetroArchSettingsService.CreateLaunch(install,game,Path.Combine(install,"diagnostic.gba"));
            config=File.ReadAllText(plan.TemporaryConfigPath);RetroArchSettingsService.DeleteSessionConfig(plan.TemporaryConfigPath);
            Assert(config.Contains("input_player1_up = \"f11\"")&&config.Contains("config_save_on_exit = \"false\""),"external RetroArch receives app keyboard mappings without persisting session overrides");

            var pad=new InputSnapshot{Connected=true};pad.Pressed.Add("StickUp");
            Assert(InputActionResolver.Resolve(profile,pad,null,true)[0],"default directional binding accepts the analog stick");
            profile.Bindings[0]="PadX";
            Assert(!InputActionResolver.Resolve(profile,pad,null,true)[0],"custom directional binding does not accept an unintended analog fallback");
            pad.Pressed.Add("PadX");
            Assert(InputActionResolver.Resolve(profile,pad,null,true)[0],"custom controller mapping activates its selected action");
            Assert(!InputActionResolver.Resolve(profile,pad,null,false).Any(value=>value),"loss of game focus releases controller actions");
            pad.Connected=false;
            Assert(!InputActionResolver.Resolve(profile,pad,null,true).Any(value=>value),"controller disconnect releases stale pressed input");
            profile.Mode=3;
            Assert(InputActionResolver.Resolve(profile,pad,new[]{true},true)[0]&&!InputActionResolver.Resolve(profile,pad,new[]{true},false)[0],"virtual controls are gated by game focus and tolerate a short snapshot");
            uint foregroundPid=0;int focusAttempts=0;
            bool[] suppressedVirtualAction={false};
            foregroundPid=VirtualFocusRecovery.RestoreForHeldVirtualAction(42,foregroundPid,true,true,()=>focusAttempts++,()=>foregroundPid);
            Assert(foregroundPid!=42&&focusAttempts==1&&!VirtualFocusRecovery.CanRoute(42,foregroundPid,true,true)&&!InputActionResolver.Resolve(profile,pad,suppressedVirtualAction,VirtualFocusRecovery.CanRoute(42,foregroundPid,true,true)).Any(value=>value),"failed foreground recovery never routes virtual keys into another window");
            foregroundPid=VirtualFocusRecovery.RestoreForHeldVirtualAction(42,foregroundPid,true,true,()=>{focusAttempts++;foregroundPid=42;},()=>foregroundPid);
            Assert(foregroundPid==42&&focusAttempts==2&&VirtualFocusRecovery.CanRoute(42,foregroundPid,true,false)&&!InputActionResolver.Resolve(profile,pad,suppressedVirtualAction,VirtualFocusRecovery.CanRoute(42,foregroundPid,true,false)).Any(value=>value),"raw virtual hold retries top-level focus while the suppressed key remains blocked");
            bool focusSettling=VirtualFocusRecovery.ShouldDeferFirstFocusedPoll(0,42,42,true,true);
            Assert(focusSettling&&!InputActionResolver.Resolve(profile,pad,new[]{true},!focusSettling)[0]&&InputActionResolver.Resolve(profile,pad,new[]{true},true)[0]&&!VirtualFocusRecovery.ShouldDeferFirstFocusedPoll(42,42,42,true,true)&&!VirtualFocusRecovery.ShouldDeferFirstFocusedPoll(0,42,42,false,true),"restored top-level focus gets one settling poll before a held virtual key routes on the next tick");
            Assert(InputActionResolver.Resolve(profile,pad,new[]{true},VirtualFocusRecovery.CanRoute(42,foregroundPid,true,false))[0],"restored foreground routes an unsuppressed virtual action");
            Assert(VirtualFocusRecovery.CanRoute(42,0,false,true),"embedded emulator still accepts an eligible focused host");
            foregroundPid=0;focusAttempts=0;
            VirtualFocusRecovery.RestoreForHeldVirtualAction(42,foregroundPid,false,true,()=>focusAttempts++,()=>foregroundPid);
            Assert(focusAttempts==0,"embedded emulator does not repeatedly steal foreground for virtual input");

            var events=new List<(Keys,bool)>();int focuses=0;
            var dispatcher=new InputKeyDispatcher(new[]{Keys.Z,Keys.Z},(key,down)=>{events.Add((key,down));return true;},()=>focuses++);
            dispatcher.Update(new[]{true,true});dispatcher.Update(new[]{false,true});
            Assert(events.SequenceEqual(new[]{(Keys.Z,true)})&&focuses==1,"two actions sharing a key retain it until both actions release");
            dispatcher.Update(new[]{false,false});
            Assert(events.SequenceEqual(new[]{(Keys.Z,true),(Keys.Z,false)}),"last shared action releases exactly one keyboard event");
            bool accepted=false;events.Clear();
            dispatcher=new InputKeyDispatcher(new[]{Keys.X},(key,down)=>{events.Add((key,down));return accepted;},()=>{});
            dispatcher.Update(new[]{true});accepted=true;dispatcher.Update(new[]{true});dispatcher.Release();
            Assert(events.SequenceEqual(new[]{(Keys.X,true),(Keys.X,true),(Keys.X,false)}),"failed keyboard injection is retried and shutdown releases accepted input");
            Assert(ControllerSelection.ConnectedSlot(2,index=>index is 0 or 2)==2,"detection preserves an already-connected chosen slot");
            Assert(ControllerSelection.ConnectedSlot(0,index=>index==3)==3,"detection finds a connected controller in a different slot");
            Assert(ControllerSelection.ConnectedSlot(1,index=>false)==-1,"no connected controller is reported without changing the selected slot");
            using var preset=new ThemeSelect();preset.Items.Add("fixture");preset.SelectedIndex=0;
            profile.Mode=2;
            using var workbench=new InputWorkbench(profile,preset,()=>new[]{"W","S","A","D","Z","X","Q","E","Enter","Backspace","C","V"},_=>{});
            workbench.Profile.Mode=2;workbench.UpdateRows();workbench.ControllerReader=index=>new InputSnapshot{Connected=index==3};workbench.SelectConnectedController();
            Assert(workbench.Profile.ControllerSlot==3,"detect control action selects the discovered slot in the editable profile");
            foreach(int width in new[]{760,1100}){
                workbench.Size=new Size(width,700);workbench.CreateControl();workbench.PerformLayout();
                var buttons=workbench.Controls.OfType<ThemeButton>().Where(button=>button.Text is "Testar comandos" or "Detectar controle").OrderBy(button=>button.Left).ToArray();
                Assert(buttons.Length==2&&buttons[0].Right<=buttons[1].Left&&buttons[1].Right<=workbench.Width,"controller and command actions fit at workbench width "+width);
                string visualOutput=Environment.GetEnvironmentVariable("POKEMONPLAY_CONTROLS_PREVIEW");
                if(!string.IsNullOrEmpty(visualOutput)) {
                    Directory.CreateDirectory(visualOutput);
                    using var bitmap=new Bitmap(workbench.Width,workbench.Height);
                    workbench.DrawToBitmap(bitmap,workbench.ClientRectangle);
                    bitmap.Save(Path.Combine(visualOutput,"controls-"+width+".png"));
                }
            }
            using var testWindow=new ConsoleTestForm(profile,keys.Concat(profile.ExtraKeys).ToArray(),0);
            Assert(testWindow.Text.Contains("comandos")&&testWindow.Controls.OfType<Panel>().SelectMany(panel=>panel.Controls.OfType<Label>()).Any(label=>label.Text.Contains("não abre uma ROM")),"command test explicitly distinguishes feedback from ROM execution");
        } finally {Environment.SetEnvironmentVariable("POKEMONPLAY_RETROARCH_TEMP",previous);}
    }
}
