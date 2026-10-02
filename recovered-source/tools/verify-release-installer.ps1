param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$CompilerPath
)
$ErrorActionPreference='Stop'
$taskFixture=Join-Path ([IO.Path]::GetTempPath()) ('ppi-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $taskFixture | Out-Null
Expand-Archive -LiteralPath (Join-Path $ReleaseDirectory 'pokemon-play-win-x64-update.zip') -DestinationPath $taskFixture
$taskRuntime=Join-Path $taskFixture 'PokemonPlayRuntime'
Expand-Archive -LiteralPath (Join-Path $taskRuntime 'emulators-runtime.zip') -DestinationPath $taskRuntime
Remove-Item -LiteralPath (Join-Path $taskRuntime 'emulators-runtime.zip')
$taskManifest=Get-Content (Join-Path $taskRuntime 'app-release.json') -Raw | ConvertFrom-Json
$taskVersionParts=$taskManifest.version.TrimStart('v').Split('.')
while($taskVersionParts.Count -lt 3){$taskVersionParts+='0'}
$taskVersion=[version]($taskVersionParts -join '.')
# Build a distinct older runtime from the same source to exercise version ordering.
if($taskVersion.Build -gt 0){$taskOlderVersion='{0}.{1}.{2}' -f $taskVersion.Major,$taskVersion.Minor,($taskVersion.Build-1)}
elseif($taskVersion.Minor -gt 0){$taskOlderVersion='{0}.{1}.0' -f $taskVersion.Major,($taskVersion.Minor-1)}
elseif($taskVersion.Major -gt 0){$taskOlderVersion='{0}.0.0' -f ($taskVersion.Major-1)}
else{throw 'Test needs a positive version.'}
& dotnet publish (Join-Path $PSScriptRoot '../main-app/Pokemons Play.csproj') -c Release -r win-x64 --self-contained true "-p:Version=$taskOlderVersion" -o $taskRuntime
if($LASTEXITCODE -ne 0){throw 'Older runtime fixture build failed.'}
$taskManifest.version='v'+$taskOlderVersion
$taskManifest | ConvertTo-Json | Set-Content (Join-Path $taskRuntime 'app-release.json') -Encoding utf8
$taskOlderOutput=Join-Path $taskFixture 'older-setup'
& (Join-Path $PSScriptRoot 'build-installer.ps1') -RuntimeDirectory $taskRuntime -Version $taskOlderVersion -OutputDirectory $taskOlderOutput -CompilerPath $CompilerPath -FastFixture
$taskOlderSetup=Join-Path $taskOlderOutput 'pokemon-play-win-x64-setup.exe'
$taskNewSetup=Join-Path $ReleaseDirectory 'pokemon-play-win-x64-setup.exe'
& (Join-Path $PSScriptRoot 'test-installed-update.ps1') -UpdateZipPath (Join-Path $ReleaseDirectory 'pokemon-play-win-x64-update.zip') -OlderRuntimeDirectory $taskRuntime
& (Join-Path $PSScriptRoot 'test-installer.ps1') -SetupPath $taskOlderSetup -NewerSetupPath $taskNewSetup -OlderSetupPath $taskOlderSetup -TimeoutSeconds 600
Write-Output 'Installer lifecycle, upgrade, downgrade and data preservation verified.'
