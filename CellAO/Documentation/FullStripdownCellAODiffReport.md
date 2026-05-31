# Full Stripdown vs CellAO N3 Diff Report

Date: 2026-05-31

Scope:
- CellAO: `C:\Users\Mike\Documents\Cellao-Clean`
- Stripdown docs/source: `C:\Users\Mike\Documents\AO stripdown\Anarchy Online\rebuild`
- Compared recovered `kN3*IIRKey` and wire-layout constants from `rebuild\include\ao\n3_message.h` plus `rebuild\docs\*_iir.md` against CellAO `N3MessageType`, AOtomation message classes, and obvious ZoneEngine usages.

Important caveat:
- This is a static protocol diff. It is good for finding wrong keys, missing message contracts, and obvious stale structures.
- It does not prove gameplay policy such as NPC chase behavior. Runtime behavior still needs packet capture plus local log/video correlation.

## Summary Counts

Recovered stripdown N3 IIR contracts compared: 79

| Status | Count | Meaning |
| --- | ---: | --- |
| OK by key/class coverage | 43 | CellAO has matching enum key and an AOtomation class or acceptable alias/custom path. |
| Key mismatch | 2 | CellAO has a similarly named enum, but the key differs from stripdown. |
| Missing enum | 5 | Stripdown recovered a packet key that CellAO does not expose in `N3MessageType`. |
| Missing class | 25 | CellAO has the enum key, but no AOtomation message class. |
| Alias/name mismatch | 4 | Key exists, but CellAO name differs or hides a semantic mismatch. |

## Hard Mismatches

These are definite differences between current-client stripdown evidence and CellAO code.

### 1. `ToClientQuit`, captured `Despawn`, and source `DropDynel` share lifecycle territory

Stripdown:
- `n3ToClientQuitIIR_t`
- key `0x36510078`
- key-only, fixed wire size 4

CellAO:
- `N3MessageType.ToClientQuit = 0x36510078`
- `N3MessageType.Despawn = 0x36510078`
- `ToClientQuitMessage` is key-only
- `DespawnMessageHandler` fills `Identity`/`Unknown`
- `Playfield.Despawn(...)` uses captured `Despawn` for NPC/corpse/world dynel removal

Correct stripdown dynel-removal packet:
- `DropDynelIIR_t`
- key `0x47483633`
- fixed wire size 24
- fields: `Identity_t` plus three floats

Runtime capture correction:
- Visible corpse/NPC cleanup in local/private captures uses
  `0x36510078 + Identity + Unknown=1`.
- Example corpse body: `36510078 0000C76A 00F0F002 01`.
- A local playtest with runtime `DropDynel` left looted corpses visible.

Repair:
- Keep `ToClientQuit` key-only.
- Use captured `Despawn` for current visible runtime removal.
- Keep `DropDynel = 0x47483633` as source-backed model/test coverage until a capture proves its runtime side effect.

### 2. `TeamInvite` key mismatch

Stripdown:
- `TeamInviteIIR_t`
- key `0x4D2A313B`
- fixed prefix 15 bytes without string, extra 9 bytes, max string `0x7FFF`

CellAO:
- `TeamInvite = 0x4D2A3A38`
- no `TeamInviteMessage` class

Repair:
- Correct enum key only when implementing/testing team invites. This is source-backed, but not urgent unless team invite work starts.

### 3. `UpdateClientVisual` key mismatch

Stripdown:
- `UpdateClientVisualIIR_t`
- key `0x45072A2D`
- fixed wire size 11

CellAO:
- `UpdateClientVisual = 0x45072A0B`
- no message class

Repair:
- Correct enum key before implementing this packet. This may matter for visual refresh systems later.

### 4. `PlayfieldAnarchyF` body is stale

Stripdown:
- `PlayfieldAnarchyFIIR_t`
- key `0x5F4B1A39`
- body is recovered `n3PlayfieldFullUpdateIIR_t` base/prefix plus either two `u32` tail values or opaque generator-owned payload metadata

CellAO:
- `PlayfieldAnarchyFMessage` uses a fixed legacy body containing character coordinates, Playfield1/2 identities, vendor info, and playfield X/Z.

Repair:
- Replace only after a login/playfield-entry capture, because this is high blast radius.
- Do not invent generator payload semantics.

### 5. `SetStat` class is under-modeled against recovered body

Stripdown:
- `SetStatIIR_t`
- key `0x6E5F566E`
- fixed wire size 36
- eight consecutive `i32` values after the key

CellAO:
- `SetStatMessage` only models `Value` and `Stat`.

Repair:
- Do not blindly patch stat sending until a capture labels the eight ints.
- Add a source assertion/documentation warning so future stat work does not assume the two-field class is current-client complete.

