# Current Task

## Active Task

Investigate private-server Grid zone-in client crash with server-side diagnostics.

## Current Scope

- Do not launch the game.
- Do not live test.
- Keep changes scoped to Grid entry routing, Grid zone-in object diagnostics, and static/build validation.
- Do not change database schemas or data.
- Preserve existing terminal-specific Grid landing routes.

## Current Evidence

- Crash occurs in the Grid on the private server.
- Client crash signature includes `E06D7363`, `KERNELBASE.dll`, `MSVCR100.dll`, repeated `N3.dll` / `Vehicle.dll`, recurring `Vehicle.dll` frame `0001:0000D906`, and repeated stack value `00000012`.
- Existing Grid entry code handles captured terminal-specific routes first, then falls back to `TeleportProxy2` destination reconstruction for template `95350` terminals.
- Existing PF `152` Grid exit statels use template `95351`.
- `0x12` is not an AORebirth `IdentityType`; when interpreted as an AORebirth stat id, decimal `18` maps to `StatIds.stamina`.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- No game launch or live smoke in this task.

## Validation Result

- `cmd /d /c git diff --check`: PASS.
- First `cmd /d /c tools\build_aorebirth_debug.cmd`: blocked by running `ZoneEngine` PID `21928` locking `AORebirth\Built\Debug\ZoneEngine.exe`.
- `cmd /d /c stop-engines.cmd`: PASS, used only to clear the build lock.
- Second `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- No game launch or live smoke performed.
