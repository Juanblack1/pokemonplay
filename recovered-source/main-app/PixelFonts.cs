using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;

internal static class PixelFonts
{
    private static readonly PrivateFontCollection Fonts = new();
    private static readonly FontFamily Family = Load();
    private static IntPtr fontData;
    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr data, uint length, IntPtr reserved, ref uint count);
    private static FontFamily Load()
    {
        using var stream = typeof(PixelFonts).Assembly.GetManifestResourceStream("PixelifySans");
        if (stream == null) throw new InvalidOperationException("Fonte pixel art não está no pacote.");
        byte[] bytes = new byte[stream.Length]; stream.ReadExactly(bytes);
        fontData = Marshal.AllocCoTaskMem(bytes.Length); Marshal.Copy(bytes, 0, fontData, bytes.Length);
        uint count = 0; AddFontMemResourceEx(fontData, (uint)bytes.Length, IntPtr.Zero, ref count);
        Fonts.AddMemoryFont(fontData, bytes.Length);
        return Fonts.Families[0];
    }
    public static Font At(float points) => new(Family, points, FontStyle.Regular);
}
