using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class UpdatePreparationForm
{
    internal static void Prepare(string runtime,string root,string stage)
    {
        // The previous helper waits only 30 seconds. A visible bootstrap window
        // acknowledges startup; the new helper keeps the backup until full readiness.
        string supervisor=Path.Combine(stage,"PokemonPlayPreparationUpdater.exe");
        File.Copy(Path.Combine(runtime,"PokemonPlayUpdater.exe"),supervisor,false);
        var start=new ProcessStartInfo(supervisor){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};
        start.ArgumentList.Add("--verify-preparation");start.ArgumentList.Add(Environment.ProcessId.ToString());start.ArgumentList.Add(root);start.ArgumentList.Add(stage);
        using var process=Process.Start(start)??throw new IOException("Não foi possível supervisionar a preparação da atualização.");
        using var window=new Form {Text="Preparando Pokémon Play",ClientSize=new Size(460,130),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,ControlBox=false};
        window.Controls.Add(new Label{Text="Preparando os componentes da nova versão.\nAguarde; seu progresso está preservado.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter});
        window.Controls.Add(new ProgressBar{Style=ProgressBarStyle.Marquee,Dock=DockStyle.Bottom,Height=18});
        Exception failure=null;
        bool finished=false;
        window.FormClosing+=(_,eventArgs)=>eventArgs.Cancel=!finished;
        window.Shown+=async(_,_)=> {
            try {
                File.WriteAllText(Path.Combine(stage,"ready"),AppRelease.Tag);
                await Task.Run(()=>BundledEmulatorArchive.EnsureExtracted(runtime));
            }catch(Exception error){failure=error;}
            finally{finished=true;window.Close();}
        };
        Application.Run(window);
        if(failure!=null)throw new IOException("A preparação da atualização falhou. A versão anterior será restaurada.",failure);
    }
}
