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
- Omni Basic General Shop live-capture import completed from AOSharp capture `20260612-012644`. The validated staged SQL added 23 `1183 ord_smarket_omni_basic` vendor rows, 16 vendor templates, and 16 shop inventory groups with 690 inventory rows. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 381`; total uncovered statel vendors dropped from `404` to `381`, and `1183 ord_smarket_omni_basic` dropped from `39` to `16`. No runtime vendor behavior changed.
- Non-shop statel template `155225` (`Refreshing Drink`) is excluded from vendor coverage metrics, missing-vendor reports, capture targeting, and import planning while remaining visible in raw statel coverage output. AOSharp captures `20260612-012644` and `20260612-044234` showed VendorFullUpdate evidence but no ShopUpdate inventory rows, and live operator verification found the Superior instances were not reachable/openable. Verification now shows `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 351`, and `StatelVendorExclusions = 30`. No SQL, vendor mappings, imports, or runtime vendor behavior changed.
- Omni Superior General Shop live-capture import completed from AOSharp capture `20260612-044234`. The validated v2 staged SQL added 27 `1185 ord_smarket_omni_sup` vendor rows, 20 vendor templates, and 19 new shop inventory groups while reusing existing map shop hash `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 324`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `351` to `324`. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324`. No runtime vendor behavior changed.
- Clan Basic General Shop live-capture import completed from AOSharp capture `20260612-225855`. The validated staged SQL added 29 `1180 ord_smarket_clan_basic` vendor rows, 29 vendor templates, and 25 new shop inventory groups with 1575 inventory rows while reusing existing shop hashes `G4XZ`, `HYDQ`, `LJI7`, and `R5R7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 295`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `324` to `295`. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295`. No runtime vendor behavior changed.
- Clan Superior General Shop live-capture import completed from AOSharp capture `20260612-232439`. The validated staged SQL added 19 `1182 ord_smarket_clan_sup` vendor rows, 19 vendor templates, and 14 new shop inventory groups with 594 inventory rows while reusing existing shop hashes `LJI7`, `CHHQ`, `OHOO`, `JYPE`, and `Cont`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 276`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `295` to `276`. Current live-capture coverage chain is `404 → 381 → 351 → 324 → 295 → 276`. No runtime vendor behavior changed.
- Omni Advanced General Shop live-capture import completed from AOSharp capture `20260613-002828`. The validated staged SQL added 23 `1184 ord_smarket_omni_advanced` vendor rows, 16 vendor templates, and 15 new shop inventory groups with 760 inventory rows while reusing existing shop hash `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 253`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `276` to `253`, and `1184 ord_smarket_omni_advanced` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253`. No runtime vendor behavior changed.
- Omni Basic Implant Terminals live-capture import completed from AOSharp capture `20260613-005616`. The validated staged SQL added 13 `1183 ord_smarket_omni_basic` implant vendor rows and 13 vendor templates, with no new shop inventory groups because existing implant shop hashes `5BUX`, `5M5F`, `6MQN`, `6YPW`, `7LZ3`, `A32J`, `JWHR`, `KV75`, `KVVT`, `O3KI`, `RNWW`, `RO4Q`, and `SBQ6` were reused. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 240`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `253` to `240`, and `1183 ord_smarket_omni_basic` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240`. No runtime vendor behavior changed.
- Neutral Basic General/Specialty Shop live-capture import completed from AOSharp captures `20260613-012810` and `20260613-014033`. The validated staged SQL added 6 `1193 spec_smarket_neut_basic` vendor rows, 6 vendor templates, and 6 new shop inventory groups with 64 inventory rows; Specialist Commerce required Trader access. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 234`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `240` to `234`, and `1193 spec_smarket_neut_basic` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234`. No runtime vendor behavior changed.
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
- `565 Newland Desert` static vendor coverage was expanded with 3 approved mappings. Commit `2bb7ad5` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`. Total uncovered statel vendors dropped from `596` to `593`, and `565 Newland Desert` dropped from `9` to `6`. No runtime vendor behavior changed.
- `2096 4holes Fashion` static vendor coverage was expanded with 3 approved mappings. Commit `0522ffb` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`. Total uncovered statel vendors dropped from `593` to `590`, and `2096 4holes Fashion` dropped from `7` to `4`. No runtime vendor behavior changed.
- `4567 Dimensional Shift - Basic` static vendor coverage was expanded with 3 approved mappings. Commit `7c10b5a` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`. Total uncovered statel vendors dropped from `590` to `587`, and `4567 Dimensional Shift - Basic` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4568 Dimensional Shift - Advanced` static vendor coverage was expanded with 3 approved mappings. Commit `5e5303b` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`. Total uncovered statel vendors dropped from `587` to `584`, and `4568 Dimensional Shift - Advanced` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4569 Dimensional Shift - Superior` static vendor coverage was expanded with 3 approved mappings. Commit `abee0ce` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`. Total uncovered statel vendors dropped from `584` to `581`, and `4569 Dimensional Shift - Superior` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4563 Hardware Dimension - Basic` static vendor coverage was expanded with 2 approved mappings. Commit `0ded4a9` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`. Total uncovered statel vendors dropped from `581` to `579`, and `4563 Hardware Dimension - Basic` dropped from `4` to `2`. No runtime vendor behavior changed.
- `6553 Arete Landing` static vendor coverage was expanded with 2 approved mappings. Commit `389e8b3` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`. Total uncovered statel vendors dropped from `579` to `577`, and `6553 Arete Landing` dropped from `8` to `6`. No runtime vendor behavior changed.
- `4564 Hardware Dimension - Advanced` static vendor coverage was expanded with 2 approved mappings. Commit `aa62dcd` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`. Total uncovered statel vendors dropped from `577` to `575`, and `4564 Hardware Dimension - Advanced` dropped from `4` to `2`. No runtime vendor behavior changed.
- `4565 Hardware Dimension - Superior` static vendor coverage was expanded with 2 approved mappings. Commit `1810408` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`. Total uncovered statel vendors dropped from `575` to `573`, and `4565 Hardware Dimension - Superior` dropped from `5` to `3`. No runtime vendor behavior changed.
- `2060 neut_basic_weapon_shop` static vendor coverage was expanded with 1 approved mapping. Commit `83fc74f` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`. Total uncovered statel vendors dropped from `573` to `572`, and `2060 neut_basic_weapon_shop` dropped from `5` to `4`. No runtime vendor behavior changed.
- `2070 neut_advanced_weapons_shop` static vendor coverage was expanded with 1 approved mapping. Commit `9c41ed9` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`. Total uncovered statel vendors dropped from `572` to `571`, and `2070 neut_advanced_weapons_shop` dropped from `5` to `4`. Backup: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`. Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered until matching `vendortemplate` evidence is found. No runtime vendor behavior changed.
- `600 Varmint Woods` static vendor coverage was expanded with 1 approved mapping. Commit `e197b9f` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, query-back confirmed `39321612 | 600 | 93063 | AdvOA`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 570`. Total uncovered statel vendors dropped from `571` to `570`, and `600 Varmint Woods` dropped from `3` to `2`. Backup: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_600_varmint_woods_20260610_052107.sql`. Rejected candidates `39321600`/`99479` and `39321601`/`99482` remain uncovered until matching `vendortemplate.ItemTemplate` evidence is found. No runtime vendor behavior changed.
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

