using System.Collections.Generic;
using System.Drawing;

internal static class PokemonTypeCatalog
{
    private static readonly string[] TypeNames =
    {
        "Normal", "Lutador", "Voador", "Veneno", "Terra", "Pedra", "Inseto", "Fantasma", "Aço",
        "Fogo", "Água", "Planta", "Elétrico", "Psíquico", "Gelo", "Dragão", "Sombrio", "Fada"
    };

    private static readonly Color[] TypeAccents =
    {
        Color.FromArgb(168, 167, 122), Color.FromArgb(194, 46, 40), Color.FromArgb(169, 143, 243),
        Color.FromArgb(163, 62, 161), Color.FromArgb(226, 191, 101), Color.FromArgb(182, 161, 54),
        Color.FromArgb(166, 185, 26), Color.FromArgb(115, 87, 151), Color.FromArgb(183, 183, 206),
        Color.FromArgb(238, 129, 48), Color.FromArgb(99, 144, 240), Color.FromArgb(122, 199, 76),
        Color.FromArgb(247, 208, 44), Color.FromArgb(249, 85, 135), Color.FromArgb(150, 217, 214),
        Color.FromArgb(111, 53, 252), Color.FromArgb(112, 87, 70), Color.FromArgb(214, 133, 173)
    };

    internal static IReadOnlyList<string> Names => TypeNames;

    internal static string GetName(int type)
        => (uint)type < (uint)TypeNames.Length ? TypeNames[type] : string.Empty;

    internal static Color GetAccent(int type)
        => (uint)type < (uint)TypeAccents.Length ? TypeAccents[type] : AppTheme.TextMuted;
}
