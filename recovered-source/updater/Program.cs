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
        if(args.Length==4&&args[0]=="--verify-preparation"){VerifyPreparation(args);return;}
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
    private static void VerifyPreparation(string[] args)
    {
        string root=null,stage=null;
        try {
            if(!int.TryParse(args[1],out int parent)||parent<=0)throw new ArgumentException("Processo de preparação inválido.");
            root=Path.GetFullPath(args[2]);stage=Path.GetFullPath(args[3]);ValidatePrepared(root,stage);
            using(var app=FindProcess(parent)) {
                if(app!=null&&!app.HasExited)try {
                    if(!string.Equals(app.MainModule.FileName,Path.Combine(root,"PokemonPlayRuntime","Pokemons Play.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Processo de preparação fora da instalação.");
                }catch(Exception)when(app.HasExited){}
                for(int attempt=0;attempt<1200;attempt++) {
                    if(File.Exists(Path.Combine(stage,"prepared"))){File.WriteAllText(Path.Combine(stage,"preparation-complete"),"ok");return;}
                    if(app==null||app.HasExited)break;
                    Thread.Sleep(500);
                }
                if(app!=null&&!app.HasExited){app.Kill();app.WaitForExit(10000);}
            }
            // If the old helper never acknowledged bootstrap, it owns rollback.
            for(int attempt=0;attempt<60&&!File.Exists(Path.Combine(stage,"success"));attempt++) {
                if(File.Exists(Path.Combine(stage,"failed")))return;
                Thread.Sleep(500);
            }
            if(!File.Exists(Path.Combine(stage,"success")))return;
            for(int attempt=0;RuntimeInUse(root)&&attempt<60;attempt++)Thread.Sleep(500);
            if(RuntimeInUse(root))throw new IOException("O runtime ainda está em uso; o backup foi mantido.");
            RestorePrepared(root,stage);
            File.WriteAllText(Path.Combine(stage,"failed"),"preparation failed; restored");
            var previous=new ProcessStartInfo(Path.Combine(root,"PokemonPlayRuntime","Pokemons Play.exe")){WorkingDirectory=root,UseShellExecute=false};
            Process.Start(previous);
            MessageBox.Show("A preparação da atualização não foi concluída. A versão anterior foi restaurada e seu progresso foi preservado.","Atualização do Pokemons Play",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }catch(Exception error){
            if(stage!=null)try{ValidateStageParent(root,stage);File.WriteAllText(Path.Combine(stage,"preparation-failed-details.txt"),error.ToString());}catch{}
            MessageBox.Show("Não foi possível concluir a preparação. O backup foi mantido.\n\n"+error.Message,"Atualização do Pokemons Play",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    private static void ValidatePrepared(string root,string stage)
    {
        ValidateStageParent(root,stage);
        foreach(string directory in new[]{root,stage,Path.Combine(root,"PokemonPlayRuntime"),Path.Combine(stage,"previous-runtime")})
            if(!Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Pasta de preparação ausente ou redirecionada.");
        if(!File.Exists(Path.Combine(stage,"previous-runtime","Pokemons Play.exe")))throw new InvalidDataException("Backup do aplicativo ausente.");
    }
    internal static void RestorePrepared(string root,string stage)
    {
        ValidatePrepared(root,stage);
        string runtime=Path.Combine(root,"PokemonPlayRuntime"),backup=Path.Combine(stage,"previous-runtime"),failed=Path.Combine(stage,"failed-preparation-runtime");
        if(Directory.Exists(failed))throw new IOException("A restauração já foi iniciada; nenhum arquivo foi substituído.");
        Directory.Move(runtime,failed);
        try{Directory.Move(backup,runtime);}
        catch{Directory.Move(failed,runtime);throw;}
        try{File.Delete(Path.Combine(stage,"success"));}catch(IOException){}catch(UnauthorizedAccessException){}
    }
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
