# Current Task

## Active Task

Fix PF `152` grid level pads that should move the player up or down inside the grid.

## Current Scope

- Keep changes scoped to `LineTeleport` behavior used by grid level pads.
- Resolve same-playfield destination handling only where the statel data proves it.
- Do not rework outside grid terminal routing, surgery clinic, bank, macro, vendor, or unrelated statel semantics.
- Preserve the existing per-terminal grid entrance mapping work until live smoke confirms it.

## Current Evidence

- User stood directly on the failing PF `152` pad at `(231.3, 3.8, 132.9)`.
- Packed `playfields.dat` identifies nearby `Terminal:C01A0098`, template `95351`, at `(231.229, 3.901, 132.863)`.
- `Terminal:C01A0098` has `OnTargetInVicinity` function `LineTeleport [100001,2228376,0]`.
- Other PF `152` grid level pads also use `LineTeleport` with destination playfield argument `0`.
- `LineTeleport` currently treats that `0` as literal PF `0`; for grid pads it must resolve to the current playfield.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- After successful rebuild, run `cmd /d /c restart-engines.cmd`.
- Live smoke:
  - Stand on PF `152` grid level pad `Terminal:C01A0098`.
  - Confirm player moves to the expected grid level destination.
  - Confirm outside grid terminal routing still enters PF `152`.

## Validation Result

- `cmd /d /c git diff --check`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping the running engines that held `ZoneEngine.exe` open.
- `cmd /d /c restart-engines.cmd`: PASS.
- Live capture `20260622-003221`: captured terminal-specific grid landings for PF `567`, `705`, `730`, `695`, `670`, `560`, `700`, and `505`.
- User-supplied named grid anchors mapped PF `640`, `710`, `567`, `540`, and `6007` entrances to their corresponding PF `152` exit-side spots.
- Live smoke for `Terminal:C0040320` and `Terminal:C002022C`: pending.
- Grid pad `Terminal:C01A0098` evidence documented in `docs/generated/grid_line_teleport_pad_result.md`.
- Current `LineTeleport` same-playfield fix build/restart: PASS.
- User live smoke for PF `152` grid level pads: PASS.
