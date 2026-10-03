using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ReservedKeyFeedbackCheck
{
    record State(int Preset, string[] Custom, string[] Extra, string[] Effective);
    static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    static void Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
    static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS reserved key " + message); }
    static State Read(SettingsView view)
    {
        int preset = Field<ThemeSelect>(view,"preset").SelectedIndex;
        var custom = (string[])Field<string[]>(view,"customKeys").Clone();
        var extra = (string[])Field<InputWorkbench>(view,"controlsCard").Profile.ExtraKeys.Clone();
        return new(preset,custom,extra,InputDeviceProfile.KeyboardKeys(preset,custom).Concat(extra).ToArray());
    }
    static void Unchanged(State before, State after) => Expect(before.Preset==after.Preset && before.Custom.SequenceEqual(after.Custom) && before.Extra.SequenceEqual(after.Extra) && before.Effective.SequenceEqual(after.Effective),"reserved/cancelled capture preserves preset and every mapping");
    static bool Has(string text,string token) => text?.Contains(token,StringComparison.OrdinalIgnoreCase)==true;
    static void Feedback(string text,string action,string kind)
    {
        Expect(Has(text,"F12") && Has(text,"reservad") && Has(text,"voltar") && Has(text,"menu") && Has(text,"outra tecla") && Has(text,action),kind+" explains reserved F12, menu return, retry and current action");
    }
    static void Case(string root,int preset,int action,Keys completion)
    {
        string folder=Path.Combine(root,"reserved-key-feedback",$"{preset}-{action}-{completion}","Settings");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"input-presets.txt"),device=InputDeviceProfile.PathFor(file);
        File.WriteAllLines(file,new[]{preset.ToString(),"0","100","0","0","1","U","I","O","P","H","J","K","L","N","M"});
        new InputDeviceProfile{TestConsole=1,ExtraKeys=new[]{"F9","F10"}}.Save(device);
        byte[] settingsBytes=File.ReadAllBytes(file),deviceBytes=File.ReadAllBytes(device);
        using var view=new SettingsView(file);
        using var owner=new Form{Width=1100,Height=800};owner.Controls.Add(view);owner.Show();Application.DoEvents();
        State before=Read(view);string actionName=action==4?"Ação A":"Ação Y";
        Exception failure=null;bool observed=false;KeyCaptureDialog dialog=null;DialogResult result=DialogResult.None;string captured=null;
        using var watchdog=new Timer{Interval=5000};
        watchdog.Tick+=(_,_)=>{
            failure??=new TimeoutException("Owned reserved-key modal exceeded five seconds");
            foreach(var item in Application.OpenForms.Cast<Form>().OfType<KeyCaptureDialog>().Where(d=>d.Owner==owner).ToArray())item.Close();
        };
        owner.BeginInvoke((Action)(()=>{
            try {
                dialog=Application.OpenForms.Cast<Form>().OfType<KeyCaptureDialog>().Single(d=>d.Owner==owner&&d.Visible);observed=true;
                Label instruction=dialog.Controls.OfType<Label>().Single();
                Expect(Has(instruction.Text,actionName)&&Has(instruction.Text,"Esc"),"initial instruction retains action and Escape cancellation");
                string initialAccessible=(instruction.AccessibilityObject.Name??"")+" "+(instruction.AccessibilityObject.Description??"");
                Expect(Has(initialAccessible,actionName)&&Has(initialAccessible,"Esc"),"initial real label accessible object exposes instruction");
                Invoke(dialog,"OnKeyDown",new KeyEventArgs(Keys.F12));
                Expect(dialog.Visible&&!dialog.IsDisposed&&dialog.DialogResult==DialogResult.None&&dialog.CapturedKey==null,"F12 keeps modal open without accepting a key");
                Unchanged(before,Read(view));
                Expect(File.ReadAllBytes(file).SequenceEqual(settingsBytes)&&File.ReadAllBytes(device).SequenceEqual(deviceBytes),"F12 and accessibility queries preserve owned settings bytes");
                Feedback(instruction.Text,actionName,"visible instruction");
                string accessible=(instruction.AccessibilityObject.Name??"")+" "+(instruction.AccessibilityObject.Description??"");
                Feedback(accessible,actionName,"actual label accessible object");
                Invoke(dialog,"OnKeyDown",new KeyEventArgs(completion));
                result=dialog.DialogResult;captured=dialog.CapturedKey;
            } catch(Exception error){failure=error;dialog?.Close();}
        }));
        watchdog.Start();
        try { Invoke(view,"CaptureKey",action); }
        finally { watchdog.Stop();if(dialog!=null&&!dialog.IsDisposed)dialog.Close(); }
        if(failure!=null)throw new Exception($"preset{preset}/action{action}/{completion}",failure);
        Expect(observed,"production SettingsView opens owned real modal");
        State after=Read(view);
        if(completion==Keys.Escape){Expect(result==DialogResult.Cancel&&captured==null,"F12 then Escape cancels without captured key");Unchanged(before,after);}
        else {
            Expect(result==DialogResult.OK&&captured=="F8","F12 then valid F8 accepts only F8");
            Expect(after.Effective[action]=="F8"&&after.Effective.Where((_,i)=>i!=action).SequenceEqual(before.Effective.Where((_,i)=>i!=action)),"real SettingsView changes only requested effective assignment");
            Expect(after.Preset==(action<10?5:before.Preset),"valid key preserves intended preset conversion semantics");
        }
        Expect(File.ReadAllBytes(file).SequenceEqual(settingsBytes)&&File.ReadAllBytes(device).SequenceEqual(deviceBytes),"unsaved completed capture preserves settings bytes");
        owner.Close();
    }
    internal static void Run(string root)
    {
        var failures=new List<string>();
        foreach(int preset in new[]{0,5})foreach(int action in new[]{4,11})foreach(Keys completion in new[]{Keys.Escape,Keys.F8}){
            try{Case(root,preset,action,completion);}catch(Exception error){failures.Add(error.ToString());Console.WriteLine("FAIL reserved key "+error);}
        }
        if(failures.Count>0)throw new Exception("Reserved-key feedback failed "+failures.Count+"/8 cases\n"+string.Join("\n",failures));
        Console.WriteLine("PASS all eight owned real reserved-key modal cases");
    }
}
