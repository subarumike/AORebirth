# Enemy/NPC DLL and AODB Map

Working notes for the enemy spawn/combat/corpse path. This is evidence gathered from the AO client DLLs, AODB/model-viewer tooling, live packet captures, and current CellAO code.

## Guardrails

- Database work must stay on `cellao_codex_clean`.
- Do not undo the sit/stand fix:
  - `FullCharacterMessageHandler.cs` uses FullCharacter `MsgVersion = 26`.
  - `ClientConnected.cs` initializes live-style action/movement state.
  - Fake server-side action bootstrap packets stay removed.
- After 3 attempts on one path, move to the next logical path. After 3 more, stop and re-evaluate.

## Useful Local Tools and Evidence

- AO client install: `C:\Funcom\Anarchy Online`
- Model/AODB tooling: `C:\Users\Mike\Documents\AO Decompiler\AO-Model-Viewer`
- AODB DLLs used:
  - `AO-Model-Viewer\Assets\Plugins\AODB.Common.dll`
  - `AO-Model-Viewer\Assets\Plugins\AODB.dll`
- AODB visual map:
  - `AO-Model-Viewer\Assets\Resources\CatMeshToMonsterData.txt`
- Live/capture tooling:
  - `C:\Users\Mike\Documents\AO Live Logger`
  - Capture with useful live Rhinoman death/corpse: `captures\2026-05-09_05-51-57`
  - Capture with client corpse `GenericCmd Use`: `captures\2026-05-09_06-05-13`

## Current CellAO Path Map

Spawn/load:
- `Server\ZoneEngine\Core\Playfields\Playfield.cs`
  - Constructor calls `LoadMobSpawns`, `LoadVendors`, `LoadStaticDynels`.
  - `LoadMobSpawns` loads `mobspawns` and `mobspawns_stats`, then calls `NonPlayerCharacterHandler.InstantiateMobSpawn`.
- `Libraries\Source\CellAO.Core\NPCHandler\NonPlayerCharacterHandler.cs`
  - `SpawnMobFromTemplate` reads `mobtemplate` and creates a `Character` with `NPCController`.
  - `InstantiateMobSpawn` creates persistent DB spawns from explicit stat rows.
- `Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs`
  - Current debug enemy path spawns `Codex Test Rhinoman` from template `A004`, then overrides visual/stats.

Client visibility:
- `Server\ZoneEngine\Core\Packets\SimpleCharFullUpdate.cs`
  - NPC detection is `NPCFamily != 0 && NPCFamily != 1234567890`.
  - Sends `SimpleNpcInfo` with family/LOS height.
  - Sends `MonsterData`, `MonsterScale`, `VisualFlags`, health, and optional head/mesh/texture data.

Combat:
- `Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs`
  - Handles Q attack toggle and sets `FightingTarget`.
- `Server\ZoneEngine\Core\MessageHandlers\StopFightMessageHandler.cs`
  - Clears `FightingTarget`.
- `Server\ZoneEngine\Core\Playfields\Playfield.cs`
  - `DoCombatTick` applies simple damage and sends `AttackInfo`.
  - `KillNpcTarget` currently sends live-like death sequence and a raw corpse full update.

Use/loot:
- `Server\ZoneEngine\Core\MessageHandlers\GenericCmdMessageHandler.cs`
  - `GenericCmdAction.Use = 3`.
  - Inventory target uses item.
  - Pooled target can run `OnUse` or `OnTrade`.
  - Non-pooled target falls through to `UseStatel`.
  - There is no corpse-specific path yet.
- `Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs`
  - Existing inventory move/add packet path.
- `Server\ZoneEngine\Core\MessageHandlers\TradeMessageHandler.cs`
  - Existing temporary-bag/vendor trade flow.
- `Libraries\Source\AOtomation\AOtomation.Messaging\Messages\N3Messages\CorpseFullUpdateMessage.cs`
  - Stub only, no real serializer.

Database loot:
- `mobtemplate` has `DropHashes`, `DropSlots`, `DropRates`.
- `mobdroptable` has `Hash`, `LowId`, `HighId`, `MinQl`, `MaxQl`, `RangeCheck`.
- `MobDroptableDao` exists, but runtime corpse loot rolling is not implemented.

