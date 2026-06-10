# AI Changelog

## 2026-06-10 - Expanded Neutral Advanced Weapon Shop Vendor Coverage

Change: Recorded the completed `2070 neut_advanced_weapons_shop` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the neutral basic weapon shop pass. The approved safe mapping pass targeted 1 `2070 neut_advanced_weapons_shop` statel vendor with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `9c41ed9` added the 1 approved source SQL mapping.
- Targeted DB import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`.
- Mapping added: `135659520 -> ICCAmmo`.
- Total uncovered statel vendors dropped from `572` to `571`.
- `2070 neut_advanced_weapons_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`.
- Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered because no matching `vendortemplate` evidence exists.
- No runtime vendor behavior changed.

Follow-up work:

- No next target selected in this documentation update.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Neutral Basic Weapon Shop Vendor Coverage

Change: Recorded the completed `2060 neut_basic_weapon_shop` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Hardware Dimension Superior pass. The approved safe mapping pass targeted 1 `2060 neut_basic_weapon_shop` statel vendor with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `83fc74f` added the 1 approved source SQL mapping.
- Targeted DB import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `573` to `572`.
- `2060 neut_basic_weapon_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`.
- No runtime vendor behavior changed.

Follow-up work:

- The `2070 neut_advanced_weapons_shop` follow-up pass is complete and recorded above.
- No next target selected in this documentation update.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Hardware Dimension Superior Vendor Coverage

Change: Recorded the completed `4565 Hardware Dimension - Superior` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Hardware Dimension Advanced pass. The approved safe mapping pass targeted 2 `4565 Hardware Dimension - Superior` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `1810408` added the 2 approved source SQL mappings.
- Targeted DB import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `575` to `573`.
- `4565 Hardware Dimension - Superior` uncovered count dropped from `5` to `3`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `2060 neut_basic_weapon _shop` next. Current post-import audit shows `5` uncovered statel vendors there, with `1` likely safe `ICCAmmo` candidate by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Hardware Dimension Advanced Vendor Coverage

Change: Recorded the completed `4564 Hardware Dimension - Advanced` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Arete Landing pass. The approved safe mapping pass targeted 2 `4564 Hardware Dimension - Advanced` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `aa62dcd` added the 2 approved source SQL mappings.
- Targeted DB import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `577` to `575`.
- `4564 Hardware Dimension - Advanced` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `4565 Hardware Dimension - Superior` next. Current post-import audit shows `5` uncovered statel vendors there, with `2` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Arete Landing Vendor Coverage

Change: Recorded the completed `6553 Arete Landing` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Hardware Dimension Basic pass. The approved safe mapping pass targeted 2 `6553 Arete Landing` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `389e8b3` added the 2 approved source SQL mappings.
- Targeted DB import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `579` to `577`.
- `6553 Arete Landing` uncovered count dropped from `8` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `4564 Hardware Dimension - Advanced` next. Current post-import audit shows `4` uncovered statel vendors there, with `2` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Hardware Dimension Basic Vendor Coverage

Change: Recorded the completed `4563 Hardware Dimension - Basic` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Dimensional Shift Superior pass. The approved safe mapping pass targeted 2 `4563 Hardware Dimension - Basic` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `0ded4a9` added the 2 approved source SQL mappings.
- Targeted DB import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `581` to `579`.
- `4563 Hardware Dimension - Basic` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `6553 Arete Landing` next. Current post-import audit shows `8` uncovered statel vendors there, with `2` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Dimensional Shift Superior Vendor Coverage

Change: Recorded the completed `4569 Dimensional Shift - Superior` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Dimensional Shift Advanced pass. The approved safe mapping pass targeted 3 `4569 Dimensional Shift - Superior` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `abee0ce` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `584` to `581`.
- `4569 Dimensional Shift - Superior` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `4563 Hardware Dimension - Basic` next. Current post-import audit shows `4` uncovered statel vendors there, with `2` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Dimensional Shift Advanced Vendor Coverage

Change: Recorded the completed `4568 Dimensional Shift - Advanced` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Dimensional Shift Basic pass. The approved safe mapping pass targeted 3 `4568 Dimensional Shift - Advanced` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `5e5303b` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `587` to `584`.
- `4568 Dimensional Shift - Advanced` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `4569 Dimensional Shift - Superior` next. Current post-import audit shows `5` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Dimensional Shift Basic Vendor Coverage

Change: Recorded the completed `4567 Dimensional Shift - Basic` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the 4holes Fashion pass. The approved safe mapping pass targeted 3 `4567 Dimensional Shift - Basic` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `7c10b5a` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `590` to `587`.
- `4567 Dimensional Shift - Basic` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `4568 Dimensional Shift - Advanced` next. Current post-import audit shows `5` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded 4holes Fashion Vendor Coverage

Change: Recorded the completed `2096 4holes Fashion` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Newland Desert pass. The approved safe mapping pass targeted 3 `2096 4holes Fashion` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `0522ffb` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `593` to `590`.
- `2096 4holes Fashion` uncovered count dropped from `7` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `4567 Dimensional Shift - Basic` next. Current post-import audit shows `5` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Newland Desert Vendor Coverage

Change: Recorded the completed `565 Newland Desert` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the neutral advanced implant shop pass. The approved safe mapping pass targeted 3 `565 Newland Desert` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `2bb7ad5` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `596` to `593`.
- `565 Newland Desert` uncovered count dropped from `9` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `2096 4holes Fashion` next. Current post-import audit shows `7` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-10 - Expanded Neutral Advanced Implant Shop Vendor Coverage

Change: Recorded the completed `2073 neut_advanced_implants_shop` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the neutral basic implant shop pass. The approved safe mapping pass targeted 3 `2073 neut_advanced_implants_shop` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `a79b5ec` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `599` to `596`.
- `2073 neut_advanced_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `565 Newland Desert` next. Current post-import audit shows `9` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Neutral Basic Implant Shop Vendor Coverage

