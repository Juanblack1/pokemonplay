param(
    [Parameter(Mandatory=$true)][string]$Tag,
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$EmulatorDirectory,
    [Parameter(Mandatory=$true)][string]$InstallerCompilerPath
)
$ErrorActionPreference='Stop'
if($Tag -notmatch '^v[0-9]{1,5}(\.[0-9]{1,5}){0,2}$'){throw 'Use uma tag estável como v114 ou v114.1.0.'}
if($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$'){throw 'Repository deve ser usuario/repositorio.'}
$taskVersion=$Tag.Substring(1)
if($taskVersion -notmatch '\.'){$taskVersion+='.0.0'}elseif(($taskVersion -split '\.').Length -eq 2){$taskVersion+='.0'}
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskBuild=Join-Path ([IO.Path]::GetTempPath()) ('pp-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$taskRuntime=Join-Path $taskBuild 'PokemonPlayRuntime'
$taskLauncher=Join-Path $taskBuild 'launcher'
$taskHelper=Join-Path $taskBuild 'updater'
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
try{
    & dotnet publish (Join-Path $taskWorkspace 'recovered-source\main-app\Pokemons Play.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$taskVersion" -o $taskRuntime
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o aplicativo.'}
    $taskEmulators=[IO.Path]::GetFullPath($EmulatorDirectory)
    foreach($taskRequired in @('RetroArch/retroarch.exe','RetroArch/cores/mgba_libretro.dll','RetroArch/cores/melondsds_libretro.dll','Azahar/azahar.exe','THIRD_PARTY.txt')){
        if(!(Test-Path -LiteralPath (Join-Path $taskEmulators $taskRequired) -PathType Leaf)){throw "Componente obrigatório ausente: $taskRequired"}
    }
    Copy-Item -LiteralPath $taskEmulators -Destination (Join-Path $taskRuntime 'Emulators') -Recurse
    if(@(Get-ChildItem (Join-Path $taskRuntime 'Emulators') -File -Recurse | Where-Object {$_.Extension -in '.gba','.nds','.3ds','.cci','.cxi','.sav','.srm','.pfx' -or $_.Name -in 'bios7.bin','bios9.bin','firmware.bin','aes_keys.txt'}).Count -gt 0){throw 'Dados de jogo/usuário proibidos no runtime dos emuladores.'}
    $taskAppAssembly=Join-Path $taskRuntime 'Pokemons Play.dll'
    if(!(Test-Path -LiteralPath $taskAppAssembly)){throw 'Assembly principal ausente no pacote.'}
    $taskAssemblyText=[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($taskAppAssembly))
    if($taskAssemblyText.Contains('client_secret') -or $taskAssemblyText.Contains('GOCSPX-')){throw 'O pacote contém um marcador de credencial OAuth confidencial.'}
    & dotnet publish (Join-Path $taskWorkspace 'recovered-source\updater\PokemonPlayUpdater.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $taskHelper
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o atualizador.'}
    $taskUpdaterExecutable=Join-Path $taskHelper 'PokemonPlayUpdater.exe'
    if(!(Test-Path -LiteralPath $taskUpdaterExecutable)){throw 'Executável self-contained do atualizador ausente.'}
    if(@(Get-ChildItem -LiteralPath $taskHelper -Filter '*.dll' -File).Count -gt 0){throw 'O atualizador não foi publicado como executável single-file.'}
    Get-ChildItem -LiteralPath $taskHelper -File | Where-Object Extension -ne '.pdb' | Copy-Item -Destination $taskRuntime
    & dotnet publish (Join-Path $taskWorkspace 'recovered-source\launcher\PokemonsPlayLauncher.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $taskLauncher
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o iniciador portátil.'}
    $taskPortableLauncher=Join-Path $taskLauncher 'Pokemons Play.exe'
    if(!(Test-Path -LiteralPath $taskPortableLauncher)){throw 'Executável self-contained do iniciador portátil ausente.'}
    if(@(Get-ChildItem -LiteralPath $taskLauncher -Filter '*.dll' -File).Count -gt 0){throw 'O iniciador portátil não foi publicado como executável single-file.'}
    @{version=$Tag;repository=$Repository} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRuntime 'app-release.json') -Encoding utf8
    @{repository=$Repository} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRuntime 'UpdateSource.json') -Encoding utf8
    foreach($taskNotice in @('LICENSE','PRIVACY.md','CODE_SIGNING.md')){
        Copy-Item -LiteralPath (Join-Path $taskWorkspace $taskNotice) -Destination $taskRuntime
    }
    $taskSigningRequested=$env:POKEMONPLAY_REQUIRE_SIGNING -eq 'true' -or ![string]::IsNullOrWhiteSpace($env:POKEMONPLAY_SIGNING_PFX_BASE64) -or ![string]::IsNullOrWhiteSpace($env:POKEMONPLAY_SIGNING_PFX_PASSWORD)
    if($taskSigningRequested){
        & (Join-Path $PSScriptRoot 'sign-windows-package.ps1') -Directory $taskBuild
        if($LASTEXITCODE -ne 0){throw 'A assinatura/verificação dos binários Windows falhou.'}
    }
    Get-ChildItem -LiteralPath $taskRuntime -Filter '*.pdb' | Remove-Item
    New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
    $taskZip=Join-Path $taskOutput 'pokemon-play-win-x64-update.zip'
    if(Test-Path -LiteralPath $taskZip){throw 'O pacote de saída já existe. Use uma pasta nova para esta release.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # Older launchers accept at most 4096 outer entries. Keep the complete emulator
    # tree in one nested archive; the new launcher expands it before confirming readiness.
    $taskEmulatorArchive=Join-Path $taskBuild 'emulators-runtime.zip'
    $taskNested=[IO.Compression.ZipFile]::Open($taskEmulatorArchive,[IO.Compression.ZipArchiveMode]::Create)
    try{
        $taskEmulatorFiles=@(Get-ChildItem -LiteralPath (Join-Path $taskRuntime 'Emulators') -File -Recurse)
        if($taskEmulatorFiles.Count -gt 32768 -or ($taskEmulatorFiles | Measure-Object Length -Sum).Sum -gt 2GB){throw 'Os emuladores excedem os limites de extração do aplicativo.'}
        foreach($taskFile in $taskEmulatorFiles){$taskRelative=$taskFile.FullName.Substring($taskRuntime.Length+1).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskNested,$taskFile.FullName,$taskRelative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
    }
    finally{$taskNested.Dispose()}
    # The portable ZIP and installer continue shipping the fully expanded runtime.
    $taskArchive=[IO.Compression.ZipFile]::Open($taskZip,[IO.Compression.ZipArchiveMode]::Create)
    try{
        foreach($taskFile in Get-ChildItem -LiteralPath $taskRuntime -File -Recurse){$taskRelative=$taskFile.FullName.Substring($taskBuild.Length+1).Replace('\','/');if($taskRelative.StartsWith('PokemonPlayRuntime/Emulators/')){continue};[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive,$taskFile.FullName,$taskRelative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive,$taskEmulatorArchive,'PokemonPlayRuntime/emulators-runtime.zip',[IO.Compression.CompressionLevel]::NoCompression)|Out-Null
    }
    finally{$taskArchive.Dispose()}
    if((Get-Item -LiteralPath $taskZip).Length -gt 512MB){throw 'O pacote excede o limite de download dos launchers instalados.'}
    $taskDigest=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($taskZip+'.sha256') -Value ($taskDigest+'  pokemon-play-win-x64-update.zip') -Encoding ascii
    $taskPortableZip=Join-Path $taskOutput 'pokemon-play-win-x64-portable.zip'
    $taskPortableArchive=[IO.Compression.ZipFile]::Open($taskPortableZip,[IO.Compression.ZipArchiveMode]::Create)
    try{
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskPortableArchive,$taskPortableLauncher,'Pokemons Play.exe',[IO.Compression.CompressionLevel]::Optimal)|Out-Null
        foreach($taskFile in Get-ChildItem -LiteralPath $taskRuntime -File -Recurse){$taskRelative=$taskFile.FullName.Substring($taskBuild.Length+1).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskPortableArchive,$taskFile.FullName,$taskRelative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}
    }
    finally{$taskPortableArchive.Dispose()}
    $taskUpdateCheck=[IO.Compression.ZipFile]::OpenRead($taskZip)
    try{$taskUpdateNames=@($taskUpdateCheck.Entries | ForEach-Object {$_.FullName});if($taskUpdateNames.Count -gt 4096){throw 'O pacote excede o limite aceito pelos launchers anteriores.'};if($taskUpdateNames -notcontains 'PokemonPlayRuntime/Pokemons Play.exe'){throw 'O pacote de atualização não contém o executável esperado.'};if($taskUpdateNames -notcontains 'PokemonPlayRuntime/emulators-runtime.zip'){throw 'Emuladores ausentes do pacote.'};if($taskUpdateNames -contains 'Pokemons Play.exe'){throw 'O pacote de atualização contém uma entrada de launcher inesperada.'}}
    finally{$taskUpdateCheck.Dispose()}
    $taskPortableCheck=[IO.Compression.ZipFile]::OpenRead($taskPortableZip)
    try{$taskPortableNames=@($taskPortableCheck.Entries | ForEach-Object {$_.FullName});if($taskPortableNames -notcontains 'Pokemons Play.exe' -or $taskPortableNames -notcontains 'PokemonPlayRuntime/Pokemons Play.exe'){throw 'O pacote portátil não contém o launcher ou o runtime esperado.'}}
    finally{$taskPortableCheck.Dispose()}
    $taskPortableDigest=(Get-FileHash -LiteralPath $taskPortableZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($taskPortableZip+'.sha256') -Value ($taskPortableDigest+'  pokemon-play-win-x64-portable.zip') -Encoding ascii
    & (Join-Path $PSScriptRoot 'build-installer.ps1') -RuntimeDirectory $taskRuntime -Version $taskVersion -OutputDirectory $taskOutput -CompilerPath $InstallerCompilerPath
    if ($taskSigningRequested) {
        & (Join-Path $PSScriptRoot 'sign-windows-package.ps1') -Directory $taskOutput -Kind Installer
        if ($LASTEXITCODE -ne 0) { throw 'A assinatura do instalador falhou.' }
        $taskSetup=Join-Path $taskOutput 'pokemon-play-win-x64-setup.exe'
        $taskSetupDigest=(Get-FileHash -LiteralPath $taskSetup -Algorithm SHA256).Hash.ToLowerInvariant()
        Set-Content -LiteralPath ($taskSetup+'.sha256') -Value ($taskSetupDigest+'  pokemon-play-win-x64-setup.exe') -Encoding ascii
    }
    Write-Output "Pacote da release $Tag pronto: $taskZip"
    Write-Output "Pacote portátil da release $Tag pronto: $taskPortableZip"
}
finally{
    if(Test-Path -LiteralPath $taskBuild){$taskResolved=(Resolve-Path -LiteralPath $taskBuild).Path;$taskTempPrefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(!$taskResolved.StartsWith($taskTempPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Pasta temporária inválida.'};Remove-Item -LiteralPath $taskResolved -Recurse}
}
