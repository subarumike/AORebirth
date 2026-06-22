# Current Task

## Active Task

Fix outside grid terminal entry so each tested terminal spawns the player beside its matching grid-side exit in playfield `152`.

## Current Scope

- Keep changes scoped to outside grid terminal use, destination transform, playfield handoff, and failure feedback.
- Do not use one global hard-coded grid spawn for all outside grid terminals.
- Preserve the existing live-captured floor height and heading behavior.
- Do not rework surgery clinic, bank, macro, vendor, or unrelated statel semantics.
- Do not commit until live smoke confirms the corrected per-terminal destinations.

## Current Evidence To Confirm

- Live capture `20260622-003221` confirmed terminal-specific PF `152` landings for `Terminal:C0010237`, `Terminal:C00602C1`, `Terminal:C00002DA`, `Terminal:C00802B7`, `Terminal:C003029E`, `Terminal:C0060230`, `Terminal:C00202BC`, and `Terminal:C02301F9`.
- Prior live capture `20260621-091447` confirmed `Terminal:C002028F` lands beside `Terminal:C04E0098`.
- `Terminal:C0040320`: determine the exact matching grid-side exit/destination from capture-backed behavior.
- `Terminal:C002022C`: inspect/capture exact `TeleportProxy2` mapping and matching grid-side exit/destination.
- Confirm whether identities such as `Terminal:C0000098` are grid-side destination anchors.
- Failure requirements must remain sourced from the statel data/capture evidence.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- After successful rebuild, run `cmd /d /c restart-engines.cmd`.
- Live smoke:
  - Use `Terminal:C0040320`, confirm PF `152` and correct matching exit/platform.
  - Use `Terminal:C002022C`, confirm PF `152` and correct matching exit/platform.
  - Confirm player is on the platform/floor, not under it.

## Validation Result

- `cmd /d /c git diff --check`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping the running engines that held `ZoneEngine.exe` open.
- `cmd /d /c restart-engines.cmd`: PASS.
- Live capture `20260622-003221`: captured terminal-specific grid landings for PF `567`, `705`, `730`, `695`, `670`, `560`, `700`, and `505`.
- Live smoke for `Terminal:C0040320` and `Terminal:C002022C`: pending.
