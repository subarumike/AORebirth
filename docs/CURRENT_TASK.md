# Current Task

Generated: 2026-06-02

This is the primary handoff file. Update it before ending a work session.

## Current Objective

Stabilize inventory, corpse loot, credits, and player trade behavior using packet evidence. The immediate symptom under investigation is client-visible credit/item desync after corpse loot and trade flows.

## Current Implementation State

Verified or previously playtested as working:

- Weapon and armor equip visuals.
- Weapon attacks using equipped weapons instead of martial arts fallback.
- Dual wield attack alternation.
- Equipment delay synchronization.
- Equipped items persisting across relog.
- Death/respawn white-screen repair.
- Basic corpse creation, loot window, item looting, unique-item checks, and corpse despawn timing.
- Correct player trade window selection reached during recent work, but item/credit display behavior still needed follow-up.

Currently unstable or unresolved:

- Corpse credit desync where local client displayed unexpected values such as `111 credits from the corpse` or changing visible credit totals after loot.
- Trade credits failed or desynced during testing.
- Stale trade-window item visuals appeared after completed trades in some sequences.
- NPC movement remains high-risk and should not be patched without source/capture evidence.

## Files Actively Being Modified Or Recently Dirty

Check `git status --short --branch` before editing. At the time these docs were created, the dirty tree included:

- `CellAO/Documentation/Index.md`
- `CellAO/Documentation/ProjectWorkingReference.md`
- `CellAO/Libraries/Source/AOtomation/AOtomation.Messaging`
- `CellAO/Libraries/Source/CellAO.Core/Entities/TemporaryBag.cs`
- `CellAO/Libraries/Source/CellAO.Stats/Stats.cs`
- `CellAO/Server/ZoneEngine/Core/CombatCorpseRules.cs`
- `CellAO/Server/ZoneEngine/Core/Controllers/PlayerController.cs`
- `CellAO/Server/ZoneEngine/Core/Functions/GameFunctions/modify.cs`
- `CellAO/Server/ZoneEngine/Core/Functions/GameFunctions/modifypercentage.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ChatCmdMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ClientMoveItemToInventoryMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/ContainerAddItemMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/StatMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `CellAO/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `tools-temp/CellAOCombatSmokeTests/Run-CombatSmokeTests.ps1`
- `tools-temp/db-backups/`
- New root `docs` handoff files.

Do not revert these blindly. Some are active user/project work.

## Known Blockers

- Packet semantics for some inventory/trade/corpse fields are still being separated from old CellAO naming.
- The client may display values from packet placement fields if the wrong response path is used.
- Trade and corpse loot flows overlap through inventory/container messages, so a fix in one path can regress the other.
- Local playtest timing matters; Mike performs in-client validation.

## Recent Progress

- External Never Knows Best repos were inspected. AOSharp and AODB are useful reference/tooling sources.
- AOSharp `ContainerAddItem`/inventory message models support the need to compare CellAO packet fields against newer client-side models.
- Existing docs under `CellAO/Documentation` now contain several high-value packet and mismatch reports.
- Root AI handoff docs were created to preserve context across sessions.

## Immediate Next Steps

1. Inspect local code paths that can emit corpse-credit chat text or modify cash stats.
2. Trace corpse loot from client request through:
   - `GenericCmd Use`
   - `InventoryUpdate`
   - `ClientMoveItemToInventory`
   - `ContainerAddItem`
   - stat/cash update
   - chat feedback
3. Compare emitted local packets with captured official/private packet evidence.
4. Do not add a workaround that clamps values without identifying the source of the bad value.
5. Run smoke/source tests after any repair.
6. Start engines only when Mike asks for playtest.

## Recommended Next Task

Build a narrow trace table for corpse credit flow. For every possible credit mutation or credit chat send, record:

- file and method,
- trigger packet or command,
- input value,
- stat write,
- chat text value,
- outbound packet value,
- source evidence.

Then patch only the confirmed bad path.

