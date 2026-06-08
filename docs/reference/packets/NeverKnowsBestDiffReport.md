# Never Knows Best Source Diff Report

Generated: 2026-06-02

## Scope

External checkout:

- `C:\Users\Mike\Documents\New project\external\never-knows-best`

Local CellAO repo:

- `C:\Users\Mike\Documents\Cellao-Clean`

Primary direct comparison:

- AOSharp external packet models:
  - `aosharp\AOSharp.Common\SmokeLounge\AOtomation\Messaging\Messages`
- CellAO bundled AOtomation models:
  - `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages`

The AODB, navmesh, and clientless repos do not have the same one-to-one source layout as CellAO. They are reference/tooling sources, not direct replacement trees.

## High-Level AOSharp Message Diff

Direct AOSharp `Messages` to CellAO `Messages` comparison:

| Result | Count |
| --- | ---: |
| Identical files | 31 |
| Same or mapped file name but different content | 76 |
| AOSharp files with no local CellAO match | 58 |

Mapping rules used:

- same relative path first;
- for N3 messages where AOSharp uses `ContainerAddItem.cs` and CellAO uses `ContainerAddItemMessage.cs`, compare by adding the `Message` suffix;
- otherwise compare by same file name.

## Most Important Differences

### 1. `TradeMessage` is structurally/semantically different

AOSharp:

- file: `C:\Users\Mike\Documents\New project\external\never-knows-best\aosharp\AOSharp.Common\SmokeLounge\AOtomation\Messaging\Messages\N3Messages\TradeMessage.cs`
- fields:
  - `[0] int Unknown1`
  - `[1] TradeAction Action`
  - `[2] int Param1`
  - `[3] int Param2`
  - `[4] int Param3`
  - `[5] int Param4`

CellAO:

- file: `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\TradeMessage.cs`
- fields:
  - `[0] int Unknown1`
  - `[1] TradeAction Action`
  - `[2] Identity Target`
  - `[3] Identity Container`

This is the most relevant diff for current player-trade issues.

The wire length can still look correct because `Param1..Param4` is four ints and two `Identity` values are also four ints. The risk is semantic: CellAO forces all trade action payloads into two identities. AOSharp keeps them as four generic params, which is safer for action-specific layouts such as credits.

### 2. `TradeAction` enum differs

AOSharp:

- file: `C:\Users\Mike\Documents\New project\external\never-knows-best\aosharp\AOSharp.Common\SmokeLounge\AOtomation\Messaging\GameData\TradeAction.cs`
- values:
  - `Open = 0`
  - `Accept = 1`
  - `Decline = 2`
  - `Confirm = 3`
  - `Complete = 4`
  - `AddItem = 5`
  - `RemoveItem = 6`
  - `UpdateCredits = 7`
  - `OtherPlayerAddItem = 8`

CellAO:

- file: `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\TradeAction.cs`
- values:
  - `None = 0`
  - `End = 1`
  - `Decline = 2`
  - `Confirm = 3`
  - `Unknown = 4`
  - `AddItem = 5`
  - `RemoveItem = 6`
  - `Credits = 7`

Risk:

- CellAO's `End = 1` likely maps to AOSharp `Accept = 1`, not close/end.
- CellAO's `Unknown = 4` likely maps to AOSharp `Complete = 4`.
- CellAO has no `OtherPlayerAddItem = 8`.
- CellAO's `Credits = 7` likely maps to AOSharp `UpdateCredits = 7`.

Current CellAO handler reads player trade credits from `message.Target.Instance`. If the real action-7 payload is param-based, this can make credits fragile or wrong.

Relevant CellAO handler:

- `CellAO\Server\ZoneEngine\Core\MessageHandlers\TradeMessageHandler.cs`

### 3. `ClientContainerAddItem` has different modeling

AOSharp:

- file: `...\N3Messages\ClientContainerAddItem.cs`
- fields:
  - `[0] Identity Target`
  - `[1] Identity Source`

CellAO:

- file: `...\N3Messages\ClientContainerAddItemMessage.cs`
- fields:
  - `Identity1`
  - `Identity2`
- no `AoMember` attributes on the class itself.
- custom serializer exists in:
  - `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\Serializers\Custom\RecoveredN3MessageSerializers.cs`

Risk:

- This is not automatically broken because CellAO has a custom serializer.
- The naming is weaker than AOSharp's `Target`/`Source` and should be compared to live inventory/trade captures before using it in runtime paths.

### 4. `ContainerAddItem` names differ but field order matches

AOSharp:

- `[0] Identity Source`
- `[1] Identity Target`
- `[2] int Slot`

CellAO:

- `[0] Identity SourceContainer`
- `[1] Identity Target`
- `[2] int TargetPlacement`

