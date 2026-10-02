using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

internal static class BundledEmulatorArchiveCheck
{
    internal static void Run(string root, Assembly app)
    {
        var method=app.GetType("BundledEmulatorArchive").GetMethod("EnsureExtracted",BindingFlags.NonPublic|BindingFlags.Static);
        string runtime=Path.Combine(root,"nested-emulator-runtime");Directory.CreateDirectory(runtime);
        string archive=Path.Combine(runtime,"emulators-runtime.zip");
        void Build(string extra=null) {
            using var zip=ZipFile.Open(archive,ZipArchiveMode.Create);
            foreach(var name in new[]{"Emulators/RetroArch/retroarch.exe","Emulators/RetroArch/cores/mgba_libretro.dll","Emulators/RetroArch/cores/melondsds_libretro.dll","Emulators/Azahar/azahar.exe","Emulators/THIRD_PARTY.txt"}) {
                using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write("synthetic emulator fixture");
            }
            if(extra!=null){using var writer=new StreamWriter(zip.CreateEntry(extra).Open());writer.Write("unsafe");}
        }
        void Ensure()=>method.Invoke(null,new object[]{runtime});
        void Check(bool valid,string message){if(!valid)throw new Exception(message);Console.WriteLine("PASS "+message);}
        Build();Ensure();
        Check(File.ReadAllText(Path.Combine(runtime,"Emulators/THIRD_PARTY.txt"))=="synthetic emulator fixture"&&!File.Exists(archive),"nested emulator archive extracts completely and removes archive");
        Ensure();Check(Directory.GetDirectories(runtime,".emulators-extract-*").Length==0,"emulator extraction is idempotent without staging residue");
        Build();Ensure();Check(!File.Exists(archive),"interrupted archive cleanup validates existing files without replacement");
        Build();File.WriteAllText(Path.Combine(runtime,"Emulators/THIRD_PARTY.txt"),"personal modification");
        bool rejected=false;try{Ensure();}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){rejected=true;}
        Check(rejected&&File.ReadAllText(Path.Combine(runtime,"Emulators/THIRD_PARTY.txt"))=="personal modification"&&File.Exists(archive),"different existing emulator files are preserved instead of overwritten");
        File.Delete(archive);Directory.Delete(Path.Combine(runtime,"Emulators"),true);
        foreach(string invalid in new[]{"Emulators/../../escaped.txt","Other/escaped.txt","Emulators/RetroArch/RETROARCH.EXE","Emulators/CON.txt","Emulators/link"}) {
            Build(invalid);
            if(invalid.EndsWith("/link")){using var zip=ZipFile.Open(archive,ZipArchiveMode.Update);zip.GetEntry(invalid).ExternalAttributes=0xA000<<16;}
            rejected=false;try{Ensure();}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){rejected=true;}
            Check(rejected&&!Directory.Exists(Path.Combine(runtime,"Emulators"))&&Directory.GetDirectories(runtime,".emulators-extract-*").Length==0,"nested emulator archive rejects unsafe entry "+invalid);
            File.Delete(archive);
        }
    }
}