Change: Recorded the completed `2064 neut_basic_implants_shop` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Clan advanced pass. The approved safe mapping pass targeted 3 `2064 neut_basic_implants_shop` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `ed869d5` added the 3 approved source SQL mappings.
- Targeted DB import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `602` to `599`.
- `2064 neut_basic_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `2073 neut_advanced_implants_shop` next. Current post-import audit shows `15` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Clan Advanced Static Vendor Coverage

Change: Recorded the completed `1181 ord_smarket_clan_advanced` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Clan basic pass. The approved safe mapping pass targeted 4 `1181 ord_smarket_clan_advanced` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `fbcc1a4` added the 4 approved source SQL mappings.
- Targeted DB import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `606` to `602`.
- `1181 ord_smarket_clan_advanced` uncovered count dropped from `30` to `26`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `2064 neut_basic_implants_shop` next. Current post-import audit shows `15` uncovered statel vendors there, with `3` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Clan Basic Static Vendor Coverage

Change: Recorded the completed `1180 ord_smarket_clan_basic` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Andromeda pass. The approved safe mapping pass targeted 4 `1180 ord_smarket_clan_basic` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `b6a6410` added the 4 approved source SQL mappings.
- Targeted DB import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `610` to `606`.
- `1180 ord_smarket_clan_basic` uncovered count dropped from `43` to `39`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `1181 ord_smarket_clan_advanced` next. Current post-import audit shows `30` uncovered statel vendors there, with `4` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Andromeda Static Vendor Coverage

Change: Recorded the completed `655 Andromeda` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Clan superior pass. The approved safe mapping pass targeted 16 `655 Andromeda` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `9217459` added the 16 approved source SQL mappings.
- Targeted DB import inserted only those 16 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `626` to `610`.
- `655 Andromeda` uncovered count dropped from `17` to `1`.
- Template `151987` remains unknown and was not mapped.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `1180 ord_smarket_clan_basic` next. Current post-import audit shows `43` uncovered statel vendors there, with `4` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Clan Superior Static Vendor Coverage

Change: Recorded the completed `1182 ord_smarket_clan_sup` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Parnassos pass. The approved safe mapping pass targeted 17 `1182 ord_smarket_clan_sup` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `d7556bb` added the 17 approved source SQL mappings.
- The 17 approved rows are present in `cellao_codex_clean.vendors`.
- The latest import run did not insert duplicates because all 17 approved IDs already existed.
- A `vendors` table backup was created before the verification/import attempt.
- Total uncovered statel vendors dropped from `643` to `626`.
- `1182 ord_smarket_clan_sup` uncovered count dropped from `44` to `27`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `655 Andromeda` next. Current post-import audit shows `17` uncovered statel vendors there, with `16` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Parnassos Static Vendor Coverage

Change: Recorded the completed `500 Parnassos` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the Omni supermarket passes. The approved safe mapping pass targeted 25 `500 Parnassos` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `d47f12e` added the 25 approved source SQL mappings.
- Targeted DB import inserted only those 25 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `668` to `643`.
- `500 Parnassos` uncovered count dropped from `140` to `115`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `1182 ord_smarket_clan_sup` next. Current post-import audit shows `44` uncovered statel vendors there, with `17` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Omni Superior Static Vendor Coverage

Change: Recorded the completed `1185 ord_smarket_omni_sup` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the completed Omni basic and advanced passes. The third approved safe mapping pass targeted 21 `1185 ord_smarket_omni_sup` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `e755c25` added the 21 approved source SQL mappings.
- Targeted DB import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `689` to `668`.
- `1185 ord_smarket_omni_sup` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Audit `500 Parnassos` next. Post-import audit shows `140` uncovered statel vendors there, with `25` likely safe candidates by existing `vendortemplate` and active `shopinventorytemplates` evidence.
- Do not guess unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Omni Advanced Static Vendor Coverage

Change: Recorded the completed `1184 ord_smarket_omni_advanced` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after the completed `1183 ord_smarket_omni_basic` pass. The second approved safe mapping pass targeted 21 `1184 ord_smarket_omni_advanced` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `aa8da43` added the 21 approved source SQL mappings.
- Targeted DB import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `710` to `689`.
- `1184 ord_smarket_omni_advanced` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Plan `1185 ord_smarket_omni_sup` next. Post-import audit shows `68` uncovered statel vendors there, with `21` statel templates already matching existing `vendortemplate` rows.
- Do not guess the remaining unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Expanded Omni Basic Static Vendor Coverage

Change: Recorded the completed `1183 ord_smarket_omni_basic` vendor coverage expansion.

Files affected:

- `CellAO/Libraries/Source/CellAO.Database/SqlTables/vendors.sql`
- `cellao_codex_clean.vendors`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Static vendor coverage remained incomplete after vendor transaction behavior was verified. The first approved safe mapping pass targeted 20 `1183 ord_smarket_omni_basic` statel vendors with matching `vendortemplate` and active shop inventory evidence.

Result:

- Commit `6dfb390` added the 20 approved source SQL mappings.
- Targeted DB import inserted only those 20 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `730` to `710`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `77` to `57`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Follow-up work:

- Plan `1184 ord_smarket_omni_advanced` next. Post-import audit shows `68` uncovered statel vendors there, with `21` statel templates already matching existing `vendortemplate` rows.
- Do not guess the remaining unknown terminals.
- Keep vendor transaction behavior and NPC movement out of scope.

## 2026-06-09 - Completed Live Persistence Verification

Change: Recorded the completed live persistence verification pass for repaired inventory, corpse loot, player trade, and vendor transaction flows.

Files affected:

- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Result:

- Inventory move passed.
- Equip item passed.
- Unequip item passed.
- Corpse item loot passed.
- Corpse credit loot passed.
- Player trade item passed.
- Player trade credits passed.
- Player trade cancel/decline passed.
- Vendor buy passed.
- Vendor sell passed.
- Vendor close/cancel passed.
- All verified behaviors matched expected client-visible behavior and survived relog.

Follow-up work:

- Audit and fill remaining static vendor coverage gaps without changing verified inventory, corpse credit, player trade, or vendor buy/sell/close behavior.
- Keep NPC movement out of scope unless explicitly selected later.

## 2026-06-09 - Verified Smoke Harness Cleanup

Change: Recorded that the broad and focused source assertion harnesses now pass after stale smoke assertions were cleaned up.

Files affected:

- `tools-temp/CellAOCombatSmokeTests/Run-CombatSmokeTests.ps1`
- `tools-temp/CellAOCombatSmokeTests/Run-CorpseCreditTraceAssertions.ps1`
- `tools-temp/CellAOCombatSmokeTests/Run-InventoryContainerRegressionAssertions.ps1`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Several broad smoke assertions still expected older source shapes after completed repairs for cash stat serialization, NPC/shop cash mutation, login-time debug enemy spawning, and corpse credit feedback.

Result:

- Broad combat smoke `-SkipBuild` passes.
- Focused corpse credit assertions pass.
- Inventory/container regression assertions pass.
- Stale harness assertions were aligned with current repaired behavior.
- No gameplay behavior was changed by the harness cleanup.

Follow-up work:

- Run live-client and DB persistence verification for repaired inventory/container flows that source assertions cannot prove.
- Keep NPC movement out of scope unless explicitly selected later.

## 2026-06-09 - Verified Player Trade Display And Commit Behavior

Change: Recorded the completed player-to-player trade verification after adding temporary trace logging.

Files affected:

- `CellAO/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `tools-temp/CellAOCombatSmokeTests/Run-CombatSmokeTests.ps1`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Player trade credit and item display needed verification after prior reports of credit desync and stale trade-window visuals.

