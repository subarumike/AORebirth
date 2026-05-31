# Stripdown Direct Repair Candidates

Date: 2026-05-31

Scope:
- Current CellAO tree: `C:\Users\Mike\Documents\Cellao-Clean`
- Current installed client: `C:\Funcom\Anarchy Online`
- Reverse engineered source/docs: `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`

The installed client binaries hash-match the AO stripdown binaries for the main protocol/runtime files checked earlier (`N3.dll`, `Gamecode.dll`, `MessageProtocol.dll`, `Connection.dll`, `DatabaseController.dll`, `GUI.dll`, `Anarchy.exe`). Treat the stripdown N3 docs as valid for this client build.

## Method

Compared stripdown recovered N3 constants and fixed wire sizes from:
- `AO stripdown\Anarchy Online\rebuild\include\ao\n3_message.h`
- `AO stripdown\Anarchy Online\rebuild\docs\*_iir.md`
- `AO stripdown\Anarchy Online\rebuild\src\n3_message.cpp`

Against CellAO implementation points:
- `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3MessageType.cs`
- `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\*.cs`
- `CellAO\Server\ZoneEngine\Core\MessageHandlers\*.cs`
- `CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs`

This report only lists differences that have a concrete static source in stripdown. Packet-flow behavior such as NPC chase smoothing still needs live capture/video/correlated log evidence before changing runtime behavior.

## Direct Code Repairs

### 1. Dynel despawn is using the wrong IIR key

Definite mismatch:
- Stripdown: `0x36510078` is `n3ToClientQuitIIR_t`, key-only, total body size 4 bytes.
- Stripdown: real dynel removal is `DropDynelIIR_t`, key `0x47483633`, total body size 24 bytes.
- CellAO: `N3MessageType.Despawn = 0x36510078`.
- CellAO: `DespawnMessageHandler` fills `Identity` and `Unknown = 1` on a key-only client quit packet.
- CellAO: `Playfield.Despawn(...)`, NPC cleanup, and corpse cleanup announce this as entity removal.

Stripdown source:
- `rebuild\docs\n3_to_client_quit_iir.md`
- `rebuild\docs\n3_drop_dynel_iir.md`

CellAO files to repair:
- `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3MessageType.cs`
- `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\DespawnMessage.cs`
- Add `DropDynelMessage.cs`
- `CellAO\Server\ZoneEngine\Core\MessageHandlers\DespawnMessageHandler.cs`
- `CellAO\Server\ZoneEngine\Core\InternalMessageHandler\DespawnMessageHandler.cs`
- `CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs`

Repair:
- Preserve `0x36510078` as `ToClientQuit` or stop exposing it as `Despawn`.
- Add `DropDynel = 0x47483633`.
- Serialize `DropDynel` as `Identity + float + float + float`.
- Use `DropDynel` for NPC/corpse/world-entity removal.
- The three floats are proven fields but not proven semantics. For despawn use, start with the dynel's current coordinates and log the values.

Why this is safe:
- The key and field layout are recovered from the current client.
- Current CellAO is definitely writing entity-removal data on a packet stripdown proves has no subclass body.

### 2. RelocateDynels exists in enum but has no message implementation

Definite gap:
- Stripdown: `RelocateDynelsIIR_t`, key `0x264B514B`.
- Layout: leading `Identity`, encoded count `(identity_count + 1) * 0x3F1`, then `Identity[]`, max decoded count `0x7530`.
- CellAO: enum value exists, but no `RelocateDynelsMessage` contract/serializer/handler.

Stripdown source:
- `rebuild\docs\n3_relocate_dynels_iir.md`

CellAO files to repair:
- Add `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\RelocateDynelsMessage.cs`
- Add serialization tests before runtime use.

Repair:
- Implement the message model and serializer support.
- Do not use it to drive NPC movement yet. The field layout is known, but runtime purpose is not fully proven.

Why this is safe:
- Test-only support increases decoder/serializer coverage without changing gameplay.
- It gives us a correct tool if a future capture shows the client expecting bulk dynel relocation.

### 3. PlayfieldAnarchyF body does not match recovered client structure

Definite mismatch:
- Stripdown: `PlayfieldAnarchyFIIR_t` is `n3PlayfieldFullUpdateIIR_t` base/prefix plus either a two-u32 tail or opaque generator-owned payload.
- CellAO: `PlayfieldAnarchyFMessage` uses a legacy fixed structure with coordinates, Playfield1/2 identities, vendor info, and playfield X/Z.

Stripdown source:
- `rebuild\docs\n3_playfield_anarchy_f_iir.md`
- `rebuild\docs\n3_playfield_full_update_iir.md`

CellAO files to repair:
- `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\PlayfieldAnarchyFMessage.cs`
- `CellAO\Server\ZoneEngine\Core\MessageHandlers\PlayfieldAnarchyFMessageHandler.cs`

Repair:
- Replace the old fixed body with a custom serializer that writes the recovered base header and either:
  - the no-generator two-u32 tail, if current CellAO login only needs that branch; or
  - opaque generator payload bytes sourced from capture.
