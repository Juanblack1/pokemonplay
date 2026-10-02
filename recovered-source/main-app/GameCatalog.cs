using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

internal static class GameCatalog
{
	public static List<GameInfo> Build(string root)
	{
		List<GameInfo> list = new List<GameInfo>();
		list.Add(new GameInfo
		{
			Title = "FireRed",
			Subtitle = "Kanto • Game Boy Advance",
			Cover = "Pokemon - Capas\\FireRed.png",
			Launcher = "Pokemon - Executaveis\\Pokemon FireRed\\Pokemon FireRed.exe",
			Top = Color.FromArgb(190, 60, 35),
			Bottom = Color.FromArgb(75, 12, 18),
			Accent = Color.FromArgb(255, 170, 120),
			Generation = 3,
			SaveFolderName = "Pokemon FireRed"
		});
		list.Add(new GameInfo
		{
			Title = "LeafGreen",
			Subtitle = "Kanto • Game Boy Advance",
			Cover = "Pokemon - Capas\\LeafGreen.png",
			Launcher = "Pokemon - Executaveis\\Pokemon LeafGreen\\Pokemon LeafGreen.exe",
			Top = Color.FromArgb(70, 170, 75),
			Bottom = Color.FromArgb(20, 75, 35),
			Accent = Color.FromArgb(180, 255, 150),
			Generation = 3,
			SaveFolderName = "Pokemon LeafGreen"
		});
		list.Add(new GameInfo
		{
			Title = "Emerald",
			Subtitle = "Hoenn • Game Boy Advance",
			Cover = "Pokemon - Capas\\Emerald.png",
			Launcher = "Pokemon - Executaveis\\Pokemon Emerald\\Pokemon Emerald.exe",
			Top = Color.FromArgb(30, 150, 95),
			Bottom = Color.FromArgb(8, 55, 50),
			Accent = Color.FromArgb(130, 255, 190),
			Generation = 3,
			SaveFolderName = "Pokemon Emerald"
		});
		list.Add(new GameInfo
		{
			Title = "HeartGold",
			Subtitle = "Johto • Nintendo DS",
			Cover = "Pokemon - Capas\\HeartGold.png",
			Launcher = "Pokemon - Executaveis\\Pokemon HeartGold\\Pokemon HeartGold.exe",
			Top = Color.FromArgb(205, 145, 30),
			Bottom = Color.FromArgb(95, 45, 5),
			Accent = Color.FromArgb(255, 224, 100),
			Generation = 4,
			SaveFolderName = "Pokemon HeartGold"
		});
		list.Add(new GameInfo
		{
			Title = "SoulSilver",
			Subtitle = "Johto • Nintendo DS",
			Cover = "Pokemon - Capas\\SoulSilver.png",
			Launcher = "Pokemon - Executaveis\\Pokemon SoulSilver\\Pokemon SoulSilver.exe",
			Top = Color.FromArgb(120, 145, 170),
			Bottom = Color.FromArgb(38, 58, 85),
			Accent = Color.FromArgb(210, 235, 255),
			Generation = 4,
			SaveFolderName = "Pokemon SoulSilver"
		});
		list.Add(new GameInfo
		{
			Title = "Platinum",
			Subtitle = "Sinnoh • Nintendo DS",
			Cover = "Pokemon - Capas\\Platinum.png",
			Launcher = "Pokemon - Executaveis\\Pokemon Platinum\\Pokemon Platinum.exe",
			Top = Color.FromArgb(125, 135, 170),
			Bottom = Color.FromArgb(42, 48, 75),
			Accent = Color.FromArgb(225, 230, 255),
			Generation = 4,
			SaveFolderName = "Pokemon Platinum"
		});
		list.Add(new GameInfo
		{
			Title = "Black",
			Subtitle = "Unova • Nintendo DS",
			Cover = "Pokemon - Capas\\Black.png",
			Launcher = "Pokemon - Executaveis\\Pokemon Black\\Pokemon Black.exe",
			Top = Color.FromArgb(25, 28, 38),
			Bottom = Color.FromArgb(5, 5, 8),
			Accent = Color.FromArgb(220, 230, 240),
			Generation = 5,
			SaveFolderName = "Pokemon Black"
		});
		list.Add(new GameInfo
		{
			Title = "White",
			Subtitle = "Unova • Nintendo DS",
			Cover = "Pokemon - Capas\\White.png",
			Launcher = "Pokemon - Executaveis\\Pokemon White\\Pokemon White.exe",
			Top = Color.FromArgb(110, 140, 170),
			Bottom = Color.FromArgb(42, 60, 78),
			Accent = Color.White,
			Generation = 5,
			SaveFolderName = "Pokemon White"
		});
		list.Add(new GameInfo
		{
			Title = "Black 2",
			Subtitle = "Unova • Nintendo DS",
			Cover = "Pokemon - Capas\\Black2.png",
			Launcher = "Pokemon - Executaveis\\Pokemon Black 2\\Pokemon Black 2.exe",
			Top = Color.FromArgb(35, 30, 48),
			Bottom = Color.FromArgb(8, 5, 15),
			Accent = Color.FromArgb(190, 130, 255),
			Generation = 5,
			SaveFolderName = "Pokemon Black 2"
		});
		list.Add(new GameInfo
		{
			Title = "White 2",
			Subtitle = "Unova • Nintendo DS",
			Cover = "Pokemon - Capas\\White2.png",
			Launcher = "Pokemon - Executaveis\\Pokemon White 2\\Pokemon White 2.exe",
			Top = Color.FromArgb(65, 130, 155),
			Bottom = Color.FromArgb(15, 50, 65),
			Accent = Color.FromArgb(180, 245, 255),
			Generation = 5,
			SaveFolderName = "Pokemon White 2"
		});
		string romsDir = Path.Combine(root, "Pokemon 3DS - Arquivos", "Roms");
		string coversDir = Path.Combine(root, "Pokemon 3DS - Capas");
		string azaharExe = Path.Combine(root, "Pokemon 3DS - Arquivos", "Azahar", "azahar.exe");
		list.AddRange(DiscoverThreeDsGames(romsDir, coversDir, azaharExe));
		return list;
	}

