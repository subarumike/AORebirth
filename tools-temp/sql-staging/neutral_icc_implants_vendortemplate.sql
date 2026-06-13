-- ============================================================
-- Neutral ICC implant/cluster staged SQL
-- Source capture: AOSharp 20260613-170220
-- Captured interior: 2064 neut_basic_implants_shop
-- Exact-template reuse target: 2073 neut_advanced_implants_shop
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate NI + 5 Base32(SHA1); shop hash 4 Base32(SHA1); collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Basic ICC Implants
-- NormalizedName: NeutralBasicICCImplants; TemplateId: 297396; ShopHash: XZXX (new); Inventory rows: 143; Capture identity: (VendingMachine:12D3D4F0); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NITV2DU', 1, 'NeutralBasicICCImplants', 297396, 'XZXX', 5, 100);

-- Terminal: Basic ICC Faded Clusters
-- NormalizedName: NeutralBasicICCFadedClusters; TemplateId: 297399; ShopHash: MSBV (new); Inventory rows: 274; Capture identity: (VendingMachine:12D3D4F1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIIOYTU', 1, 'NeutralBasicICCFadedClusters', 297399, 'MSBV', 25, 100);

-- Terminal: Basic ICC Bright Clusters
-- NormalizedName: NeutralBasicICCBrightClusters; TemplateId: 297402; ShopHash: KMMP (new); Inventory rows: 274; Capture identity: (VendingMachine:12D3D4F2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NI4JLQO', 1, 'NeutralBasicICCBrightClusters', 297402, 'KMMP', 25, 100);

-- Terminal: Basic ICC Shiny Clusters
-- NormalizedName: NeutralBasicICCShinyClusters; TemplateId: 297405; ShopHash: LQBF (new); Inventory rows: 274; Capture identity: (VendingMachine:12D3D4F3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIKTHHA', 1, 'NeutralBasicICCShinyClusters', 297405, 'LQBF', 25, 100);

-- Terminal: Advanced ICC Implants
-- NormalizedName: NeutralAdvancedICCImplants; TemplateId: 297397; ShopHash: XNTQ (new); Inventory rows: 130; Capture identity: (VendingMachine:12D3D4F5); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIKUGMU', 1, 'NeutralAdvancedICCImplants', 297397, 'XNTQ', 110, 200);

-- Terminal: Advanced ICC Faded Clusters
-- NormalizedName: NeutralAdvancedICCFadedClusters; TemplateId: 297400; ShopHash: VUI3 (new); Inventory rows: 108; Capture identity: (VendingMachine:12D3D4F6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIJIZJC', 1, 'NeutralAdvancedICCFadedClusters', 297400, 'VUI3', 200, 200);

-- Terminal: Advanced ICC Bright Clusters
-- NormalizedName: NeutralAdvancedICCBrightClusters; TemplateId: 297403; ShopHash: LZWI (new); Inventory rows: 108; Capture identity: (VendingMachine:12D3D4F7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NI2ZU2M', 1, 'NeutralAdvancedICCBrightClusters', 297403, 'LZWI', 200, 200);

-- Terminal: Advanced ICC Shiny Clusters
-- NormalizedName: NeutralAdvancedICCShinyClusters; TemplateId: 297406; ShopHash: YRLY (new); Inventory rows: 108; Capture identity: (VendingMachine:12D3D4F8); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIXARAY', 1, 'NeutralAdvancedICCShinyClusters', 297406, 'YRLY', 200, 200);

-- Terminal: Refined ICC Implants
-- NormalizedName: NeutralRefinedICCImplants; TemplateId: 297398; ShopHash: AN3B (new); Inventory rows: 130; Capture identity: (VendingMachine:12D3D4FA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIGQ25C', 1, 'NeutralRefinedICCImplants', 297398, 'AN3B', 210, 300);

-- Terminal: Refined ICC Faded Clusters
-- NormalizedName: NeutralRefinedICCFadedClusters; TemplateId: 297401; ShopHash: AQDG (new); Inventory rows: 109; Capture identity: (VendingMachine:12D3D4FB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NICEGHE', 1, 'NeutralRefinedICCFadedClusters', 297401, 'AQDG', 300, 300);

-- Terminal: Refined ICC Bright Clusters
-- NormalizedName: NeutralRefinedICCBrightClusters; TemplateId: 297404; ShopHash: CF73 (new); Inventory rows: 109; Capture identity: (VendingMachine:12D3D4FC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIVUZHQ', 1, 'NeutralRefinedICCBrightClusters', 297404, 'CF73', 300, 300);

-- Terminal: Refined ICC Shiny Clusters
-- NormalizedName: NeutralRefinedICCShinyClusters; TemplateId: 297407; ShopHash: N5PM (new); Inventory rows: 109; Capture identity: (VendingMachine:12D3D4FD); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NILZLFQ', 1, 'NeutralRefinedICCShinyClusters', 297407, 'N5PM', 300, 300);

COMMIT;
