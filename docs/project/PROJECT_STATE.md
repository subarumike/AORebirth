# Current Status

CellAO NightPredator is a local C#/.NET Framework-era Anarchy Online server workspace. Current work is focused on making the server compatible with Mike's current AO client and local `cellao_codex_clean` MySQL database through evidence-backed packet, gameplay, and data repairs.

# Working Systems

- Login, chat, and zone engines build and run locally.
- Current-client `FullCharacter` version 26 and live-style login state are locked project decisions.
- Sit/stand behavior is repaired.
- Weapon and armor equipment visuals are repaired for the current test scope.
- Equipped items persist across relog in the documented test scope.
- Inventory Move Live Verification result: PASS. A junk item moved correctly between inventory slots before relog and remained in the correct slot after relog.
- Equip Item Live Verification result: PASS. Item equipped correctly before relog, no duplicate remained in inventory, and after relog the item remained equipped in the correct equipment slot.
- Unequip Item Live Verification result: PASS. Item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item stayed in inventory while the equipment slot stayed empty.
- Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.
- Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash persisted after relog, and no duplicate corpse credit feedback was observed.
- Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.
- Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.
- Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.
- Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.
- Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.
- Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.
- Live Persistence Verification complete: inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.
- Death/respawn white-screen behavior is repaired.
- Corpse use, item loot, credit loot, XP text, and corpse despawn have working documented paths. The completed corpse credit investigation fixed the `CorpseFullUpdate` cash offset, removed duplicate manual corpse credit chat, retained focused assertions, and passed Cliff Malle playtest verification.
- Player trade item and credit transfer have been repaired and verified in the documented test scope. Credit-only, item-only, mixed item-plus-credit, and cancel/decline trades behaved as expected, and no player trade display or commit defect was reproduced. Temporary `TRADE_*` logging remains available for future trade investigation.
- Broad combat smoke `-SkipBuild`, focused corpse credit assertions, and inventory/container regression assertions pass after stale harness assertions were cleaned up. The cleanup changed harness expectations only, not gameplay behavior.
- Vendor shop buy, sell, close, and current-client ICC shop stock coverage have been repaired for the captured Fair Trade areas.
- `1183 ord_smarket_omni_basic` static vendor coverage was expanded with 20 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `730` to `710`, and `1183 ord_smarket_omni_basic` dropped from `77` to `57`. No runtime vendor behavior changed.
- `1184 ord_smarket_omni_advanced` static vendor coverage was expanded with 21 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `710` to `689`, and `1184 ord_smarket_omni_advanced` dropped from `68` to `47`. No runtime vendor behavior changed.
- `1185 ord_smarket_omni_sup` static vendor coverage was expanded with 21 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `689` to `668`, and `1185 ord_smarket_omni_sup` dropped from `68` to `47`. No runtime vendor behavior changed.
- `500 Parnassos` static vendor coverage was expanded with 25 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `668` to `643`, and `500 Parnassos` dropped from `140` to `115`. No runtime vendor behavior changed.
- `1182 ord_smarket_clan_sup` static vendor coverage was expanded with 17 approved mappings. The approved rows are present in `cellao_codex_clean.vendors`; the latest import run did not insert duplicates because all 17 IDs already existed. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `643` to `626`, and `1182 ord_smarket_clan_sup` dropped from `44` to `27`. No runtime vendor behavior changed.
- `655 Andromeda` static vendor coverage was expanded with 16 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `626` to `610`, and `655 Andromeda` dropped from `17` to `1`. Template `151987` remains unknown. No runtime vendor behavior changed.
- `1180 ord_smarket_clan_basic` static vendor coverage was expanded with 4 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `610` to `606`, and `1180 ord_smarket_clan_basic` dropped from `43` to `39`. No runtime vendor behavior changed.
- `1181 ord_smarket_clan_advanced` static vendor coverage was expanded with 4 approved mappings. Commit `fbcc1a4` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`. Total uncovered statel vendors dropped from `606` to `602`, and `1181 ord_smarket_clan_advanced` dropped from `30` to `26`. No runtime vendor behavior changed.
- `2064 neut_basic_implants_shop` static vendor coverage was expanded with 3 approved mappings. Commit `ed869d5` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`. Total uncovered statel vendors dropped from `602` to `599`, and `2064 neut_basic_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- `2073 neut_advanced_implants_shop` static vendor coverage was expanded with 3 approved mappings. Commit `a79b5ec` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`. Total uncovered statel vendors dropped from `599` to `596`, and `2073 neut_advanced_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- Surgery clinic and implant flows have documented repaired behavior.

# Partially Working Systems

- Inventory, corpse item loot, corpse credit loot, player trade item/credit/cancel, and vendor buy/sell/close persistence flows have passing source assertion coverage where available and completed live-client relog verification for the documented repaired paths.
- Combat works for basic weapon/NPC test scenarios, but packet semantics are not complete.
- Corpse visuals and `CorpseFullUpdate` remain areas for broader cleanup, but the corpse cash value offset is repaired and guarded by focused assertions.
- Shop/vendor database coverage is improved but still has remaining static vendor coverage gaps.
- Playfield/interior mapping has repaired fixtures and remaining audit candidates.
- Enemy spawn testing has supported low-level families, but final spawn tables are not complete.
- DB-backed mob loot is modeled and partially wired, with limited reviewed data.
- Nanos, tradeskills, teams, organizations, pets, missions, quests, perks, research, bank, bags, stacks, and containers need separate focused work.

# Known Broken Systems

- NPC chase/movement is high risk and not gameplay-ready.
- `PlayfieldAnarchyF` is documented as a current-client structure mismatch.
- Some packet classes are missing, under-modeled, or awaiting capture-backed runtime use.
- Broad static vendor coverage remains incomplete.
- Full gameplay systems for missions, quests, perks, research, pets, PvP/towers, teams, and organizations are not complete.

# Current Development Focus

The latest completed milestone expanded `2073 neut_advanced_implants_shop` static vendor coverage after live persistence was verified for repaired inventory, corpse loot, corpse credits, player trade, and vendor transaction flows. The next high-value target remains static vendor coverage data, because broad shop stock coverage is still documented as incomplete while transaction paths are verified.

# Last Completed Milestone

`2073 neut_advanced_implants_shop` vendor coverage expansion completed:

- Commit `a79b5ec` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `599` to `596`.
- `2073 neut_advanced_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`.
- No runtime vendor behavior changed.

Prior `2064 neut_basic_implants_shop` vendor coverage expansion completed:

- Commit `ed869d5` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `602` to `599`.
- `2064 neut_basic_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`.
- No runtime vendor behavior changed.

Prior `1181 ord_smarket_clan_advanced` vendor coverage expansion completed:

- Commit `fbcc1a4` added the 4 approved source SQL mappings.
- A targeted import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `606` to `602`.
- `1181 ord_smarket_clan_advanced` uncovered count dropped from `30` to `26`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`.
- No runtime vendor behavior changed.

