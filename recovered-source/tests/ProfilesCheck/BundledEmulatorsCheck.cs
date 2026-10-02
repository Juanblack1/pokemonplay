using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class BundledEmulatorsCheck
{
    static object Call(Type type,string name,params object[] args) => type.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(null,args);
    static object Property(object value,string name)=>value.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
    static void Assert(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    internal static void Run(string root,Assembly app)
    {
        string install=Path.Combine(root,"bundled-install");
        string ra=Path.Combine(install,"PokemonPlayRuntime","Emulators","RetroArch");
        string az=Path.Combine(install,"PokemonPlayRuntime","Emulators","Azahar","azahar.exe");
        Directory.CreateDirectory(Path.Combine(ra,"cores")); Directory.CreateDirectory(Path.GetDirectoryName(az));
        foreach(string file in new[]{Path.Combine(ra,"retroarch.exe"),Path.Combine(ra,"cores","mgba_libretro.dll"),Path.Combine(ra,"cores","melondsds_libretro.dll"),az})File.WriteAllText(file,"fixture");
        Type service=app.GetType("RetroArchSettingsService");
        var settings=Call(service,"Load",install);
        Assert((bool)Property(settings,"UseForGba")&&(bool)Property(settings,"UseForDs"),"fresh installation selects bundled GBA and DS without setup");
        Assert((string)Property(settings,"DsCorePath")==Path.Combine(ra,"cores","melondsds_libretro.dll"),"official melonDS DS core name is discovered");
        Call(service,"Save",install,settings);
        string moved=install+"-moved"; Directory.Move(install,moved);
        settings=Call(service,"Load",moved);
        Assert((string)Property(settings,"ExecutablePath")==Path.Combine(moved,"PokemonPlayRuntime","Emulators","RetroArch","retroarch.exe"),"portable bundle paths rebase after moving the installation");
        settings.GetType().GetProperty("UseForDs").SetValue(settings,false); Call(service,"Save",moved,settings);
        Assert(!(bool)Property(Call(service,"Load",moved),"UseForDs"),"saved opt-out remains disabled");
        string external=Path.Combine(root,"external-retroarch.exe");File.WriteAllText(external,"fixture");
        settings.GetType().GetProperty("ExecutablePath").SetValue(settings,external); Call(service,"Save",moved,settings);
        Assert((string)Property(Call(service,"Load",moved),"ExecutablePath")==external,"explicit external emulator selection is preserved");
        settings=Call(app.GetType("BundledEmulators"),"Defaults",moved); Call(service,"Save",moved,settings);
        var gbaGame=Activator.CreateInstance(app.GetType("GameInfo"));gbaGame.GetType().GetField("Generation").SetValue(gbaGame,3);gbaGame.GetType().GetField("SaveFolderName").SetValue(gbaGame,"Bundle GBA");
        string gbaRom=Path.Combine(root,"fixture.gba"); File.WriteAllText(gbaRom,"fixture");
        var plan=Call(service,"CreateLaunch",moved,gbaGame,gbaRom);
        string session=(string)Property(plan,"TemporaryConfigPath");string config=File.ReadAllText(session);
        Assert(((string)Property(plan,"Arguments")).Contains("--config") && config.Contains("input_player1_up = \"w\"") && config.Contains("config_save_on_exit = \"false\""),"bundled launch uses persistent user config and app keyboard preset without persisting session overrides");
        Assert(File.Exists(Path.Combine(moved,"Settings","Emulators","RetroArch","retroarch.cfg")),"RetroAchievements config is outside the runtime replaced by updates");
        Call(service,"DeleteSessionConfig",session);
        string legacy=Path.Combine(root,"legacy-install","Pokemon - Arquivos","visualboyadvance-m.exe");Directory.CreateDirectory(Path.GetDirectoryName(legacy));File.WriteAllText(legacy,"fixture");
        Assert(!(bool)Property(Call(service,"Load",Path.GetDirectoryName(Path.GetDirectoryName(legacy))),"UseForGba"),"installations without a bundle keep their existing standalone engine");
        var game=Activator.CreateInstance(app.GetType("GameInfo"));game.GetType().GetField("Generation").SetValue(game,6);game.GetType().GetField("IsImported").SetValue(game,true);
        string rom=Path.Combine(root,"fixture.cci");File.WriteAllText(rom,"fixture");game.GetType().GetField("RomPath").SetValue(game,rom);
        Directory.CreateDirectory(Path.Combine(moved,"Pokemon 3DS - Arquivos","Azahar","user","config"));
        object[] args={moved,game,Path.Combine(moved,"Pokemon 3DS - Arquivos","Azahar","azahar.exe"),"\""+rom+"\"",null,null};
        Call(app.GetType("LauncherSettings"),"PrepareGame",args);
        Assert((string)args[2]==Path.Combine(moved,"PokemonPlayRuntime","Emulators","Azahar","azahar.exe"),"3DS selects bundled Azahar without copying an emulator manually");
        Call(app.GetType("GameSessionSettingsService"),"Cleanup",args[5]);
    }
}
