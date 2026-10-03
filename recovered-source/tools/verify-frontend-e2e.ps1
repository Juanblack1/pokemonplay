param(
    [ValidateSet('Prepare','Run','All')][string]$Mode='All',
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$BundleZip,
    [string]$BundleSha256,
    [string]$ReleaseTag,
    [Parameter(Mandatory)][string]$Commit,
    [string]$Python='python'
)
$ErrorActionPreference='Stop'
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
$taskPlanPath=Join-Path $taskOutput 'plan.json'
$taskEnvironmentNames=@('LIBRETRO_VIDEO_SHADER_DIRECTORY','LIBRETRO_VIDEO_FILTER_DIRECTORY','LIBRETRO_ASSETS_DIRECTORY','LIBRETRO_AUTOCONFIG_DIRECTORY','LIBRETRO_CHEATS_DIRECTORY','LIBRETRO_DATABASE_DIRECTORY','LIBRETRO_SYSTEM_DIRECTORY','LIBRETRO_DIRECTORY')

function Write-AtomicJson($Value,[string]$Path) {
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath ($Path+'.tmp') -Encoding utf8
    [IO.File]::Move($Path+'.tmp',$Path,$true)
}
function Assert-ChildPath([string]$Path,[string]$Parent) {
    $full=[IO.Path]::GetFullPath($Path)
    $prefix=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Parent))+[IO.Path]::DirectorySeparatorChar
    if(!$full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path is outside the owned fixture.'}
    Assert-NoReparse $full
    return $full
}
function Assert-NoReparse([string]$Path) {
    $current=[IO.Path]::GetFullPath($Path)
    while($current) {
        if(Test-Path -LiteralPath $current){if((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse point is forbidden in owned fixture ancestry.'}}
        $parent=[IO.Directory]::GetParent($current)
        $current=if($null -eq $parent){$null}else{$parent.FullName}
    }
}
function Get-FixtureHashes([string]$Root) {
    $paths=@{harnessExe='PokemonPlayRuntime/Check.exe';harnessDll='PokemonPlayRuntime/Check.dll';app='PokemonPlayRuntime/Pokemons Play.dll';retroarch='PokemonPlayRuntime/Emulators/RetroArch/retroarch.exe';core='PokemonPlayRuntime/Emulators/RetroArch/cores/mgba_libretro.dll';rom='roms/diagnostic.gba'}
    $hashes=@{}
    foreach($key in $paths.Keys){$path=Assert-ChildPath (Join-Path $Root $paths[$key]) $Root;$hashes[$key]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
    return $hashes
}
function Assert-Checkout {
    $repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $head=& git -C $repo rev-parse HEAD
    if($LASTEXITCODE -ne 0 -or $head -ne $Commit){throw 'Claimed commit differs from the actual checkout.'}
    $dirty=& git -C $repo status --porcelain
    if($LASTEXITCODE -ne 0 -or $dirty){throw 'Frontend build/execution requires a clean current checkout.'}
}
function Get-ExpectedExecutable([string]$Root) {
    return Join-Path $Root 'PokemonPlayRuntime/Emulators/RetroArch/retroarch.exe'
}
function Stop-OwnedChildren([string]$Root,[datetime]$NotBefore) {
    # Never kill by name. Enumeration is only a discovery step; each candidate
    # must match the exact GUID-owned executable and its observed start time.
    $expected=[IO.Path]::GetFullPath((Get-ExpectedExecutable $Root))
    $identityPath=Join-Path $Root 'evidence/child-identity.json'
    $identity=$null
    if(Test-Path -LiteralPath $identityPath){$identity=Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json}
    $stopped=@()
    $candidates=if($null -ne $identity){try{@([Diagnostics.Process]::GetProcessById([int]$identity.pid))}catch [ArgumentException]{@()}}else{@([Diagnostics.Process]::GetProcessesByName('retroarch'))}
    $ambiguities=@()
    foreach($candidate in $candidates) {
        try {
            $candidatePath=$candidate.MainModule.FileName
            $observedStart=$candidate.StartTime.ToUniversalTime()
            if(![string]::Equals([IO.Path]::GetFullPath($candidatePath),$expected,[StringComparison]::OrdinalIgnoreCase)){if($null -ne $identity){throw 'Recorded child PID now points outside its owned executable.'};continue}
            if($observedStart -lt $NotBefore){throw 'Owned-path process predates this run; refusing to kill.'}
            if($null -ne $identity) {
                $identityStart=[datetime]::Parse($identity.startUtc,$null,[Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
                if($candidate.Id -ne $identity.pid -or $observedStart.Ticks -ne $identityStart.Ticks -or ![string]::Equals([IO.Path]::GetFullPath($identity.path),$expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Child identity mismatch; refusing to kill.'}
            }
            # Revalidate immediately before acting, including PID reuse guard.
            $check=[Diagnostics.Process]::GetProcessById($candidate.Id)
            if($check.StartTime.ToUniversalTime().Ticks -ne $observedStart.Ticks -or ![string]::Equals($check.MainModule.FileName,$expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Process identity changed; refusing to kill.'}
            $check.Kill()
            if(!$check.WaitForExit(10000)){throw 'Owned child did not exit after emergency cleanup.'}
            $stopped+=@{pid=$candidate.Id;startUtc=$observedStart.ToString('O');reason='emergency_cleanup_does_not_prove_key_release'}
        } catch {
            $ambiguities+=@{pid=$candidate.Id;reason=$_.Exception.Message}
        } finally {$candidate.Dispose()}
    }
    if($ambiguities.Count -gt 0){Write-AtomicJson @{ambiguous=$ambiguities;stopped=$stopped} (Join-Path $Root 'evidence/cleanup-ambiguity.json');throw 'Process cleanup identity/access was ambiguous; no ambiguous candidate was killed.'}
    return @($stopped)
}
function Require-Status($Result,[string]$Name,[string]$Expected) {
    $property=$Result.checks.PSObject.Properties[$Name]
    if($null -eq $property -or $property.Value.status -ne $Expected){throw "Required result status missing or incorrect: $Name ($Expected)."}
}
function Assert-RunPlan($Plan,[string]$ExpectedCommit) {
    if($Plan.schema -ne 1 -or $Plan.commit -ne $ExpectedCommit -or $Plan.runs.Count -ne 2){throw 'Current run plan identity mismatch.'}
    if(@($Plan.runs|Where-Object scenario -eq 'positive').Count -ne 1 -or @($Plan.runs|Where-Object scenario -eq 'suppressed-a').Count -ne 1 -or @($Plan.runs.root|ForEach-Object{[IO.Path]::GetFullPath($_).ToLowerInvariant()}|Select-Object -Unique).Count -ne 2){throw 'Plan must contain one positive and one negative in distinct roots.'}
}
function Assert-RunIdentity($Result,$Run,[string]$Root,[string]$ExpectedCommit,$Hashes) {
    if($Result.schema -ne 1 -or $Result.commit -ne $ExpectedCommit -or $Result.scenario -ne $Run.scenario){throw 'Returned scenario/schema/commit mismatch.'}
    if($Result.identity.runId -ne $Run.runId -or ![string]::Equals([IO.Path]::GetFullPath($Result.identity.root),$Root,[StringComparison]::OrdinalIgnoreCase)){throw 'Returned run/root identity mismatch.'}
    foreach($key in $Hashes.Keys){if($Result.identity.hashes.PSObject.Properties[$key].Value -ne $Hashes[$key]){throw 'Returned binary/ROM identity mismatch.'}}
}
function Test-FinalRunPass([bool]$Candidate,[bool]$TimedOut,[int]$EmergencyCount,[bool]$ForcedHarness,[bool]$CleanupError) {
    return $Candidate -and !$TimedOut -and $EmergencyCount -eq 0 -and !$ForcedHarness -and !$CleanupError
}
function Stop-OwnedHarness($Process,[string]$Expected,[datetime]$Started) {
    if($Process.HasExited){return $false}
    $check=[Diagnostics.Process]::GetProcessById($Process.Id)
    try {
        if($check.StartTime.ToUniversalTime().Ticks -ne $Started.Ticks -or ![string]::Equals($check.MainModule.FileName,$Expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Harness identity changed; refusing to kill.'}
        $check.Kill()
        if(!$check.WaitForExit(10000)){throw 'Owned harness watchdog cleanup failed.'}
        return $true
    } finally {$check.Dispose()}
}

if($Mode -in @('Prepare','All')) {
    Assert-Checkout
    Assert-NoReparse $taskOutput
    if($Commit -notmatch '^[0-9a-f]{40}$' -or $BundleSha256 -notmatch '^[0-9a-f]{64}$' -or $ReleaseTag -notmatch '^v\d+\.\d+\.\d+$'){throw 'Expected fixed commit, public release tag and ZIP digest.'}
    if(Test-Path -LiteralPath $taskOutput){throw 'Output must be fresh; refusing to reuse old run evidence.'}
    if((Get-FileHash -LiteralPath $BundleZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $BundleSha256){throw 'Public bundle ZIP digest mismatch.'}
    [IO.Directory]::CreateDirectory($taskOutput)|Out-Null
    $taskBundle=Join-Path $taskOutput 'bundle'
    Expand-Archive -LiteralPath $BundleZip -DestinationPath $taskBundle
    $taskManifest=Get-Content -LiteralPath (Join-Path $taskBundle 'PokemonPlayRuntime/app-release.json') -Raw | ConvertFrom-Json
    if($taskManifest.version -ne $ReleaseTag -or $taskManifest.repository -ne 'Juanblack1/pokemonplay'){throw 'Bundle manifest identity mismatch.'}
    Expand-Archive -LiteralPath (Join-Path $taskBundle 'PokemonPlayRuntime/emulators-runtime.zip') -DestinationPath (Join-Path $taskBundle 'expanded-emulators')
    $taskRetroArch=Join-Path $taskBundle 'expanded-emulators/Emulators/RetroArch'
    foreach($relative in @('retroarch.exe','cores/mgba_libretro.dll')){if(!(Test-Path -LiteralPath (Join-Path $taskRetroArch $relative))){throw 'Required real bundled binary missing.'}}
    $taskBuild=Join-Path $taskOutput 'harness-build'
    & dotnet publish (Join-Path $PSScriptRoot '../tests/FrontendE2E/FrontendE2E.csproj') -c Release -r win-x64 --self-contained false -o $taskBuild
    if($LASTEXITCODE -ne 0){throw 'Frontend harness build failed.'}
    if(!(Test-Path -LiteralPath (Join-Path $taskBuild 'Check.exe'))){throw 'Harness apphost missing.'}
    $taskRuns=@()
    foreach($scenario in @('positive','suppressed-a')) {
        $root=Join-Path $taskOutput ('run-'+[guid]::NewGuid().ToString('N'))
        Assert-ChildPath $root $taskOutput | Out-Null
        $runtime=Join-Path $root 'PokemonPlayRuntime'
        foreach($relative in @('PokemonPlayRuntime','PokemonPlayRuntime/Emulators','Settings','roms','temp','sessions','evidence','captures/inbox')){[IO.Directory]::CreateDirectory((Join-Path $root $relative))|Out-Null}
        Get-ChildItem -LiteralPath $taskBuild | Copy-Item -Destination $runtime -Recurse
        Copy-Item -LiteralPath $taskRetroArch -Destination (Join-Path $runtime 'Emulators') -Recurse
        $rom=Join-Path $root 'roms/diagnostic.gba'
        & $Python -B -c 'import sys; from pathlib import Path; sys.path.insert(0,sys.argv[1]); from original_gba_frontend_test import original_gba_frontend_test_rom; original_gba_frontend_test_rom(Path(sys.argv[2]))' $PSScriptRoot $rom
        if($LASTEXITCODE -ne 0){throw 'Original diagnostic generator failed.'}
        $profile=@{Mode=3;TestConsole=0;ControllerSlot=0;DeadZone=24;ExtraKeys=@('C','V');Bindings=@('PadUp','PadDown','PadLeft','PadRight','PadA','PadB','PadLB','PadRB','PadStart','PadBack','PadX','PadY')}
        Write-AtomicJson $profile (Join-Path $root 'Settings/input-device.json')
        Set-Content -LiteralPath (Join-Path $root 'Settings/input-presets.txt') -Value '1' -Encoding utf8
        $import=@(@{Id='frontend-diagnostic';RomPath=$rom;Title='Original GBA diagnostic';BaseGame='Original';Generation=3;IsHackRom=$false})
        Write-AtomicJson $import (Join-Path $root 'Settings/ImportedPokemonGames.json')
        $socket=[Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,0))
        try{$port=$socket.Client.LocalEndPoint.Port}finally{$socket.Dispose()}
        if($port -lt 49152 -or $port -gt 65535){throw 'Ephemeral UDP port outside allowed range.'}
        # Reservation alone is insufficient. Harness corroborates listener PID
        # after the real process starts, and before every SCREENSHOT request.
        $taskRuns+=@{root=$root;runId=[IO.Path]::GetFileName($root);scenario=$scenario;port=$port;hashes=(Get-FixtureHashes $root)}
    }
    Write-AtomicJson @{schema=1;commit=$Commit;release=$ReleaseTag;bundleSha256=$BundleSha256;runs=$taskRuns} $taskPlanPath
    Write-Output 'Prepared two fresh owned frontend fixtures; no native test executed.'
}

if($Mode -in @('Run','All')) {
    if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows'){throw 'Native frontend execution is restricted to the dedicated Windows CI; local App Control is not bypassed.'}
    Assert-Checkout
    Assert-NoReparse $taskPlanPath
    $taskPlan=Get-Content -LiteralPath $taskPlanPath -Raw | ConvertFrom-Json
    Assert-RunPlan $taskPlan $Commit
    $taskResults=@()
    $taskUnsafeToContinue=$false
    foreach($run in $taskPlan.runs) {
        $root=Assert-ChildPath $run.root $taskOutput
        $runtime=Join-Path $root 'PokemonPlayRuntime'
        $evidence=Join-Path $root 'evidence'
        if($taskUnsafeToContinue){$taskResults+=@{scenario=$run.scenario;root=$root;passed=$false;error='not_run_after_unverified_input_cleanup'};continue}
        foreach($relative in @('evidence','captures/inbox','sessions')){$owned=Assert-ChildPath (Join-Path $root $relative) $root;if(@(Get-ChildItem -LiteralPath $owned -Recurse -Force -File).Count -gt 0){throw 'Stale evidence/session/capture files exist before execution.'}}
        if($run.runId -ne [IO.Path]::GetFileName($root)){throw 'Run nonce/root mismatch.'}
        $actualHashes=Get-FixtureHashes $root
        foreach($key in $actualHashes.Keys){if($run.hashes.PSObject.Properties[$key].Value -ne $actualHashes[$key]){throw 'Fixture changed since preparation.'}}
        $startedPath=Join-Path $evidence 'started.json'
        $marker=[IO.File]::Open($startedPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        $marker.Dispose()
        Write-AtomicJson @{runId=$run.runId;startedUtc=[datetime]::UtcNow.ToString('O');hashes=$actualHashes} $startedPath
        if($run.scenario -notin @('positive','suppressed-a') -or $run.port -lt 49152 -or $run.port -gt 65535){throw 'Invalid run scenario or port.'}
        $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $runtime 'Check.exe'))
        $start.UseShellExecute=$false
        $start.WorkingDirectory=$runtime
        $start.RedirectStandardOutput=$true
        $start.RedirectStandardError=$true
        foreach($argument in @('--root',$root,'--scenario',$run.scenario,'--port',[string]$run.port,'--commit',$Commit)){$start.ArgumentList.Add($argument)}
        $start.Environment['TEMP']=Join-Path $root 'temp'
        $start.Environment['TMP']=Join-Path $root 'temp'
        $start.Environment['POKEMONPLAY_RETROARCH_TEMP']=Join-Path $root 'sessions'
        foreach($name in $taskEnvironmentNames){$start.Environment.Remove($name)|Out-Null}
        $notBefore=[datetime]::UtcNow
        $process=[Diagnostics.Process]::Start($start)
        $processStart=$process.StartTime.ToUniversalTime()
        $stdout=$process.StandardOutput.ReadToEndAsync()
        $stderr=$process.StandardError.ReadToEndAsync()
        $clock=[Diagnostics.Stopwatch]::StartNew()
        $timedOut=$false
        $emergency=@()
        $forcedHarness=$false
        $cleanupError=$false
        $result=$null
        try {
            while(!$process.WaitForExit(250)) {
                if($clock.Elapsed.TotalSeconds -ge 120){$timedOut=$true;break}
            }
            if($timedOut) {
                Write-AtomicJson @{schema=1;scenario=$run.scenario;commit=$Commit;passed=$false;reason='external_wall_watchdog';elapsed=$clock.Elapsed.TotalSeconds} (Join-Path $evidence 'supervisor-timeout.json')
                $emergency=@(Stop-OwnedChildren $root $notBefore)
                Stop-OwnedHarness $process (Join-Path $runtime 'Check.exe') $processStart | Out-Null
            }
            if(!$stdout.Wait(5000) -or !$stderr.Wait(5000)){throw 'Bounded stdout/stderr collection timed out.'}
            $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $evidence 'harness-stdout.log') -Encoding utf8
            $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $evidence 'harness-stderr.log') -Encoding utf8
            $emergency+=@(Stop-OwnedChildren $root $notBefore)
            $result=Get-Content -LiteralPath (Join-Path $evidence 'result.json') -Raw | ConvertFrom-Json
            if($timedOut -or $emergency.Count -gt 0 -or $process.ExitCode -ne 0 -or $result.schema -ne 1 -or $result.commit -ne $Commit -or $result.scenario -ne $run.scenario -or !$result.passed){throw 'Frontend run did not provide valid successful current evidence.'}
            Assert-RunIdentity $result $run $root $Commit $actualHashes
            foreach($name in @('boot','process_identity','root_identity','embedding','focus','cadence','neutral','A-release','cleanup','default_driver','configuration','capture_identity')){Require-Status $result $name 'passed'}
            if($run.scenario -eq 'positive') {
                foreach($name in @('A-held','B-held','B-release')){Require-Status $result $name 'passed'}
            } else {
                Require-Status $result 'A-held' 'failed'
                Require-Status $result 'guest_hold_color' 'failed'
                Require-Status $result 'negative_validity' 'passed'
                foreach($name in @('B-held','B-release')){Require-Status $result $name 'not_run'}
            }
            $taskResults+=@{scenario=$run.scenario;root=$root;passed=$true;elapsed=$clock.Elapsed.TotalSeconds}
        } catch {
            $taskResults+=@{scenario=$run.scenario;root=$root;passed=$false;elapsed=$clock.Elapsed.TotalSeconds;error=$_.Exception.Message}
        } finally {
            # A child cleanup exception must not leave a hung owned STA harness.
            try {if(Stop-OwnedHarness $process (Join-Path $runtime 'Check.exe') $processStart){$forcedHarness=$true;$taskResults[-1].passed=$false;$taskUnsafeToContinue=$true}}catch{$cleanupError=$true;$taskResults[-1].passed=$false;$taskResults[-1].harnessCleanupError=$_.Exception.Message;$taskUnsafeToContinue=$true}
            try {$lastEmergency=@(Stop-OwnedChildren $root $notBefore);if($lastEmergency.Count -gt 0){$emergency+=$lastEmergency;$taskResults[-1].passed=$false}}catch{$cleanupError=$true;$taskResults[-1].passed=$false;$taskResults[-1].cleanupError=$_.Exception.Message;$taskUnsafeToContinue=$true}
            if($timedOut -or $emergency.Count -gt 0){$taskUnsafeToContinue=$true}
            if($null -eq $result -or $null -eq $result.checks.cleanup -or $result.checks.cleanup.status -ne 'passed'){$taskUnsafeToContinue=$true}
            $taskResults[-1].passed=Test-FinalRunPass $taskResults[-1].passed $timedOut $emergency.Count $forcedHarness $cleanupError
            Write-AtomicJson @{timedOut=$timedOut;emergency=$emergency;forcedHarness=$forcedHarness;cleanupError=$cleanupError;passed=$taskResults[-1].passed;elapsed=$clock.Elapsed.TotalSeconds} (Join-Path $evidence 'supervisor.json')
            $process.Dispose()
        }
    }
    Write-AtomicJson @{schema=1;commit=$Commit;release=$taskPlan.release;bundleSha256=$taskPlan.bundleSha256;runs=$taskResults;passed=(@($taskResults|Where-Object{!$_.passed}).Count -eq 0)} (Join-Path $taskOutput 'result.json')
    if(@($taskResults|Where-Object{!$_.passed}).Count -ne 0){throw 'Default frontend positive/valid-negative gate failed. Owned evidence retained.'}
    Write-Output 'Default real launcher frontend positive and valid negative verified.'
}
