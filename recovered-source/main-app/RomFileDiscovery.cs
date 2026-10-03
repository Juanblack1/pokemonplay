using System;
using System.Collections.Generic;
using System.IO;

internal sealed class RomDiscoveryResult
{
    internal readonly List<string> Files = new();
    internal int SkippedLocations;
    internal bool LimitReached;
}

internal static class RomFileDiscovery
{
    internal static RomDiscoveryResult Scan(string root, Func<string,bool> supported, int maximumFiles=int.MaxValue,
        Func<string,IEnumerable<FileSystemInfo>> enumerate=null, bool skipReparseFiles=true)
    {
        if(maximumFiles<1)throw new ArgumentOutOfRangeException(nameof(maximumFiles));
        enumerate ??= path=>new DirectoryInfo(path).EnumerateFileSystemInfos("*",new EnumerationOptions {
            RecurseSubdirectories=false, IgnoreInaccessible=false, AttributesToSkip=0 });
        var result=new RomDiscoveryResult();
        var pending=new Stack<(string Path,int Depth)>();
        pending.Push((Path.GetFullPath(root),0));
        int directories=0,entries=0;
        while(pending.Count>0) {
            var current=pending.Pop();
            if(++directories>10000){result.LimitReached=true;break;}
            try {
                foreach(var entry in enumerate(current.Path)) {
                    if(++entries>100000){result.LimitReached=true;pending.Clear();break;}
                    try {
                    var attributes=entry.Attributes;
                    if((attributes&FileAttributes.ReparsePoint)!=0&&((attributes&FileAttributes.Directory)!=0||skipReparseFiles)){result.SkippedLocations++;continue;}
                    if((attributes&FileAttributes.Directory)!=0) {
                        if(current.Depth>=64){result.SkippedLocations++;continue;}
                        if(directories+pending.Count>=10000){result.LimitReached=true;continue;}
                        pending.Push((entry.FullName,current.Depth+1));
                    }else if(supported(entry.FullName)) {
                        result.Files.Add(entry.FullName);
                        if(result.Files.Count>=maximumFiles){result.LimitReached=true;pending.Clear();break;}
                    }
                    }catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException) {result.SkippedLocations++;}
                }
            }catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
                result.SkippedLocations++;
            }
        }
        result.Files.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
