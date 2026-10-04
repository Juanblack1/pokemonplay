# C23 — Let a recovered foreground settle before virtual key delivery

## Evidence and problem

The successful native run `37170873248` on PR40 delivered A/B after the owned emulator already remained foreground through mouse capture. The newer run `37172920731` on `aec9c222297a84751e504b23e43884db770f1832` showed a different transition: after the actual pad mouse-down, foreground briefly read as PID 0; the production bridge then restored the owned RetroArch PID, sent one mapped Z key-down (`SendInput` count 1, error 0), and Windows reported `GetAsyncKeyState(Z)` down. The full guest decoder still saw mask0 in both the D3D11 capture and desktop frames while its frame counter advanced.

The trace does not prove why the guest missed that input. It makes a narrow timing hypothesis testable: the first `SendInput` in the same bridge poll that restores foreground can precede the input driver's focus reacquisition. Allow one 16ms bridge tick after an observed foreground PID transition before resolving/sending the still-held action. Continue to require actual foreground ownership and the guest pixel oracle; do not retry successful key-downs or treat host key state as guest delivery.

## Behavior

- Only apply the settling gate in mode3 for a raw held virtual action and a top-level emulator.
- When the poll begins outside the owned emulator PID and its focus recovery returns the emulator PID, record `FocusSettling=true` and resolve no virtual actions for that poll.
- On the next poll, route the currently supplied action only if the emulator still owns foreground. Keep input suppression effective and keep retrying focus while the hold remains.
- Do not change embedded-window routing, physical controller input, key bindings, focus policy, window activation rules, or release behavior.

## Acceptance

1. The `GameControlsCheck` contract proves the restoration poll injects no virtual action, the next still-held poll routes it, failed restoration stays blocked, and embedded input has no settling delay.
2. Full `ProfilesCheck` passes.
3. Native positive and suppressed-A E2E passes with the actual focus transition, guest masks and counter progression; `SendInput`/`GetAsyncKeyState` alone remain insufficient.
4. All standard source, package, installer and updater checks pass on the final merge commit before publishing.

## Scope and status

This adds one input-poll of latency only when a top-level foreground transition was just recovered during a raw virtual hold. It leaves the production foreground guard intact. Status: implementation candidate; native confirmation pending.
