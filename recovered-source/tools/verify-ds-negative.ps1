param([Parameter(Mandatory)][string]$Core,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
$cases=@(
    @{name='suppressed';flag='--suppress-input';phase='a-held';count=2;expected=1;observed=0},
    @{name='swapped-ab';flag='--swap-ds-ab';phase='a-held';count=2;expected=1;observed=2},
    @{name='sticky';flag='--sticky-ds-input';phase='a-released';count=3;expected=0;observed=1},
    @{name='extended-suppressed';flag='--suppress-ds-extended';phase='x-held';count=22;expected=1024;observed=0}
)
foreach($case in $cases) {
    $directory=Join-Path $OutputDirectory $case.name
    if(Test-Path -LiteralPath $directory){throw ('Negative evidence directory must be new: '+$directory)}
    & python (Join-Path $PSScriptRoot 'probe-libretro.py') --system ds $case.flag --core $Core --output $directory --frames 30
    if($LASTEXITCODE -eq 0){throw ('Oracle approved invalid input: '+$case.name)}
    $failure=Get-Content -LiteralPath (Join-Path $directory 'native-failure.json') -Raw|ConvertFrom-Json
    $result=Get-Content -LiteralPath (Join-Path $directory 'result.json') -Raw|ConvertFrom-Json
    $coreHash=(Get-FileHash -LiteralPath $Core -Algorithm SHA256).Hash.ToLowerInvariant()
    $romHash=(Get-FileHash -LiteralPath (Join-Path $directory 'original-input-test.nds') -Algorithm SHA256).Hash.ToLowerInvariant()
    $generatorHash=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'original_ds_test.py') -Algorithm SHA256).Hash.ToLowerInvariant()
    if($result.result -ne 'failed' -or $result.error -ne $failure.error -or $failure.core_sha256 -ne $coreHash -or $failure.rom_sha256 -ne $romHash -or $failure.generator_sha256 -ne $generatorHash){throw ('Current negative evidence mismatch: '+$case.name)}
    if($failure.error -ne ('DS keypad oracle rejected '+$case.phase) -or $failure.guest_input.Count -ne $case.count){throw ('Negative failed for another reason: '+$case.name)}
    $last=$failure.guest_input[-1]
    if($last.passed -or $last.phase -ne $case.phase -or $last.expected_mask -ne $case.expected -or $last.frames.Count -ne 2){throw ('Invalid negative phase: '+$case.name)}
    foreach($frame in $last.frames){if($frame.observed_mask -ne $case.observed -or $frame.regions.Count -ne 12){throw ('Wrong negative observation: '+$case.name)}}
    if(@($failure.guest_input|Select-Object -SkipLast 1|Where-Object{!$_.passed}).Count -ne 0){throw ('Earlier phases failed: '+$case.name)}
    Write-Output ('PASS DS negative '+$case.name+' rejects '+$case.phase+' with observed mask '+$case.observed)
}
# Expected child-process failures must not leak into the successful gate status.
exit 0
