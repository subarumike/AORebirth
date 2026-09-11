# NewEngine Official Client Staging Acceptance

## Current reconciliation acceptance — 032ee4cd39433bbe124217a745474573e720e820

The shared character/inventory DAO is integrated. Windows exact-source acceptance passes 544 NewEngine tests, 1129 AOtomation tests and all 12 mandatory stages. Account/character/mission/full-isolated DAO checks pass 303/551/261/275; real-MySQL faults, schema/restart, connected lifecycle and Linux publication pass. No new direct SQL violations.

Local Linux container `aorebirth-linux-staging-032ee4cd` replaces only the prior staging applications, with a pre-change database backup and saved rollback arguments under `build-verify/hydration`. Linux binary hashes and complete gates are in [the machine-readable receipt](NEWENGINE_CHARACTER_HYDRATION_RECEIPT.json). Production, master, both source branches and Legacy are unchanged.

Official-client result: NOT_RUN. Local isolated staging prepared on this source. Official-client actions/evidence pending. Production cutover remains **NOT APPROVED**. Do not relabel the historical failed attempt or prior user gameplay as acceptance of this exact source.

See [hydration reconciliation](NEWENGINE_CHARACTER_HYDRATION_RECONCILIATION.md) and [field matrix](NEWENGINE_RETAIL_SPAWN_FIELD_MATRIX.md). Unknown Legacy-only stat requirements remain explicit; real armor effects are outside this milestone.

## Historical evidence (preserved)

**Date:** 2026-09-10  
**Candidate:** `codex/newengine-production-cutover-001` at `7f157d4fa915a4bb872786a1f004578a8a349665`  
**Environment:** isolated Linux staging, `192.168.1.207:7500-7501`  
**Result:** **FAIL — RETAIL WORLD ENTRY NOT ACCEPTED**

## Executive finding

Login authentication, character selection, the LoginEngine-to-NewEngine handoff,
zone admission and server transmission all complete. The retail client then remains
on its loading screen and never sends `CharInPlay`. It also never opens its ChatEngine
connection. The zone TCP connection stays established until the client is closed.

The current staging character is not valid retail world-entry input. NewEngine admits
it because `CharacterHydrationResult.IsSpawnReady` requires only a character row and
one or more stat rows. Character 9950 has only 23 stat rows, lacks required appearance
and character-flag state, and has `Health=1000` with `MaxHealth=19`. NewEngine converts
that state directly into the entering-player `SimpleCharFullUpdate` and
`FullCharacter` messages.

The packet-order repair in `7f157d4f` was necessary but not sufficient. It now sends
the same bounded ready-block ordering observed in both official transitions, but the
official client still rejects or cannot complete the player payload.

## Observed transaction

1. LoginEngine accepted `staging50d` with client version `18.8.62_EP2`.
2. LoginEngine marked character 9950 online and issued the zone handoff.
3. NewEngine accepted the zone socket and consumed the authorized handoff.
4. NewEngine hydrated character 9950 with 23 stats, one item and no uploaded nanos.
5. NewEngine created PF4582 and emitted the entering-player world-state block.
6. NewEngine logged `ZoneLogin completed character=9950 playfield=4582`.
7. The client sent no `CharInPlay` and made no ChatEngine connection.
8. The client kept the PF4582 TCP connection open until it was closed approximately
   two and a half minutes later.

This places the failure after secure admission and before retail world-entry
acknowledgement.

## Confirmed invalid character state

The staging database contains these relevant values for character 9950:

| Field | Stored value | Retail payload consequence |
|---|---:|---|
| `Health` (27) | 1000 | Greater than maximum health |
| `MaxHealth` (1) | 19 | SCFU advertises maximum health 19 |
| computed `HealthDamage` | -981 | Negative damage is emitted by `BuildSpawnMessage` |
| `Flags` (0) | absent | `Stats.Get` returns `CharacterStat.Unset` = 1234567890 |
| emitted character flags | `0x499602D2` | Accidentally includes `Tower` and `HasBlueName` |
| `HeadMesh` (64) | absent | No entering-player head mesh |
| `Fatness` (47) | absent | Defaulted to zero |
| `Race` (89) | absent | Locally substituted with race 1 |
| `Expansion` (389) | absent | Defaulted to zero |
| `VisualFlags` (673) | absent | Defaulted to zero |
| primary abilities | absent | Written as zeros in SCFU and FullCharacter |

