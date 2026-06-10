# Current Task

Generated: 2026-06-02

This is the primary handoff file. Update it before ending a work session.

## Current Objective

Stabilize CellAO behavior using packet evidence, local playtests, and focused source assertions. Inventory, corpse loot, corpse credits, player trade, and vendor buy/sell/close live persistence verification are complete for the documented repaired flows. The next highest-value unfinished area is static vendor coverage: broad shop stock data still has documented gaps, while the transaction behavior itself is now verified.

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
- Smoke harness cleanup completed: broad combat smoke `-SkipBuild`, focused corpse credit assertions, and inventory/container regression assertions all pass after stale source assertions were aligned with current repaired behavior. No gameplay behavior changed during this cleanup.
- Inventory Move Live Verification result: PASS. One junk item moved between inventory slots, stayed correct before relog, and remained in the correct slot after relog.
- Equip Item Live Verification result: PASS. Item equipped correctly before relog, no duplicate remained in inventory, and after relog the item remained equipped in the correct equipment slot.
- Unequip Item Live Verification result: PASS. Item moved from equipment to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item remained in inventory while the equipment slot stayed empty.
- Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.
- Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash persisted after relog, and no duplicate corpse credit feedback was observed.
- Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.
- Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.
- Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.
- Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.
- Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.
- Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.
- Live Persistence Verification complete: inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.
- `1183 ord_smarket_omni_basic` vendor coverage expansion completed. The 20 approved static vendor mappings were committed, imported into `cellao_codex_clean.vendors`, and verified with `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `730` to `710`; `1183 ord_smarket_omni_basic` dropped from `77` to `57`. A `vendors` table backup was created before import, and no runtime vendor behavior changed.
- `1184 ord_smarket_omni_advanced` vendor coverage expansion completed. The 21 approved static vendor mappings were committed, imported into `cellao_codex_clean.vendors`, and verified with `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `710` to `689`; `1184 ord_smarket_omni_advanced` dropped from `68` to `47`. A `vendors` table backup was created before import, and no runtime vendor behavior changed.
- `1185 ord_smarket_omni_sup` vendor coverage expansion completed. The 21 approved static vendor mappings were committed, imported into `cellao_codex_clean.vendors`, and verified with `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `689` to `668`; `1185 ord_smarket_omni_sup` dropped from `68` to `47`. A `vendors` table backup was created before import, and no runtime vendor behavior changed.
- `500 Parnassos` vendor coverage expansion completed. The 25 approved static vendor mappings were committed, imported into `cellao_codex_clean.vendors`, and verified with `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `668` to `643`; `500 Parnassos` dropped from `140` to `115`. A `vendors` table backup was created before import, and no runtime vendor behavior changed.
- `1182 ord_smarket_clan_sup` vendor coverage expansion completed. The 17 approved static vendor mappings are present in `cellao_codex_clean.vendors`; the latest import run did not insert duplicates because all 17 IDs already existed. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `643` to `626`; `1182 ord_smarket_clan_sup` dropped from `44` to `27`. A `vendors` table backup was created before the verification/import attempt, and no runtime vendor behavior changed.
- `655 Andromeda` vendor coverage expansion completed. The 16 approved static vendor mappings were committed, imported into `cellao_codex_clean.vendors`, and verified with `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `626` to `610`; `655 Andromeda` dropped from `17` to `1`. A `vendors` table backup was created before import, template `151987` remains unknown, and no runtime vendor behavior changed.
- `1180 ord_smarket_clan_basic` vendor coverage expansion completed. The 4 approved static vendor mappings were committed, imported into `cellao_codex_clean.vendors`, and verified with `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `610` to `606`; `1180 ord_smarket_clan_basic` dropped from `43` to `39`. A `vendors` table backup was created before import, and no runtime vendor behavior changed.
- `1181 ord_smarket_clan_advanced` vendor coverage expansion completed. Commit `fbcc1a4` added the 4 approved source SQL mappings, the targeted DB import inserted only those rows, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`. Total uncovered statel vendors dropped from `606` to `602`; `1181 ord_smarket_clan_advanced` dropped from `30` to `26`. No runtime vendor behavior changed.
- `2064 neut_basic_implants_shop` vendor coverage expansion completed. Commit `ed869d5` added the 3 approved source SQL mappings, the targeted DB import inserted only those rows, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`. Total uncovered statel vendors dropped from `602` to `599`; `2064 neut_basic_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- `2073 neut_advanced_implants_shop` vendor coverage expansion completed. Commit `a79b5ec` added the 3 approved source SQL mappings, the targeted DB import inserted only those rows, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`. Total uncovered statel vendors dropped from `599` to `596`; `2073 neut_advanced_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.

Currently unstable or unresolved:

- Broad static vendor coverage remains incomplete. Transaction behavior for vendor buy, sell, and close/cancel is verified; remaining work is data coverage for shop stock and statel-to-template mappings, not transaction semantics. Current post-import audit shows `596` uncovered statel vendors.
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
- Stale broad smoke assertions were cleaned up after source-backed repairs changed expected shapes. `Run-CombatSmokeTests.ps1 -SkipBuild`, `Run-CorpseCreditTraceAssertions.ps1`, and `Run-InventoryContainerRegressionAssertions.ps1` now pass.
- Inventory Move Live Verification result: PASS. One junk item moved correctly between inventory slots and persisted in the correct slot after relog.
- Equip Item Live Verification result: PASS. Item equipped correctly before relog, no duplicate remained in inventory, and the item stayed equipped in the correct equipment slot after relog.
- Unequip Item Live Verification result: PASS. Item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item stayed in inventory while the equipment slot stayed empty.
- Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.
- Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash value persisted after relog, and no duplicate corpse credit feedback was observed.
- Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.
- Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.
- Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.
- Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.
- Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.
- Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.
- Live Persistence Verification complete: inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.
- `1183 ord_smarket_omni_basic` vendor coverage expansion completed. Commit `6dfb390` added the 20 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `730` to `710`. The remaining uncovered count for `1183 ord_smarket_omni_basic` is `57`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `1184 ord_smarket_omni_advanced` vendor coverage expansion completed. Commit `aa8da43` added the 21 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `710` to `689`. The remaining uncovered count for `1184 ord_smarket_omni_advanced` is `47`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `1185 ord_smarket_omni_sup` vendor coverage expansion completed. Commit `e755c25` added the 21 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `689` to `668`. The remaining uncovered count for `1185 ord_smarket_omni_sup` is `47`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `500 Parnassos` vendor coverage expansion completed. Commit `d47f12e` added the 25 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `668` to `643`. The remaining uncovered count for `500 Parnassos` is `115`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `1182 ord_smarket_clan_sup` vendor coverage expansion completed. Commit `d7556bb` added the 17 approved source SQL mappings. The approved rows are present in `cellao_codex_clean.vendors`; the latest import run did not insert duplicates because all 17 IDs already existed. Post-import verification reduced total uncovered statel vendors from `643` to `626`. The remaining uncovered count for `1182 ord_smarket_clan_sup` is `27`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `655 Andromeda` vendor coverage expansion completed. Commit `9217459` added the 16 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `626` to `610`. The remaining uncovered count for `655 Andromeda` is `1`, with template `151987` intentionally left unknown. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `1180 ord_smarket_clan_basic` vendor coverage expansion completed. Commit `b6a6410` added the 4 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `610` to `606`. The remaining uncovered count for `1180 ord_smarket_clan_basic` is `39`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`.
- `1181 ord_smarket_clan_advanced` vendor coverage expansion completed. Commit `fbcc1a4` added the 4 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `606` to `602`. The remaining uncovered count for `1181 ord_smarket_clan_advanced` is `26`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`; `StatelVendorIssues = 602`.
- `2064 neut_basic_implants_shop` vendor coverage expansion completed. Commit `ed869d5` added the 3 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `602` to `599`. The remaining uncovered count for `2064 neut_basic_implants_shop` is `12`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`; `StatelVendorIssues = 599`.
- `2073 neut_advanced_implants_shop` vendor coverage expansion completed. Commit `a79b5ec` added the 3 approved source SQL mappings, the targeted DB import inserted only those rows after backing up `vendors`, and post-import verification reduced total uncovered statel vendors from `599` to `596`. The remaining uncovered count for `2073 neut_advanced_implants_shop` is `12`. `DataFileIssues`, `VendorDbIssues`, and `ShopInventoryIssues` are all `0`; `StatelVendorIssues = 596`.

## Immediate Next Steps

1. Audit and plan the next static vendor mapping pass for `565 Newland Desert`.
2. Compare current-client data verification output, statel template IDs, `vendortemplate` rows, and active shop inventory counts before proposing rows.
3. Keep inventory, corpse credits, player trade, and vendor transaction semantics out of scope unless a new verified regression appears.
4. Keep the temporary `TRADE_*` logging available until another trade issue appears or Mike approves removing it.
5. Do not patch NPC movement as part of vendor coverage work.
6. Run focused source/data assertions after any vendor coverage repair.
7. Start engines only when Mike asks for playtest.

## Recommended Next Task

Create a static vendor mapping audit and patch plan for `565 Newland Desert`. The current post-import audit shows `9` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence. It is now the highest safe candidate target after the `2073 neut_advanced_implants_shop` pass. Patch only confirmed mappings; do not guess unknown terminals or change inventory, corpse credit, player trade, or vendor buy/sell/close behavior.
