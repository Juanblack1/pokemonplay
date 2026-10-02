using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        string root=null,stage=null;
        try
        {
            if(args.Length!=3||!int.TryParse(args[0],out int parent)||parent<=0)throw new ArgumentException("Parâmetros de atualização inválidos.");
            root=Path.GetFullPath(args[1]);stage=Path.GetFullPath(args[2]);Validate(root,stage);
            using(var process=FindProcess(parent)){if(process!=null&&!process.WaitForExit(120000))throw new IOException("O aplicativo ainda está aberto. Nenhum arquivo foi substituído.");}
            for(int attempt=0;RuntimeInUse(root)&&attempt<60;attempt++)Thread.Sleep(500);
            if(RuntimeInUse(root))throw new IOException("Outra janela do aplicativo está usando o runtime. Feche-a antes de atualizar.");
            Apply(root,stage,()=>
            {
                var start=new ProcessStartInfo(Path.Combine(root,"PokemonPlayRuntime","Pokemons Play.exe")){WorkingDirectory=root,UseShellExecute=false};
                start.Arguments="--update-ready \""+stage+"\"";
                using(var app=Process.Start(start))
                {
                    if(app==null)throw new IOException("Não foi possível reabrir o aplicativo.");
                    for(int attempt=0;attempt<60;attempt++)
                    {
                        if(File.Exists(Path.Combine(stage,"ready")))return;
                        if(app.HasExited)throw new IOException("A versão nova não conseguiu abrir.");
                        Thread.Sleep(500);
                    }
                    app.Kill();app.WaitForExit(10000);throw new IOException("A versão nova não confirmou a abertura. Restaurando a anterior.");
                }
            });
            File.WriteAllText(Path.Combine(stage,"success"),"ok");
        }
        catch(Exception e)
        {
            if(stage!=null)try{ValidateStageParent(root,stage);File.WriteAllText(Path.Combine(stage,"failed"),"failed");}catch{}
            MessageBox.Show("A atualização não foi concluída. A versão anterior foi preservada.\n\n"+e.Message,"Atualização do Pokemons Play",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            if(root!=null&&!RuntimeInUse(root))
            {
                string exe=Path.Combine(root,"PokemonPlayRuntime","Pokemons Play.exe");
                if(File.Exists(exe))try{Process.Start(new ProcessStartInfo(exe){WorkingDirectory=root,UseShellExecute=false});}catch{}
            }
        }
    }
    private static Process FindProcess(int id){try{return Process.GetProcessById(id);}catch(ArgumentException){return null;}}
    internal static void Validate(string root,string stage)
    {
        ValidateStageParent(root,stage);
        foreach(string directory in new[]{root,stage,Path.Combine(stage,"PokemonPlayRuntime"),Path.Combine(root,"PokemonPlayRuntime")})if(!Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Pasta de atualização ausente ou redirecionada.");
        if(!File.Exists(Path.Combine(stage,"PokemonPlayRuntime","Pokemons Play.exe")))throw new InvalidDataException("Runtime de atualização ausente.");
    }
    private static void ValidateStageParent(string root,string stage)
    {
        string parent=Path.GetDirectoryName(stage.TrimEnd(Path.DirectorySeparatorChar));
        string name=Path.GetFileName(stage);Guid id;
        if(!string.Equals(parent,root.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||!name.StartsWith(".pokemonplay-update-",StringComparison.Ordinal)||!Guid.TryParseExact(name.Substring(20),"N",out id))throw new InvalidDataException("Pasta de atualização inválida.");
    }
    internal static void Apply(string root,string stage,Action start)
    {
        Validate(root,stage);string runtime=Path.Combine(root,"PokemonPlayRuntime");string next=Path.Combine(stage,"PokemonPlayRuntime");string backup=Path.Combine(stage,"previous-runtime");
        if(Directory.Exists(backup))throw new IOException("Esta atualização já foi aplicada.");
        Directory.Move(runtime,backup);
        try{Directory.Move(next,runtime);start();}
        catch
        {
            if(Directory.Exists(runtime))Directory.Move(runtime,Path.Combine(stage,"failed-runtime"));
            Directory.Move(backup,runtime);throw;
        }
    }
    private static bool RuntimeInUse(string root)
    {
        string prefix=Path.Combine(root,"PokemonPlayRuntime")+Path.DirectorySeparatorChar;
        foreach(var process in Process.GetProcesses())using(process)
        {
            try{if(process.MainModule.FileName.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return true;}catch{}
        }
        return false;
    }
}