Prior `1180 ord_smarket_clan_basic` vendor coverage expansion completed:

- Commit `b6a6410` added the 4 approved source SQL mappings.
- A targeted import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `610` to `606`.
- `1180 ord_smarket_clan_basic` uncovered count dropped from `43` to `39`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `655 Andromeda` vendor coverage expansion completed:

- Commit `9217459` added the 16 approved source SQL mappings.
- A targeted import inserted only those 16 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `626` to `610`.
- `655 Andromeda` uncovered count dropped from `17` to `1`.
- Template `151987` remains unknown and was not mapped.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1182 ord_smarket_clan_sup` vendor coverage expansion completed:

- Commit `d7556bb` added the 17 approved source SQL mappings.
- The 17 approved rows are present in `cellao_codex_clean.vendors`.
- The latest import run did not insert duplicates because all 17 approved IDs already existed.
- A `vendors` table backup was created before the verification/import attempt.
- Total uncovered statel vendors dropped from `643` to `626`.
- `1182 ord_smarket_clan_sup` uncovered count dropped from `44` to `27`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `500 Parnassos` vendor coverage expansion completed:

- Commit `d47f12e` added the 25 approved source SQL mappings.
- A targeted import inserted only those 25 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `668` to `643`.
- `500 Parnassos` uncovered count dropped from `140` to `115`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1185 ord_smarket_omni_sup` vendor coverage expansion completed:

