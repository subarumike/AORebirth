-- ============================================================
-- Arete ICC staged SQL
-- Source capture: AOSharp 20260613-172753
-- Target: 6553 Arete Landing core ICC vendors only
-- Expected coverage: 147 -> 142 (5 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate AI + 5 Base32(SHA1); shop hash 4 Base32(SHA1); collision-safe window.
-- Excludes incidental nearby Arete vendors captured in the same session.
-- ============================================================
START TRANSACTION;

-- Terminal: ICC Basic Implants
-- NormalizedName: AreteICCBasicImplants; TemplateId: 297320; ShopHash: B5K4 (new); Inventory rows: 39; Capture identity: (VendingMachine:12D1BF1D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('AIYZS6H', 1, 'AreteICCBasicImplants', 297320, 'B5K4', 1, 10);

-- Terminal: ICC Faded Clusters
-- NormalizedName: AreteICCFadedClusters; TemplateId: 297321; ShopHash: QDLJ (new); Inventory rows: 166; Capture identity: (VendingMachine:12D1BF1C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('AIBFIDW', 1, 'AreteICCFadedClusters', 297321, 'QDLJ', 5, 50);

-- Terminal: ICC Bright Clusters
-- NormalizedName: AreteICCBrightClusters; TemplateId: 297322; ShopHash: RLKT (new); Inventory rows: 166; Capture identity: (VendingMachine:12D1BF1B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('AIGFHES', 1, 'AreteICCBrightClusters', 297322, 'RLKT', 5, 50);

-- Terminal: ICC Shiny Clusters
-- NormalizedName: AreteICCShinyClusters; TemplateId: 297323; ShopHash: EFER (new); Inventory rows: 166; Capture identity: (VendingMachine:12D1BF1A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('AIZDXJ7', 1, 'AreteICCShinyClusters', 297323, 'EFER', 5, 50);

-- Terminal: ICC Pharmacy
-- NormalizedName: AreteICCPharmacy; TemplateId: 297325; ShopHash: KPMO (new); Inventory rows: 36; Capture identity: (VendingMachine:12D1BF19); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('AIYTWIL', 1, 'AreteICCPharmacy', 297325, 'KPMO', 5, 30);

COMMIT;
