# Current Task

Generated: 2026-06-02

This is the primary handoff file. Update it before ending a work session.

## Current Objective

Stabilize inventory, corpse loot, credits, and player trade behavior using packet evidence. The corpse credit desync investigation is complete; the next highest-value unfinished area is player trade credit/item display behavior.

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
- Correct player trade window selection reached during recent work, but item/credit display behavior still needed follow-up.

Currently unstable or unresolved:

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
- Corpse credit root cause was traced and repaired. The hardcoded `111` came from the old corpse cash template value, `CorpseFullUpdate` was corrected to patch the cash value word at offset `207`, and the server-side manual corpse credit `ChatText` was removed after the client generated the corrected message itself.
- `tools-temp/CellAOCombatSmokeTests/Run-CorpseCreditTraceAssertions.ps1` now guards the corpse credit flow, including credit roll ranges, `CorpseFullUpdate` cash offset `207`, delayed cash mutation, changed-stat emission, and item loot not mutating cash.
- Playtest verification passed on `Codex Test Cliff Malle`: the client displayed a single `You received 3 credits from the corpse.` line.

## Immediate Next Steps

1. Trace player trade credit and item display behavior end to end.
2. Compare trade packet fields against current local logs, AOSharp/AOtomation references, and any available capture evidence.
3. Verify whether stale trade-window item visuals are caused by trade-window close/reset packets, item return acks, inventory refresh, or stat/cash emission ordering.
4. Do not patch NPC movement as part of trade/inventory work.
5. Do not add broad inventory rewrites or cash clamps; patch only the confirmed trade emitter/path.
6. Run focused smoke/source assertions after any repair.
7. Start engines only when Mike asks for playtest.

## Recommended Next Task

Build a narrow trace table for player trade credit/item flow. For every possible trade credit mutation, item transfer, item return, trade-window update, stat send, and chat/error message, record:

- file and method,
- trigger packet or command,
- input value,
- cash/stat write,
- item inventory write,
- trade-window outbound packet,
- chat/error text value,
- outbound packet value,
- source evidence.

Then patch only the confirmed bad path.
