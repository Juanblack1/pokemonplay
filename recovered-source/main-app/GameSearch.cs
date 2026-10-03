using System;
using System.Globalization;
using System.Linq;

internal static class GameSearch
{
    internal static bool Matches(GameInfo game,string query)
    {
        string[] words=(query??string.Empty).Split((char[])null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        string alias=game.Generation==3?"GBA":game.Generation is 4 or 5?"DS":"3DS";
        string[] fields={game.Title??string.Empty,game.Subtitle??string.Empty,alias};
        var comparer=CultureInfo.CurrentCulture.CompareInfo;
        return words.All(word=>fields.Any(field=>comparer.IndexOf(field,word,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0));
    }
}
