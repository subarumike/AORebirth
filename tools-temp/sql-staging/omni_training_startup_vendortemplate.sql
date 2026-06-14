-- ============================================================
-- Omni Training Startup Shop staged SQL
-- Source capture: AOSharp 20260613-231115
-- Target: 950 Omni Training Startup Shop!
-- Expected coverage: 106 -> 105 (1 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate OT + 5 Base32(SHA1); collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Startup Shop!
-- NormalizedName: OmniTrainingStartupShop; TemplateId: 100035; ShopHash: AMJX (new); Inventory rows: 7; Capture identity: (VendingMachine:12E530CC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OTY56RU', 1, 'OmniTrainingStartupShop', 100035, 'AMJX', 1, 1);

COMMIT;
