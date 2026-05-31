# Current Client vs CellAO Structure Mismatch Report

Generated: 2026-05-31

## Scope

This report compares the current CellAO codebase against the reverse-engineered
current-client files in:

- `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`
- `C:\Users\Mike\Documents\AO stripdown\Anarchy Online\rebuild`
- `C:\Users\Mike\Documents\AO stripdown\Anarchy Online\decompile_report`
- `C:\Users\Mike\Documents\New project`

The installed client under `C:\Funcom\Anarchy Online` matches the AO stripdown
binary set for the checked client-critical files:

| File | Hash match |
| --- | --- |
| `N3.dll` | yes |
| `Gamecode.dll` | yes |
| `MessageProtocol.dll` | yes |
| `Connection.dll` | yes |
| `DatabaseController.dll` | yes |
| `GUI.dll` | yes |
| `Anarchy.exe` | yes |

That means the AO stripdown reverse-engineered files are valid evidence for the
currently installed client, not stale notes.

## Definite Bad Spots

### 1. `PlayfieldAnarchyF` body is wrong in CellAO

Status: definite mismatch.

Current-client evidence:

- `rebuild\docs\n3_playfield_anarchy_f_iir.md`
- `rebuild\include\ao\n3_message.h`
- `rebuild\src\n3_message.cpp`

Recovered client structure:

- IIR key: `0x5F4B1A39`
- Body is based on `n3PlayfieldFullUpdateIIR_t`
- If no generator body follows, only an 8-byte subclass tail is recovered
- If a generator body follows, the rebuild records opaque generator payload
  metadata; it does not decode the current fixed CellAO field list

Current CellAO structure:

- `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\PlayfieldAnarchyFMessage.cs`
- `CellAO\Server\ZoneEngine\Core\MessageHandlers\PlayfieldAnarchyFMessageHandler.cs`

CellAO currently models this packet as a fixed legacy structure containing:

- character coordinates
- `Playfield1` / `Playfield2` identities
- two unknown ints
- `PlayfieldVendorInfo`
- playfield X/Z

Why this is bad:

The current-client reverse-engineered structure does not support that fixed
CellAO body as the canonical live layout. This is the same class of problem as
the fixed death respawn white screen: the client can receive bytes, but if the
body does not match the expected current-client shape, downstream world-init
state can fail silently or rely on later packets to paper over it.

Repair path:

1. Add a custom `PlayfieldAnarchyF` serializer.
2. Model the recovered `n3PlayfieldFullUpdateIIR_t` base prefix first.
3. Preserve generator-owned payload as opaque until its semantics are proven.
4. Stop emitting the old fixed vendor/X/Z structure as if it were the recovered
   current-client body.
5. Verify with a login/respawn capture that the local packet length and decoded
   body class match live.

### 2. Runtime dynel despawn uses the captured `Despawn` frame

Status: repaired after corpse-cleanup playtest regression.

Current-client evidence:

- `rebuild\docs\n3_to_client_quit_iir.md`
- `rebuild\docs\n3_drop_dynel_iir.md`
- `rebuild\include\ao\n3_message.h`

Recovered client structures:

- `n3ToClientQuitIIR_t`
  - key: `0x36510078`
  - recovered body: key-only
- `DropDynelIIR_t`
  - key: `0x47483633`
  - recovered body: identity plus three floats
  - fixed wire size: 24 bytes including key

Capture evidence:

- Local/private corpse and NPC cleanup captures show visible dynel removal as
  `0x36510078 + Identity + Unknown=1`.
- Example observed corpse removal body:
  `36510078 0000C76A 00F0F002 01`.
- Switching runtime removal to `DropDynel` left looted corpses visible in the
  current client.

Current CellAO structure:

- AOtomation names `0x36510078` as both key-only `ToClientQuit` and runtime
  `Despawn`
- AOtomation names `0x47483633` as `DropDynel`
- `ToClientQuitMessage` remains key-only
- `DespawnMessage` serializes the normal N3 identity/unknown frame
- `DropDynelMessage` serializes identity plus three floats
- `Playfield.Despawn(...)`, NPC cleanup, corpse cleanup, and teleport cleanup
  announce capture-backed `Despawn` for visible runtime removal

Why this mattered:

Static stripdown evidence proves `DropDynelIIR_t` exists, but runtime capture
evidence proves the current visible corpse/NPC cleanup path still uses the
identity-bearing `0x36510078` frame. Treating `DropDynel` as the runtime removal
packet made the client keep looted corpses visible.

Repair notes:

1. `ToClientQuitMessage` stays key-only for the source-backed quit packet.
2. `DespawnMessage` is restored for runtime visible dynel removal using the
   captured identity/unknown body.
3. `DropDynelMessage` stays modeled and source-tested, but should remain
   test-only until a capture proves its runtime side effect.

### 3. `RelocateDynels` is recovered in the client but not implemented in CellAO

Status: definite missing implementation.

Current-client evidence:

- `rebuild\docs\n3_relocate_dynels_iir.md`
- `rebuild\include\ao\n3_message.h`

Recovered client structure:

