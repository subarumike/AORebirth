# Owned Private City Entry Capture 20260623-021643

Capture folder:

`tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-021643`

This report documents the live owned-private-city entry and CityController open captured from Mike's client. It records packet evidence only; it does not infer unobserved owned-city behavior.

## Capture Status

- `events.log` and `packets.hex.log` were written.
- `events.log` captured decoded N3 events through `2026-06-23T07:18:35.2417772Z`.
- `system-messages.log`, `chat-dialogue.log`, `inventory-updates.csv`, and shop/vendor logs were empty in this capture.

## Character And Organization

- Character: `Valk2024`
- Character identity: `SimpleChar:67341D9C`
- Organization stat observed on zone init:
  - `Clan=1970177`
  - `ClanLevel=1`
- `OrgInfoPacket` was sent on each zone init path inspected, including private city entry.
- Raw `OrgInfoPacket` payloads contained ASCII `Est. 2024`.

## Entry Timeline

### Initial Position

- Initial snapshot:
  - `playfield=(Playfield2:0320)`
  - Decimal playfield: `800`
  - Position: `(683.8555, 66.81001, 700.5378)`

### Grid Entry

At `2026-06-23T07:16:51.2327617Z`:

- OUT `GenericCmd Use`
  - Target: `Terminal:C0040320`
  - Fields: `Temp1=0 Count=1 Action=Use Temp4=1`

At `2026-06-23T07:16:51.3112662Z`:

- IN `GenericCmd` success ack
  - Target: `Terminal:C0040320`
  - Fields: `Temp1=1 Count=1 Action=Use Temp4=1`

At `2026-06-23T07:16:51.5015764Z` through `2026-06-23T07:16:52.1427898Z`:

- `Stat SocialStatus=4`
- `N3Teleport`
- `PLAYFIELD-INIT 1181697`
- Snapshot resource: `Playfield2:120801`
- `OrgInfoPacket`
- `Clan=1970177`
- `ClanLevel=1`
- `FullCharacter`
- `PlayfieldAllTowers`
- `PlayfieldAllCities`

### Public Montroyal / ICC Area

At `2026-06-23T07:17:10.6083072Z` through `2026-06-23T07:17:12.4639362Z`:

- `Stat SocialStatus=4`
- `N3Teleport`
- `PLAYFIELD-INIT 655`
- Snapshot resource: `Playfield2:028F`
- Spawned public-area city-route dynels included:
  - `Door:C019028F` = `Shuttle to Montroyal`, position `(3138.533, 59.07018, 799.7945)`
  - `Door:C017028F` = `Shuttle to Serenity Islands`, position `(3111.794, 59.07018, 826.4664)`
  - `Door:C016028F` = `Shuttle to Playa del Desierto`, position `(3164.706, 59.07018, 825.7336)`
  - `Terminal:C000028F` = `Teleport Up`, position `(3159, 36.6, 866.5)`
  - `Terminal:C001028F` = `Teleport Down`, position `(3156, 51.5, 866)`
  - `Terminal:C002028F` = `Enter The Grid`, position `(3178.737, 35.91216, 880.7335)`
- Ready/character sequence included:
  - `PlayfieldAnarchyF`
  - multiple `DoorStatusUpdate`
  - `OrgInfoPacket`
  - `Clan=1970177`
  - `ClanLevel=1`
  - `FullCharacter`
  - `PlayfieldAllTowers`
  - `PlayfieldAllCities`

### Public Area Movement Trigger To Owned City

The private-city entry itself was movement-triggered, not `GenericCmd Use`.

Last movement near the trigger:

- `2026-06-23T07:17:31.6219093Z`
  - OUT `CharDCMove ForwardStop`
  - Position: `(3138.645, 51.49371, 798.8262)`
- `2026-06-23T07:17:31.7124172Z`
  - IN `CharDCMove Update`
  - Position: `(3138.117, 51.54187, 799.8146)`
- `2026-06-23T07:17:33.7918148Z`
  - IN `N3Teleport`
- `2026-06-23T07:17:34.6752315Z`
  - `PLAYFIELD-INIT 1196034`
  - Snapshot resource: `Playfield2:124002`

## Owned Private City Init

Private city playfield:

- Decimal playfield: `1196034`
- Resource identity: `Playfield2:124002`

Spawned owned-city dynels:

