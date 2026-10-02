using System;
using System.Collections.Generic;
using System.IO;

internal static class LauncherSettings
{
    private static readonly Dictionary<string,string> roms = new Dictionary<string,string> {
            {"FireRed","1636 - Pokemon Fire Red (U)(Squirrels).gba"}, {"LeafGreen","Pokemon - Leaf Green Version (U) (V1.1).gba"}, {"Emerald","Pokemon - Emerald Version (USA, Europe).gba"},
            {"HeartGold","Pokemon - HeartGold Version (USA).nds"}, {"SoulSilver","Pokemon - SoulSilver Version (USA).nds"}, {"Platinum","Pokemon - Platinum Version (USA) (Rev 1).nds"},
            {"Black","5216 - Pokemon - Black (DSi Enhanced) (J).nds"}, {"White","Pokemon - White Version (USA, Europe) (NDSi Enhanced).nds"}, {"Black 2","Pokemon - Black Version 2 (USA, Europe) (NDSi Enhanced).nds"}, {"White 2","Pokemon - White Version 2 (USA, Europe) (NDSi Enhanced).nds"}
        };
    private static string SaveBaseName(GameInfo game) => game.IsImported
        ? Path.GetFileNameWithoutExtension(game.RomPath)
        : roms.TryGetValue(game.Title, out string rom) ? Path.GetFileNameWithoutExtension(rom) : throw new InvalidOperationException("Jogo sem configuração de save.");
    public static string SaveFileName(GameInfo game) => SaveBaseName(game) + ".sav";
    internal static string RetroArchSaveFileName(GameInfo game) => SaveBaseName(game) + ".srm";
    internal static string SaveFileName(string root, GameInfo game) => SaveBaseName(game) + (RetroArchSettingsService.UsesRetroArch(root, game) ? ".srm" : ".sav");
    public static void PrepareGame(string root, GameInfo game, ref string executable, ref string arguments)
        => PrepareGame(root, game, ref executable, ref arguments, out _, out _);

