-- ============================================================
-- Jobe Advanced dimensions vendortemplate staged SQL
-- Source capture: AOSharp 20260614-002319
-- Targets: 4564 Hardware Dimension - Advanced and 4568 Dimensional Shift - Advanced
-- Expected coverage: 96 -> 93 (3 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendortemplate rows: 3
-- Hash rules: vendortemplate JA + 5 Base32(SHA1); collision-safe window.

-- Terminal: Advanced Armor
-- NormalizedName: JobeHardwareAdvancedArmor; TemplateId: 99571; ShopHash: UVH2 (new); Inventory rows: 29; Capture identity: (VendingMachine:12E5137B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JARLAWF', 1, 'JobeHardwareAdvancedArmor', 99571, 'UVH2', 34, 89);

-- Terminal: Costly Regenerative Supplies --- 1-90
-- NormalizedName: JobeDimensionalAdvancedRegenerativeSupplies; TemplateId: 220329; ShopHash: HMIZ (reused existing); Inventory rows: 30; Capture identity: (VendingMachine:12E53D47); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JASODU6', 1, 'JobeDimensionalAdvancedRegenerativeSupplies', 220329, 'HMIZ', 1, 90);

-- Terminal: Advanced Implants
-- NormalizedName: JobeDimensionalAdvancedImplants; TemplateId: 155223; ShopHash: HE4N (new); Inventory rows: 39; Capture identity: (VendingMachine:12E53D48); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JAVMXFQ', 1, 'JobeDimensionalAdvancedImplants', 155223, 'HE4N', 30, 90);

COMMIT;