This supports the existing conclusion that CellAO's historical names are misleading. The body shape is the same three fields, but callers must use the correct live meaning for identity 0, identity 1, and placement.

### 5. `ClientMoveItemToInventory` shape matches, name differs

AOSharp:

- `[0] Identity SourceContainer`
- `[1] int Slot`

CellAO:

- `[0] Identity SourceContainer`
- `[1] int TargetPlacement`

Shape is the same. Semantic naming differs.

### 6. `InventoryUpdate` shape mostly matches, names differ

AOSharp:

- `[0] int Unknown1`
- `[1] int Unknown2`
- `[2] InventorySlot[] Items`
- `[3] Identity InventoryIdentity`
- `[4] int Handle`
- `[5] int Unknown3`

CellAO:

- `[0] int NumberOfSlots`
- `[1] int Unknown1`
- `[2] InventoryEntry[] Entries`
- `[3] Identity BagIdentity`
- `[4] int SlotnumberInMainInventory`
- `[5] int Unknown2`

The nested item entry wire fields are effectively the same:

- placement/slot,
- flags,
- count,
- item identity,
- low id,
- high id,
- quality,
- unknown.

AOSharp's name `Handle` is probably more useful than CellAO's `SlotnumberInMainInventory` for corpse/trade windows. This is relevant to stale trade-window and corpse-window state.

### 7. `FollowTarget` is a major model divergence

AOSharp:

- `[0] FollowTargetType Type`
- `[1] byte Unknown1`
- `[2] polymorphic Info`
- `Type = 1` is `NpcPath`
- `Type = 2` is `Target`

CellAO:

- `[0] FollowInfo Info`
- separate capture-backed local data classes:
  - `FollowCoordinateInfo`
  - `FollowTargetInfo`
  - `FollowPositionInfo`
  - `FollowStopInfo`

Do not replace CellAO's current local model with AOSharp's older/simple model. CellAO's local model has recent capture/stripdown work behind it. Use AOSharp here as a comparison source only.

### 8. `CorpseFullUpdate` differs sharply

AOSharp has a full model:

- owner,
- position,
- heading,
- playfield,
- state machine,
- stats,
- name,
- animation effects,
- textures.

CellAO AOtomation class is intentionally marked obsolete/placeholder:

- file: `CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\CorpseFullUpdateMessage.cs`

CellAO runtime sends corpse dynels through:

- `CellAO\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs`

Risk:

- The runtime custom packet works enough for current local corpse tests.
- AOSharp provides a better first reference for cleaning this up, but it still needs capture comparison before replacing the custom builder.

### 9. `CharDCMove` differs because CellAO has current-client/stripdown repairs

AOSharp:

- `[0] MovementAction MoveType`
- `[1] Quaternion Heading`
- `[2] Vector3 Position`
- `[3] int DeltaTime`
- `[4] int Unknown2`
- `[5] int Unknown3`

CellAO:

- `[0] byte MoveType`
- `[1] Quaternion Heading`
- `[2] Vector3 Coordinates`
- `[3] int Unknown1`
- `[4] float AuxA`
- `[5] float AuxB`

CellAO's `float AuxA/AuxB` tail is from current-client stripdown work. Do not replace this with AOSharp's older int tail.

### 10. Combat packet names differ, field counts align

Examples:

- AOSharp `AttackInfo`: `Amount`, `AmmoCount`, `WeaponSlot`, `Target`, `Unk1`, `HitType`, `WeaponInstance`
- CellAO `AttackInfo`: `Unknown1..Unknown6` plus `Target`

The field count/order appears compatible, but AOSharp has better semantic names. Do not change combat values unless a visible/text combat bug appears.

## N3MessageType Differences

After normalizing hex values:

### Definite value mismatch

| Name | AOSharp | CellAO |
| --- | ---: | ---: |
| `ServerPathPosDebugInfo` | `0x3D746C70` | `0x3d746c7c` |

This should be checked against AO stripdown before changing anything.

### Same key, different names

| AOSharp | CellAO | Key |
| --- | --- | ---: |
| `Despawn` | `ToClientQuit` / `Despawn` alias | `0x36510078` |
| `PickUp` | `ClientGetItem` | `0x37136C6B` |
| `ChestFullUpdate` | `ChestItemFullUpdate` | `0x465A5D73` |
| `GiveQuestToMembers` | `GiveQuestToMember` | `0x77230927` |
| `LaserTagList` | `LaserTargetList` | `0x2933154F` |

These are mostly naming/alias issues, not necessarily runtime bugs.

### AOSharp has entries not present locally

Potentially useful later:

- `CentralControllerFullUpdate`
- `CentralControllerState`
- `ConstructBuilding`
- `ItemReplaced`
- `MarketSend`
- `RaidCmd`
- `ToggleCloak`
- `WaypointPath`

