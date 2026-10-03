# C13 candidate — recovering emulator configuration

Source evidence: RetroArchSettingsService.Load returns disabled empty settings for JsonException/IO/UnauthorizedAccess. LauncherSettings.PrepareGame then selects legacy executables and throws when absent. Fresh installer uses bundled RetroArch and may have no legacy exe. This is a source-supported hypothesis, not a reproduced runtime defect yet.

Proposed user outcome: if retroarch.json becomes unreadable or malformed, bundled installed games remain recoverable and Settings explains the condition; opening settings or launching must preserve original bytes. Valid user opt-outs and custom external paths must retain their behavior.

Acceptance candidates:
1. Red test with fresh bundled fixture, malformed JSON, no legacy executable. Reproduce disabled selection/launch failure first.
2. Invalid JSON/null settings should derive safe installed defaults in memory with explicit recovery status. Preserve exact bytes; do not auto-save defaults.
3. IO/access errors must produce an explicit recovery state and never overwrite the inaccessible file. Restore access and reread successfully without restart.
4. Valid UseForGba/UseForDs=false remains false; custom external paths and moving portable bundle behavior remain unchanged.
5. If no valid installed bundle exists, do not invent an executable or claim recovery. Existing legacy selection should retain behavior.
6. Settings shows a readable warning and actual native controls expose its description. Explicit Save can replace invalid settings with validated selected settings; reading cannot.
7. Test CreateLaunch plan/config only with owned dummy executable/core files; do not execute emulator or upload personal ROMs. Use real UI refresh/load path for recovery when available. Local App Control prohibits substituting these tests for native execution.
8. Publish only after application, package/updater/installer gates pass on final head, previous release is public/verified, and release artifact hashes/version are checked.

Next action: after C12 release, isolated codex branch; add red reproduction to ProfilesCheck and run Windows CI. If hypothesis is contradicted, revise this candidate before implementation. Research is not needed to establish this local file-handling defect; use primary docs only if behavior contracts become uncertain.