## Live Death and Corpse Sequence

Live capture: `AO Live Logger\captures\2026-05-09_05-51-57\s2c_frames.jsonl`

Relevant packet sequence:

| pkt | message | identity | target | important fields |
| ---: | --- | --- | --- | --- |
| 1857 | `Attack` | `Char:3CAC6F14` | `Char:776B9578` | player attack |
| 1859 | `AttackInfo` | `Char:3CAC6F14` | `Char:776B9578` | damage/unknown1 `1400`, `u2=40`, `u3=8`, tail `000000040000000300000000` |
| 1859 | `StopFight` | `Char:3CAC6F14` | none | sent immediately on killing hit |
| 1861 | `CharacterAction` | `Char:776B9578` | none | action `99`, param2 `503` (`0x1F7`) |
| 1863 | `Feedback` | `Char:3CAC6F14` | none | message id observed as `0x6E0EE3EB` |
| 1863 | `CorpseFullUpdate` | `Corpse:00F0F001` | none | 414 bytes, name `Remains of Rhinoman Mother` |

Current CellAO approximates this order in `Playfield.KillNpcTarget`.

Important interpretation:
- `CharacterAction action=99, param2=503` is a live packet value.
- `503` is not the raw AODB `.ani` resource id. The AODB death animation for Rhinoman is `15397:rhinoman_die-pain_01_01.ani`.
- Treat `503` as the client action/animation key observed in the packet, not as a resource id.

## AODB MonsterData Map

Queried through `AODB.RdbController("C:\Funcom\Anarchy Online")`.

| Mob/source | MonsterData | CatMesh stat 12 | CatMesh name | useful anim keys |
| --- | ---: | ---: | --- | --- |
| Beach Leet `A004` | `17655` | `15222` | `cutecreature.cir` | `120 -> idle-stand`, `1034 -> attack-push`, `1037 -> attack-push2`, `6000 -> 18107:cutecreature_die-pain_01_01.ani` |
| Cheerleet `EERL` | `247832` | `247821` | `ai_cutecreature_cheerleetr.cir` | same cutecreature anim set, `6000 -> 18107:cutecreature_die-pain_01_01.ani` |
| Masculeet `ASCU` | `247831` | `247826` | `ai_cutecreature_mascu-leet.cir` | same cutecreature anim set, `6000 -> 18107:cutecreature_die-pain_01_01.ani` |
| Rhinoman test | `31114` | `31102` | `rhinoman_female.cir` | `1030 -> unarmed-start`, `1031 -> idle-unarmed`, `1032 -> unarmed-stop`, `1034 -> attack-teeth`, `1037 -> attack-head`, `6000 -> 15397:rhinoman_die-pain_01_01.ani` |

Useful conclusion:
- MonsterData already carries the CatMesh relationship through stat `12`.
- MonsterData carries visual animation choices by abstract anim keys.
- Key `6000` is the reliable AODB death animation mapping for these creature records.
- Current hardcoded `MonsterDataToCorpseCatMesh` can become data-driven from AODB-derived mappings later. For now, do not add a runtime dependency on the model-viewer DLLs inside ZoneEngine.

## Action and Animation Hints

CellAO docs:
- `ActionType.Attack = 11`
- `ActionType.CombatIdle = 16`
- `ActionType.CombatIdleStart = 26`
- `ActionType.CombatIdleEnd = 27`
- `CharacterActionType.ItemAnim = 99`

AODB MonsterData keys seen on the current creature records:
- `100` walk
- `101` run
- `120` idle stand
- `127` impact chest
- `200..203` spell animations
- `1030` unarmed/smallarms start
- `1031` combat idle
- `1032` unarmed/smallarms stop
- `1034` common creature attack
- `1037` alternate creature attack
- `6000` death pain

Function type names already in CellAO docs and matching client strings:
- `AnimAction = 53047`
- `SpawnMonster = 53049`
- `NpcFightSelected = 53077`
- `NpcFakeAttackOnTarget = 53145`
- `NpcEnableDieOfBoredom = 53146`
- `NpcToggleFightModeRegenrate = 53179`
- `NpcMovementAction = 53191`
- `NpcUseSpecialAttackItem = 53194`
- `NpcSetMoveToTarget = 53198`

