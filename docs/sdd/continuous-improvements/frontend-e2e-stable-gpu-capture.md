# C22 — Stable screenshots for the real frontend E2E

## Problem

Native run `37172044954` on `96e696270238281d7dc8a965cd4740a79a9445a0` reached A-held with the real RetroArch window foreground, the production input guard eligible, and raw A/capture held. Its one internal PNG had a mixed counter region, so the strict decoder rejected it. Three prior neutral PNGs in the same process decoded, while the independent desktop ring showed full advancing guest frames. No second screenshot request was made.

The session config had `video_gpu_screenshot=false`. At the pinned RetroArch source, `CMD_EVENT_TAKE_SCREENSHOT` calls `take_screenshot(..., use_thread=true)` in [`retroarch.c`](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/retroarch.c#L3597-L3608). With GPU screenshots disabled, software-frame capture uses `frame_cache_data` in [`task_screenshot.c`](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/tasks/task_screenshot.c#L510-L538), and the async task encodes that frame pointer later. The pinned D3D11 backend provides `read_viewport`, which copies the swapchain image to a staging texture and maps it before returning in [`d3d11.c`](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/gfx/drivers/d3d11.c#L3991-L4077). The configured D3D11 viewport-readback path therefore gives the encoder owned stable pixels. This diagnosis is supported by source and the failure artifacts; native confirmation is still required.

## Behavior

- Keep exactly one loopback `SCREENSHOT` request per phase sample, the one-pending-file rule, and no retries.
- Enable `video_gpu_screenshot=true` only in the owned test session. Keep the default distributed D3D11/DINPUT selection and all production config untouched.
- Treat the returned PNG as a D3D11 game-viewport readback. Calibrate it from the visible guest border and decode the full canonical regions. Do not relax color thresholds, pixel coverage, counter progression, pair timing or phase masks.
- Continue to capture the visible child with independent `CopyFromScreen`; require its own valid advancing frame and retain the existing pair criteria.
- Record both capture dimensions and normalized viewport for diagnosability.

## Acceptance

1. `--verify-evidence-contract` proves native-size and scaled GPU-viewport decoding return identical masks/counters and rejects a mixed button region.
2. Session-config contract records `video_gpu_screenshot=true`; protected paths, bindings, pause policy and default drivers remain unchanged.
3. On the exact tested commit, the real positive and suppressed-A frontend runs each use one request per sample and pass the unchanged strict decoder, actual key-down/up, focus, guest cadence, negative and cleanup gates.
4. Full source, packaging, installer and updater verification passes on the exact final merge commit before any release.

## Scope and status

This changes only test-session screenshot acquisition and the decoder's viewport normalization. It does not alter production RetroArch settings, controller/input mapping, rendering driver, guest protocol or acceptance thresholds. Status: implementation candidate; native runtime evidence pending.
