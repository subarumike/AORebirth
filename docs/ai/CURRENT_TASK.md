# Current Task

Generated: 2026-06-02

This is the primary handoff file. Update it before ending a work session.

## Current Objective

Stabilize inventory, corpse loot, credits, and player trade behavior using packet evidence. The corpse credit desync investigation and player-to-player trade verification are complete; the next highest-value unfinished area is broader inventory/container regression coverage across repaired flows.

## Current Implementation State

Verified or previously playtested as working:

- Weapon and armor equip visuals.
- Weapon attacks using equipped weapons instead of martial arts fallback.
- Dual wield attack alternation.
- Equipment delay synchronization.
- Equipped items persisting across relog.
- Death/respawn white-screen repair.
- Basic corpse creation, loot window, item looting, unique-item checks, credit loot, and corpse despawn timing.
- Corpse credit investigation completed: `CorpseFullUpdate` now patches corpse cash at offset `207`, manual corpse credit chat feedback is suppressed, focused assertions are retained, and Cliff Malle playtest showed one correct `3 credits` client message.
- Player-to-player trade verification passed: credit-only, item-only, mixed item-plus-credit, and cancel/decline trades behaved as expected. No player trade display or commit defect was reproduced. Temporary `TRADE_*` logging remains available for future trade investigation.

Currently unstable or unresolved:

- Inventory and container behavior work for several repaired flows but still need broader regression coverage.
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
- Corpse credit root cause was traced and repaired. The hardcoded `111` came from the old corpse cash template value, `CorpseFullUpdate` was corrected to patch the cash value word at offset `207`, and the server-side manual corpse credit `ChatText` was removed after the client generated the corrected message itself.
- `tools-temp/CellAOCombatSmokeTests/Run-CorpseCreditTraceAssertions.ps1` now guards the corpse credit flow, including credit roll ranges, `CorpseFullUpdate` cash offset `207`, delayed cash mutation, changed-stat emission, and item loot not mutating cash.
- Playtest verification passed on `Codex Test Cliff Malle`: the client displayed a single `You received 3 credits from the corpse.` line.
- Player-to-player trade verification passed after temporary `TRADE_*` logging was added. Credit-only, item-only, mixed item-plus-credit, and cancel/decline trades all behaved as expected; no display or commit defect was reproduced.

## Immediate Next Steps

1. Build broader inventory/container regression coverage for the repaired corpse loot, player trade, vendor shop, equipment, and normal inventory move flows.
2. Keep the temporary `TRADE_*` logging available until another trade issue appears or Mike approves removing it.
3. Compare any future inventory/container failures against current local logs, AOSharp/AOtomation references, and available capture evidence.
4. Do not patch NPC movement as part of trade/inventory work.
5. Do not add broad inventory rewrites or cash clamps; patch only confirmed packet or persistence paths.
6. Run focused smoke/source assertions after any repair.
7. Start engines only when Mike asks for playtest.

## Recommended Next Task

Build a focused inventory/container regression checklist and assertions for repaired flows. Cover:

- normal inventory moves,
- equipment move acknowledgements,
- corpse item loot and credit loot,
- player trade item/credit completion and cancel/decline,
- vendor buy/sell/close inventory and credit changes,
- relog persistence after each flow.

Then patch only confirmed failures, keeping trade, corpse loot, vendor shop, and equipment paths separated.