	public static List<GameInfo> SaveGames(string root)
	{
		List<GameInfo> list = new List<GameInfo>();
		foreach (GameInfo item in Build(root))
		{
			if (item.Generation < 6)
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static bool HasAzahar(string root)
	{
		return File.Exists(Path.Combine(root, "Pokemon 3DS - Arquivos", "Azahar", "azahar.exe"));
	}

	private static List<GameInfo> DiscoverThreeDsGames(string romsDir, string coversDir, string azaharExe)
	{
		List<GameInfo> list = new List<GameInfo>();
		if (!Directory.Exists(romsDir) || !File.Exists(azaharExe))
		{
			return list;
		}
		string[] extensions = new string[5] { ".3ds", ".cci", ".cxi", ".3dsx", ".zcci" };
		string[] files = Directory.GetFiles(romsDir, "*.*", SearchOption.AllDirectories)
			.Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		var nameCounts = files.GroupBy(path => ThreeDsGameName(path), StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
		foreach (string text in files)
		{
			string gameName = ThreeDsGameName(text);
			string relativeRomPath = Path.ChangeExtension(Path.GetRelativePath(romsDir, text), null);
			if (nameCounts[gameName] > 1)
			{
				string relativeIdentity = Path.GetRelativePath(romsDir, text)
					.Replace(Path.DirectorySeparatorChar, '›').Replace(Path.AltDirectorySeparatorChar, '›');
				gameName += " · " + relativeIdentity;
			}
			list.Add(new GameInfo
			{
				Title = gameName.ToUpperInvariant(),
				Subtitle = "Nintendo 3DS • Azahar",
				Cover = FindCover(coversDir, relativeRomPath),
				Launcher = "Pokemon 3DS - Arquivos\\Azahar\\azahar.exe",
				EmulatorProcess = "azahar",
				Arguments = QuoteArgument(text),
				Top = Color.FromArgb(39, 105, 190),
				Bottom = Color.FromArgb(14, 33, 72),
				Accent = AppTheme.BlueSoft,
				Generation = 6,
				SaveFolderName = "Pokemon 3DS - " + gameName
			});
		}
		return list;
	}

	private static string ThreeDsGameName(string path)
	{
		string name = Path.GetFileNameWithoutExtension(path).Replace("_", " ").Replace("-", " ").Trim();
		return name.Length == 0 ? "JOGO 3DS" : name;
	}

	private static string FindCover(string coversDir, string relativeRomPath)
	{
		string mirroredCover = Path.Combine("Pokemon 3DS - Capas", Path.ChangeExtension(relativeRomPath, ".png"));
		string baseName = Path.GetFileNameWithoutExtension(relativeRomPath);
		string directory = Path.GetDirectoryName(relativeRomPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
			{
				string path = Path.Combine(coversDir, Path.ChangeExtension(relativeRomPath, extension));
				if (File.Exists(path))
					return Path.Combine("Pokemon 3DS - Capas", Path.GetRelativePath(coversDir, path));
			}
		}
		if (!Directory.Exists(coversDir)) return mirroredCover;
		foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
		{
			string path = Path.Combine(coversDir, baseName + extension);
			if (File.Exists(path))
				return Path.Combine("Pokemon 3DS - Capas", Path.GetFileName(path));
		}
		return mirroredCover;
	}

	private static string QuoteArgument(string value)
	{
		return "\"" + value.Replace("\"", "\\\"") + "\"";
	}
}
