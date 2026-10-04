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
    internal static bool IsNewer(string tag,Version current)
    {
        return ForUpdateComparison(ParseVersion(tag))>ForUpdateComparison(current);
    }
    private static Version ForUpdateComparison(Version version)
    {
        // v2 is the product's new public version line; map it after the legacy v171 line
        // only for ordering so users on v171 can receive v2.0.0 through the updater.
        return version.Major==2?new Version(172,version.Minor,version.Build,version.Revision):version;
    }
}
