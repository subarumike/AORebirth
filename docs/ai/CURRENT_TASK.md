# Current Task

## Active Task

Investigate private-server Grid crash around Grid entry and PF152 floor-pad movement.

## Current Scope

- Do not launch the game.
- Do not live test.
- Keep changes scoped to Grid entry routing, Grid zone-in object diagnostics, PF152 Grid floor-pad teleport handling, and static/build validation.
- Do not change database schemas or data.
- Preserve existing terminal-specific Grid landing routes.

## Current Evidence

- Crash occurs in the Grid on the private server.
- Client crash signature includes `E06D7363`, `KERNELBASE.dll`, `MSVCR100.dll`, repeated `N3.dll` / `Vehicle.dll`, recurring `Vehicle.dll` frame `0001:0000D906`, and repeated stack value `00000012`.
- Existing Grid entry code handles captured terminal-specific routes first, then falls back to `TeleportProxy2` destination reconstruction for template `95350` terminals.
- Existing PF `152` Grid exit statels use template `95351`.
- `0x12` is not an AORebirth `IdentityType`; when interpreted as an AORebirth stat id, decimal `18` maps to `StatIds.stamina`.
- Live private-server diagnostic after entering from `PF545 Terminal:C0020221` showed the Grid entry route is correct: destination `PF152 Terminal:C0000098`, landing `(170.077,4.600,243.439)`.
- The repeated `0x12` value in the post-zone-in diagnostics is the logged-in character instance `CanbeAffected:18`, not a Vehicle object route.
- No outbound Grid zone-in object was classified as `Vehicle`; emitted object classes were `Playfield2`, `CanbeAffected`, and `WeaponInstance`.
- The crash/reconnect path occurred after stepping on PF152 floor pad `Terminal:C0160098`, which fired `OnTargetInVicinity`/`LineTeleport` and was handled as a full PF152-to-PF152 zone transfer.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd` if engines are stopped, or a focused temp-output ZoneEngine build if `ZoneEngine.exe` is locked by a live server.
- No Codex game launch or Codex live smoke in this task; Mike live smoke is authoritative.

## Validation Result

- `cmd /d /c git diff --check`: PASS.
- First `cmd /d /c tools\build_aorebirth_debug.cmd`: blocked by running `ZoneEngine` PID `21928` locking `AORebirth\Built\Debug\ZoneEngine.exe`.
- `cmd /d /c stop-engines.cmd`: PASS, used only to clear the build lock.
- Second `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Current Grid floor-pad fix `cmd /d /c git diff --check`: PASS.
- Current full `cmd /d /c tools\build_aorebirth_debug.cmd`: blocked only by running `ZoneEngine` PID `23692` locking `AORebirth\Built\Debug\ZoneEngine.exe`; engines were not stopped because Mike was logged in.
- Current temp-output ZoneEngine build with `OutDir=../../Built/CodexValidation/`: PASS.
- Codex performed no game launch or live smoke.
- Mike live smoke after the Grid floor-pad fix: PASS, no crash.
