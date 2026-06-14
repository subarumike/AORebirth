-- ============================================================
-- Newland + Omni startup staged SQL
-- Source capture: AOSharp 20260613-185338
-- Targets: 565 Newland Desert vendors + 710 Omni-1 Trade startup equipment
-- Expected coverage: 142 -> 133 (9 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate NL/OT + 5 Base32(SHA1); shop hash 4 Base32(SHA1) over numeric-sorted [Low:High:QL] rows; collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Basic Armor
-- NormalizedName: NewlandBasicArmor; TemplateId: 99570; ShopHash: BZQE (new); Inventory rows: 29; Capture identity: (VendingMachine:C0010235); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NLPY36V', 1, 'NewlandBasicArmor', 99570, 'BZQE', 3, 48);

-- Terminal: Basic Startup Equipment
-- NormalizedName: NewlandBasicStartupEquipment; TemplateId: 99643; ShopHash: 3UDB (new); Inventory rows: 9; Capture identity: (VendingMachine:C0040235); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NLLVEHN', 1, 'NewlandBasicStartupEquipment', 99643, '3UDB', 1, 4);

-- Terminal: Basic Nano Clusters
-- NormalizedName: NewlandBasicNanoClusters; TemplateId: 118287; ShopHash: VGC2 (new); Inventory rows: 149; Capture identity: (VendingMachine:C0050235); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NLD7VOT', 1, 'NewlandBasicNanoClusters', 118287, 'VGC2', 1, 50);

-- Terminal: Food
-- NormalizedName: NewlandFood; TemplateId: 121035; ShopHash: RDV2 (new); Inventory rows: 17; Capture identity: (VendingMachine:C0070235); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NLV4RLO', 1, 'NewlandFood', 121035, 'RDV2', 1, 1);

-- Terminal: Drinks
-- NormalizedName: NewlandDrinks; TemplateId: 121037; ShopHash: 7YJX (new); Inventory rows: 18; Capture identity: (VendingMachine:C0080235); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NLHROVV', 1, 'NewlandDrinks', 121037, '7YJX', 1, 1);

-- Terminal: OT Basic Startup Equipment
-- NormalizedName: OTBasicStartupEquipment; TemplateId: 99555; ShopHash: NTV2 (new); Inventory rows: 10; Capture identity: (VendingMachine:C00002C6), (VendingMachine:C00102C6), (VendingMachine:C00202C6), (VendingMachine:C00302C6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OTRDLME', 1, 'OTBasicStartupEquipment', 99555, 'NTV2', 1, 1);

COMMIT;
