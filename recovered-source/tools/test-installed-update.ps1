param(
    [Parameter(Mandatory)][string]$UpdateZipPath,
    [Parameter(Mandatory)][string]$OlderRuntimeDirectory
)
$ErrorActionPreference='Stop'
$taskRoot=Join-Path ([IO.Path]::GetTempPath()) ('PP atualização '+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $taskRoot | Out-Null
$taskRuntime=Join-Path $taskRoot 'PokemonPlayRuntime'
Copy-Item -LiteralPath $OlderRuntimeDirectory -Destination $taskRuntime -Recurse
$taskSentinels=@{}
foreach($relative in @('Saves/QA/progress.sav','Settings/SaveProfiles/QA.json','Settings/Emulators/RetroArch/test.cfg','Pokemon Bank/test.pk9','Backups/Automaticos/test.bin')){
    $path=Join-Path $taskRoot $relative;New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    [IO.File]::WriteAllText($path,'synthetic update sentinel');$taskSentinels[$relative]=(Get-FileHash $path).Hash
}
$taskStage=Join-Path $taskRoot ('.pokemonplay-update-'+[guid]::NewGuid().ToString('N'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path $UpdateZipPath))
try{
    if($taskZip.Entries.Count -gt 4096){throw 'Update exceeds legacy launcher entry limit.'}
    foreach($entry in $taskZip.Entries){if(!$entry.FullName.StartsWith('PokemonPlayRuntime/') -or $entry.FullName.Contains('..')){throw 'Unexpected update package path.'}}
}finally{$taskZip.Dispose()}
Expand-Archive -LiteralPath $UpdateZipPath -DestinationPath $taskStage
$taskExpected=Get-Content (Join-Path $taskStage 'PokemonPlayRuntime/app-release.json') -Raw | ConvertFrom-Json
$taskHelper=Join-Path $taskStage 'PokemonPlayUpdater.exe'
Copy-Item -LiteralPath (Join-Path $taskRuntime 'PokemonPlayUpdater.exe') -Destination $taskHelper
# Supply a PID that has exited, as the real launcher exits before applying.
$taskExited=Start-Process -FilePath $env:ComSpec -ArgumentList '/c exit 0' -WindowStyle Hidden -Wait -PassThru
$taskProcess=$null
try{
    $taskProcess=Start-Process -FilePath $taskHelper -ArgumentList @($taskExited.Id,('"'+$taskRoot+'"'),('"'+$taskStage+'"')) -WindowStyle Hidden -PassThru
    $taskTimer=[Diagnostics.Stopwatch]::StartNew()
    while(!(Test-Path (Join-Path $taskStage 'success')) -and !(Test-Path (Join-Path $taskStage 'failed')) -and $taskTimer.Elapsed.TotalSeconds -lt 100){Start-Sleep -Milliseconds 250}
    if(!(Test-Path (Join-Path $taskStage 'success'))){
        Write-Output "Helper exited: $($taskProcess.HasExited); stage failed marker: $(Test-Path (Join-Path $taskStage 'failed'))"
        foreach($candidateRoot in @($taskRuntime,(Join-Path $taskStage 'failed-runtime'))){
            if(Test-Path $candidateRoot){
                Get-ChildItem $candidateRoot -Filter 'emulator-extraction-error.log' -Recurse | ForEach-Object {Get-Content $_.FullName}
                Get-ChildItem $candidateRoot -Directory -Filter '.emulators-extract-*' | ForEach-Object {Write-Output "Incomplete extraction: $(@(Get-ChildItem $_.FullName -File -Recurse).Count) files"}
                Write-Output "Emulators ready: $(Test-Path (Join-Path $candidateRoot 'Emulators/THIRD_PARTY.txt'))"
            }
        }
        throw "Actual legacy updater failed or timed out. Evidence: $taskRoot"
    }
    $taskActual=Get-Content (Join-Path $taskRuntime 'app-release.json') -Raw | ConvertFrom-Json
    if($taskActual.version -ne $taskExpected.version){throw 'Version was not advanced.'}
    foreach($relative in $taskSentinels.Keys){if((Get-FileHash (Join-Path $taskRoot $relative)).Hash -ne $taskSentinels[$relative]){throw "Sentinel changed: $relative"}}
    foreach($relative in @('Emulators/RetroArch/retroarch.exe','Emulators/RetroArch/cores/mgba_libretro.dll','Emulators/RetroArch/cores/melondsds_libretro.dll','Emulators/Azahar/azahar.exe','Emulators/THIRD_PARTY.txt')){
        if(!(Test-Path (Join-Path $taskRuntime $relative))){throw "Updated emulator missing: $relative"}
    }
    if(Test-Path (Join-Path $taskRuntime 'emulators-runtime.zip')){throw 'Nested archive did not finish extraction before readiness.'}
    @{version=$taskActual.version;seconds=$taskTimer.Elapsed.TotalSeconds;sentinels=$taskSentinels;result='passed'} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $taskRoot 'update-result.json')
    Write-Output "PASS actual legacy helper update to $($taskActual.version), preserved data, expanded emulators. Evidence: $taskRoot"
}finally{
    # Stop only executable processes launched under this disposable fixture.
    foreach($candidate in Get-Process){
        try{$path=$candidate.MainModule.FileName;if($path.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){$candidate.Kill();$candidate.WaitForExit(10000)|Out-Null}}catch{}
    }
    Write-Output "Update fixture retained: $taskRoot"
}
