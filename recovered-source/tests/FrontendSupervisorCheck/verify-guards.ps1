$ErrorActionPreference='Stop'
$taskSource=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tools/verify-frontend-e2e.ps1'))
$taskTokens=$null;$taskErrors=$null
$taskAst=[Management.Automation.Language.Parser]::ParseFile($taskSource,[ref]$taskTokens,[ref]$taskErrors)
if($taskErrors.Count){throw 'Supervisor syntax errors.'}
# Load only these pure production functions. Never evaluate provisioning,
# Process.Start/kill or a native frontend while checking false-positive guards.
$taskNames=@('Assert-RunPlan','Assert-RunIdentity','Require-Status','Test-FinalRunPass','Write-AtomicJson','New-DiagnosticCatalog')
$taskFunctions=$taskAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)
foreach($name in $taskNames){$function=$taskFunctions|Where-Object Name -eq $name;if(@($function).Count -ne 1){throw 'Required guard missing.'};. ([scriptblock]::Create($function.Extent.Text))}
$taskCount=0
# Exercise the actual fixture writer: a one-game catalog must remain an array.
$taskJsonRoot=Join-Path ([IO.Path]::GetTempPath()) ('frontend-json-guard-'+[guid]::NewGuid().ToString('N'))
$null=[IO.Directory]::CreateDirectory($taskJsonRoot)
$taskJsonPath=Join-Path $taskJsonRoot 'fixture.json'
try {
    foreach($case in @(
        @{value=@(@{Id='one';RomPath='diagnostic.gba'});kind='Array';length=1},
        @{value=@(@{Id='one'},@{Id='two'});kind='Array';length=2},
        @{value=@();kind='Array';length=0},
        @{value=@{schema=1;runs=@(@{scenario='positive'})};kind='Object';length=-1}
    )) {
        Write-AtomicJson $case.value $taskJsonPath
        $document=[System.Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($taskJsonPath))
        try {
            if($document.RootElement.ValueKind.ToString() -ne $case.kind){throw 'Fixture JSON root shape changed during serialization.'}
            if($case.length -ge 0 -and $document.RootElement.GetArrayLength() -ne $case.length){throw 'Fixture JSON array length changed.'}
            $taskCount++
        } finally {$document.Dispose()}
    }
    $catalog=New-DiagnosticCatalog (Join-Path $taskJsonRoot 'diagnostic.gba')
    Write-AtomicJson $catalog $taskJsonPath
    $loaded=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($taskJsonPath)) -NoEnumerate
    $catalogId=[guid]::Empty
    if($loaded -isnot [array] -or $loaded.Count -ne 1 -or ![guid]::TryParseExact($loaded[0].Id,'N',[ref]$catalogId)){throw 'Diagnostic catalog does not satisfy the production GUID/list contract.'}
    if($loaded[0].RomPath -ne (Join-Path $taskJsonRoot 'diagnostic.gba') -or $loaded[0].Generation -ne 3 -or $loaded[0].IsHackRom -ne $false -or [string]::IsNullOrWhiteSpace($loaded[0].Title) -or $loaded[0].Title.Length -gt 80 -or [string]::IsNullOrWhiteSpace($loaded[0].BaseGame)){throw 'Diagnostic catalog metadata differs from the owned GBA fixture.'}
    $taskCount++
} finally {
    foreach($ownedFile in @($taskJsonPath,($taskJsonPath+'.tmp'))){if(Test-Path -LiteralPath $ownedFile){Remove-Item -LiteralPath $ownedFile}}
    [IO.Directory]::Delete($taskJsonRoot,$false)
}
function Must-Fail([scriptblock]$Action,[string]$Label) {
    $rejected=$false
    try{& $Action}catch{$rejected=$true}
    if(!$rejected){throw "Guard accepted invalid evidence: $Label"}
    $script:taskCount++
}
$taskCommit='a'*40
$taskRoot=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'frontend-guard-owned/run-one'))
$taskOther=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'frontend-guard-owned/run-two'))
function Plan($First='positive',$Second='suppressed-a',$SecondRoot=$taskOther) {
    return [pscustomobject]@{schema=1;commit=$taskCommit;runs=@([pscustomobject]@{scenario=$First;root=$taskRoot},[pscustomobject]@{scenario=$Second;root=$SecondRoot})}
}
Assert-RunPlan (Plan) $taskCommit;$taskCount++
Must-Fail {Assert-RunPlan (Plan 'positive' 'positive') $taskCommit} 'two positives'
Must-Fail {Assert-RunPlan (Plan 'suppressed-a' 'suppressed-a') $taskCommit} 'two negatives'
Must-Fail {Assert-RunPlan (Plan 'positive' 'suppressed-a' $taskRoot) $taskCommit} 'same root reused'
Must-Fail {Assert-RunPlan (Plan) ('b'*40)} 'wrong checkout identity'
$taskHashes=@{harnessExe='1'*64;harnessDll='2'*64;app='3'*64;retroarch='4'*64;core='5'*64;rom='6'*64}
$taskRun=[pscustomobject]@{runId='run-one';scenario='positive'}
function Result {
    return [pscustomobject]@{schema=1;commit=$taskCommit;scenario='positive';identity=[pscustomobject]@{root=$taskRoot;runId='run-one';hashes=[pscustomobject]$taskHashes.Clone()};checks=[pscustomobject]@{cleanup=[pscustomobject]@{status='passed'}}}
}
Assert-RunIdentity (Result) $taskRun $taskRoot $taskCommit $taskHashes;$taskCount++
foreach($key in $taskHashes.Keys){$taskKey=$key;Must-Fail {$changed=Result;$changed.identity.hashes.$taskKey='f'*64;Assert-RunIdentity $changed $taskRun $taskRoot $taskCommit $taskHashes} "mutated $taskKey"}
Must-Fail {$changed=Result;$changed.identity.root=$taskOther;Assert-RunIdentity $changed $taskRun $taskRoot $taskCommit $taskHashes} 'wrong root'
Must-Fail {$changed=Result;$changed.identity.runId='other';Assert-RunIdentity $changed $taskRun $taskRoot $taskCommit $taskHashes} 'wrong nonce'
Must-Fail {$changed=Result;$changed.scenario='suppressed-a';Assert-RunIdentity $changed $taskRun $taskRoot $taskCommit $taskHashes} 'wrong scenario'
Require-Status (Result) 'cleanup' 'passed';$taskCount++
Must-Fail {Require-Status (Result) 'not-present' 'passed'} 'missing required subcheck'
Must-Fail {$changed=Result;$changed.checks.cleanup.status='not_run';Require-Status $changed 'cleanup' 'passed'} 'not-run cleanup'
Must-Fail {$changed=Result;$changed.checks.cleanup.status='failed';Require-Status $changed 'cleanup' 'passed'} 'failed cleanup'
if(!(Test-FinalRunPass $true $false 0 $false $false)){throw 'Valid final completion rejected.'};$taskCount++
foreach($flags in @(@($true,$false,1,$false,$false),@($true,$true,0,$false,$false),@($true,$false,0,$true,$false),@($true,$false,0,$false,$true),@($false,$false,0,$false,$false))){if(Test-FinalRunPass @flags){throw 'Emergency/timeout/cleanup failure accepted as success.'};$taskCount++}
Write-Output "PASS: $taskCount pure supervisor evidence guards; no native process executed."