    internal static void PrepareGame(string root, GameInfo game, ref string executable, ref string arguments, out string processName, out string temporaryConfigPath)
    {
        processName = !string.IsNullOrWhiteSpace(game.EmulatorProcess) ? game.EmulatorProcess : game.Generation == 3 ? "visualboyadvance-m" : game.Generation >= 6 ? "azahar" : "melonDS";
        temporaryConfigPath = null;
        if(game.Generation>=6)
        {
            if(game.IsImported && !File.Exists(game.RomPath))
                throw new FileNotFoundException("Não encontrei a ROM local selecionada. Confira se a unidade ou pasta ainda está disponível.",game.RomPath);
            if(!File.Exists(executable))
                throw new FileNotFoundException("Não encontrei o Azahar. Coloque o emulador em Pokemon 3DS - Arquivos\\Azahar e tente novamente.",executable);
            ApplyAzahar(root);
            temporaryConfigPath=AzaharSessionSettingsService.Begin(AzaharConfigPath(root));
            return;
        }
        if(!game.IsImported && !roms.TryGetValue(game.Title,out _))throw new InvalidOperationException("Jogo sem configuração de entrada.");
        string dir=Path.Combine(root,game.Generation==3?"Pokemon - Arquivos":"Pokemon DS - Arquivos");
        string romPath=game.IsImported?Path.GetFullPath(game.RomPath):Path.Combine(dir,roms[game.Title]);
        if(!File.Exists(romPath))throw new FileNotFoundException(game.IsImported?"Não encontrei a ROM local selecionada. Confira se a unidade ou pasta ainda está disponível.":"Não encontrei a ROM deste jogo na instalação.",romPath);
        RetroArchLaunchPlan retroArch=RetroArchSettingsService.CreateLaunch(root,game,romPath);
        if(retroArch!=null){executable=retroArch.ExecutablePath;arguments=retroArch.Arguments;processName="retroarch";temporaryConfigPath=retroArch.TemporaryConfigPath;return;}
        executable=Path.Combine(dir,game.Generation==3?"visualboyadvance-m.exe":"melonDS.exe");
        if(!File.Exists(executable))throw new FileNotFoundException("Não encontrei o executável do emulador instalado.",executable);
        if(game.Generation==3){string[] configPaths=VbaSessionSettingsService.ConfigPaths(root);ApplyVba(root,game.SaveFolderName);temporaryConfigPath=VbaSessionSettingsService.Begin(configPaths);}else{ApplyMelon(root,game.SaveFolderName,romPath);temporaryConfigPath=MelonDsSessionSettingsService.Begin(Path.Combine(dir,"melonDS.toml"));}
        arguments="\""+romPath+"\"";
    }
    private static void ApplyAzahar(string root)
    {
        string path=AzaharConfigPath(root);
        var lines=File.Exists(path)?new List<string>(File.ReadAllLines(path)):new List<string>();
        var keys=InputDeviceProfile.KeyboardKeys(ReadPreset(root),ReadCustomKeys(root));
        var profile=InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json"));
        string[] names={"button_up","button_down","button_left","button_right","button_a","button_b","button_l","button_r","button_start","button_select"};
        int count=0, own=0;
        foreach(string line in lines){int eq=line.IndexOf('=');if(eq<0)continue;string key=line.Substring(0,eq).Trim(),value=line.Substring(eq+1).Trim();if(key=="profiles\\size")int.TryParse(value,out count);if(key.StartsWith("profiles\\")&&key.EndsWith("\\name")&&value=="Pokemons Play"){var parts=key.Split('\\');if(parts.Length==3)int.TryParse(parts[1],out own);}}
        if(own==0)own=++count;
        string prefix="profiles\\"+own+"\\";
        var values=new Dictionary<string,string>{{"profile",(own-1).ToString()},{"profile\\default","false"},{"profiles\\size",count.ToString()},{prefix+"name","Pokemons Play"}};
        for(int i=0;i<10;i++){values[prefix+names[i]]="\"engine:keyboard,code:"+MelonKey(keys[i])+"\"";values[prefix+names[i]+"\\default"]="false";}
        for(int i=0;i<2;i++){string name=i==0?"button_x":"button_y";values[prefix+name]="\"engine:keyboard,code:"+MelonKey(profile.ExtraKeys[i])+"\"";values[prefix+name+"\\default"]="false";}
        values[prefix+"circle_pad"]="\"engine:analog_from_button,up:engine$0keyboard$1code$0"+MelonKey(keys[0])+",down:engine$0keyboard$1code$0"+MelonKey(keys[1])+",left:engine$0keyboard$1code$0"+MelonKey(keys[2])+",right:engine$0keyboard$1code$0"+MelonKey(keys[3])+",modifier:engine$0keyboard$1code$016777248,modifier_scale:0.5\"";
        values[prefix+"circle_pad\\default"]="false";
        SetIniSection(lines,"Controls",values);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllLines(path,lines);
    }

