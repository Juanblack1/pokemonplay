using System.Security.Cryptography;
using System.Text.RegularExpressions;

internal static class SessionConfig
{
    internal static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    internal static Dictionary<string,string> Read(string path)
    {
        var map=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(string line in File.ReadLines(path)){
            if(string.IsNullOrWhiteSpace(line)||line.TrimStart().StartsWith('#'))continue;
            var match=Regex.Match(line,"^\\s*([a-zA-Z0-9_]+)\\s*=\\s*\"([^\"]*)\"\\s*$");
            if(!match.Success||!map.TryAdd(match.Groups[1].Value,match.Groups[2].Value))throw new InvalidDataException("configuration: unparseable or duplicate key");
        }
        foreach(var item in map)if((item.Key.Contains("password")||item.Key.Contains("token")||item.Key.Contains("username"))&&item.Value.Length>0)throw new InvalidDataException("configuration: credentials forbidden");
        return map;
    }
    internal static void Owned(string root,string path)
    {
        string full=Path.GetFullPath(path);if(!full.StartsWith(Path.TrimEndingDirectorySeparator(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("root_identity: path outside fixture");
        for(string cursor=full;cursor!=null;cursor=Path.GetDirectoryName(cursor))if((File.Exists(cursor)||Directory.Exists(cursor))&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("root_identity: reparse path forbidden");
    }
    internal static object Instrument(string root,string file,int port)
    {
        Owned(root,file);var original=Read(file);var baseConfig=Read(Path.Combine(root,"Settings","Emulators","RetroArch","retroarch.cfg"));
        if(baseConfig.Keys.Any(key=>key!="config_save_on_exit"))throw new InvalidDataException("configuration: base must be fresh production bootstrap config only");
        foreach(string key in new[]{"savefile_directory","savestate_directory","system_directory","libretro_directory","libretro_info_path","assets_directory"}){if(!original.ContainsKey(key))throw new InvalidDataException("configuration: missing protected path "+key);Owned(root,original[key]);}
        if(original.GetValueOrDefault("pause_nonactive")!="true"||original.GetValueOrDefault("config_save_on_exit")!="false")throw new InvalidDataException("configuration: pause/save protection absent");
        if(original.Keys.Any(k=>k.EndsWith("_driver")||k=="video_context_driver"))throw new InvalidDataException("default_driver: explicit driver override forbidden");
        var allowed=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(string key in new[]{"cheevos_enable","cheevos_hardcore_mode_enable","cloud_sync_enable","discord_allow","ai_service_enable","network_on_demand_thumbnails","network_remote_enable","netplay_public_announce","netplay_nat_traversal","netplay_use_mitm_server","stdin_cmd_enable","game_specific_options","auto_overrides_enable","auto_remaps_enable","auto_shaders_enable","video_shader_enable","input_overlay_enable","input_overlay_enable_autopreferred","video_font_enable","menu_enable_widgets","log_to_file_timestamp"})allowed[key]="false";
        allowed["video_gpu_screenshot"]="true";
        foreach(string key in new[]{"network_cmd_enable","global_core_options","log_verbosity","log_to_file"})allowed[key]="true";
        foreach(string key in new[]{"cheevos_username","cheevos_password","cheevos_token"})allowed[key]="";
        allowed["network_cmd_port"]=port.ToString();allowed["frontend_log_level"]="0";allowed["libretro_log_level"]="0";
        string data=Path.Combine(root,"Settings","Emulators","RetroArch");
        allowed["screenshot_directory"]=Path.Combine(root,"captures","inbox");allowed["core_options_path"]=Path.Combine(data,"diagnostic-core-options.cfg");
        allowed["rgui_config_directory"]=Path.Combine(data,"overrides-empty");allowed["input_remapping_directory"]=Path.Combine(data,"remaps-empty");allowed["video_shader_dir"]=Path.Combine(data,"shaders-empty");allowed["log_dir"]=Path.Combine(root,"evidence","retroarch-log");
        foreach(string key in new[]{"screenshot_directory","rgui_config_directory","input_remapping_directory","video_shader_dir","log_dir"}){Directory.CreateDirectory(allowed[key]);if(Directory.EnumerateFileSystemEntries(allowed[key]).Any())throw new InvalidDataException("configuration: diagnostic directory is not fresh");}
        File.WriteAllText(allowed["core_options_path"],"mgba_use_bios = \"OFF\"\nmgba_skip_bios = \"ON\"\n");
        Directory.CreateDirectory(original["system_directory"]);
        if(Directory.EnumerateFileSystemEntries(original["system_directory"]).Any())throw new InvalidDataException("configuration: BIOS system directory not empty");
        string evidence=Path.Combine(root,"evidence");File.Copy(file,Path.Combine(evidence,"session-original.cfg"),false);
        var final=new Dictionary<string,string>(original,StringComparer.Ordinal);foreach(var item in allowed)final[item.Key]=item.Value;
        File.WriteAllLines(file,final.Select(item=>$"{item.Key} = \"{item.Value}\""));
        var actual=Read(file);foreach(var item in original)if(!allowed.ContainsKey(item.Key)&&actual.GetValueOrDefault(item.Key)!=item.Value)throw new InvalidDataException("configuration: protected key changed");
        foreach(var item in allowed)if(actual.GetValueOrDefault(item.Key)!=item.Value)throw new InvalidDataException("configuration: whitelist value mismatch");
        File.Copy(file,Path.Combine(evidence,"session-final.cfg"),false);
        return new{originalHash=Hash(Path.Combine(evidence,"session-original.cfg")),finalHash=Hash(file),changes=allowed.Keys.ToArray(),protectedKeys=original.Keys.Except(allowed.Keys).ToArray(),precedence="pinned-source+owned-files; effective options require runtime logs"};
    }
}
