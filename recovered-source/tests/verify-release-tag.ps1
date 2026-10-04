$ErrorActionPreference = 'Stop'
$tool = Join-Path $PSScriptRoot '..\tools\verify-release-tag.ps1'
$current = 'a' * 40
$stale = 'b' * 40

& $tool -TagCommit $current -DefaultBranchCommit $current -DefaultBranch 'main' | Out-Null

$staleRejected = $false
try {
    & $tool -TagCommit $stale -DefaultBranchCommit $current -DefaultBranch 'main' | Out-Null
} catch {
    $staleRejected = $_.Exception.Message -match 'must target the current HEAD'
}
if (-not $staleRejected) { throw 'A stale release tag was not rejected.' }

$malformedRejected = $false
try {
    & $tool -TagCommit 'not-a-sha' -DefaultBranchCommit $current -DefaultBranch 'main' | Out-Null
} catch {
    $malformedRejected = $_.Exception.Message -match 'full commit SHAs'
}
if (-not $malformedRejected) { throw 'A malformed release commit was not rejected.' }

Write-Output 'Release tag guard accepts the current commit and rejects stale or malformed commits.'
