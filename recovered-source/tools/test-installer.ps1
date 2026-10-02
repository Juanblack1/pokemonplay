<#
Runs real silent installation on a disposable Windows user/VM. Retains evidence.
Does not prove clean Windows support, launcher startup or internal updater behavior.
OlderSetupPath must be strictly older; NewerSetupPath must be strictly newer.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SetupPath,
    [string]$OlderSetupPath,
    [string]$NewerSetupPath,
    [switch]$TestDesktopShortcut,
    [ValidateRange(30,1800)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This test requires Windows.' }
foreach ($candidate in @($SetupPath,$OlderSetupPath,$NewerSetupPath)) {
    if ($candidate -and !(Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Missing setup: $candidate" }
}
# A fixed AppId shares uninstall registration regardless of /DIR. Refuse real installs.
foreach ($key in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
    foreach ($entry in Get-ChildItem $key -ErrorAction SilentlyContinue) {
        $registration = Get-ItemProperty $entry.PSPath
        if ($registration.DisplayName -match '(?i)pok[eé]mons?\s*play') {
            throw 'Pokemon Play is already registered. Use a disposable Windows user/VM.'
        }
    }
}
if (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'Programs\PokemonPlay')) {
    throw 'Default PokemonPlay directory exists. Use a disposable Windows user/VM.'
}
$id = [Guid]::NewGuid().ToString('N')
$evidenceRoot = Join-Path ([IO.Path]::GetTempPath()) "Pokemon Play QA ação $id"
$installRoot = Join-Path ([IO.Path]::GetTempPath()) ('PP ação '+$id.Substring(0,8))
$groupName = "PokemonPlay QA $id"
$groupRoot = Join-Path ([Environment]::GetFolderPath('Programs')) $groupName
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Pokémon Play.lnk'
if ($TestDesktopShortcut -and (Test-Path -LiteralPath $desktopShortcut)) {
    throw 'Desktop shortcut already exists. Refusing to overwrite it.'
}
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
$results = [Collections.Generic.List[object]]::new()
$sentinels = @{}
function Assert-Check([bool]$condition,[string]$name) {
    $results.Add([pscustomobject]@{check=$name;result=$(if($condition){'passed'}else{'failed'})})
    if (!$condition) { throw "FAIL: $name" }
    Write-Host "PASS: $name"
}
function Invoke-Setup([string]$exe,[string]$phase,[bool]$expectFailure = $false) {
    $log = Join-Path $evidenceRoot "$phase.log"
    $arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$log`"")
    if ($phase -notin @('uninstall','final-uninstall')) {
        $taskSelection = if ($TestDesktopShortcut) { '/TASKS="desktopicon"' } else { '/TASKS=""' }
        $arguments += @("/DIR=`"$installRoot`"", "/GROUP=`"$groupName`"", $taskSelection)
    }
    $process = Start-Process -FilePath (Resolve-Path -LiteralPath $exe).Path -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill(); $process.WaitForExit()
        throw "Timed out: $phase. Evidence: $log"
    }
    $process.Refresh()
    $results.Add([pscustomobject]@{check=$phase;exitCode=$process.ExitCode;log=$log})
    Assert-Check (Test-Path -LiteralPath $log) "$phase produced log"
    Assert-Check $(if($expectFailure){$process.ExitCode -ne 0}else{$process.ExitCode -eq 0}) "$phase exit code"
}
function Assert-Sentinels([string]$phase) {
    foreach ($relative in $sentinels.Keys) {
        $path = Join-Path $installRoot $relative
        Assert-Check ((Test-Path -LiteralPath $path) -and ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sentinels[$relative])) "$phase preserves $relative"
    }
}
function Read-Version {
    return (Get-Content -LiteralPath (Join-Path $installRoot 'PokemonPlayRuntime\app-release.json') -Raw | ConvertFrom-Json).version
}
function Convert-Version([string]$tag) {
    $parts = $tag.TrimStart('v').Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return [version]($parts -join '.')
}
function Assert-Runtime {
    foreach ($file in @('Pokemons Play.exe','PokemonPlayUpdater.exe','app-release.json','UpdateSource.json',
        'Emulators\RetroArch\retroarch.exe','Emulators\RetroArch\cores\mgba_libretro.dll',
        'Emulators\RetroArch\cores\melondsds_libretro.dll','Emulators\Azahar\azahar.exe','Emulators\THIRD_PARTY.txt')) {
        Assert-Check (Test-Path -LiteralPath (Join-Path $installRoot "PokemonPlayRuntime\$file")) "runtime contains $file"
    }
    $links = @(Get-ChildItem -LiteralPath $groupRoot -Filter '*.lnk' -ErrorAction SilentlyContinue)
    $shell = New-Object -ComObject WScript.Shell
    $target = Join-Path $installRoot 'PokemonPlayRuntime\Pokemons Play.exe'
    Assert-Check (@($links | Where-Object { $shell.CreateShortcut($_.FullName).TargetPath -eq $target }).Count -ge 1) 'Start menu shortcut points to installed launcher'
    if ($TestDesktopShortcut) {
        Assert-Check ((Test-Path -LiteralPath $desktopShortcut) -and ($shell.CreateShortcut($desktopShortcut).TargetPath -eq $target)) 'optional desktop shortcut points to installed launcher'
    }
}
try {
    Invoke-Setup $SetupPath 'install'
    Assert-Runtime
    $initialVersion = Read-Version
    foreach ($relative in @('Saves\QA Game\progress.sav','Saves\ProfileSaves\QA Game\qa-profile\progress.sav',
        'Settings\SaveProfiles\QA Game.json','Pokemon Bank\qa-sentinel.pk9',
        'Settings\qa-sentinel.json','Settings\Emulators\RetroArch\qa-sentinel.cfg',
        'Settings\Emulators\Azahar\qa-sentinel.ini','Backups\Automaticos\qa-sentinel.bin',
        'Saves\Backups\Automaticos\qa-sentinel.bin','Emulators\qa-sentinel.ini',
        'Roms\qa-sentinel.txt','user-note.txt')) {
        $path = Join-Path $installRoot $relative
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        [IO.File]::WriteAllText($path,"Synthetic QA $id $relative")
        $sentinels[$relative] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    $sentinels | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidenceRoot 'sentinel-hashes.json')
    # Lock a versioned runtime DLL without launching GUI or touching real data.
    $runtimeDll = Join-Path $installRoot 'PokemonPlayRuntime\Pokemons Play.dll'
    $beforeLockedHash = (Get-FileHash -LiteralPath $runtimeDll -Algorithm SHA256).Hash
    $beforeLockedExeHash = (Get-FileHash -LiteralPath (Join-Path $installRoot 'PokemonPlayRuntime\Pokemons Play.exe') -Algorithm SHA256).Hash
    $lockedStream = [IO.File]::Open($runtimeDll,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
    try { Invoke-Setup $SetupPath 'locked-reinstall' $true }
    finally { $lockedStream.Dispose() }
    Assert-Check ((Get-FileHash -LiteralPath $runtimeDll -Algorithm SHA256).Hash -eq $beforeLockedHash -and
        (Get-FileHash -LiteralPath (Join-Path $installRoot 'PokemonPlayRuntime\Pokemons Play.exe') -Algorithm SHA256).Hash -eq $beforeLockedExeHash) 'locked-runtime rejection preserves runtime hashes'
    Assert-Sentinels 'locked-reinstall'
    Invoke-Setup $SetupPath 'reinstall'
    Assert-Sentinels 'reinstall'; Assert-Runtime
    Assert-Check ((Read-Version) -eq $initialVersion) 'same-version reinstall retains version'
    if ($NewerSetupPath) {
        Invoke-Setup $NewerSetupPath 'newer-install'
        Assert-Check ((Convert-Version (Read-Version)) -gt (Convert-Version $initialVersion)) 'newer installer upgrades version'
        Assert-Sentinels 'newer-install'; Assert-Runtime
    }
    if ($OlderSetupPath) {
        $beforeVersion = Read-Version
        $beforeHash = (Get-FileHash -LiteralPath (Join-Path $installRoot 'PokemonPlayRuntime\Pokemons Play.exe')).Hash
        Invoke-Setup $OlderSetupPath 'older-install' $true
        Assert-Check ((Read-Version) -eq $beforeVersion -and (Get-FileHash -LiteralPath (Join-Path $installRoot 'PokemonPlayRuntime\Pokemons Play.exe')).Hash -eq $beforeHash) 'older installer cannot replace current runtime'
        Assert-Sentinels 'older-install'
    }
    # Simulate a file introduced by the internal updater after initial setup.
    [IO.File]::WriteAllText((Join-Path $installRoot 'PokemonPlayRuntime\qa-new-version-file.bin'),'managed runtime fixture')
    $uninstallers = @(Get-ChildItem -LiteralPath $installRoot -Filter 'unins*.exe')
    Assert-Check ($uninstallers.Count -eq 1) 'exactly one uninstaller'
    Invoke-Setup $uninstallers[0].FullName 'uninstall'
    Assert-Sentinels 'uninstall'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $installRoot 'PokemonPlayRuntime'))) 'uninstall removes managed runtime including new files'
    Assert-Check (!(Test-Path -LiteralPath $groupRoot)) 'uninstall removes isolated Start menu group'
    if ($TestDesktopShortcut) { Assert-Check (!(Test-Path -LiteralPath $desktopShortcut)) 'uninstall removes optional desktop shortcut' }
    $finalSetup = if($NewerSetupPath){$NewerSetupPath}else{$SetupPath}
    Invoke-Setup $finalSetup 'install-after-uninstall'
    Assert-Sentinels 'install-after-uninstall'; Assert-Runtime
    $finalUninstaller = @(Get-ChildItem -LiteralPath $installRoot -Filter 'unins*.exe')
    Assert-Check ($finalUninstaller.Count -eq 1) 'final installation has one uninstaller'
    Invoke-Setup $finalUninstaller[0].FullName 'final-uninstall'
    Assert-Sentinels 'final-uninstall'
    if ($TestDesktopShortcut) { Assert-Check (!(Test-Path -LiteralPath $desktopShortcut)) 'final uninstall removes optional desktop shortcut' }
} finally {
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidenceRoot 'results.json')
    Write-Host "Evidence retained: $evidenceRoot"
    # No recursive deletion: preserve all evidence and synthetic sentinels.
}
