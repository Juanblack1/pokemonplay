param([string]$Directory = (Join-Path ([IO.Path]::GetTempPath()) 'pokemonplay-inno-6.7.3'))
$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $Directory 'ISCC.exe'
if (Test-Path -LiteralPath $taskCompiler) {
    if ((Get-FileHash -LiteralPath $taskCompiler -Algorithm SHA256).Hash.ToLowerInvariant() -ne '0a8757031b33777e4c9cbffee40f11a5062b36d25cbe144c1db73b6102b80ad7') { throw 'Compilador Inno Setup não corresponde à versão fixada.' }
    return $taskCompiler
}
New-Item -ItemType Directory -Path $Directory -Force | Out-Null
$taskDownload = Join-Path $Directory 'innosetup-6.7.3.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $taskDownload
if ((Get-FileHash -LiteralPath $taskDownload -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732') { throw 'Hash do instalador Inno Setup divergente.' }
$taskProcess = Start-Process -FilePath $taskDownload -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', ('/DIR="' + $Directory + '"')) -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskCompiler)) { throw 'Não foi possível preparar o compilador Inno Setup.' }
if ((Get-FileHash -LiteralPath $taskCompiler -Algorithm SHA256).Hash.ToLowerInvariant() -ne '0a8757031b33777e4c9cbffee40f11a5062b36d25cbe144c1db73b6102b80ad7') { throw 'Hash do compilador Inno Setup divergente.' }
$taskCompiler