- Commit `e755c25` added the 21 approved source SQL mappings.
- A targeted import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `689` to `668`.
- `1185 ord_smarket_omni_sup` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1184 ord_smarket_omni_advanced` vendor coverage expansion completed:

- Commit `aa8da43` added the 21 approved source SQL mappings.
- A targeted import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `710` to `689`.
- `1184 ord_smarket_omni_advanced` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1183 ord_smarket_omni_basic` vendor coverage expansion completed:

- Commit `6dfb390` added the 20 approved source SQL mappings.
- A targeted import inserted only those 20 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `730` to `710`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `77` to `57`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Smoke harness cleanup passed after stale assertions were aligned with current repaired behavior:

- `Run-CombatSmokeTests.ps1 -SkipBuild` passes.
- `Run-CorpseCreditTraceAssertions.ps1` passes.
- `Run-InventoryContainerRegressionAssertions.ps1` passes.
- Stale assertions for cash stat serialization, NPC/shop cash mutation, login-time debug enemy spawning, and corpse credit feedback were cleaned up.
- No gameplay behavior was changed by the harness cleanup.

Inventory Move Live Verification result: PASS. The item moved correctly before relog and remained in the correct slot after relog.

Equip Item Live Verification result: PASS. The item equipped correctly before relog, no duplicate remained in inventory, and the item remained equipped in the correct equipment slot after relog.

Unequip Item Live Verification result: PASS. The item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item remained in inventory while the equipment slot stayed empty.

Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.

Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash value persisted after relog, and no duplicate corpse credit feedback was observed.

Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.

Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.

Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.

Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.

Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.

Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.

Live Persistence Verification complete. Inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.

Player-to-player trade verification passed after temporary `TRADE_*` trace logging was added in commit `4b68d4e`. Verification showed:

- Credit-only trade behaved as expected.
- Item-only trade behaved as expected.
- Mixed item-plus-credit trade behaved as expected.
- Cancel/decline trade behaved as expected.
- No player trade display or commit defect was reproduced.
- Temporary `TRADE_*` logging remains available for future trade investigation.

Prior corpse credit repairs were pushed to `origin/master` in commits `343a31d` and `e953c76` after verification showed:

- `CorpseFullUpdate` cash stat id remains at offset `203`.
- Corpse cash value is patched at offset `207`.
- The old hardcoded `111` cash value is not preserved.
- Delayed corpse credit award mutates cash once and sends the normal changed-stat packet.
- Manual server `ChatText` corpse credit feedback is suppressed so the client displays one corrected message.
- Cliff Malle playtest displayed one `You received 3 credits from the corpse.` message.

Prior ICC/Fair Trade vendor stock repairs were pushed to `origin/master` in commit `cffc5da` after verification showed:

- vendor DB issues: 0
- shop inventory item-cache issues: 0
- tradeskill room captured rows: 3,101
- tradeskill vendor rows: 38

# Next Milestone

Plan and patch the next confirmed static vendor coverage group without changing verified buy/sell/close transaction behavior. The recommended next target is `565 Newland Desert`, which has `9` remaining uncovered statel vendors in the current post-import audit and `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence. It is now the highest safe candidate target after the `2073 neut_advanced_implants_shop` pass. Keep NPC movement out of scope unless explicitly selected later.
