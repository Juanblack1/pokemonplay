param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference='Stop'
$bundle=[IO.Path]::GetFullPath($Directory)
if(Test-Path -LiteralPath $bundle){throw 'Use uma pasta de provisionamento nova.'}
New-Item -ItemType Directory -Path $bundle | Out-Null
$download=Join-Path $bundle 'downloads'; $sources=Join-Path $bundle 'sources'; $runtime=Join-Path $bundle 'Emulators'
New-Item -ItemType Directory -Path $download,$sources,$runtime | Out-Null
function Fetch([string]$Url,[string]$Name,[string]$Sha){
    $target=Join-Path $download $Name
    Invoke-WebRequest -Uri $Url -OutFile $target
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Sha){throw "Hash incorreto: $Name"}
    return $target
}
function Clone([string]$Repo,[string]$Ref,[string]$Name){
    $target=Join-Path $sources $Name
    & git init $target
    if($LASTEXITCODE -ne 0){throw "Clone falhou: $Repo"}
    & git -C $target remote add origin "https://github.com/$Repo.git"
    & git -C $target fetch --depth 1 origin $Ref
    if($LASTEXITCODE -ne 0){throw "Ref ausente: $Repo $Ref"}
    & git -C $target checkout --detach FETCH_HEAD
    if($LASTEXITCODE -ne 0){throw "Ref ausente: $Repo $Ref"}
    & git -C $target submodule update --init --recursive --depth 1
    if($LASTEXITCODE -ne 0){throw "Submódulos ausentes: $Repo"}
}
$ra=Fetch 'https://buildbot.libretro.com/stable/1.22.2/windows/x86_64/RetroArch.7z' 'RetroArch.7z' 'b2139b1d0f9d4526dc6b5ce23cbb3efdc766096fa6f2c3df016818b486ac6372'
& 7z x $ra "-o$(Join-Path $download 'retroarch')" -y | Out-Null
if($LASTEXITCODE -ne 0){throw 'Extração RetroArch falhou.'}
$raExe=@(Get-ChildItem (Join-Path $download 'retroarch') -Recurse -Filter retroarch.exe)
if($raExe.Count -ne 1){throw 'Layout RetroArch inesperado.'}
$raTarget=Join-Path $runtime 'RetroArch'; New-Item -ItemType Directory $raTarget | Out-Null
Copy-Item (Join-Path $raExe[0].DirectoryName '*') $raTarget -Recurse
if(@(Get-ChildItem (Join-Path $raTarget 'cores') -Filter '*.dll' -ErrorAction SilentlyContinue).Count -gt 0){throw 'O pacote frontend contém cores inesperados.'}
$az=Fetch 'https://github.com/azahar-emu/azahar/releases/download/2126.1.2/azahar-windows-msys2-2126.1.2.zip' 'Azahar.zip' 'e18a162e2e992cef406eccaa9de85d1940f93b460d670070c4235e91baa98249'
Expand-Archive -LiteralPath $az -DestinationPath (Join-Path $download 'azahar')
$azExe=@(Get-ChildItem (Join-Path $download 'azahar') -Recurse -Filter azahar.exe)
if($azExe.Count -ne 1){throw 'Layout Azahar inesperado.'}
$azTarget=Join-Path $runtime 'Azahar'; New-Item -ItemType Directory $azTarget | Out-Null
Copy-Item (Join-Path $azExe[0].DirectoryName '*') $azTarget -Recurse
if(Test-Path (Join-Path $azTarget 'user')){throw 'Azahar contém dados de usuário inesperados.'}
$null=Fetch 'https://github.com/azahar-emu/azahar/releases/download/2126.1.2/azahar-unified-source-2126.1.2.tar.xz' 'azahar-unified-source-2126.1.2.tar.xz' 'ca0626312af68370e04c70b60eab4dd95e6f3f248d1d1c6a22ee784dfce6b361'
Clone 'libretro/RetroArch' '69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576' 'RetroArch'
Clone 'mgba-emu/mgba' '26b7884bc25a5933960f3cdcd98bac1ae14d42e2' 'mgba'
Clone 'JesseTG/melonds-ds' 'f394adbacb5722ee97c1b37c8064da9a25818310' 'melonds-ds'
Copy-Item (Join-Path $sources 'RetroArch\COPYING') (Join-Path $raTarget 'COPYING')
@'
Pokémon Play distributes separate, unmodified RetroArch 1.22.2 (GPLv3+) and
Azahar 2126.1.2 (GPLv2+), plus locally compiled mGBA 0.10.5 (MPL 2.0) and
melonDS DS 1.4.0 (GPLv3+). Each component retains its upstream licenses.
Corresponding sources, dependencies and build commands are in the release asset
pokemon-play-emulator-sources.zip at https://github.com/Juanblack1/pokemonplay/releases.
No Nintendo ROMs, BIOS, firmware, keys or user data are included.
Official projects: https://www.retroarch.com/ https://mgba.io/
https://github.com/JesseTG/melonds-ds https://azahar-emu.org/
'@ | Set-Content (Join-Path $runtime 'THIRD_PARTY.txt') -Encoding utf8
Write-Output "Upstream provisionado em $bundle"
