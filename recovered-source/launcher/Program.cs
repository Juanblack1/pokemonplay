using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string app = Path.Combine(root, "PokemonPlayRuntime", "Pokemons Play.exe");
        if (!File.Exists(app))
        {
            MessageBox.Show(
                "O aplicativo não foi encontrado. Extraia o ZIP portátil completo antes de iniciar o Pokémon Play.",
                "Pokémon Play",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app,
                WorkingDirectory = Path.GetDirectoryName(app),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Não foi possível iniciar o Pokémon Play.\n\n" + ex.Message,
                "Pokémon Play",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
