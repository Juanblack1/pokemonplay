param(
    [Parameter(Mandatory=$true)][string]$Tag,
    [Parameter(Mandatory=$true)][string]$Repository,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if($Tag -notmatch '^v[0-9]{1,5}(\.[0-9]{1,5}){0,2}$'){throw 'Use uma tag estável como v114 ou v114.1.0.'}
if($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*/[A-Za-z0-9][A-Za-z0-9_.-]*$'){throw 'Repository deve ser usuario/repositorio.'}
$taskVersion=$Tag.Substring(1)
if($taskVersion -notmatch '\.'){$taskVersion+='.0.0'}elseif(($taskVersion -split '\.').Length -eq 2){$taskVersion+='.0'}
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskBuild=Join-Path ([IO.Path]::GetTempPath()) ('pokemonplay-release-'+[guid]::NewGuid().ToString('N'))
$taskRuntime=Join-Path $taskBuild 'PokemonPlayRuntime'
$taskHelper=Join-Path $taskBuild 'updater'
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
try{
    & dotnet publish (Join-Path $taskWorkspace 'recovered-source\main-app\Pokemons Play.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$taskVersion" -o $taskRuntime
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o aplicativo.'}
    $taskAppAssembly=Join-Path $taskRuntime 'Pokemons Play.dll'
    if(!(Test-Path -LiteralPath $taskAppAssembly)){throw 'Assembly principal ausente no pacote.'}
    $taskAssemblyText=[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($taskAppAssembly))
    if($taskAssemblyText.Contains('client_secret') -or $taskAssemblyText.Contains('GOCSPX-')){throw 'O pacote contém um marcador de credencial OAuth confidencial.'}
    & dotnet build (Join-Path $taskWorkspace 'recovered-source\updater\PokemonPlayUpdater.csproj') -c Release -o $taskHelper
    if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o atualizador.'}
    Copy-Item -LiteralPath (Join-Path $taskHelper 'PokemonPlayUpdater.exe') -Destination $taskRuntime
    Copy-Item -LiteralPath (Join-Path $taskHelper 'PokemonPlayUpdater.exe.config') -Destination $taskRuntime
    @{version=$Tag;repository=$Repository} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRuntime 'app-release.json') -Encoding utf8
    @{repository=$Repository} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRuntime 'UpdateSource.json') -Encoding utf8
    Get-ChildItem -LiteralPath $taskRuntime -Filter '*.pdb' | Remove-Item
    New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
    $taskZip=Join-Path $taskOutput 'pokemon-play-win-x64-update.zip'
    if(Test-Path -LiteralPath $taskZip){throw 'O pacote de saída já existe. Use uma pasta nova para esta release.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # Zip exactly the runtime folder, never the repository's ROMs, saves or credentials.
    $taskArchive=[IO.Compression.ZipFile]::Open($taskZip,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($taskFile in Get-ChildItem -LiteralPath $taskRuntime -File -Recurse){$taskRelative=$taskFile.FullName.Substring($taskBuild.Length+1).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive,$taskFile.FullName,$taskRelative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}
    finally{$taskArchive.Dispose()}
    $taskDigest=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($taskZip+'.sha256') -Value ($taskDigest+'  pokemon-play-win-x64-update.zip') -Encoding ascii
    Write-Output "Pacote da release $Tag pronto: $taskZip"
}
finally{
    if(Test-Path -LiteralPath $taskBuild){$taskResolved=(Resolve-Path -LiteralPath $taskBuild).Path;$taskTempPrefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath());if(!$taskResolved.StartsWith($taskTempPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Pasta temporária inválida.'};Remove-Item -LiteralPath $taskResolved -Recurse}
}