- `Terminal:5751538B` = `Private City Guest Key Generator`
  - Position: `(534, 160.6381, 578)`
- `Door:108D96ED` = `Shuttle Door`
  - Position: `(530.4664, 171.0702, 590.7054)`
- `CityController:9C182E` = `City Controller`
  - Position: `(586, 160.638, 598.4673)`

Player spawn/update:

- `SimpleChar:67341D9C`
- Position: `(528.6631, 163.2526, 580.9919)`
- `SimpleCharFullUpdate` playfield id: `1196034`

Private-city ready sequence:

1. `PlayfieldAnarchyF` for `Playfield2:124002`
2. `DoorStatusUpdate` for `Door:108D96ED`
3. `SimpleCharFullUpdate` for `SimpleChar:67341D9C`
4. `WeaponItemFullUpdate`
5. `SimpleItemFullUpdate` for existing item `51056:6D780D`, template `280642`, unknown slot/value `333`
6. `SimpleItemFullUpdate` for existing item `51056:48900B`, template `281129`, unknown slot/value `339`
7. `GameTime`
8. `OrgInfoPacket`
9. `Stat SocialStatus=4`
10. `Stat Clan=1970177`
11. `Stat ClanLevel=1`
12. additional `SocialStatus=4` stats
13. `FullCharacter`
14. `PlayfieldAllTowers`
15. `PlayfieldAllCities`
16. `SpecialAttackWeapon`
17. `CharacterAction` packets

Raw `PlayfieldAnarchyF` evidence included the private-city identities:

- `CityController:9C182E`
- `Terminal:5751538B`
- `Door:108D96ED`

No separate decoded CityController-state packet was observed on entry beyond the spawned dynel and the raw identities in `PlayfieldAnarchyF`.

## CityController Open

At `2026-06-23T07:17:44.6372531Z`:

- OUT `GenericCmd Use`
  - Target: `CityController:9C182E`
  - Fields: `Temp1=0 Count=3 Action=Use Temp4=1`

At `2026-06-23T07:17:44.7102662Z`:

- IN `AOTransportSignal` x5
- IN `GenericCmd` success ack
  - Target: `CityController:9C182E`
  - Fields: `Temp1=1 Count=3 Action=Use Temp4=1`

Immediate raw `AOTransportSignal` packet details:

- `IN #312 len=84`
  - contains raw ASCII `Est. 2024`
  - raw also includes `CityController:9C182E` context in the same response block
- `IN #313 len=37`
  - trailing payload bytes: `E4E1C0`
- `IN #314 len=37`
  - trailing payload bytes: `278805`
- `IN #315 len=41`
  - trailing payload bytes: `0000000095C5CCD7`
- `IN #316 len=37`
  - trailing payload bytes: `3F800000`

At `2026-06-23T07:17:46.6467272Z`:

- OUT raw/unknown N3 packet:
  - `n3=0x1B3C614D`
  - `len=33`

At `2026-06-23T07:17:46.7367280Z`:

- IN `AOTransportSignal`
  - `len=53`

No decoded `Feedback` packet was observed for the owned CityController open.

## Not Observed

- No decoded `OrgClient` traffic.
- No decoded `CityAdvantages` packet.
- No decoded `Feedback` packet.
- No inventory movement.
- No purchase/ownership transition; ownership was already established before this capture.
- No teleport/playfield transition after opening the owned CityController.

## Proven Missing Feature Candidate

The first implementation target proven by this capture is the owned private-city entry/initialization path:

- Resolve the owned organization's private-city instance from the public-area movement trigger instead of hardcoding a single private playfield id.
- Send the private-city init sequence for the resolved instance:
  - captured guest-key terminal, shuttle door, and CityController dynels
  - `PlayfieldAnarchyF`
  - `DoorStatusUpdate`
  - character/item updates
  - `OrgInfoPacket`
  - `Clan` and `ClanLevel` stats
  - `FullCharacter`
  - `PlayfieldAllTowers`
  - `PlayfieldAllCities`
- Keep `CityAdvantages` and `OrgClient` out of this entry implementation unless a later capture proves those packets in the relevant path.

The owned CityController open path also has new packet evidence, but it depends on `AOTransportSignal` payload support and still did not produce decoded `OrgClient` or `CityAdvantages` traffic in this capture.
