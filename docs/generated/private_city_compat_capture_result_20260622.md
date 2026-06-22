# Private City Compatibility Capture Result

Date: 2026-06-22

Capture inputs:

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-092054`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-092724`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-093102`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-093540`

Implemented compatibility plumbing:

- Private-city candidate detection now includes the captured dynamic city playfield `0x104868` without binding behavior to that one ID.
- Normal private-city zone-in sends the captured city-ready block shape: empty towers, non-empty captured `PlayfieldAllCities`, and a private-city `PlayfieldAnarchyF` generator payload for controller, guest-key terminal, and door dynels.
- `GenericCmd Use` on `CityController` returns the captured success ack path and does not fall through to generic statel use.
- `GenericCmd Use` on captured guest-key terminal `Terminal:574DF8BB` emits captured-compatible temporary City Access Card `280642` and overflow-slot add packets, then a success ack. The item is not persisted.
- `OrgClient BankAdd = 19` remains routed/logged safely for city controllers with no city-bank semantics.
- `OrgClient CityAdvantages = 31` is recognized and answered with the four captured QL 300 advantages: `254403`, `254387`, `254406`, and `254395`.
- `InstancedPlayerCity` now has a narrow route for the captured private-city grid exit. It resolves the character organization `CityId` first; captured PF `1067112` is only fallback for captured org `1370122`.

Not implemented:

- City-bank money, item, storage, or persistence semantics.
- Full private-city ownership, invite, guest-key validation, or city-building systems.
- General `GridDestinationSelect` / `GridSelected` UI modeling beyond the captured teleport compatibility path.

Validation status:

- `git diff --check`: PASS.
- `tools\build_aorebirth_debug.cmd`: PASS after stopping running engines that locked built DLLs.
- `SmokeLounge.AOtomation.Messaging.Tests.csproj` build: PASS.
- `restart-engines.cmd`: PASS.
- User gameplay smoke still required.
