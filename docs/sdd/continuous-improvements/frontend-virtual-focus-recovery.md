# C22 — recover top-level emulator focus during virtual holds

## Problem

With RetroArch hosted as a top-level popup, pressing a virtual control activates the launcher before the game window. A one-shot focus transfer can be denied during that transition. The input bridge then sees no foreground owner and drops the held action until release.

## Behavior

- While a raw virtual-control action is physically held and the emulator uses a top-level window, retry the existing focus transfer from the input poll, even when an input filter suppresses the mapped action.
- Re-read the foreground process after each attempt. Route keyboard input only after the emulator process owns foreground.
- When a held virtual action restores a top-level emulator from another/unknown foreground PID, let one bridge poll run with input gated before delivering on the following focused poll. This gives the foreground/input stack one tick to settle after activation.
- Keep embedded emulator focus behavior unchanged and never send a virtual key into another foreground window when recovery fails.
- Stop retrying automatically when the virtual hold ends.

## Acceptance checks

1. A focused contract test restores a top-level emulator foreground on a held virtual action.
2. When the focus callback fails, the foreground guard remains false and the resolver produces no key action.
3. Embedded-window input does not invoke repeated foreground recovery.
4. A just-restored top-level foreground is gated for one poll; a still-held unsuppressed virtual action routes on the next poll, while suppressed input remains blocked.
5. The real frontend E2E verifies that virtual A/B presses and releases reach advancing guest frames while the owned RetroArch process is foreground.

## Scope

This affects only top-level RetroArch virtual-control focus recovery. It does not loosen the foreground gate or change physical controller routing.