### 6. `CharDCMove` field names/types are stale even though total size can line up

Stripdown movement:
- key `0x54111123`
- fixed wire size 54
- layout: key, identity, pass-on flag, action, quaternion, position, tick/delta, two float aux values

CellAO:
- `CharDCMoveMessage` uses `MoveType`, quaternion, coordinates, and three `int` unknowns.
- The final two aux fields are typed as `int`, while stripdown says they are `float32`.
- The base `N3Message.Unknown` appears to correspond to the pass-on flag, and subclass `MoveType` appears to correspond to action.

Repair:
- Rename fields to match recovered meaning.
- Change the final aux fields to `float` only after verifying all current send/read paths.
- This is directly relevant to movement debugging, but still needs packet comparison before changing NPC policy.

## Recovered In Stripdown, Missing Entirely From CellAO Enum

| Stripdown IIR | Key | Shape |
| --- | ---: | --- |
| `CentralControllerState` | `0x08536F65` | fixed 5 bytes |
| `ClientContainerAddItem` | `0x1F4D5F7E` | fixed 20 bytes |
| `ClientGetItem` | `0x37136C6B` | fixed 12 bytes |
| `LocalityUpdate` | `0x4C530320` | fixed 17 bytes |
| `ToServerUnBlock` | `0x4655752E` | key-only 4 bytes |

Notes:
- `LocalityUpdate` is movement-adjacent and worth adding as decode/test infrastructure.
- `ClientContainerAddItem` and `ClientGetItem` are inventory/loot-adjacent and worth adding when tightening item flow.

## Enum Exists But Message Class Is Missing

These are known keys in CellAO, but there is no AOtomation N3 message class to serialize/deserialize them.

| IIR | Key | Stripdown shape |
| --- | ---: | --- |
| `Absorb` | `0x264E5F61` | fixed 12 |
| `AcceptBSInvite` | `0x166A435E` | fixed 13 |
| `ArriveAtBs` | `0x540E3B27` | vector list, `0x3F1` count scale |
| `BattleOver` | `0x4B062919` | fixed 12 |
| `CharSecSpecAttack` | `0x51492120` | fixed 16 |
| `CityAdvantages` | `0x365E555B` | records, fixed prefix 8 |
| `DropDynel` | `0x47483633` | fixed 24 |
| `DropTemplate` | `0x3A243F41` | fixed 16 |
| `GfxTrigger` | `0x7A222202` | min fixed 12 |
| `GridSelected` | `0x3A322A4A` | fixed 28 |
| `InfromPlayer` | `0x3301337A` | fixed 12 |
| `LeaveBattle` | `0x3F3A1914` | key-only 4 |
| `NewLevel` | `0x7F405A16` | fixed 36 |
| `PlayfieldTowerUpdateClient` | `0x5B1E052C` | fixed 16 |
| `Quest` | `0x212C487A` | fixed 28, version gate 1 |
| `QueueUpdate` | `0x2C2F061C` | fixed 8 |
| `ReflectAttack` | `0x1C3A4F77` | fixed 20 |
| `Reload` | `0x26515E61` | fixed 12 |
| `RelocateDynels` | `0x264B514B` | identity list, `0x3F1` count scale |
| `Script` | `0x204F4871` | fixed prefix 40 plus strings |
| `SendScore` | `0x44483B3A` | value lists, `0x3F1` count scale |
| `SetName` | `0x734E5A7B` | fixed prefix 25 plus name |
| `SpawnMech` | `0x464D000A` | fixed 8 |
| `TrapDisarmed` | `0x2A253F5F` | key-only 4 |
| `Visibility` | `0x49222612` | fixed 5 |

Highest-value missing/classes to keep covered:
- `DropDynel`: source-backed layout exists; keep test-only until runtime use is captured.
- `RelocateDynels`: movement/lifecycle-adjacent; add test-only first.
- `LocalityUpdate`: no enum/class yet, movement-adjacent.
- `ClientContainerAddItem`/`ClientGetItem`: item/loot-adjacent.
- `BattleOver`/`LeaveBattle`: combat state cleanup.

## Alias Or Naming Mismatches

| Stripdown name | Key | CellAO name | Assessment |
| --- | ---: | --- | --- |
| `ToClientQuit` | `0x36510078` | `ToClientQuit`/`Despawn` | Same key has key-only source packet and captured identity-bearing runtime removal frame. |
| `Teleport` | `0x43197D22` | `N3Teleport` | Acceptable alias; custom serializer already fixed death/white screen. |
| `PlayfieldFullUpdate` | `0x30161355` | `N3PlayfieldFullUpdate` | Acceptable alias, but no class. |
| `GiveQuestToMembers` | `0x77230927` | `GiveQuestToMember` | Naming singular/plural mismatch; no class. |

