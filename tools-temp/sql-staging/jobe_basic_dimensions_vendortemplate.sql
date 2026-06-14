-- ============================================================
-- Jobe Basic dimensions vendortemplate staged SQL
-- Source capture: AOSharp 20260614-000058
-- Targets: 4563 Hardware Dimension - Basic and 4567 Dimensional Shift - Basic
-- Expected coverage: 99 -> 96 (3 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendortemplate rows: 3
-- Hash rules: vendortemplate JB + 5 Base32(SHA1); collision-safe window.

-- Terminal: Basic Armor
-- NormalizedName: JobeHardwareBasicArmor; TemplateId: 99570; ShopHash: J2PQ (new); Inventory rows: 29; Capture identity: (VendingMachine:12E48221); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JB5P4OH', 1, 'JobeHardwareBasicArmor', 99570, 'J2PQ', 1, 46);

-- Terminal: Costly Regenerative Supplies --- 1-90
-- NormalizedName: JobeDimensionalBasicRegenerativeSupplies; TemplateId: 220329; ShopHash: HMIZ (new); Inventory rows: 30; Capture identity: (VendingMachine:12E5134A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JBZYZLQ', 1, 'JobeDimensionalBasicRegenerativeSupplies', 220329, 'HMIZ', 1, 90);

-- Terminal: Basic Implants
-- NormalizedName: JobeDimensionalBasicImplants; TemplateId: 155222; ShopHash: TGDC (new); Inventory rows: 39; Capture identity: (VendingMachine:12E5134B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JBMTLP6', 1, 'JobeDimensionalBasicImplants', 155222, 'TGDC', 1, 49);

COMMIT;