- IIR key: `0x264B514B`
- Leading identity
- Encoded count using `(identity_count + 1) * 0x3F1`
- Identity list payload
- Max decoded identities: `0x7530`

Current CellAO structure:

- `N3MessageType.RelocateDynels` exists
- No `RelocateDynelsMessage` class exists
- No serializer or handler exists

Why this is bad:

The current client has a recovered bulk dynel relocation/update packet, but
CellAO has no way to emit it. That leaves CellAO dependent on one-off movement,
set-position, or despawn packets for cases where the client has a dedicated
dynel relocation mechanism.

Repair path:

1. Add a report-only AOtomation message model first.
2. Add tests for the scaled count format.
3. Do not use it in runtime movement until a targeted live trace proves the
   trigger.
4. Use it as the candidate mechanism for bulk world corrections, zone scene
   adjustment, or dynel relocation only after capture evidence.

## Confirmed Good Or Already Repaired

### `N3Teleport`

Status: repaired and verified by playtest.

Current-client evidence:

- `rebuild\docs\n3_teleport_iir.md`

CellAO now sends:

- `Unknown = 0`
- playfield proxy marker `0x61`
- live playfield proxy identity type `0xC79E`
- `GameServerId = 1`
- `SgId = 0`
- `ChangePlayfield = Playfield2`
- trailing 12-byte destination payload

This fixed the death respawn white screen.

### `FullCharacter`

Status: key version gate repaired; nested body remains unresolved.

Current-client evidence:

- `rebuild\docs\n3_full_character_iir.md`
- `decompile_report\client_bootstrap_state.md`

Recovered current-client requirement:

- `FullCharacterIIR_t` key `0x29304349`
- version must be `26`

CellAO already sends version `26`, which fixed sit/stand together with the
live-style login state initialization.

### `CharInPlay`

Status: current AOtomation class matches recovered subclass body.

Current-client evidence:

- `rebuild\docs\n3_char_in_play_iir.md`

Recovered subclass body after the N3 base is key-only. The current
`CharInPlayMessage` has no subclass fields. The `Unknown` seen in code is the
base `N3Message.Unknown` byte, not a recovered `CharInPlay` subclass field.

### `CharDCMove`

Status: repaired and locked with source assertions.

Current-client evidence:

- `rebuild\docs\n3_movement_iir.md`
- `rebuild\include\ao\n3_message.h`

Recovered structure:

- IIR key: `0x54111123`
- Fixed body size: 54 bytes including key
- Tail after position is one int/tick field followed by two `float` aux fields

CellAO/AOtomation now models the tail as `Unknown1`, `AuxA`, and `AuxB`.
Compatibility `Unknown2`/`Unknown3` wrappers remain for old call sites, but the
wire serializer now emits floats in the recovered current-client layout.

### `PlayfieldAllCities`

Status: basic wire shape matches recovered current-client structure.

Current-client evidence:

- `rebuild\docs\n3_playfield_all_cities_iir.md`

CellAO now has `PlayfieldAllCitiesMessage` as a uint16-sized payload. The
internal city payload semantics remain unknown, so empty payload is acceptable
for now but not full live parity.

### Combat event packet shapes

Status: wire layouts mostly match; semantics remain partially unresolved.

Current-client evidence:

- `rebuild\docs\n3_attack_info_iir.md`
- `rebuild\docs\n3_health_damage_iir.md`
- `rebuild\docs\n3_missed_attack_info_iir.md`

The current AOtomation classes have the right fixed-size field counts/order for
`AttackInfo`, `HealthDamage`, and `MissedAttackInfo`. The exact field semantics
are still not fully assigned by the reverse-engineered client files. Keep normal
weapon/unarmed hits as `AttackInfo` only unless a targeted capture proves a
specific `HealthDamage` use case.

### Inventory update packet shapes

Status: basic wire shapes match recovered structures; semantics remain partly
unresolved.

Current-client evidence:

- `rebuild\docs\n3_inventory_updated_iir.md`
- `rebuild\docs\n3_container_add_item_iir.md`
- `rebuild\docs\n3_client_container_add_item_iir.md`
- `rebuild\docs\n3_client_move_item_to_inventory_iir.md`

Current CellAO/AOtomation structures match the recovered field counts/order for
the basic inventory/container packets. The exact role names remain partially
unknown in the reverse-engineered files, so avoid renaming semantics as fact
until capture-backed.

## Repair Priority

1. `PlayfieldAnarchyF` custom serializer and login/respawn packet comparison.
2. `DropDynel` implementation and switch NPC/corpse dynel removal away from
   key-only `Despawn`.
3. `RelocateDynels` message model and serializer tests, but keep runtime use
   gated behind live-capture evidence.
4. Continue combat/HealthDamage only after a targeted capture proves a visible
   or text bug.

## Rule Going Forward

For current-client parity work, treat these as source tiers:

1. Live official capture against the current client.
2. AO stripdown reverse-engineered files when installed-client hashes match.
3. Private-server capture for packet richness, clearly labeled as private.
4. Existing CellAO behavior only as legacy behavior, not proof of current-client
   correctness.
