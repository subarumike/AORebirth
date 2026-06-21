# Surgery Clinic Terminal Capture Result

Date: 2026-06-21

## Scope

This result covers the terminal-use bug where `GenericCmd Action=Use` reaches Statel lookup and stops after `Found Statel with 2 events`.

The implementation is intentionally limited to captured surgery-clinic terminal behavior. It does not implement a generic Statel event VM, shop routing, mission routing, teleport routing, dialog routing, requirements semantics, or any database schema change.

## Capture Evidence

- Private AO Rebirth capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260620-213807/events.log:51-52` proves the affected identity `Terminal:C00204A2` spawned as `Stationary Automated Surgery Clinic` at `(174.9927, 5.982423, 157.8344)`.
- The same private capture at `events.log:33-34` proves nearby `Terminal:C00004A2` is also `Stationary Automated Surgery Clinic`.
- Official live capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-062224` captured repeated `GenericCmd Use` against surgery-clinic-family target `Terminal:574AF83F` and the server response sequence.

## Captured Use Sequence

For each captured use of `Terminal:574AF83F`, the client sent:

- `GenericCmdMessage` with `Action=Use`, `Temp1=0`, `Temp4=1`, and target `Terminal:574AF83F`.

The live server replied with:

- `StatMessage` updating `Cash`; repeated uses dropped cash by 300 credits.
- `FormatFeedbackMessage` with message `~&!!!":!!!)<sHYou have 5 minutes (or until you leave the playfield) to swap implants.`
- `CastNanoSpell` for `NanoProgram:26732` on the player.
- `CharacterAction SetNanoDuration` for `NanoProgram:26732`, `Parameter1=<character instance>`, `Parameter2=90000`.
- `CharacterAction SpecialUsed` with `Parameter1=124`, `Parameter2=5`.
- `GenericCmd` success acknowledgement with `Temp1=1`, original count/action/target, and `Unknown=0`.
- A delayed `CharacterAction SpecialAvailable` with `Parameter1=0`, `Parameter2=124`.

Representative evidence:

- `events.log:52-71`: first captured use through success ack and delayed special-available.
- `events.log:80-101`: second captured use with the same pattern.
- `events.log:116-139`: third captured use with the same pattern.
- `packets.hex.log:17-30`: first use request and success acknowledgement bytes.
- `packets.hex.log:23-26`: `CastNanoSpell` and `SetNanoDuration` bytes for `NanoProgram:26732`.

## Implementation

`GenericCmdMessageHandler` now routes captured surgery-clinic terminal use before the generic Statel fallback when the target is a captured surgery clinic identity or matching known surgery clinic template.

The route:

- Debits 300 credits through the existing cash stat update path.
- Sends captured-format feedback text.
- Sends the captured nano cast and nano duration action.
- Sends the captured special-used action and delayed special-available action.
- Sends the normal `GenericCmd` success acknowledgement.

Uncaptured insufficient-credit behavior is not fabricated. If the character has less than the captured 300-credit cost, the route logs the unsupported state and falls through to existing behavior.

## Validation

- `cmd /d /c tools\build_aorebirth_debug.cmd` initially reached compile/copy but failed because running `ZoneEngine.exe` locked `AORebirth/Built/Debug/ZoneEngine.exe`.
- `cmd /d /c stop-engines.cmd` stopped Chat/Login/Zone cleanly.
- `cmd /d /c tools\build_aorebirth_debug.cmd` then passed.
- `cmd /d /c restart-engines.cmd` restarted Chat/Login/Zone and confirmed listening ports.
- No post-restart private live click of `Terminal:C00204A2` had appeared in `logs/ZoneEngine.stdout.log` at the time this result was written, so final live smoke remains pending.
