using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;

internal static class PokemonsPlayStub
{
	private const int MaxBundleEntries = 4096;
	private const int MaxBundlePathBytes = 32768;
	private const long MaxBundleBytes = 5L * 1024 * 1024 * 1024;

	private sealed class LoadingForm : Form
	{
		private readonly Label status;

		private readonly ProgressBar progress;

		public LoadingForm()
		{
			Text = "Pokemons Play";
			Width = 520;
			Height = 190;
			MinimumSize = new Size(520, 190);
			MaximumSize = new Size(520, 190);
			StartPosition = FormStartPosition.CenterScreen;
			FormBorderStyle = FormBorderStyle.FixedSingle;
			ControlBox = false;
			BackColor = Color.FromArgb(15, 18, 30);
			ForeColor = Color.White;
			ShowInTaskbar = true;
			try
			{
				Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
			}
			catch
			{
			}
			Label value = new Label
			{
				Text = "POKEMONS PLAY",
				Font = new Font("Segoe UI", 17f, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(24, 18),
				AutoSize = true
			};
			status = new Label
			{
				Text = "Preparando...",
				Font = new Font("Segoe UI", 9f),
				ForeColor = Color.FromArgb(190, 198, 220),
				Location = new Point(26, 56),
				Width = 450,
				Height = 22,
				AutoEllipsis = true
			};
			progress = new ProgressBar
			{
				Location = new Point(26, 92),
				Width = 450,
				Height = 24,
				Minimum = 0,
				Maximum = 1,
				Style = ProgressBarStyle.Continuous
			};
			Label value2 = new Label
			{
				Text = "A primeira inicializacao pode demorar. Nao feche esta janela.",
				Font = new Font("Segoe UI", 8f),
				ForeColor = Color.FromArgb(145, 160, 185),
				Location = new Point(26, 128),
				AutoSize = true
			};
			Controls.Add(value);
			Controls.Add(status);
			Controls.Add(progress);
			Controls.Add(value2);
		}

		public void SetMessage(string message)
		{
			status.Text = message;
			Application.DoEvents();
		}

		public void SetProgress(int current, int total, string name)
		{
			progress.Maximum = Math.Max(1, total);
			progress.Value = Math.Min(progress.Maximum, current);
			status.Text = "Extraindo " + current + "/" + total + ": " + Path.GetFileName(name);
			Application.DoEvents();
		}
	}

	private static readonly byte[] Marker = Encoding.ASCII.GetBytes("POKEMONS_PLAY_DATA_20261001_V113");

