# C21 — wait for the matching composed frontend frame

## Problem

The frontend E2E check sends one screenshot request to the owned RetroArch process, then immediately searches the latest desktop samples. A screenshot can arrive just before the next sampler tick. The check then rejects the run even though a matching composed frame is decoded within the documented 500 ms window.

## Behavior

- Preserve exactly one screenshot request per observation; wait for a composed desktop sample rather than retrying the request.
- Accept only a sample captured during the current phase, within 500 ms of the internal screenshot, with the same full input mask and a guest counter at most 12 frames away.
- Bound the pairing wait by the existing phase deadline and the 500 ms sample window. Continue checking process identity and foreground/input safety during the wait.
- Keep stale frames, wrong masks, distant counters, missing screenshots, and expired deadlines as failures.

## Acceptance checks

1. A deterministic contract fixture starts with only pre-phase, wrong-mask, and distant-counter samples; it adds one valid sample after the screenshot response. The matcher waits and selects the fresh valid sample.
2. The fixture rejects the same samples when the phase deadline has expired.
3. A real launcher/frontend run still proves advancing paired frames, focus, actual A/B press and release, and clean process shutdown without sending a second screenshot request.

## Scope

This changes only the diagnostic E2E timing gate. It does not loosen the 500 ms/counter/mask criteria or change application input behavior.