The latest completed milestone promoted the Neutral Basic General/Specialty Shop live-capture import from AOSharp captures `20260613-012810` and `20260613-014033`, reducing actionable uncovered statel vendors from `240` to `234` after the prior Omni Basic import, `155225` non-shop exclusion, Omni Superior import, Clan Basic import, Clan Superior import, Omni Advanced import, and Omni Basic implant import. Static vendor coverage data remains the active campaign area, because broad shop stock coverage is still documented as incomplete while transaction paths are verified.

# Last Completed Milestone

Neutral Basic General/Specialty Shop import completed:

- Source captures: AOSharp captures `20260613-012810` and `20260613-014033`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 6 `1193 spec_smarket_neut_basic` vendor rows, 6 vendor templates, and 6 new shop inventory groups with 64 inventory rows.
- Specialist Commerce required Trader access and was captured in the second AOSharp session.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\neutral_basic_before_import_20260613-014923.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 234`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `240` to `234`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234`.
- Spot checks passed for `NeutralBasicComputers`, `NeutralBasicSpecialistCommerce`, and `NeutralBasicSuperiorCars`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Basic Implant Terminals import completed:

- Source capture: AOSharp capture `20260613-005616`.
- Source SQL promotion added the validated staged inserts to `vendortemplate.sql` and `vendors.sql`; `shopinventorytemplates.sql` was unchanged because all implant inventories reused existing shop hashes.
- Coverage added: 13 `1183 ord_smarket_omni_basic` implant vendor rows and 13 vendor templates, with existing implant shop hashes `5BUX`, `5M5F`, `6MQN`, `6YPW`, `7LZ3`, `A32J`, `JWHR`, `KV75`, `KVVT`, `O3KI`, `RNWW`, `RO4Q`, and `SBQ6` reused.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\omni_basic_implants_before_import_20260613-011140.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 240`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `253` to `240`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240`.
- Spot checks passed for `BasicOmniTekAdventurerImplants`, `BasicOmniTekMetaPhysicistImplants`, and `BasicOmniTekKeeperImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Advanced General Shop import completed:

- Source capture: AOSharp capture `20260613-002828`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 23 `1184 ord_smarket_omni_advanced` vendor rows, 16 vendor templates, and 15 new shop inventory groups with 760 inventory rows, while reusing existing shop hash `LJI7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\omni_advanced_before_import_20260613-004623.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 253`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `276` to `253`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253`.
- Spot checks passed for `OTAdvancedArmor`, `OTAdvancedWeapons`, and `AdvancedImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Superior General Shop import completed:

- Source capture: AOSharp capture `20260612-232439`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 19 `1182 ord_smarket_clan_sup` vendor rows, 19 vendor templates, and 14 new shop inventory groups with 594 inventory rows, while reusing existing shop hashes `LJI7`, `CHHQ`, `OHOO`, `JYPE`, and `Cont`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\clan_superior_before_import_20260613-000803.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 276`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `295` to `276`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276`.
- Spot checks passed for `ClanSuperiorArmor`, `ClanSuperiorWeapons`, and `ClanSuperiorContainers`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Basic General Shop import completed:

- Source capture: AOSharp capture `20260612-225855`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 29 `1180 ord_smarket_clan_basic` vendor rows, 29 vendor templates, and 25 new shop inventory groups with 1575 inventory rows, while reusing existing shop hashes `G4XZ`, `HYDQ`, `LJI7`, and `R5R7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\clan_basic_before_import_20260612-231024.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 295`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `324` to `295`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295`.
- Spot checks passed for `ClanBasicArmor`, `ClanBasicWeapons`, `BasicClanAdventurerImplants`, and `BasicImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Superior General Shop import completed:

- Source capture: AOSharp capture `20260612-044234`.
- Source SQL promotion added the validated v2 staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 27 `1185 ord_smarket_omni_sup` vendor rows, 20 vendor templates, and 19 new shop inventory groups, with existing map shop hash `LJI7` reused.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\omni_superior_v2_before_import_20260612-220448.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 324`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `351` to `324`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324`.
- Spot checks passed for `OTSuperiorArmor`, `OTSuperiorWeapons`, and `SuperiorImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior coverage exclusion for non-shop `Refreshing Drink` statels completed:

- Excluded template: `155225`.
- Exclusion reason: `NonShopStatelTemplate`.
- Evidence: AOSharp captures `20260612-012644` and `20260612-044234` emitted VendorFullUpdate rows but no ShopUpdate inventory rows for these identities, and live operator verification found the Superior instances were not reachable/openable.
- Implementation: current-client verification keeps excluded rows in `statel-vendor-coverage.csv` with `CoverageExcluded` and `ExclusionReason`, but excludes them from coverage metrics, missing-vendor reports, `vendor-scan-targets.csv`, capture targeting, and import planning.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 351`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `381` to `351`.
- No SQL, vendor mappings, imports, or runtime vendor behavior changed.

Prior Omni Basic General Shop import completed:

- Source capture: AOSharp capture `20260612-012644`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 23 `1183 ord_smarket_omni_basic` vendor rows, 16 vendor templates, and 16 shop inventory groups with 690 inventory rows.
- A test DB backup was created before import: `C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\db-backups\omni_basic_before_staged_import_20260612-032350.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 381`.
- Total uncovered statel vendors dropped from `404` to `381`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `39` to `16`.
- Spot checks passed for `OTBasicArmor`, `OTBasicWeapons`, and `BasicImplants`.
- No runtime vendor behavior changed.

Prior `600 Varmint Woods` vendor coverage expansion completed:

- Commit `e197b9f` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- Query-back confirmed `39321612 | 600 | 93063 | AdvOA`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_600_varmint_woods_20260610_052107.sql`.
- Total uncovered statel vendors dropped from `571` to `570`.
- `600 Varmint Woods` uncovered count dropped from `3` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 570`.
- Rejected candidates `39321600`/`99479` and `39321601`/`99482` remain uncovered because no matching `vendortemplate.ItemTemplate` evidence exists.
- No runtime vendor behavior changed.

Prior `2070 neut_advanced_weapons_shop` vendor coverage expansion completed:

- Commit `9c41ed9` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`.
- Total uncovered statel vendors dropped from `572` to `571`.
- `2070 neut_advanced_weapons_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`.
- Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered because no matching `vendortemplate` evidence exists.
- No runtime vendor behavior changed.