The most serious structural defect is the missing `Flags` stat. `BuildSpawnMessage`
uses `Stats.Get(CharacterStat.Flags)` without normalizing the Unset sentinel. The
SCFU serializer therefore treats this ordinary player as a tower and writes the
tower-specific tail field. The health pair is independently invalid.

## NewEngine compatibility gaps exposed by this attempt

### Admission accepts incomplete aggregates

`CharacterHydrationResult.IsSpawnReady` currently checks only `Stats.Count > 0`.
It does not require or normalize the minimum retail identity, appearance, health,
nano, movement and profession stat set. An incomplete database aggregate therefore
crosses the secure handoff boundary and becomes a malformed client payload.

### NewEngine does not prepare the character before serialization

Legacy performs skill calculation and vital-stat synchronization before its SCFU and
FullCharacter sequence. NewEngine serializes the DAO projection directly. Missing
stats become zero, except raw `Stats.Get` calls can leak the Unset sentinel onto the
wire. This is why synthetic persistence fixtures can pass while a retail client does
not enter the world.

### FullCharacter is still reduced

The observed NewEngine `FullCharacter` is 1,702 bytes. The available official retail
capture has a 4,687-byte `FullCharacter`. Raw size alone is not an acceptance rule
because inventory and character state differ, but source comparison confirms that
NewEngine omits fields still emitted by Legacy, including temporary save fields,
features, NanoAC, extended mission-bit fields, AutoAttackFlags, MetaType,
SpecialCondition, LastSK, NextSK and the second MaxNanoEnergy representation.

### The synthetic acceptance client is not a retail-world-entry oracle

The connected acceptance client verifies codecs, authentication, secure admission,
DAO persistence and reconnect behavior. It sends `CharInPlay` after receiving
`FullCharacter`; it does not render or semantically validate the player SCFU as the
official client does. Its PASS result is valid for those covered boundaries and does
not establish official-client compatibility.

## Findings ruled out

- **Credentials:** authentication and character selection succeeded.
- **Zone redirect and cookies:** NewEngine accepted the authorized handoff.
- **Endpoint reachability:** the client established and retained TCP to port 7501.
- **Missing completion packets alone:** the deployed candidate sent GameTime,
  SocialStatus, FullCharacter, PlayfieldAllTowers, PlayfieldAllCities and
  SpecialAttackWeapon in the capture-backed order; the client still did not answer.
- **ChatEngine as the initiating fault:** the client never reached the point where it
  attempted the ChatEngine connection.
- **PF4582 surface warnings as the loading trigger:** they affect server collision and
  world geometry, but the client stopped before acknowledging its own player object.

## Required repair and acceptance sequence

1. Define and enforce a minimum retail spawn aggregate at hydration. Fail closed with
   a specific diagnostic when required stats are absent or internally inconsistent.
2. Normalize `Flags` and every other optional raw `Stats.Get` value before building
   client messages. Never serialize `CharacterStat.Unset`.
3. Clamp or repair current health against maximum health before SCFU and
   FullCharacter serialization.
4. Supply capture-backed player appearance defaults, including head mesh, textures,
   meshes, visual flags, race, breed, gender and expansion state.
5. Reconcile the remaining FullCharacter field coverage against the official capture
   and the working Legacy sender.
6. Add a retail-payload contract test that rejects the exact 23-stat staging fixture.
7. Repeat the official-client test with a fresh packet capture and require:
   client `CharInPlay`, server `CharInPlay`, ChatEngine connection, visible PF4582,
   movement, logout and reconnect.

Production cutover remains **NOT APPROVED**. This staging attempt proves secure
handoff and NewEngine execution, but it also proves that retail player hydration and
serialization are not yet operationally acceptable.

## Evidence inspected

- `tools-temp/linux-staging-50d/logs/loginengine.log`
- `tools-temp/linux-staging-50d/logs/zoneengine.log`
- `tools-temp/linux-staging-50d/logs/chatengine.log`
- `tools-temp/linux-staging-50d/retail-world-entry-stall-1.pcapng`
- official capture `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260623-042326`
- `AORebirth/Server/ZoneEngine_New/Core/Characters/CharacterHydrationResult.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Entities/Character.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Entities/Player.cs`
- `AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/FullCharacterMessageHandler.cs`
- AOtomation `SimpleCharFullUpdateSerializer.cs`, `CharacterFlags.cs` and
  `CharacterStat.cs`
