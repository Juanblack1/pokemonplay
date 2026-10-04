[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$TagCommit,
    [Parameter(Mandatory=$true)][string]$DefaultBranchCommit,
    [Parameter(Mandatory=$true)][string]$DefaultBranch
)

$ErrorActionPreference = 'Stop'
$tagSha = $TagCommit.Trim().ToLowerInvariant()
$defaultSha = $DefaultBranchCommit.Trim().ToLowerInvariant()
if ($tagSha -notmatch '^[0-9a-f]{40}$' -or $defaultSha -notmatch '^[0-9a-f]{40}$') {
    throw 'Release tag and default branch must resolve to full commit SHAs.'
}
if ($tagSha -ne $defaultSha) {
    throw "Release tag must target the current HEAD of the default branch '$DefaultBranch'. Tag: $tagSha; default branch: $defaultSha."
}
Write-Output "Release source $tagSha matches default branch '$DefaultBranch'."