Prior `2060 neut_basic_weapon_shop` vendor coverage expansion completed:

- Commit `83fc74f` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `573` to `572`.
- `2060 neut_basic_weapon_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`.
- No runtime vendor behavior changed.

Prior `4565 Hardware Dimension - Superior` vendor coverage expansion completed:

- Commit `1810408` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `575` to `573`.
- `4565 Hardware Dimension - Superior` uncovered count dropped from `5` to `3`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`.
- No runtime vendor behavior changed.

Prior `4564 Hardware Dimension - Advanced` vendor coverage expansion completed:

- Commit `aa62dcd` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `577` to `575`.
- `4564 Hardware Dimension - Advanced` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`.
- No runtime vendor behavior changed.

Prior `6553 Arete Landing` vendor coverage expansion completed:

- Commit `389e8b3` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `579` to `577`.
- `6553 Arete Landing` uncovered count dropped from `8` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`.
- No runtime vendor behavior changed.

Prior `4563 Hardware Dimension - Basic` vendor coverage expansion completed:

- Commit `0ded4a9` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `581` to `579`.
- `4563 Hardware Dimension - Basic` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`.
- No runtime vendor behavior changed.

Prior `4569 Dimensional Shift - Superior` vendor coverage expansion completed:

- Commit `abee0ce` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `584` to `581`.
- `4569 Dimensional Shift - Superior` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`.
- No runtime vendor behavior changed.

Prior `4568 Dimensional Shift - Advanced` vendor coverage expansion completed:

- Commit `5e5303b` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `587` to `584`.
- `4568 Dimensional Shift - Advanced` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`.
- No runtime vendor behavior changed.

Prior `4567 Dimensional Shift - Basic` vendor coverage expansion completed:

- Commit `7c10b5a` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `590` to `587`.
- `4567 Dimensional Shift - Basic` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`.
- No runtime vendor behavior changed.

Prior `2096 4holes Fashion` vendor coverage expansion completed:

- Commit `0522ffb` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `593` to `590`.
- `2096 4holes Fashion` uncovered count dropped from `7` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`.
- No runtime vendor behavior changed.

Prior `565 Newland Desert` vendor coverage expansion completed:

- Commit `2bb7ad5` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `596` to `593`.
- `565 Newland Desert` uncovered count dropped from `9` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`.
- No runtime vendor behavior changed.

Prior `2073 neut_advanced_implants_shop` vendor coverage expansion completed:

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

Static vendor coverage remains the active data-campaign area, but no next target is selected in this documentation update. Continue only after Mike selects or approves the next target from current audit data. Keep NPC movement out of scope unless explicitly selected later.