Interpretation:
- Server combat should not need to send raw `.ani` resource ids for every hit. The client can resolve abstract MonsterData animation keys if the creature state/action is correct.
- `CharacterActionType.ItemAnim` with `param2=503` is confirmed for the live Rhinoman death case, but normal attack and idle transitions should be checked against live packets before hardcoding more action parameters.

## Client DLL Symbol Map

Client DLL string/export pass showed the following useful symbols.

`Gamecode.dll`:
- Messages/classes:
  - `AttackIIR_t`
  - `AttackInfoIIR_t`
  - `CharacterActionIIR_t`
  - `FightModeUpdate_t`
  - `StopFightIIR_t`
  - `HealthDamageIIR_t`
  - `CorpseFullUpdateIIR_t`
  - `BankCorpseIIR_t`
  - `SimpleCharFullUpdateIIR_t`
- Corpse/loot UI:
  - `CORPSE_INVENTORY`
  - `CorpseEntry_t`
  - `Corpse_t`
  - `N3Msg_SetLootAccess`
  - `N3Msg_UseItem`
  - `N3Msg_DefaultActionOnDynel`
- Combat UI/action:
  - `N3Msg_DefaultAttack`
  - `N3Msg_StopAttack`
  - `N3Msg_CanAttack`
  - `N3Msg_GetAttackingID`
  - `N3Msg_IsAttacking`
  - `N3Msg_GetCurrentFightMode`
- Feedback names:
  - `Feedback_Attacking`
  - `Feedback_UnableToAttackTarget`
  - `Feedback_PleaseWaitUntilPreviousAction`
  - `Feedback_UseAggDefSlider`
  - `Feedback_TargetIsAlreadyDead`
- Stat names present in client:
  - `e_DeadTimer`
  - `e_ItemAnim`
  - `e_Corpse_Hash`
  - `e_DisplayCATMesh`
  - `e_CorpseType`
  - `e_CorpseInstance`
  - `e_CorpseAnimKey`
  - `e_HasAlwaysLootable`
  - `e_SelectedTarget`
  - `e_IsFightingMe`
  - `e_PercentRemainingHealth`

Export notes from `objdump -p Gamecode.dll`:
- `N3Msg_DefaultActionOnDynel` export value `0x010F`.
- `N3Msg_DefaultAttack` export value `0x0110`.
- `N3Msg_CanAttack` export value `0x0101`.
- `N3Msg_GetAttackingID` export value `0x0128`.
- `N3Msg_GetCurrentFightMode` export value `0x013B`.
- `N3Msg_IsAttacking` export value `0x01A8`.
- `N3Msg_RequestInfoPacket` export value `0x01F7`.
- `N3Msg_SetLootAccess` export value `0x020E`.
- `N3Msg_StopAttack` export value `0x021B`.
- `N3Msg_UseItem` export value `0x0236`.

Do not confuse the `N3Msg_RequestInfoPacket` export value `0x01F7` with the live death `CharacterAction` param2 `0x01F7`; they are in different namespaces.

`N3.dll`:
- `n3EngineClient_t::ToClientDynelDead`
- `n3Dynel_t::Die`
- `n3VisualDynel_t::GetImpactAnim`
- `VisualCATMesh_t::SetCatMesh`
- `VisualCATMesh_t::RunFunction`
- `n3PlayfieldFullUpdateIIR_t::ReadSubClass/WriteSubClass/Activate`

Export notes from `objdump -p N3.dll`:
- `n3PlayfieldFullUpdateIIR_t::Activate`
- `n3PlayfieldFullUpdateIIR_t::ReadSubClass`
- `n3PlayfieldFullUpdateIIR_t::WriteSubClass`
- `n3EngineClient_t::ToClientDynelDead`

Takeaway:
- The client has a real corpse/dynel-dead path. It is not just a static mesh.
- Corpse persistence likely depends on a coherent corpse identity/state/loot-access model, not only a visual `CorpseFullUpdate` blob.

## Captured Corpse Use

