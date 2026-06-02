# Bugs

Generated: 2026-06-02

## Critical - Corpse Credit Desync

Description: Looting some corpses produced unexpected client-visible credit values such as `111 credits from the corpse` or changing displayed credits.

Reproduction steps:

1. Start local engines.
2. Log in with a test character.
3. Kill a local test leet or spider.
4. Loot corpse credits.
5. Watch chat text and visible credit total.

Affected files:

- `CellAO/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ClientMoveItemToInventoryMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ContainerAddItemMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/StatMessageHandler.cs`
- possibly trade/inventory helpers under `CellAO/Libraries/Source/CellAO.Core/Inventory`

Suggested fix: Trace all credit mutation and credit-chat paths. Identify where the bad value enters the flow before patching. Do not clamp the symptom.

Status: Open.

## High - Player Trade Credit Transfer And Stale Item Visuals

Description: Player trade had sequences where credits failed, items showed stale in the trade window, or inventory visuals duplicated after trade.

Reproduction steps:

1. Dual log two local characters.
2. Start player trade.
3. Place item and/or credits.
4. Complete trade.
5. Reopen trade and inspect both trade windows and inventories.

Affected files:

- `CellAO/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ContainerAddItemMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ClientMoveItemToInventoryMessageHandler.cs`
- inventory page classes under `CellAO/Libraries/Source/CellAO.Core/Inventory`

Suggested fix: Compare exact live player-trade packet sequence against CellAO responses. Clear trade-window session state only through the correct player-trade packet flow.

Status: Open / partially repaired.

## High - NPC Chase Movement Jitter/Teleport/Circle Behavior

Description: NPCs have shown snapping, teleporting, circling, delayed pursuit, and death-position artifacts during combat movement.

Reproduction steps:

1. Spawn hostile test NPC.
2. Attack it.
3. Move around the NPC in a circle or move out of range.
4. Observe chase and attack animation behavior.

Affected files:

- `CellAO/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/FollowTargetMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `CellAO/Server/ZoneEngine/Core/EnemyBehaviorContract.cs`

Suggested fix: Stop runtime guessing. Mine captured chase windows into replay fixtures and compare local outgoing movement packets against evidence.

Status: Open.

## Medium - PlayfieldAnarchyF Current-Client Mismatch

Description: Current CellAO `PlayfieldAnarchyF` structure does not match recovered current-client stripdown structure.

Reproduction steps:

1. Capture login/playfield entry.
2. Compare CellAO packet shape to AO stripdown `n3_playfield_anarchy_f_iir` docs.

Affected files:

- `CellAO/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/PlayfieldAnarchyFMessage.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/PlayfieldAnarchyFMessageHandler.cs`

Suggested fix: Implement only with captured login/playfield-entry packet evidence.

Status: Open.

## Medium - CorpseFullUpdate Still Needs Cleanup

Description: Corpse creation works, but corpse full update is still described as debug/template-ish in existing notes.

Reproduction steps:

1. Kill NPC.
2. Capture corpse creation.
3. Compare local `CorpseFullUpdate` against live/private evidence and AOSharp model.

Affected files:

- `CellAO/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs`
- `CellAO/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- AOtomation corpse message model.

Suggested fix: Clean the packet builder toward evidence-backed current-client shape.

Status: Open.

## Low - Logs Can Become Too Noisy

Description: Movement and combat debugging can create excessive logs, making root-cause analysis harder.

Reproduction steps:

1. Enable broad movement/combat logging.
2. Run a playtest loop.
3. Inspect log size/noise.

Affected files:

- Logger call sites across `ZoneEngine`, especially movement/combat paths.

Suggested fix: Use targeted short-lived telemetry with entity IDs, timestamps, and phase labels.

Status: Open.

