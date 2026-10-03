using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

internal static class RomDiscoveryCheck
{
    internal static void Run(string root)
    {
        string folder=Path.Combine(root,"rom-discovery"),nested=Path.Combine(folder,"Pokémon 子"),denied=Path.Combine(folder,"unavailable");
        Directory.CreateDirectory(nested);Directory.CreateDirectory(denied);
        string gba=Path.Combine(folder,"Z.gba"),ds=Path.Combine(nested,"Émeraude.NDS"),hidden=Path.Combine(folder,"hidden.3ds");
        File.WriteAllText(gba,"fixture");File.WriteAllText(ds,"fixture");File.WriteAllText(hidden,"fixture");
        File.SetAttributes(hidden,FileAttributes.Hidden);File.WriteAllText(Path.Combine(folder,"unrelated.txt"),"fixture");
        void Assert(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var found=RomFileDiscovery.Scan(folder,ImportedGameCatalog.IsSupportedRom);
        Assert(found.Files.Count==3&&found.Files.Contains(ds)&&found.Files.Contains(hidden),"ROM discovery includes nested Unicode, uppercase extensions and hidden files");
        Assert(found.Files.SequenceEqual(found.Files.OrderBy(path=>path,StringComparer.OrdinalIgnoreCase)),"ROM discovery returns deterministic path order");
        IEnumerable<FileSystemInfo> Enumerate(string path){if(path==denied)throw new UnauthorizedAccessException("synthetic denied subtree");return new DirectoryInfo(path).EnumerateFileSystemInfos();}
        found=RomFileDiscovery.Scan(folder,ImportedGameCatalog.IsSupportedRom,enumerate:Enumerate);
        Assert(found.Files.Count==3&&found.SkippedLocations==1,"denied subtree preserves accessible ROMs and reports partial discovery");
        found=RomFileDiscovery.Scan(folder,ImportedGameCatalog.IsSupportedRom,2);
        Assert(found.Files.Count==2&&found.LimitReached,"ROM discovery stops at its caller's file limit");
        found=RomFileDiscovery.Scan(Path.Combine(folder,"missing"),ImportedGameCatalog.IsSupportedRom);
        Assert(found.Files.Count==0&&found.SkippedLocations==1,"vanished root reports incomplete discovery without crashing the library");
        IEnumerable<FileSystemInfo> Interrupted(string path){yield return new FileInfo(gba);throw new IOException("synthetic interrupted enumeration");}
        found=RomFileDiscovery.Scan(folder,ImportedGameCatalog.IsSupportedRom,enumerate:Interrupted);
        Assert(found.Files.SequenceEqual(new[]{gba})&&found.SkippedLocations==1,"interrupted enumeration retains ROMs delivered before the I/O failure");
        int delivered=0;
        IEnumerable<FileSystemInfo> LargeNonRomTree(string path){var entry=new FileInfo(Path.Combine(folder,"unrelated.txt"));for(int i=0;i<100010;i++){delivered++;yield return entry;}}
        found=RomFileDiscovery.Scan(folder,ImportedGameCatalog.IsSupportedRom,enumerate:LargeNonRomTree);
        Assert(found.Files.Count==0&&found.LimitReached&&delivered==100001,"discovery bounds work even when a large tree contains no compatible ROMs");
        if(OperatingSystem.IsWindows()) {
            string link=Path.Combine(folder,"loop-junction");
            using var junction=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe") {
                Arguments="/c mklink /J \""+link+"\" \""+folder+"\"",UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardOutput=true,RedirectStandardError=true });
            junction.WaitForExit(10000);
            if(junction.ExitCode!=0)throw new Exception("Diagnostic directory junction could not be created.");
            try {
                int legacyCount=Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Where(ImportedGameCatalog.IsSupportedRom).Take(8).Count();
                Assert(legacyCount==8,"baseline recursive enumeration reproduces ROM duplication through a junction cycle");
                found=RomFileDiscovery.Scan(folder,ImportedGameCatalog.IsSupportedRom);
                Assert(found.Files.Count==3&&found.SkippedLocations==1,"directory junction cycle is skipped without duplicating or escaping the ROM folder");
            }
            finally {Directory.Delete(link);}
        }
        Assert(File.ReadAllText(gba)=="fixture"&&File.ReadAllText(ds)=="fixture","discovery does not modify original ROM fixtures");
    }
}
