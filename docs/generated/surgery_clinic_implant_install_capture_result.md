# Surgery Clinic Implant Install Capture Result

Date: 2026-06-21

## Scope

This result covers the post-terminal implant-install flow after activating a Stationary Automated Surgery Clinic.

The implementation remains limited to the captured surgery-clinic state handoff and the existing implant inventory/equipment move path. It does not change generic Statel behavior, invent no-clinic failure text, alter database schema, or rework non-implant item movement.

## Capture Evidence

- AOSharp live capture folder: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-063942`.
- Captured character: `Brackenridge`, identity `SimpleChar:44666C00`, playfield `(Playfield2:10A007)`.
- The capture file has duplicate decoded lines for each N3 message; references below cite the first line of each duplicate pair.
- Manual chat markers were not used because the in-game `/aocap mark` command was not available. Packet timestamps provide the capture boundaries.

## Captured Terminal Activation

At `events.log:122`, the client sent `GenericCmd Action=Use` against surgery-clinic terminal `Terminal:57495519`.

The live server responded with the already-captured surgery-clinic terminal sequence:

- `events.log:124`: `Stat` cash update.
- `events.log:126`: `FormatFeedback` with the 5-minute implant-swap message.
- `events.log:128`: `CastNanoSpell` for the surgery clinic nano.
- `events.log:130`: `CharacterAction SetNanoDuration` for `NanoProgram:26732` with duration `90000`.
- `events.log:134`: `CharacterAction SpecialUsed` with `Parameter1=124`, `Parameter2=5`.
- `events.log:138`: `GenericCmd` success acknowledgement.

## Captured Implant Move Sequence

The successful implant swap used `ClientMoveItemToInventory`, not `ClientContainerAddItem`.

First, the live client moved the currently equipped implant out of the implant slot:

- `events.log:199`: C2S `ClientMoveItemToInventory` with `SourceContainer=(ImplantPage:0023)` and `Slot=111` (`0x6F`, next free inventory slot).
- `events.log:201`: S2C `TemplateAction` for item `107341/107342` QL 125 with `Unknown2=7`, placement `ImplantPage:0023` (unequip visual/update action).
- `events.log:203`: S2C `ContainerAddItem` with `Source=(ImplantPage:0023)`, `Target=(SimpleChar:44666C00)`, `Slot=79` (`0x4F`, resolved inventory slot).

Then, the live client moved that implant from inventory back into the implant slot:

- `events.log:215`: C2S `ClientMoveItemToInventory` with `SourceContainer=(Inventory:004F)` and `Slot=35` (`0x23`, implant page slot).
- `events.log:217`: S2C `ContainerAddItem` with `Source=(Inventory:004F)`, `Target=(SimpleChar:44666C00)`, `Slot=35`.
- `events.log:221`: S2C `TemplateAction` for item `107341/107342` QL 125 with `Unknown2=6`, placement `ImplantPage:0023` (equip visual/update action).

Repeated attempts at `events.log:223-254` confirmed the same unequip/equip response family.

No implant-specific `InventoryUpdate` was observed in the successful sequence. No additional implant-specific `Stat` update for `SimpleChar:44666C00` was observed between the first unequip at `events.log:199` and the repeated equip sequence ending at `events.log:307`; unrelated stat traffic in that window belonged to other character identities.

## Implementation Decision

Existing AO Rebirth implant movement already routes `ClientMoveItemToInventory` through the equipment path, checks item requirements, sends `ContainerAddItem`, sends `TemplateAction` through `Equip`/`UnEquip`, recalculates skills, and persists inventory.

The missing link was server-side implant access after the captured terminal route. The terminal route now grants the existing `Character.GrantImplantAccess` state for 300 seconds, matching the captured "5 minutes" feedback text. The existing implant-page gate then permits the captured implant move path only during that window.

Failure behavior without a clinic was not captured in this session. The existing implant-access denial path remains unchanged, but no new failure text or behavior was added from guesswork.

## Validation

- `cmd /d /c tools\build_aorebirth_debug.cmd` initially compiled code but failed copying `ZoneEngine.exe` because running `ZoneEngine` pid `23192` locked `AORebirth/Built/Debug/ZoneEngine.exe`.
- `cmd /d /c stop-engines.cmd` stopped Zone/Login/Chat cleanly.
- `cmd /d /c tools\build_aorebirth_debug.cmd` then passed.
- `cmd /d /c restart-engines.cmd` restarted Chat/Login/Zone and confirmed ports `6996`, `7012`, `7500`, and `7501`.
- `cmd /d /c git diff --check` passed with only existing LF-to-CRLF warnings.
- Private AO Rebirth post-restart live smoke passed: implant install and removal now work after surgery-clinic activation, the clinic nano exits NCU properly on zone, and the clinic effect expires as expected.