	public static void Main()
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pokemons Play");
		string text2 = Path.Combine(text, ".complete-v113");
		LoadingForm loading = null;
		try
		{
			string launchDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			bool installedEntry = string.Equals(launchDirectory, text, StringComparison.OrdinalIgnoreCase)
				&& File.Exists(Path.Combine(text, "PokemonPlayRuntime", "Pokemons Play.exe"));
			string runtimeExecutable = Path.Combine(text, "PokemonPlayRuntime", "Pokemons Play.exe");
			if (!installedEntry && (!File.Exists(text2) || !File.Exists(runtimeExecutable)))
			{
				loading = new LoadingForm();
				loading.Show();
				loading.SetMessage("Aguardando a preparação do Pokemons Play...");
				EnsureExtracted(text, text2, (int current, int total, string name) =>
				{
					loading.SetProgress(current, total, name);
				});
			}
			string fileName = runtimeExecutable;
			if (loading != null)
			{
				loading.SetMessage("Abrindo o launcher...");
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = fileName;
			processStartInfo.WorkingDirectory = text;
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Não foi possível preparar o Pokemons Play:\n\n" + ex.Message, "Pokemons Play", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		finally
		{
			if (loading != null)
			{
				loading.Close();
				loading.Dispose();
			}
		}
	}

	private static void EnsureExtracted(string install, string complete, Action<int, int, string> report)
	{
		using (System.Threading.Mutex mutex = new System.Threading.Mutex(false, "Local\\PokemonsPlayBootstrap"))
		{
			bool ownsMutex = false;
			try
			{
				try
				{
					ownsMutex = mutex.WaitOne();
				}
				catch (System.Threading.AbandonedMutexException)
				{
					ownsMutex = true;
				}
				if (!ownsMutex)
					throw new InvalidOperationException("Não foi possível aguardar a preparação do aplicativo.");

				string runtimeExecutable = Path.Combine(install, "PokemonPlayRuntime", "Pokemons Play.exe");
				if (File.Exists(complete) && File.Exists(runtimeExecutable))
					return;

				Extract(install, complete, report);
			}
			finally
			{
				if (ownsMutex)
					mutex.ReleaseMutex();
			}
		}
	}

	private static void Extract(string install, string complete, Action<int, int, string> report)
	{
		string fileName = Process.GetCurrentProcess().MainModule.FileName;
		string text = install + ".extracting-" + Guid.NewGuid().ToString("N");
		try
		{
			Directory.CreateDirectory(text);
			using (FileStream fileStream = File.OpenRead(fileName))
			{
				long num = FindMarker(fileStream);
				fileStream.Position = num + Marker.Length;
				using DeflateStream input = new DeflateStream(fileStream, CompressionMode.Decompress, leaveOpen: true);
				using BinaryReader binaryReader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
				int num2 = binaryReader.ReadInt32();
				if (num2 <= 0 || num2 > MaxBundleEntries)
					throw new InvalidDataException("Quantidade invalida de arquivos no pacote.");
				long totalBytes = 0;
				HashSet<string> extractedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < num2; i++)
				{
					int count = binaryReader.ReadInt32();
					if (count <= 0 || count > MaxBundlePathBytes)
						throw new InvalidDataException("Caminho invalido no pacote.");
					byte[] pathBytes = binaryReader.ReadBytes(count);
					if (pathBytes.Length != count)
						throw new EndOfStreamException();
					string text2 = Encoding.UTF8.GetString(pathBytes);
					long length = binaryReader.ReadInt64();
					if (length < 0 || length > MaxBundleBytes - totalBytes)
						throw new InvalidDataException("Tamanho invalido no pacote.");
					totalBytes += length;
					report?.Invoke(i + 1, num2, text2);
					string fullPath = Path.GetFullPath(Path.Combine(text, text2));
					if (!fullPath.StartsWith(text + "\\", StringComparison.OrdinalIgnoreCase) || !extractedPaths.Add(fullPath))
						throw new InvalidDataException("Caminho invalido no pacote.");
					Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
					using FileStream output = File.Create(fullPath);
					CopyBytes(input, output, length);
				}
			}
			string text3 = Path.Combine(install, "Saves");
			string destination = Path.Combine(text, "Saves");
			if (Directory.Exists(text3))
			{
				CopyDirectory(text3, destination);
			}
			string text4 = Path.Combine(install, "Settings");
			string destination2 = Path.Combine(text, "Settings");
			if (Directory.Exists(text4))
			{
				CopyDirectory(text4, destination2);
			}
			string text5 = Path.Combine(install, "Cloud");
			if (Directory.Exists(text5))
			{
				CopyDirectory(text5, Path.Combine(text, "Cloud"));
			}
            string bank = Path.Combine(install, "Pokemon Bank");
            if (Directory.Exists(bank)) CopyDirectory(bank, Path.Combine(text, "Pokemon Bank"));
			string text6 = install + ".previous-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N");
			if (Directory.Exists(install))
			{
				Directory.Move(install, text6);
			}
			try
			{
				Directory.Move(text, install);
			}
			catch
			{
				if (Directory.Exists(text6) && !Directory.Exists(install))
				{
					Directory.Move(text6, install);
				}
				throw;
			}
			File.WriteAllText(complete, DateTime.Now.ToString("O"));
		}
		finally
		{
			if (Directory.Exists(text))
			{
				try
				{
					ClearReadOnly(text);
					Directory.Delete(text, recursive: true);
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
		}
	}

	private static void ClearReadOnly(string directory)
	{
		string[] files = Directory.GetFiles(directory);
		foreach (string path in files)
		{
			FileAttributes attributes = File.GetAttributes(path);
			File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
		}
		files = Directory.GetDirectories(directory);
		foreach (string directory2 in files)
		{
			ClearReadOnly(directory2);
		}
	}

	private static void CopyDirectory(string source, string destination)
	{
		Directory.CreateDirectory(destination);
		string[] files = Directory.GetFiles(source);
		foreach (string text in files)
		{
			File.Copy(text, Path.Combine(destination, Path.GetFileName(text)), overwrite: true);
		}
		files = Directory.GetDirectories(source);
		foreach (string text2 in files)
		{
			CopyDirectory(text2, Path.Combine(destination, Path.GetFileName(text2)));
		}
	}

	private static long FindMarker(FileStream input)
	{
		int num = 0;
		int num2;
		while ((num2 = input.ReadByte()) != -1)
		{
			if (num2 == Marker[num])
			{
				num++;
				if (num == Marker.Length)
				{
					return input.Position - Marker.Length;
				}
			}
			else
			{
				num = ((num2 == Marker[0]) ? 1 : 0);
			}
		}
		throw new InvalidDataException("Pacote interno não encontrado.");
	}

	private static void CopyBytes(Stream input, Stream output, long length)
	{
		byte[] array = new byte[1048576];
		while (length > 0)
		{
			int num = input.Read(array, 0, (int)Math.Min(array.Length, length));
			if (num == 0)
			{
				throw new EndOfStreamException();
			}
			output.Write(array, 0, num);
			length -= num;
		}
	}
}
