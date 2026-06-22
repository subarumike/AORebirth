# Private City / CityController Compatibility Result

Date: 2026-06-22

Capture source: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-073015`

## Live Capture Evidence

- Private-city playfield identities observed: `Playfield2:116000`, `Playfield2:120005`, `Playfield2:121001`.
- Private-city zone-in received empty `PlayfieldAllTowers` and empty `PlayfieldAllCities`.
- `CityController:9C8818` received `GenericCmd Action=Use` and live returned a success ack with `Temp1=1`, same `Count`, same `Action`, same `Temp4`, same `User`, and same `Target`.
- City-bank UI emitted `OrgClient BankAdd` command `19` targeting `CityController:9C8818` with command args `300000000`.
- The capture did not include enough server-response evidence to implement money transfer, persistence, or org-bank state changes.

## Implemented Compatibility Layer

- Normal private-city zone connect now sends the existing empty playfield towers/cities block when the playfield looks like a captured dynamic private-city playfield and is not present in checked-in playfield metadata.
- `GenericCmdAction.Use` now explicitly acknowledges `IdentityType.CityController` targets and does not fall through to generic statel handling.
- Inbound `OrgClientMessage` now has a subscribed handler that recognizes captured `BankAdd = 19` messages to city controllers and logs them without changing money, org state, city state, or persistence.

## Validation

- `cmd /d /c git diff --check`: PASS.
- First `cmd /d /c tools\build_aorebirth_debug.cmd`: blocked only by running `ZoneEngine` PID `4736` locking `AORebirth\Built\Debug\ZoneEngine.exe`.
- `cmd /d /c stop-engines.cmd`: PASS, used only to clear the build lock.
- Second `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- `cmd /d /c restart-engines.cmd`: PASS.
- AOSharp smoke capture `20260622-081427` completed cleanly with no capture issues, but it stayed in `Playfield2:028F`; no private-city, CityController, or `OrgClient BankAdd` smoke evidence was produced.

## Remaining Work

- Capture or decode the live server response to city-controller `OrgClient BankAdd` before implementing any city-bank or org-bank state behavior.
- Reconstruct city dynel definitions only from identity-linked full-update evidence, not from name or proximity.
