param([Parameter(Mandatory=$true)][string]$Directory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$ra=Join-Path $Directory 'Emulators/RetroArch'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CoreSmoke {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetDllDirectory(string path);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint ApiVersion();
 public static void Check(string directory,string file) {
  if(!SetDllDirectory(directory)) throw new Exception("Cannot set dependency search path");
  IntPtr handle=NativeLibrary.Load(System.IO.Path.Combine(directory,"cores",file));
  try { var version=Marshal.GetDelegateForFunctionPointer<ApiVersion>(NativeLibrary.GetExport(handle,"retro_api_version"));
   if(version()!=1) throw new Exception("Invalid libretro ABI: "+file);
   Console.WriteLine("PASS native core loads with dependencies: "+file);
  } finally {NativeLibrary.Free(handle); SetDllDirectory(null);}
 }
}
'@
[CoreSmoke]::Check($ra,'mgba_libretro.dll')
[CoreSmoke]::Check($ra,'melondsds_libretro.dll')
$executable=Join-Path $ra 'retroarch.exe'
$process=Start-Process -FilePath $executable -ArgumentList '--version' -PassThru -WindowStyle Hidden
if(!$process.WaitForExit(20000)){$process.Kill();throw 'RetroArch não concluiu --version.'}
if($process.ExitCode -ne 0){throw "RetroArch falhou: $($process.ExitCode)"}
# Azahar's current Qt frontend does not implement --version. Open without a game
# and check that it survives startup, then close only this smoke process.
$executable=Join-Path $Directory 'Emulators/Azahar/azahar.exe'
$process=Start-Process -FilePath $executable -PassThru -WindowStyle Hidden
try {
    if($process.WaitForExit(5000)){throw "Azahar encerrou durante inicialização: $($process.ExitCode)"}
    Write-Output 'PASS Azahar survives startup with its packaged dependencies (no ROM loaded).'
} finally {
    if(!$process.HasExited){$null=$process.CloseMainWindow();if(!$process.WaitForExit(5000)){$process.Kill();$process.WaitForExit()}}
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$sourceStage=Join-Path $Directory 'source-distribution'; New-Item -ItemType Directory $sourceStage | Out-Null
Copy-Item (Join-Path $Directory 'core-sources.tar.gz'),(Join-Path $Directory 'retroarch-sources.tar.gz'),(Join-Path $Directory 'downloads/azahar-unified-source-2126.1.2.tar.xz'),(Join-Path $PSScriptRoot 'build-emulator-cores.sh'),(Join-Path $PSScriptRoot 'prepare-emulators.ps1') $sourceStage
Copy-Item (Join-Path $Directory 'Emulators/THIRD_PARTY.txt') $sourceStage
@'
Corresponding emulator source distribution. Extract the archives to inspect
licenses, attribution, build scripts and the exact core source/dependency trees.
Core builds: Windows x64, MSYS2 MINGW64 GCC/CMake/Ninja; commands in
build-emulator-cores.sh. For an offline rebuild, point FETCHCONTENT_SOURCE_DIR_*
at the matching directories extracted from core-sources.tar.gz.
RetroArch 1.22.2: official unmodified Windows build; upstream configure/Makefile
and packaged COPYING document the build and GPL license.
Azahar: official unmodified MSYS2 build 2126.1.2; unified archive includes
submodules and upstream CMake files. See its BUILDING documentation.
No files from a user's game library are used to generate this distribution.
'@ | Set-Content (Join-Path $sourceStage 'README.txt') -Encoding utf8
$zip=Join-Path $OutputDirectory 'pokemon-play-emulator-sources.zip'
Compress-Archive -Path (Join-Path $sourceStage '*') -DestinationPath $zip
$digest=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content ($zip+'.sha256') "$digest  pokemon-play-emulator-sources.zip" -Encoding ascii
