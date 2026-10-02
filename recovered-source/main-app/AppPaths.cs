using System;
using System.IO;

internal static class AppPaths
{
	public static string Root { get; } = ResolveRoot(AppDomain.CurrentDomain.BaseDirectory);

	internal static string ResolveRoot(string directory)
	{
		string current = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		DirectoryInfo parent = Directory.GetParent(current);
		if (string.Equals(Path.GetFileName(current), "PokemonPlayRuntime", StringComparison.OrdinalIgnoreCase) && parent != null)
			return parent.FullName;
		return current;
	}
}
