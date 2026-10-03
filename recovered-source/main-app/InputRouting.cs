using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

internal static class InputActionResolver
{
    internal static bool[] Resolve(InputDeviceProfile profile,InputSnapshot pad,bool[] virtualActions,bool focused)
    {
        var actions=new bool[12];
        if(!focused)return actions;
        for(int i=0;i<actions.Length;i++) {
            if(profile.Mode==3)actions[i]=virtualActions!=null&&i<virtualActions.Length&&virtualActions[i];
            else if(profile.Mode==2&&pad.Connected) {
                actions[i]=pad.Pressed.Contains(profile.Bindings[i]);
                if(i<4&&profile.Bindings[i]==InputReader.PadInputs[i])
                    actions[i]|=pad.Pressed.Contains(new[]{"StickUp","StickDown","StickLeft","StickRight"}[i]);
            }
        }
        return actions;
    }
}

internal sealed class InputKeyDispatcher
{
    private readonly Keys[] keys;
    private readonly Func<Keys,bool,bool> send;
    private readonly Action focus;
    private readonly HashSet<Keys> held=new();
    internal InputKeyDispatcher(Keys[] keys,Func<Keys,bool,bool> send,Action focus)
    {this.keys=(Keys[])keys.Clone();this.send=send;this.focus=focus;}
    internal void Update(bool[] actions)
    {
        var desired=new HashSet<Keys>(keys.Where((key,index)=>key!=Keys.None&&index<actions.Length&&actions[index]));
        foreach(Keys key in held.Except(desired).ToArray())if(send(key,false))held.Remove(key);
        Keys[] presses=desired.Except(held).ToArray();
        if(presses.Length>0)focus();
        foreach(Keys key in presses)if(send(key,true))held.Add(key);
    }
    internal void Release()=>Update(Array.Empty<bool>());
}

internal static class ControllerSelection
{
    internal static int ConnectedSlot(int preferred,Func<int,bool> connected)
    {
        if(preferred is >= 0 and < 4&&connected(preferred))return preferred;
        for(int slot=0;slot<4;slot++)if(slot!=preferred&&connected(slot))return slot;
        return -1;
    }
}
