-- ============================================================
-- Uncle Bazzit's Workshop vendortemplate staged SQL
-- Source capture: AOSharp 20260613-184615
-- Target: 4354 Uncle Bazzits Workshop (Dng)
-- Expected coverage: 104 -> 99 (5 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendortemplate rows: 5; Maria's Fashion reuses existing shop hash Fash.
-- Hash rules: vendortemplate UB + 5 Base32(SHA1); collision-safe window.

-- Terminal: Maria's Fashion
-- NormalizedName: BazzitsMariasFashion; TemplateId: 247744; ShopHash: Fash (reused existing); Inventory rows: 197; Capture identity: (VendingMachine:12E4E8EB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('UBG2HDY', 1, 'BazzitsMariasFashion', 247744, 'Fash', 1, 1);

-- Terminal: Uncle Bazzit's Miscellany
-- NormalizedName: BazzitsMiscellany; TemplateId: 247743; ShopHash: EJAW (new); Inventory rows: 38; Capture identity: (VendingMachine:12E4E8EC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('UBQF46P', 1, 'BazzitsMiscellany', 247743, 'EJAW', 1, 239);

-- Terminal: Uncle Bazzit's Floorplans
-- NormalizedName: BazzitsFloorplans; TemplateId: 254816; ShopHash: BDX3 (new); Inventory rows: 36; Capture identity: (VendingMachine:12E4E8ED); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('UBMRCLI', 1, 'BazzitsFloorplans', 254816, 'BDX3', 100, 201);

-- Terminal: Uncle Bazzit's Landscaping
-- NormalizedName: BazzitsLandscaping; TemplateId: 255998; ShopHash: VZX3 (new); Inventory rows: 14; Capture identity: (VendingMachine:12E4E8EE); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('UBQEFLQ', 1, 'BazzitsLandscaping', 255998, 'VZX3', 1, 1);

-- Terminal: Uncle Bazzit's Furnishings
-- NormalizedName: BazzitsFurnishings; TemplateId: 255997; ShopHash: TCJR (new); Inventory rows: 41; Capture identity: (VendingMachine:12E4E8EF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('UBNWVTI', 1, 'BazzitsFurnishings', 255997, 'TCJR', 1, 1);

COMMIT;
