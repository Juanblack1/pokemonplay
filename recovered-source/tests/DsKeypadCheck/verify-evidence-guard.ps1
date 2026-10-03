$ErrorActionPreference='Stop'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ds-negative-guard-'+[guid]::NewGuid().ToString('N'))
try {
[IO.Directory]::CreateDirectory((Join-Path $fixture 'suppressed'))|Out-Null
Set-Content -LiteralPath (Join-Path $fixture 'suppressed/native-failure.json') -Value '{"error":"DS keypad oracle rejected a-held"}'
$wrapper=Join-Path $PSScriptRoot '../../tools/verify-ds-negative.ps1'
$rejected=$false
try{& $wrapper -Core (Join-Path $fixture 'missing.dll') -OutputDirectory $fixture}catch{if($_.Exception.Message -notlike 'Negative evidence directory must be new:*'){throw};$rejected=$true}
if(!$rejected){throw 'Stale evidence was accepted.'}
Write-Output 'PASS stale negative evidence rejected before loading missing DLL.'
$fresh=Join-Path $fixture 'fresh'
$rejected=$false
try{& $wrapper -Core (Join-Path $fixture 'missing.dll') -OutputDirectory $fresh}catch{$rejected=$true}
if(!$rejected){throw 'Missing DLL was counted as a valid negative.'}
$result=Get-Content -LiteralPath (Join-Path $fresh 'suppressed/result.json') -Raw|ConvertFrom-Json
if($result.result -ne 'failed' -or !$result.error.Contains('missing.dll') -or (Test-Path -LiteralPath (Join-Path $fresh 'suppressed/native-failure.json'))){throw 'Missing-DLL guard failed for another reason.'}
Write-Output 'PASS missing DLL cannot count as a valid guest-input negative.'
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    $temporary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if(![string]::Equals([IO.Path]::GetDirectoryName($resolved),$temporary,[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture cleanup escaped the temporary directory.'}
    if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
# Expected child-process failure is verified above, then the test exits successfully.
exit 0
