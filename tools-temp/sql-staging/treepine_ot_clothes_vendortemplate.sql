-- ============================================================
-- Treepine Hut OT Clothes staged SQL
-- Source capture: AOSharp 20260613-233535
-- Target: 1887 Treepine Hut OT Clothes
-- Expected coverage: 105 -> 104 (1 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate TP + 5 Base32(SHA1); collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: OT Clothes
-- NormalizedName: TreepineOTClothes; TemplateId: 99490; ShopHash: YXAF (new); Inventory rows: 16; Capture identity: (VendingMachine:12E522FB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TPNFK3D', 1, 'TreepineOTClothes', 99490, 'YXAF', 1, 1);

COMMIT;
