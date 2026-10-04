# Release tag must match default branch HEAD

## Problem and outcome

The release workflow accepts any pushed `v*` tag. An annotated tag created from a stale local branch can therefore publish older source under a newer version. The workflow must fail before emulator setup or packaging unless the tag resolves to the current HEAD of the repository's default branch.

## Requirements and acceptance

- R1: Resolve the repository's configured default branch and its remote HEAD at workflow start.
- R2: Resolve annotated or lightweight release refs to a commit and require an exact 40-character SHA match with the default branch HEAD. Missing refs, API/git errors, malformed SHAs, or mismatches fail closed before expensive build steps.
- R3: A small automated check proves that equal SHAs are accepted and stale or malformed SHAs are rejected.
- R4: A valid current-main tag continues through the existing build, install-preservation, asset, and stable-publish steps without changing release contents.

## Scope and assumptions

Only release source validation changes. Release filenames, package contents, version format, install behavior, and stable-channel semantics remain as they are. The GitHub repository's configured default branch is authoritative; it is read through the GitHub API rather than hardcoded as `main`.

## Implementation plan

1. Add a reusable PowerShell assertion for commit SHA validation and exact equality.
2. Add a release workflow preflight that resolves default branch HEAD and the workflow's tag commit, then invokes the assertion before emulator setup.
3. Add a regression check for matching, stale, and malformed inputs to the existing Windows verification workflow.
4. Run the targeted check and full PR CI. Merge only the exact green head; publish and anonymously verify the next stable release.

## Verification evidence

The cancelled v171.10.14 run used stale commit `cdaa4e6e2ba7f8aad29e5f9f2fdd18bc9b274609`; the corrected run uses the merged and fully tested commit `b8f6c6adc8428557ce5ec7c6375647664e2061c5`. Public release verification must continue to prove exact commit, stable latest tag, eight assets, matching SHA-256 sidecars/GitHub digests, and the embedded app manifest.
