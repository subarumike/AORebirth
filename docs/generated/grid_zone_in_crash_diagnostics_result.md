# Grid Zone-In Crash Diagnostics Result

## Scope

Investigate private-server Grid crash risk without launching the game or live testing.

## Findings

- Grid entry handling is in `GenericCmdMessageHandler`.
- Exact captured terminal routes are handled before the generic `TeleportProxy2` reconstruction path.
- Generic Grid entry resolves template `95350` source terminals to PF `152` destination statels with template `95351`.
- Post-zone login sends playfield info, vending-machine full updates, nearby character SCFUs/CharInPlay, the local character visual update, weapon item definitions, full character state, and follow-up stat/action messages.
- Static statel data is used for terminal routing and collision handling; it is not sent as a generic PF `152` statel object list in the inspected zone-in path.
- `0x12` / decimal `18` is not a known `IdentityType` in AORebirth. It maps to `StatIds.stamina` when interpreted as a character stat id.
- No AORebirth server-side `IdentityType.Vehicle` mapping was found in the inspected object routing path. Runtime diagnostics now warn if an outbound Grid zone-in object's resolved type or message name contains `Vehicle`.

## Change

- Added process-local Grid entry context before teleport.
- Added a PF `152` zone-in diagnostic window that logs outbound N3 object-bearing messages sent to the reconnecting client.
- Each diagnostic line includes playfield id, source terminal identity/template, destination statel identity/template, object identity/type/name, coordinates, heading, and model/resource/mesh details when available.
- Added warning logs for emitted integer values equal to `0x12` and for any object classified/routed as `Vehicle`.
- Added comparison logging for the raw reconstructed Grid exit versus the expected terminal-specific Grid exit, including nearby PF `152` statels around each exit.

## Validation

- `cmd /d /c git diff --check`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping the running `ZoneEngine` process that locked the build output.
- No game launch or live smoke performed.