- Do not invent generator semantics.

Risk:
- This is a login/playfield-entry packet. It is direct evidence-backed, but high blast radius. Patch only after saving a before/after login capture.

## Direct Test Repairs

Add serializer-size/source assertions for recovered fixed-size packets. These are low-risk because they do not change runtime behavior unless a test exposes a bad serializer.

High-value assertions:
- `DropDynel`: 24 bytes including IIR key.
- `ToClientQuit`: 4 bytes including IIR key.
- `RelocateDynels`: `16 + identity_count * 8` bytes; encoded count must be `(identity_count + 1) * 0x3F1`.
- `CharInPlay`: 4 bytes including key.
- `SetWantedDirection`: 16 bytes.
- `InventoryUpdated`: 8 bytes.
- `ContainerAddItem`: 24 bytes.
- `ClientMoveItemToInventory`: 16 bytes.
- `Resurrect`: 12 bytes.
- `Attack`: 13 bytes.
- `AttackInfo`: 36 bytes.
- `HealthDamage`: 32 bytes.
- `MissedAttackInfo`: 32 bytes.
- `SpecialAttackInfo`: 32 bytes.
- `StartLogout`: 12 bytes.
- `StopLogout`: 12 bytes.
- `StopFight`: 8 bytes.
- `Teleport`: custom live-shaped serializer already verified by playtest; keep this locked.
- `FullCharacter`: version gate must remain `26`.

## Missing Message Classes With Recovered Fixed Layouts

These are not urgent gameplay repairs, but they are clean source-backed gaps:

| Stripdown IIR | Key | Recovered size/shape | CellAO status |
| --- | ---: | --- | --- |
| `LocalityUpdate` | `0x4C530320` | 17 bytes | no enum/class |
| `ClientContainerAddItem` | `0x1F4D5F7E` | 20 bytes | no enum/class |
| `ClientGetItem` | `0x37136C6B` | 12 bytes | no enum/class |
| `DropDynel` | `0x47483633` | 24 bytes | enum only, no class |
| `LeaveBattle` | `0x3F3A1914` | 4 bytes | enum only, no class |
| `QueueUpdate` | `0x2C2F061C` | 8 bytes | enum only, no class |
| `GridSelected` | `0x3A322A4A` | 28 bytes | enum only, no class |
| `NewLevel` | `0x7F405A16` | 36 bytes | enum only, no class |
| `UpdateClientVisual` | `0x45072A2D` | 11 bytes | no enum/class |
| `Visibility` | `0x49222612` | 5 bytes | enum only, no class |

Do not wire these into runtime paths just because they exist. Add classes/tests when a gameplay system needs them or when a capture shows CellAO is missing them.

## Already Good Or Recently Repaired

- `N3Teleport`: repaired to current-client shape and verified in playtest; this fixed the white death/respawn screen.
- `FullCharacter`: message version `26` matches current client expectation; do not revert.
- `CharInPlay`: stripdown says key-only subclass body; CellAO class has no subclass members, which matches.
- `PlayfieldAllCities`: current uint16-sized opaque payload matches recovered fixed prefix shape.
- `InventoryUpdated`, `ContainerAddItem`, `ClientMoveItemToInventory`: basic fixed wire sizes and field counts match stripdown.
- `AttackInfo`, `HealthDamage`, `MissedAttackInfo`, `SpecialAttackInfo`: fixed field count/order appears aligned, but field semantics still need targeted capture before changing combat text/damage behavior.

## Do Not Repair From Stripdown Alone

### NPC chase/follow behavior

Stripdown gives useful packet contracts (`CharDCMove`, `SetWantedDirection`, `RelocateDynels`, `DropDynel`, `LocalityUpdate`, `Teleport`), but not enough proven runtime policy to keep changing AI movement blindly.

Needed source of truth before further chase work:
- Live/private capture with target identity, NPC identity, player position, NPC position, and timestamps correlated to local logs/video.
- Compare exact outgoing CellAO movement packets to captured live/private movement packets.

### HealthDamage normal auto-attacks

Stripdown proves `HealthDamage` field shape, not when the live server chooses it. Local normal attacks previously duplicated combat text when `HealthDamage` was sent with `AttackInfo`.

Rule:
- Keep normal weapon/MA hits as `AttackInfo` only.
- Add `HealthDamage` later only for DoT/HoT/nano/environment/status cases after targeted capture.

### PlayfieldAnarchyF generator payload semantics

The old CellAO packet is structurally wrong, but the generator-owned branch is intentionally opaque in stripdown. Use capture bytes or no-generator tail only. Do not invent city/vendor/resource semantics.

## Recommended Repair Order

1. Implement `DropDynel` and stop using `ToClientQuit` as entity despawn.
2. Add serializer/source assertions for `DropDynel`, `ToClientQuit`, `RelocateDynels`, and the already-fixed death/login packet set.
3. Add `RelocateDynelsMessage` as test-only infrastructure.
4. Capture login/playfield entry and repair `PlayfieldAnarchyF` with a known-good before/after.
5. Only then revisit NPC movement using packet comparison, not AI guesses.