Do not wire these unless a tested system needs them.

### CellAO has entries not present in AOSharp

Several were added from AO stripdown/current-client work:

- `LocalityUpdate`
- `RelocateDynels`
- `ClientContainerAddItem`
- `ClientGetItem`
- `ToClientQuit`
- shop/player-shop messages
- `ResearchRequest`

These are not suspect just because AOSharp lacks them.

## AODB Comparison

AODB is not a direct CellAO server-code match. It is useful as a data parser/source reference.

Important AODB files:

- `aodb\ItemParser\ItemObject.cs`
- `aodb\AODB.Common\RDBObjects\ItemObject_1000020.cs`
- `aodb\AODB.Common\RDBObjects\MonsterData_1040023.cs`
- `aodb\AODB.Common\RDBObjects\PlayfieldDynels_1000026.cs`
- `aodb\AODB\Playfield\PlayfieldParser.cs`

The only meaningful same-name parser overlap found was:

- AODB: `aodb\AODB\Playfield\PlayfieldParser.cs`
- CellAO: `Tools\Algorithman\Extractor Serializer\PlayfieldParser.cs`

They are not equivalent:

- AODB parser uses an RDB controller and composes terrain/statel/water parser outputs.
- CellAO's old extractor parser manually parses byte buffers for doors, statels, destinations, and names.

Use AODB to improve data extraction and semantic labels. Do not treat it as a drop-in replacement for CellAO runtime code.

## Navmesh Comparison

`anarchy-online-navmeshes` contains `.navmesh` files. The only direct same-name overlap is with older AOSharp navigator assets already under `tools-temp\external`.

No current CellAO runtime path directly consumes these navmesh files.

Use them later only after movement packet flow is correct.

## AOSharp.Clientless Comparison

No direct CellAO server runtime file match was found that should be patched from this repo.

Use it as a protocol/login/chat harness reference after a focused review.

## Recommended Repair Order From This Diff

1. Use the official live trade captures to decode `TradeMessage` as `Unknown1 + Action + Param1..Param4`.
2. Compare those params to CellAO's current `Target`/`Container` identity assumptions in `TradeMessageHandler.cs`.
3. Fix trade credits and stale trade-window state only after that param table is built.
4. Keep `ContainerAddItem` and `ClientMoveItemToInventory` field order locked, but clean naming/docs around source/target/placement.
5. Use AOSharp `CorpseFullUpdate` as a cleanup reference, but do not replace the working custom CellAO corpse builder without capture replay.
6. Verify `ServerPathPosDebugInfo` key against AO stripdown.
7. Do not replace CellAO's current `FollowTarget`/`CharDCMove` repairs with AOSharp's older models.

## Repairs Applied From This Diff

### `TradeMessage` packet body

Applied 2026-06-02:

- `TradeMessage` now exposes the AOSharp/live-shaped body: `Unknown1`, `Action`, `Param1`, `Param2`, `Param3`, `Param4`.
- `Target` and `Container` remain as compatibility wrappers over `Param1/Param2` and `Param3/Param4` so existing open/add/remove item handling does not lose the identity interpretation.
- `TradeAction` now includes canonical action names from AOSharp/current-client evidence:
  - `Open = 0`
  - `Accept = 1`
  - `Decline = 2`
  - `Confirm = 3`
  - `Complete = 4`
  - `AddItem = 5`
  - `RemoveItem = 6`
  - `UpdateCredits = 7`
  - `OtherPlayerAddItem = 8`
- Legacy CellAO aliases remain during migration:
  - `None = Open`
  - `End = Accept`
  - `Unknown = Complete`
  - `Credits = UpdateCredits`

### Official live trade param table

Source:

- `tools-temp\live-pcaps\official-live-player-trade-exact\2026-05-31_20-35-53\ao_frames.jsonl`

Decoded C2S rows:

| Action | Meaning | Param1 | Param2 | Param3 | Param4 |
| ---: | --- | --- | --- | --- | --- |
| `0` | open player trade | target identity type | target identity instance | `0` | `0` |
| `5` | add item | offer-owner identity type | offer-owner identity instance | source container type, observed `104` inventory | source slot |
| `6` | remove item | offer-owner identity type | offer-owner identity instance | source trade-window type, observed `108` | source slot |
| `2` | decline/abort | observed `0` | observed `1` | `0` | `0` |

Action `7` credit rows were not present in that decoded capture. Current-client source symbols provide the credit field evidence:

- `N3Msg_TradeSetCash(int)`

Based on that source-backed function signature, CellAO now reads and writes player-trade credit amount through raw `Param1`, not `Target.Instance`.

Smoke assertions were added in:

- `tools-temp\CellAOCombatSmokeTests\Run-CombatSmokeTests.ps1`