## CellAO Enum Values Not Recovered In Stripdown Docs

These are not automatically wrong. They are CellAO-known packet names for which this stripdown pass did not have a recovered `*_iir.md` contract.

| CellAO enum | Key | Class status |
| --- | ---: | --- |
| `AddPet` | `0x194E4F76` | class exists |
| `AddTemplate` | `0x052E2F0C` | class exists |
| `AoTransportSignal` | `0x62741E15` | no class |
| `AppearanceUpdate` | `0x41624F0D` | class exists |
| `ApplySpells` | `0x342C1D1D` | no class |
| `BankCorpse` | `0x52213420` | no class |
| `Buff` | `0x39343C68` | class exists |
| `ChatCmd` | `0x5C525A7B` | class exists |
| `ChestItemFullUpdate` | `0x465A5D73` | class exists |
| `Clone` | `0x3C265179` | no class |
| `CorpseFullUpdate` | `0x4F474E05` | class exists |
| `DoorFullUpdate` | `0x365A5071` | class exists |
| `DoorStatusUpdate` | `0x4C7D403B` | class exists |
| `FightModeUpdate` | `0x371D0542` | class exists |
| `FollowTarget` | `0x260F3671` | class exists |
| `GenericCmd` | `0x52526858` | class exists |
| `InfoPacket` | `0x4D38242E` | class exists |
| `InventoryUpdate` | `0x4E536976` | class exists |
| `KnuBot*` family | multiple | mostly classes exist |
| `OrgClient` | `0x7F4B3108` | class exists |
| `PerkUpdate` | `0x435F7023` | class exists |
| `PetToMaster` | `0x0D381F02` | class exists |
| `PlaySound` | `0x455D2938` | class exists |
| `QuestAlternative` | `0x5C436609` | class exists |
| `QuestFullUpdate` | `0x465A4061` | class exists |
| `ResearchRequest` | `0x3115534D` | class exists |
| `ResearchUpdate` | `0x253D0240` | class exists |
| `SetPos` | `0x195E496E` | class exists |
| `ShopUpdate` | `0x58362220` | class exists |
| `SimpleItemFullUpdate` | `0x3B11256F` | class exists |
| `Skill` | `0x3E205660` | class exists |
| `SocialActionCmd` | `0x3B290771` | class exists |
| `SpellList` | `0x4D450114` | class exists |
| `Stat` | `0x2B333D6E` | class exists |
| `StopMovingCmd` | `0x742E2314` | class exists |
| `TrapItemFullUpdate` | `0x59313928` | no class |
| `VendingMachineFullUpdate` | `0x7F544905` | class exists |
| `WeaponItemFullUpdate` | `0x3B1D2268` | class exists |

This bucket needs either more stripdown recovery or live captures before declaring anything bad.

## Existing Classes With Known Recovered Shape To Audit

These classes exist in CellAO, but stripdown has enough body detail that they deserve source assertions or capture comparison before more gameplay depends on them:

| Class | Reason |
| --- | --- |
| `SetStatMessage` | Stripdown has eight `i32`; CellAO models only `Value` and `Stat`. |
| `PlayfieldAnarchyFMessage` | CellAO body is legacy/stale versus recovered base-header/tail/generator shape. |
| `CharDCMoveMessage` | Total size can line up through base fields, but aux field types/names are stale. |
| `TemplateActionMessage` | Equipment works now, but stripdown shows a strict marker and fixed size; add serialization assertion before future item-use changes. |
| `TradeMessage` | Stripdown shows marker/fixed size; audit before trade work. |
| `TeamMemberInfoMessage` | Fixed layout exists in stripdown; verify before team UI work. |
| `WeatherControlMessage` | Fixed size appears aligned; add assertion because visual/environment effects are client-sensitive. |

## Recommended Repair Order From This Full Diff

1. Keep `ToClientQuit`/captured `Despawn`/`DropDynel` separated by evidence: key-only quit, runtime removal, source-backed test-only dynel-drop.
2. Add source assertions for known fixed-size packets, starting with death/logout/movement/combat/inventory packets.
3. Correct enum-only key mismatches for `TeamInvite` and `UpdateClientVisual`, but do not wire behavior until those systems are tested.
4. Add decode/test-only classes for `RelocateDynels`, `LocalityUpdate`, `ClientContainerAddItem`, and `ClientGetItem`.
5. Audit `CharDCMoveMessage` field names/types against stripdown and live captures before further NPC movement edits.
6. Repair `PlayfieldAnarchyF` only with a captured login/playfield-entry packet in hand.