Capture: `AO Live Logger\captures\2026-05-09_06-05-13\ao_frames.jsonl`

The client sends `GenericCmd` to use the corpse:

- N3 id `0x52526858` (`GenericCmd`)
- `Action = 3` (`Use`) once decoded with CellAO's serializer layout
- `User = Char:3CAC6F14`
- `Target = Corpse:00F0F001`

The analyzer summary currently prints some fields byte-swapped, for example `action=117440512`, but the frame body contains `00000003` at the action position and matches `GenericCmdAction.Use`.

Server implication:
- A live/usable corpse should be addressable by `IdentityType.Corpse`.
- `GenericCmdMessageHandler` needs a corpse branch before the `UseStatel` fallback.
- The corpse identity must map to a server-side corpse/loot state. Right now raw corpse ids are not registered in `Pool`.

## Current Likely Failure Points

1. Corpse identity is visual-only.
   - `Playfield.SendDebugCorpseFullUpdate` sends a raw corpse packet and schedules a despawn.
   - It does not add a corpse object/state to `Pool`.
   - `GenericCmd Use` on the corpse cannot resolve to an entity, so it falls through as a statel.

2. Corpse state is not modeled as first-class data.
   - Relevant stats exist: `Corpse_Hash` 398, `CorpseType` 415, `CorpseInstance` 416, `CorpseAnimKey` 417, `HasAlwaysLootable` 345.
   - Current dead NPC code only sets health/state/deadtimer/itemanim/corpseanimkey/dieanim.
   - Current raw `CorpseFullUpdate` may make the client briefly create a corpse, then discard it because the surrounding corpse/loot access state is inconsistent.

3. Loot tables exist but are unused.
   - `mobtemplate.DropHashes/DropSlots/DropRates` describes roll groups.
   - `mobdroptable` describes possible item records.
   - No runtime path rolls loot on death or ties rolled items to a corpse identity.

4. Death animation should stay packet-faithful, but visual data should be data-driven.
   - Keep live observed `CharacterAction(99, param2=503)` for Rhinoman death unless a capture proves another value.
   - Use MonsterData key `6000` to find which actual AODB animation a creature owns, especially when moving away from one hardcoded Rhinoman.

## Smallest Useful Next Implementation Path

1. Add a minimal in-memory corpse registry in the playfield.
   - Key: `IdentityType.Corpse` instance.
   - Value: original NPC identity, name, coordinates, MonsterData/CatMesh, lifetime, rolled credits/items.
   - This can be local to `Playfield` at first. No schema change.

2. Register the corpse before sending `CorpseFullUpdate`.
   - Allocate fresh corpse identity.
   - Store corpse state.
   - Set dead NPC `CorpseType`, `CorpseInstance`, `CorpseAnimKey`, and possibly `HasAlwaysLootable`.
   - Keep current live packet order.

3. Add a corpse branch in `GenericCmdMessageHandler`.
   - If `message.Action == Use` and target type is `Corpse`, call a playfield corpse-use method.
   - For the first pass, log and acknowledge the correct corpse identity.
   - Then open a minimal loot container only after the response packet is known.

4. Use captures to identify the server response to corpse use/loot.
   - Look specifically for `BankCorpse`, `ContainerAddItem`, inventory/temp-bag packets, and any `SetLootAccess`-like action.
   - Do not guess the loot window protocol from vendor trade unless capture evidence matches.

5. Move creature visual mapping out of hardcoded one-offs.
   - Short term: extend a small static map for tested MonsterData ids (`17655`, `247832`, `247831`, `31114`).
   - Medium term: generate a checked-in data file from AODB for `MonsterData -> CatMesh -> death anim key/resource`.

## Suggested Morning Test Target

Use the same Rhinoman test mob until corpse identity/use is stable:

- Kill the mob.
- Confirm player leaves fighting state automatically.
- Confirm corpse stays for 30 seconds if no item loot.
- Press Use/Open on the corpse.
- Server log should show `GenericCmd Use target=Corpse:<new corpse id>` and match the visible corpse, not a stale/other corpse.

Once this is stable, switch back to a leet/cutecreature test to confirm the AODB-driven mapping handles `MonsterData 17655` and death anim key `6000`.
