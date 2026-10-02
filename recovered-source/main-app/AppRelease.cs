using System;
using System.Reflection;

internal static class AppRelease
{
    internal static Version Version => Assembly.GetExecutingAssembly().GetName().Version;
    internal static string Tag => "v" + (Version.Minor==0&&Version.Build==0 ? Version.Major.ToString() : $"{Version.Major}.{Version.Minor}.{Version.Build}");
    internal static Version ParseVersion(string tag)
    {
        string text=(tag??string.Empty).Trim().TrimStart('v','V');
        if(!System.Text.RegularExpressions.Regex.IsMatch(text,@"^\d{1,5}(\.\d{1,5}){0,3}$"))throw new FormatException("A release deve ter uma versão como v114 ou v114.1.0.");
        string[] parts=text.Split('.');
        return new Version(int.Parse(parts[0]),parts.Length>1?int.Parse(parts[1]):0,parts.Length>2?int.Parse(parts[2]):0,parts.Length>3?int.Parse(parts[3]):0);
    }
}
