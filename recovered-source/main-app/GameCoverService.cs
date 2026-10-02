using System;
using System.Drawing;
using System.IO;

internal static class GameCoverService
{
    internal static Image Load(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        try
        {
            string path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!File.Exists(path))
                return null;

            using FileStream stream = File.OpenRead(path);
            using Image decoded = Image.FromStream(stream);
            return new Bitmap(decoded);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OutOfMemoryException)
        {
            return null;
        }
    }
}
