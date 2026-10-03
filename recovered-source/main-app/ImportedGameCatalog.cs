using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

internal sealed class ImportedPokemonGame
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string RomPath { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string BaseGame { get; set; } = string.Empty;
	public int Generation { get; set; }
	public bool IsHackRom { get; set; }
}

internal static class ImportedGameCatalog
{
	private const int MaximumEntries = 500;
	private const long MaximumSettingsBytes = 1024 * 1024;
	private static string SettingsPath(string root) => Path.Combine(root, "Settings", "ImportedPokemonGames.json");

	internal static List<GameInfo> Build(string root)
	{
		return Load(root).Where(entry => File.Exists(entry.RomPath)).Select(ToGameInfo).ToList();
	}

	internal static List<ImportedPokemonGame> Entries(string root) => Load(root);

	internal static GameInfo Relocate(string root, string id, string newPath)
	{
		List<ImportedPokemonGame> entries = Load(root);
		ImportedPokemonGame entry = entries.SingleOrDefault(candidate => candidate.Id == id)
			?? throw new InvalidDataException("O jogo não está mais no catálogo. Atualize a lista e tente novamente.");
		string fullPath = Path.GetFullPath(newPath);
		if (!File.Exists(fullPath)) throw new FileNotFoundException("Não encontrei a ROM selecionada.", fullPath);
		if (!ExtensionMatchesGeneration(fullPath, entry.Generation))
			throw new InvalidDataException("Escolha uma ROM do mesmo sistema que o jogo original.");
		if (!string.Equals(Path.GetFileName(fullPath), Path.GetFileName(entry.RomPath), StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException("Para preservar a associação com seus saves, escolha o arquivo com o mesmo nome: " + Path.GetFileName(entry.RomPath));
		if (entries.Any(candidate => candidate.Id != id && SamePath(candidate.RomPath, fullPath)))
			throw new InvalidDataException("Esta ROM já está associada a outro jogo da biblioteca.");
		ReadIdentity(fullPath); // Confirm access before changing persisted metadata.
		entry.RomPath = fullPath;
		Save(root, entries);
		return ToGameInfo(entry);
	}

	internal static int ImportFiles(IWin32Window owner, string root, IEnumerable<string> paths)
	{
		List<ImportedPokemonGame> entries = Load(root);
		int imported = 0;
		foreach (string path in paths.Where(IsSupportedRom).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			try
			{
				RomIdentity identity = ReadIdentity(path);
				ImportedPokemonGame existing = entries.FirstOrDefault(entry => SamePath(entry.RomPath, path));
				using var dialog = new RomImportDialog(path, identity, existing);
				if (dialog.ShowDialog(owner) != DialogResult.OK) break;
				ImportedPokemonGame entry = dialog.Result;
				if (existing != null) entries.Remove(existing);
				entries.Add(entry);
				imported++;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
			{
				MessageBox.Show(owner, "Não foi possível ler esta ROM. Verifique se o arquivo está acessível e tente novamente.\n\n" + ex.Message, "Adicionar jogo", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}
		if (imported > 0) Save(root, entries);
		return imported;
	}

	internal static bool IsSupportedRom(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return false;
		string extension = Path.GetExtension(path);
		return extension.Equals(".gba", StringComparison.OrdinalIgnoreCase) || extension.Equals(".nds", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".3ds", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cci", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".cxi", StringComparison.OrdinalIgnoreCase) || extension.Equals(".3dsx", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".zcci", StringComparison.OrdinalIgnoreCase);
	}

	internal static GameInfo ToGameInfo(ImportedPokemonGame entry)
	{
		bool gba = entry.Generation == 3;
		string system = gba ? "Game Boy Advance" : entry.Generation >= 6 ? "Nintendo 3DS" : "Nintendo DS";
		return new GameInfo
		{
			Title = entry.Title,
			Subtitle = entry.IsHackRom ? $"Hack de {entry.BaseGame} • {system}" : $"{entry.BaseGame} • {system} • Arquivo local",
			Cover = BaseCover(entry.BaseGame),
			Launcher = entry.Generation >= 6 ? "Pokemon 3DS - Arquivos\\Azahar\\azahar.exe" : string.Empty,
			Top = gba ? Color.FromArgb(68, 54, 130) : entry.Generation >= 6 ? Color.FromArgb(39, 105, 190) : Color.FromArgb(32, 76, 120),
			Bottom = entry.Generation >= 6 ? Color.FromArgb(14, 33, 72) : Color.FromArgb(14, 20, 45),
			Accent = AppTheme.BlueSoft,
			Generation = entry.Generation,
			Arguments = entry.Generation >= 6 ? "\"" + entry.RomPath.Replace("\"", "\\\"") + "\"" : null,
			EmulatorProcess = entry.Generation >= 6 ? "azahar" : null,
			SaveFolderName = "Imported - " + entry.Id,
			RomPath = entry.RomPath,
			IsImported = true,
			IsHackRom = entry.IsHackRom,
			BaseGame = entry.BaseGame
		};
	}

	private static string BaseCover(string title)
	{
		string file = title switch
		{
			"FireRed" => "FireRed.png", "LeafGreen" => "LeafGreen.png", "Emerald" => "Emerald.png",
			"Ruby" => "Ruby.png", "Sapphire" => "Sapphire.png", "Diamond" => "Diamond.png", "Pearl" => "Pearl.png",
			"HeartGold" => "HeartGold.png", "SoulSilver" => "SoulSilver.png", "Platinum" => "Platinum.png",
			"Black" => "Black.png", "White" => "White.png", "Black 2" => "Black2.png", "White 2" => "White2.png",
			"X" => "Pokemon X.png", "Y" => "Pokemon Y.png", "Omega Ruby" => "Pokemon Omega Ruby.png", "Alpha Sapphire" => "Pokemon Alpha Sapphire.png",
			"Sun" => "Pokemon Sun.png", "Moon" => "Pokemon Moon.png", "Ultra Sun" => "Pokemon Ultra Sun.png", "Ultra Moon" => "Pokemon Ultra Moon.png",
			_ => string.Empty
		};
		return string.IsNullOrEmpty(file) ? null : Path.Combine(title is "X" or "Y" or "Omega Ruby" or "Alpha Sapphire" or "Sun" or "Moon" or "Ultra Sun" or "Ultra Moon" ? "Pokemon 3DS - Capas" : "Pokemon - Capas", file);
	}

	private static List<ImportedPokemonGame> Load(string root)
	{
		string path = SettingsPath(root);
		if (!File.Exists(path)) return new List<ImportedPokemonGame>();
		var info = new FileInfo(path);
		if (info.Length <= 0 || info.Length > MaximumSettingsBytes) throw new InvalidDataException("O catálogo de ROMs locais está inválido.");
		var entries = JsonSerializer.Deserialize<List<ImportedPokemonGame>>(File.ReadAllText(path)) ?? new List<ImportedPokemonGame>();
		if (entries.Count > MaximumEntries || entries.Any(entry => entry == null || !Guid.TryParseExact(entry.Id, "N", out _) || !IsSupportedRom(entry.RomPath) || string.IsNullOrWhiteSpace(entry.Title) || entry.Title.Length > 80 || string.IsNullOrWhiteSpace(entry.BaseGame) || entry.Generation is < 3 or > 6 || !ExtensionMatchesGeneration(entry.RomPath, entry.Generation)) || entries.Select(entry => entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
			throw new InvalidDataException("O catálogo de ROMs locais contém dados inválidos. Os arquivos originais foram preservados.");
		return entries;
	}

	private static void Save(string root, List<ImportedPokemonGame> entries)
	{
		if (entries.Count > MaximumEntries) throw new InvalidDataException("O limite de 500 jogos adicionados foi atingido.");
		string path = SettingsPath(root);
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
			File.Move(temp, path, true);
		}
		finally { if (File.Exists(temp)) File.Delete(temp); }
	}

	private static bool SamePath(string left, string right)
	{
		try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
		catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) { return false; }
	}

	private static bool ExtensionMatchesGeneration(string path, int generation)
	{
		string extension = Path.GetExtension(path);
		return generation switch
		{
			3 => extension.Equals(".gba", StringComparison.OrdinalIgnoreCase),
			4 or 5 => extension.Equals(".nds", StringComparison.OrdinalIgnoreCase),
			6 => new[] { ".3ds", ".cci", ".cxi", ".3dsx", ".zcci" }.Contains(extension, StringComparer.OrdinalIgnoreCase),
			_ => false
		};
	}

	private static RomIdentity ReadIdentity(string path)
	{
		string extension = Path.GetExtension(path);
		using var stream = File.OpenRead(path);
		byte[] bytes = new byte[0xB0];
		int read = stream.Read(bytes, 0, bytes.Length);
		string title = extension.Equals(".gba", StringComparison.OrdinalIgnoreCase) && read >= 0xAC
			? Decode(bytes, 0xA0, 12)
			: extension.Equals(".nds", StringComparison.OrdinalIgnoreCase) && read >= 12
				? Decode(bytes, 0, 12)
				: string.Empty;
		string baseGame = InferBaseGame(title) ?? InferBaseGame(Path.GetFileNameWithoutExtension(path));
		bool threeDs = new[] { ".3ds", ".cci", ".cxi", ".3dsx", ".zcci" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
		int generation = threeDs ? 6 : baseGame is "FireRed" or "LeafGreen" or "Emerald" ? 3 : extension.Equals(".gba", StringComparison.OrdinalIgnoreCase) ? 3 : baseGame is "Black" or "White" or "Black 2" or "White 2" ? 5 : 4;
		bool recognized = title.Contains("POKEMON", StringComparison.OrdinalIgnoreCase) || title.Contains("POKÉMON", StringComparison.OrdinalIgnoreCase);
		if (string.IsNullOrEmpty(baseGame)) baseGame = generation == 3 ? "FireRed" : generation == 5 ? "Black" : generation == 6 ? "X" : "Platinum";
		return new RomIdentity(title, baseGame, generation, recognized);
	}

	private static string Decode(byte[] bytes, int offset, int count)
	{
		int length = 0;
		while (length < count && bytes[offset + length] != 0) length++;
		return Encoding.ASCII.GetString(bytes, offset, length).Trim().Replace('_', ' ');
	}

	private static string InferBaseGame(string title)
	{
		string value = title.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
		if (value.Contains("OMEGARUBY")) return "Omega Ruby";
		if (value.Contains("ALPHASAPPHIRE")) return "Alpha Sapphire";
		if (value.Contains("RUBY")) return "Ruby";
		if (value.Contains("SAPPHIRE")) return "Sapphire";
		if (value.Contains("DIAMOND")) return "Diamond";
		if (value.Contains("PEARL")) return "Pearl";
		if (value.Contains("FIRERED")) return "FireRed";
		if (value.Contains("LEAFGREEN")) return "LeafGreen";
		if (value.Contains("EMERALD")) return "Emerald";
		if (value.Contains("HEARTGOLD")) return "HeartGold";
		if (value.Contains("SOULSILVER")) return "SoulSilver";
		if (value.Contains("PLATINUM")) return "Platinum";
		if (value.Contains("BLACK2")) return "Black 2";
		if (value.Contains("WHITE2")) return "White 2";
		if (value.Contains("BLACK")) return "Black";
		if (value.Contains("WHITE")) return "White";
		return null;
	}
}

internal sealed record RomIdentity(string HeaderTitle, string BaseGame, int Generation, bool RecognizedPokemon);
