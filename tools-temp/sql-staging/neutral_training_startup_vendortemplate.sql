-- ============================================================
-- Neutral Training startup vendortemplate staged SQL
-- Source capture: AOSharp 20260614-002319
-- Target: 954 Neutral Training startup equipment
-- Expected coverage: 29 -> 27 (2 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate NT + 5 Base32(SHA1); collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Basic Startup Equipment
-- NormalizedName: NeutralTrainingBasicStartupEquipment; TemplateId: 99643; ShopHash: WHBW (new); Inventory rows: 9; Capture identities: (VendingMachine:12E4B870), (VendingMachine:12E4B871); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NT37J3W', 1, 'NeutralTrainingBasicStartupEquipment', 99643, 'WHBW', 1, 6);

COMMIT;
