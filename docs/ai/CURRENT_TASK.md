# Current Task

## Active Task

Implement first-layer private-city and CityController compatibility plumbing from live capture `20260622-073015`.

## Current Scope

- Do not continue Grid work.
- Do not hardcode captured private-city playfield IDs as permanent behavior.
- Do not split `Playfield.cs`.
- Do not change database schemas or data.
- Do not implement full city-bank, org-bank persistence, city ownership, guest-key generation, or item semantics.
- Packet-sensitive behavior must remain backed by the live capture.

## Current Evidence

- Live capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-073015` is authoritative for this task.
- Captured private-city playfield identities included `Playfield2:116000`, `Playfield2:120005`, and `Playfield2:121001`.
- Captured private-city zone-in received empty `PlayfieldAllTowers` and empty `PlayfieldAllCities`.
- Captured CityController use sent `GenericCmd Action=Use` to `CityController:9C8818` and received a success ack with `Temp1=1`, the same count, action, temp4, user, and target.
- Captured city-bank UI emitted `OrgClient BankAdd` command `19` targeting `CityController:9C8818` with command arg text `300000000`.
- No live server response behavior was captured for the `OrgClient BankAdd` command, so no money or persistence semantics are implemented.

## Implementation Plan

- Reuse the existing empty playfield towers/cities send block for normal private-city zone connect.
- Add explicit `CityController` use handling in `GenericCmdMessageHandler`.
- Subscribe an inbound `OrgClientMessage` handler for safe `BankAdd` recognition and logging only.
- Add a generated result note under `docs/generated/`.

## Validation Plan

- Run `cmd /d /c git diff --check`.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `cmd /d /c restart-engines.cmd` after rebuild.
- Prefer Mike live smoke for private-city zone-in, CityController use ack, and `OrgClient BankAdd` safe routing.

## Validation Result

- `cmd /d /c git diff --check`: PASS.
- First `cmd /d /c tools\build_aorebirth_debug.cmd`: blocked only by running `ZoneEngine` PID `4736` locking `AORebirth\Built\Debug\ZoneEngine.exe`.
- `cmd /d /c stop-engines.cmd`: PASS, used only to clear the build lock.
- Second `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- `cmd /d /c restart-engines.cmd`: PASS.
- AOSharp smoke capture `20260622-081427` completed cleanly with no capture issues, but it stayed in `Playfield2:028F`; no private-city, CityController, or `OrgClient BankAdd` smoke evidence was produced.
