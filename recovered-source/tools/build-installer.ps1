param(
    [Parameter(Mandatory=$true)][string]$RuntimeDirectory,
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$CompilerPath,
    [switch]$FastFixture
)
$ErrorActionPreference='Stop'
$taskVersion=$Version.TrimStart('v')
if($taskVersion -notmatch '^\d{1,5}(\.\d{1,5}){0,2}$'){throw 'Version deve ser uma versão estável numérica, como 171.0.0.'}
$taskParts=@($taskVersion.Split('.') | ForEach-Object {[int]$_})
if(@($taskParts | Where-Object {$_ -gt 65535}).Count){throw 'Cada componente da versão deve ser menor que 65536.'}
while($taskParts.Count -lt 3){$taskParts+=0}
$taskVersion=$taskParts -join '.'
$taskRuntime=(Resolve-Path -LiteralPath $RuntimeDirectory).Path
$taskCompiler=(Resolve-Path -LiteralPath $CompilerPath).Path
# ISCC does not expose the compiler release in its PE FileVersion resource.
$taskCompilerDigest=(Get-FileHash -LiteralPath $taskCompiler -Algorithm SHA256).Hash.ToLowerInvariant()
if($taskCompilerDigest -ne '0a8757031b33777e4c9cbffee40f11a5062b36d25cbe144c1db73b6102b80ad7'){throw 'Use o compilador Inno Setup 6.7.3 verificado para este projeto.'}
foreach($taskRequired in @('Pokemons Play.exe','Pokemons Play.dll','PokemonPlayUpdater.exe','app-release.json','UpdateSource.json')){
    if(!(Test-Path -LiteralPath (Join-Path $taskRuntime $taskRequired) -PathType Leaf)){throw "Arquivo obrigatório ausente: $taskRequired"}
}
$taskManifest=Get-Content -LiteralPath (Join-Path $taskRuntime 'app-release.json') -Raw | ConvertFrom-Json
$taskManifestParts=@(([string]$taskManifest.version).TrimStart('v').Split('.'))
while($taskManifestParts.Count -lt 3){$taskManifestParts+='0'}
if(($taskManifestParts -join '.') -ne $taskVersion){throw 'A versão do manifesto não corresponde ao instalador.'}
$taskExeVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskRuntime 'Pokemons Play.exe')).FileVersion
if(([version]$taskExeVersion) -ne ([version]($taskVersion+'.0'))){throw "A versão do runtime ($taskExeVersion) não corresponde ao instalador ($taskVersion)."}
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
$taskArtifact=Join-Path $taskOutput 'pokemon-play-win-x64-setup.exe'
if(Test-Path -LiteralPath $taskArtifact){throw 'O instalador de saída já existe. Use uma pasta nova.'}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskScript=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\installer\pokemon-play.iss'))
$taskVersionMS=([uint32]$taskParts[0] -shl 16) -bor [uint32]$taskParts[1]
$taskVersionLS=[uint32]$taskParts[2] -shl 16
$taskCompression=if($FastFixture){'none'}else{'lzma2'}
& $taskCompiler "/DCompressionMode=$taskCompression" "/DVersionMS=$taskVersionMS" "/DVersionLS=$taskVersionLS" "/DRuntimeDirectory=$taskRuntime" "/DAppVersion=$taskVersion" "/DInstallerOutputDirectory=$taskOutput" $taskScript
if($LASTEXITCODE -ne 0){throw "Falha ao compilar instalador (código $LASTEXITCODE)."}
if(!(Test-Path -LiteralPath $taskArtifact -PathType Leaf)){throw 'O compilador não gerou o instalador esperado.'}
$taskDigest=(Get-FileHash -LiteralPath $taskArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($taskArtifact+'.sha256') -Value ($taskDigest+'  pokemon-play-win-x64-setup.exe') -Encoding ascii
Write-Output $taskArtifact


