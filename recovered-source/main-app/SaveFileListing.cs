using System;
using System.IO;
using System.Linq;

internal sealed record SaveFileListing(string[] Files,bool Complete)
{
    internal static SaveFileListing Read(string folder)
    {
        if(!Directory.Exists(folder))return new(Array.Empty<string>(),true);
        var scan=RomFileDiscovery.Scan(folder,_=>true,513,skipReparseFiles:false);
        return new(scan.Files.Take(512).ToArray(),!scan.LimitReached&&scan.SkippedLocations==0);
    }
}
