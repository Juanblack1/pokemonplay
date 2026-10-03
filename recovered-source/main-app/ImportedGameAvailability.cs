using System;
using System.IO;
using System.Linq;
using System.Text.Json;

internal sealed record ImportedGameAvailability(int UnavailableCount,bool CatalogUnreadable)
{
    internal static ImportedGameAvailability Inspect(string root)
    {
        try {return new(ImportedGameCatalog.Entries(root).Count(entry=>!File.Exists(entry.RomPath)),false);}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or System.Security.SecurityException) {return new(0,true);}
    }
}