Result:

- Credit-only trade behaved as expected.
- Item-only trade behaved as expected.
- Mixed item-plus-credit trade behaved as expected.
- Cancel/decline trade behaved as expected.
- No player trade display or commit defect was reproduced.
- Temporary `TRADE_*` logging remains available for future trade investigation.

Follow-up work:

- Build broader inventory/container regression coverage for repaired corpse loot, player trade, vendor shop, equipment, and normal inventory move flows.
- Keep NPC movement out of scope unless explicitly selected later.

## 2026-06-08 - Completed Corpse Credit Investigation

Change: Fixed the corpse credit client-visible value path and removed duplicate corpse credit chat.

Files affected:

- `CellAO/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs`
- `CellAO/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `tools-temp/CellAOCombatSmokeTests/Run-CorpseCreditTraceAssertions.ps1`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Local playtest showed corpse credits could display as stale/hardcoded values such as `111`, and after the packet offset repair the client displayed duplicate corpse credit text when the server also sent manual `ChatText` feedback.

Result:

- `CorpseFullUpdate` now patches corpse cash at offset `207`, the cash value word after stat id `61` at offset `203`.
- The old hardcoded `111` corpse cash template value is not preserved.
- Manual server corpse credit `ChatText` feedback was suppressed because the current client displays the corrected corpse credit message from the corpse cash/stat flow.
- Focused corpse credit assertions were added and retained for credit roll ranges, offset `207`, delayed cash mutation, stat emission, and item loot not mutating cash.
- Cliff Malle playtest passed with one correct `You received 3 credits from the corpse.` message.

Follow-up work:

- Trace player trade credit/item display behavior next.
- Keep NPC movement out of scope unless explicitly selected later.

## 2026-06-02 - Created AI Documentation System

Change: Added root AI handoff documentation.

Files affected:

- `AI_START_HERE.md`
- `docs/project/OVERVIEW.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ROADMAP.md`
- `docs/project/FEATURES.md`
- `docs/backlog/BUGS.md`
- `docs/ai/TESTING.md`
- `docs/archive/ai/SESSION_NOTES.md`
- `docs/ai/CODE_STANDARDS.md`
- `docs/ai/CHANGELOG_AI.md`
- `docs/backlog/TODO.md`
- `docs/archive/ai/LESSONS_LEARNED.md`

Reason: Preserve project context across AI context compaction, session resets, model changes, and handoffs.

Follow-up work:

- Keep `docs/ai/CURRENT_TASK.md` current after each major work block.
- Add source-backed findings to `docs/project/KNOWN_DECISIONS.md` when behavior is locked.
- Move resolved bugs from `docs/backlog/BUGS.md` into changelog entries.
