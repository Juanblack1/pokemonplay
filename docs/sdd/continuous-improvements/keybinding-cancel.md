# C16 — preserve keyboard preset when capture is cancelled or rejected

The current SettingsView.CaptureKey converts actions0–9 to Personalizado before opening the modal. Escape, window close and a duplicate assignment can therefore change a preset that the person never accepted. Saving another preference later persists this unintended change.

This independent cycle starts from verified public v171.10.7 (df36b888). C15 frontend instrumentation remains an unfinished draft and is excluded from this release. No emulator, personal ROM or physical controller verification is claimed here.

## Acceptance

- R1: For presets0–4, Escape or window close leaves preset selection, custom keys, extra keys and effective mapping unchanged.
- R2: A duplicate assigned to another action or an extra X/Y key is rejected with the existing message and no mapping/preset change.
- R3: Only an accepted, valid key converts to Personalizado, changes the requested action and preserves the other nine effective preset keys and both extra keys.
- R4: Existing Personalizado and extra-key capture cancellation retain their state. Saving after cancellation preserves the selected preset and mapping in the owned fixture.
- R5: Existing settings responsive tests and complete app/package/updater/installer CI pass on the release head. Public release assets and hashes are verified after publication.

## Plan and ownership

1. QA owns a focused real modal regression in ProfilesCheck plus its existing command registration/workflow selector. Reproduce R1/R2 on unchanged production code in Windows CI. Local native execution remains prohibited by App Control; build alone is not runtime proof.
2. Root owns SettingsView.CaptureKey. Compute a candidate from the current effective mapping, validate the captured key, then commit only after OK and validation. No persistence format or public API change.
3. QA independently rechecks R1–R4 and reviews the diff. Root integrates, runs focused and full CI, merges the reviewed head and publishes/validates the next update (R5), as the user authorized.

Tests drive the actual modal and CaptureKey path in a synthetic root. They may send fixture-controlled KeyDown/close events to the actual dialog; this does not prove physical keyboard behavior. Tests must not replace ShowDialog with a production seam. All callbacks are bounded and propagate failure. No unrelated accessibility/design change in this slice.

State: specified; static defect identified, native red/green and implementation pending.

Native red evidence: Windows engineering run37125404326 on1fb0b35965b7d76c3af146ce1052970641dab61c executed all100 real modal cases;20 failed exactly cancellation persistence/duplicate rejection across presets0–4,80 passed. No mock ShowDialog or emulator was used. Log and artifact preserved in output/keybinding-cancel-red-ci.log and keybinding-cancel-red-evidence. Production now computes candidate effective keys after OK, validates duplicates without mutation, then copies candidate and selects Personalizado. Local fresh build passed0errors/1existingCS0108 warning. Native green/full/release verification pending.