    internal static string AzaharConfigPath(string root)
    {
        string portable=Path.Combine(root,"Pokemon 3DS - Arquivos","Azahar","user","config","qt-config.ini");
        return Directory.Exists(Path.GetDirectoryName(portable))?portable:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"azahar","config","qt-config.ini");
    }
	private static readonly string[] PresetNames = new string[6] { "Clássico", "WASD", "Numpad", "ESDF", "Controle", "Personalizado" };

	public static void ApplyVba(string root, string gameName)
	{
		try
		{
			int preset = ReadPreset(root);
			ReadAudioSettings(root, out var volume, out var muted, out var _, out var interpolation);
			Dictionary<string, string> dictionary = VbaKeys(preset, ReadCustomKeys(root));
			string text = SaveProfileService.ActiveFolder(root, gameName);
			Directory.CreateDirectory(text);
			dictionary["BatteryDir"] = text;
			string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "visualboyadvance-m");
			Directory.CreateDirectory(text2);
			string path = Path.Combine(text2, "vbam.ini");
			List<string> list = (File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>());
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>(dictionary);
			dictionary2.Remove("BatteryDir");
			SetIniSection(list, "Joypad/1", dictionary2);
			SetIniSection(list, "General", new Dictionary<string, string> { { "BatteryDir", text } });
			SetIniSection(list, "Sound", new Dictionary<string, string>
			{
				{
					"Volume",
					((!muted) ? volume : 0).ToString()
				},
				{
					"GBAInterpolation",
					interpolation ? "1" : "0"
				}
			});
			File.WriteAllLines(path, list.ToArray());
            string portable=Path.Combine(root,"Pokemon - Arquivos","pt_BR","vbam.ini");
            var local=File.Exists(portable)?new List<string>(File.ReadAllLines(portable)):new List<string>(list);
            SetIniSection(local,"Joypad/1",dictionary2);SetIniSection(local,"General",new Dictionary<string,string>{{"BatteryDir",text}});
            SetIniSection(local,"Sound",new Dictionary<string,string>{{"Volume",((!muted)?volume:0).ToString()},{"GBAInterpolation",interpolation?"1":"0"}});
            Directory.CreateDirectory(Path.GetDirectoryName(portable));File.WriteAllLines(portable,local);
		}
		catch(Exception ex)
        { throw new IOException("Não foi possível aplicar os controles. Feche o emulador e tente novamente.",ex); }
	}

	public static void ApplyMelon(string root, string gameName, string romName)
	{
		try
		{
			int preset = ReadPreset(root);
			int num = ReadScreen(root);
			string path = Path.Combine(root, "Pokemon DS - Arquivos");
			string path2 = Path.Combine(path, "melonDS.toml");
			if (Directory.Exists(path))
			{
				string text = SaveProfileService.ActiveFolder(root, gameName);
				Directory.CreateDirectory(text);
				string rom = Path.Combine(path, romName);
				List<string> list = File.Exists(path2) ? new List<string>(File.ReadAllLines(path2)) : new List<string>();
				ReadAudioSettings(root, out var volume, out var muted, out var audioSync, out var interpolation);
				int num2 = (int)Math.Round((double)((!muted) ? volume : 0) * 256.0 / 100.0);
				SetTomlSection(list, "Instance0", new Dictionary<string, string> {
				{
					"SaveFilePath",
					TomlString(text)
				} });
                var keyboard=MelonKeys(preset,ReadCustomKeys(root));var profile=InputDeviceProfile.Load(Path.Combine(root,"Settings","input-device.json"));keyboard["X"]=MelonKey(profile.ExtraKeys[0]);keyboard["Y"]=MelonKey(profile.ExtraKeys[1]);
				SetTomlSection(list, "Instance0.Keyboard", keyboard);
                var joystick=new Dictionary<string,string>();foreach(string action in keyboard.Keys)joystick[action]="-1";SetTomlSection(list,"Instance0.Joystick",joystick);
				SetTomlSection(list, "Instance0.Audio", new Dictionary<string, string> {
				{
					"Volume",
					num2.ToString()
				} });
				SetTomlSection(list, "Instance0.Window0", new Dictionary<string, string> {
				{
					"ScreenLayout",
					num.ToString()
				} });
				SetTomlSection(list, "Audio", new Dictionary<string, string> {
				{
					"Interpolation",
					interpolation ? "1" : "0"
				} });
				SetTomlValue(list, "AudioSync", audioSync ? "true" : "false");
				ReplaceRecentRom(list, rom);
				File.WriteAllLines(path2, list.ToArray());
			}
		}
		catch(Exception ex)
        { throw new IOException("Não foi possível aplicar os controles. Feche o emulador e tente novamente.",ex); }
	}

	public static string PresetLabel(int index)
	{
		return (index >= 0 && index < PresetNames.Length) ? PresetNames[index] : PresetNames[1];
	}

	private static int ReadPreset(string root)
	{
		ReadSelections(root, out var preset, out var _);
		return preset;
	}

	private static int ReadScreen(string root)
	{
		ReadSelections(root, out var _, out var screen);
		return screen;
	}

	private static void ReadAudioSettings(string root, out int volume, out bool muted, out bool audioSync, out bool interpolation)
	{
		volume = 100;
		muted = false;
		audioSync = false;
		interpolation = true;
		string path = Path.Combine(root, "Settings", "input-presets.txt");
		if (File.Exists(path))
		{
			string[] array = File.ReadAllLines(path);
			if (array.Length > 2 && int.TryParse(array[2], out var result))
			{
				volume = Math.Max(0, Math.Min(100, result));
			}
			if (array.Length > 3)
			{
				muted = IsTrue(array[3]);
			}
			if (array.Length > 4)
			{
				audioSync = IsTrue(array[4]);
			}
			if (array.Length > 5)
			{
				interpolation = IsTrue(array[5]);
			}
		}
	}

	private static bool IsTrue(string value)
	{
		return string.Equals(value.Trim(), "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
	}

	private static void ReadSelections(string root, out int preset, out int screen)
	{
		preset = 1;
		screen = 0;
		string path = Path.Combine(root, "Settings", "input-presets.txt");
		if (File.Exists(path))
		{
			string[] array = File.ReadAllLines(path);
			if (array.Length > 0 && int.TryParse(array[0], out var result) && result >= 0 && result < 6)
			{
				preset = result;
			}
			if (array.Length > 1 && int.TryParse(array[1], out result) && result >= 0 && result < 5)
			{
				screen = result;
			}
		}
	}

	private static string[] ReadCustomKeys(string root)
	{
		string[] array = new string[10] { "W", "S", "A", "D", "Z", "X", "Q", "E", "Enter", "Backspace" };
		string path = Path.Combine(root, "Settings", "input-presets.txt");
		if (!File.Exists(path))
		{
			return array;
		}
		string[] array2 = File.ReadAllLines(path);
		for (int i = 0; i < array.Length && i + 6 < array2.Length; i++)
		{
			if (!string.IsNullOrWhiteSpace(array2[i + 6]))
			{
				array[i] = array2[i + 6].Trim();
			}
		}
		return array;
	}

	private static string VbaKey(string key)
	{
		if (string.Equals(key, "Up", StringComparison.OrdinalIgnoreCase))
		{
			return "UP";
		}
		if (string.Equals(key, "Down", StringComparison.OrdinalIgnoreCase))
		{
			return "DOWN";
		}
		if (string.Equals(key, "Left", StringComparison.OrdinalIgnoreCase))
		{
			return "LEFT";
		}
		if (string.Equals(key, "Right", StringComparison.OrdinalIgnoreCase))
		{
			return "RIGHT";
		}
		if (string.Equals(key, "Enter", StringComparison.OrdinalIgnoreCase))
		{
			return "ENTER";
		}
		if (string.Equals(key, "Backspace", StringComparison.OrdinalIgnoreCase))
		{
			return "BACK";
		}
		if (string.Equals(key, "Shift", StringComparison.OrdinalIgnoreCase))
		{
			return "SHIFT";
		}
		return key.ToUpperInvariant();
	}

	private static string MelonKey(string key)
	{
		if (string.Equals(key, "Up", StringComparison.OrdinalIgnoreCase))
		{
			return "16777235";
		}
		if (string.Equals(key, "Down", StringComparison.OrdinalIgnoreCase))
		{
			return "16777237";
		}
		if (string.Equals(key, "Left", StringComparison.OrdinalIgnoreCase))
		{
			return "16777234";
		}
		if (string.Equals(key, "Right", StringComparison.OrdinalIgnoreCase))
		{
			return "16777236";
		}
		if (string.Equals(key, "Enter", StringComparison.OrdinalIgnoreCase))
		{
			return "16777220";
		}
		if (string.Equals(key, "Backspace", StringComparison.OrdinalIgnoreCase))
		{
			return "16777219";
		}
		if (string.Equals(key, "Shift", StringComparison.OrdinalIgnoreCase))
		{
			return "16777248";
		}
        var parsed=InputReader.ParseKey(key);
        if(parsed>=System.Windows.Forms.Keys.F1&&parsed<=System.Windows.Forms.Keys.F24)return (16777264+(int)parsed-(int)System.Windows.Forms.Keys.F1).ToString();
        return parsed switch {
            System.Windows.Forms.Keys.ControlKey=>"16777249",System.Windows.Forms.Keys.Menu=>"16777251",System.Windows.Forms.Keys.Tab=>"16777217",System.Windows.Forms.Keys.Space=>"32",System.Windows.Forms.Keys.Delete=>"16777223",System.Windows.Forms.Keys.Insert=>"16777222",System.Windows.Forms.Keys.Home=>"16777232",System.Windows.Forms.Keys.End=>"16777233",System.Windows.Forms.Keys.PageUp=>"16777238",System.Windows.Forms.Keys.PageDown=>"16777239",_=>((int)char.ToUpperInvariant(key[0])).ToString()};
	}

	private static Dictionary<string, string> VbaKeys(int preset, string[] custom)
	{
		return preset switch
		{
			5 => Map(VbaKey(custom[0]), VbaKey(custom[1]), VbaKey(custom[2]), VbaKey(custom[3]), VbaKey(custom[4]), VbaKey(custom[5]), VbaKey(custom[6]), VbaKey(custom[7]), VbaKey(custom[8]), VbaKey(custom[9])),
			0 => Map("UP", "DOWN", "LEFT", "RIGHT", "Z", "X", "A", "S", "ENTER", "BACK"),
			2 => Map("8", "5", "4", "6", "1", "2", "7", "9", "ENTER", "0"),
			3 => Map("E", "D", "S", "F", "J", "K", "A", "G", "ENTER", "SHIFT"),
			4 => Map("W", "S", "A", "D", "J", "K", "Q", "E", "ENTER", "BACK"),
			_ => Map("W", "S", "A", "D", "Z", "X", "Q", "E", "ENTER", "BACK"),
		};
	}

	private static Dictionary<string, string> MelonKeys(int preset, string[] custom)
	{
		return preset switch
		{
			5 => Map(MelonKey(custom[0]), MelonKey(custom[1]), MelonKey(custom[2]), MelonKey(custom[3]), MelonKey(custom[4]), MelonKey(custom[5]), MelonKey(custom[6]), MelonKey(custom[7]), MelonKey(custom[8]), MelonKey(custom[9])),
			0 => Map("16777235", "16777237", "16777234", "16777236", "90", "88", "65", "83", "16777220", "16777219"),
			2 => Map("56", "53", "52", "54", "49", "50", "55", "57", "16777220", "48"),
			3 => Map("69", "68", "83", "70", "74", "75", "65", "71", "16777220", "16777248"),
			4 => Map("87", "83", "65", "68", "74", "75", "81", "69", "16777220", "16777219"),
			_ => Map("87", "83", "65", "68", "90", "88", "81", "69", "16777220", "16777219"),
		};
	}

	private static Dictionary<string, string> Map(string up, string down, string left, string right, string a, string b, string l, string r, string start, string select)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary.Add("Up", up);
		dictionary.Add("Down", down);
		dictionary.Add("Left", left);
		dictionary.Add("Right", right);
		dictionary.Add("A", a);
		dictionary.Add("B", b);
		dictionary.Add("L", l);
		dictionary.Add("R", r);
		dictionary.Add("Start", start);
		dictionary.Add("Select", select);
		return dictionary;
	}

	private static void SetIniSection(List<string> lines, string section, Dictionary<string, string> values)
	{
		string text = "[" + section + "]";
		int num = -1;
		int num2 = lines.Count;
		for (int i = 0; i < lines.Count; i++)
		{
			if (string.Equals(lines[i].Trim(), text, StringComparison.OrdinalIgnoreCase))
			{
				num = i;
				break;
			}
		}
		if (num >= 0)
		{
			for (int i = num + 1; i < lines.Count; i++)
			{
				if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal))
				{
					num2 = i;
					break;
				}
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = num + 1; i < num2; i++)
			{
				int num3 = lines[i].IndexOf('=');
				if (num3 > 0)
				{
					string text2 = lines[i].Substring(0, num3).Trim();
					if (values.TryGetValue(text2, out var value))
					{
						lines[i] = text2 + "=" + value;
						hashSet.Add(text2);
					}
				}
			}
			int num4 = num2;
			{
				foreach (KeyValuePair<string, string> value2 in values)
				{
					if (!hashSet.Contains(value2.Key))
					{
						lines.Insert(num4++, value2.Key + "=" + value2.Value);
					}
				}
				return;
			}
		}
		if (lines.Count > 0 && lines[lines.Count - 1].Length > 0)
		{
			lines.Add("");
		}
		lines.Add(text);
		foreach (KeyValuePair<string, string> value3 in values)
		{
			lines.Add(value3.Key + "=" + value3.Value);
		}
	}

	private static void SetTomlSection(List<string> lines, string section, Dictionary<string, string> values)
	{
		string text = "[" + section + "]";
		int num = -1;
		int num2 = lines.Count;
		for (int i = 0; i < lines.Count; i++)
		{
			if (string.Equals(lines[i].Trim(), text, StringComparison.OrdinalIgnoreCase))
			{
				num = i;
				break;
			}
		}
		if (num >= 0)
		{
			for (int i = num + 1; i < lines.Count; i++)
			{
				if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal))
				{
					num2 = i;
					break;
				}
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = num + 1; i < num2; i++)
			{
				int num3 = lines[i].IndexOf('=');
				if (num3 > 0)
				{
					string text2 = lines[i].Substring(0, num3).Trim();
					if (values.TryGetValue(text2, out var value))
					{
						lines[i] = text2 + " = " + value;
						hashSet.Add(text2);
					}
				}
			}
			int num4 = num2;
			{
				foreach (KeyValuePair<string, string> value2 in values)
				{
					if (!hashSet.Contains(value2.Key))
					{
						lines.Insert(num4++, value2.Key + " = " + value2.Value);
					}
				}
				return;
			}
		}
		if (lines.Count > 0 && lines[lines.Count - 1].Length > 0)
		{
			lines.Add("");
		}
		lines.Add(text);
		foreach (KeyValuePair<string, string> value3 in values)
		{
			lines.Add(value3.Key + " = " + value3.Value);
		}
	}

	private static void SetTomlValue(List<string> lines, string key, string value)
	{
		string value2 = key + "=";
		string value3 = key + " =";
		for (int i = 0; i < lines.Count; i++)
		{
			string text = lines[i].Trim();
			if (text.StartsWith(value2, StringComparison.Ordinal) || text.StartsWith(value3, StringComparison.Ordinal))
			{
				lines[i] = key + " = " + value;
				return;
			}
			if (text.StartsWith("[", StringComparison.Ordinal))
			{
				break;
			}
		}
		lines.Insert(0, key + " = " + value);
	}

	private static void ReplaceRecentRom(List<string> lines, string rom)
	{
		for (int i = 0; i < lines.Count; i++)
		{
			if (!lines[i].TrimStart().StartsWith("RecentROM", StringComparison.Ordinal))
			{
				continue;
			}
			int j = i + 1;
			if (lines[i].IndexOf(']') < 0)
			{
				for (; j < lines.Count && !lines[j].Trim().Equals("],", StringComparison.Ordinal) && !lines[j].Trim().Equals("]", StringComparison.Ordinal); j++)
				{
				}
				if (j < lines.Count)
				{
					j++;
				}
			}
			lines.RemoveRange(i, j - i);
			lines.Insert(i, "RecentROM = [ \"" + TomlPath(rom) + "\" ]");
			return;
		}
		lines.Insert(0, "RecentROM = [ \"" + TomlPath(rom) + "\" ]");
	}

	private static string TomlString(string value)
	{
		return "\"" + TomlPath(value) + "\"";
	}

	private static string TomlPath(string value)
	{
		return value.Replace('\\', '/').Replace("\"", "\\\"");
	}
}
