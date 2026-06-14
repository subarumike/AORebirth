-- ============================================================
-- Treepine Hut OT Clothes staged SQL
-- Source capture: AOSharp 20260613-233535
-- Target: 1887 Treepine Hut OT Clothes
-- Expected coverage: 105 -> 104 (1 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: shop hash 4 Base32(SHA1) over numeric-sorted [Low:High:QL] rows; collision-safe window.
-- ============================================================
START TRANSACTION;

-- ShopHash: YXAF
-- Terminal: OT Clothes
-- NormalizedName: TreepineOTClothes; TemplateId: 99490; Capture identity: (VendingMachine:12E522FB); Inventory rows: 16; Shop window: 0
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27377, 27377, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27367, 27367, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27368, 27368, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27370, 27370, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27363, 27363, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27362, 27362, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27376, 27376, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27366, 27366, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27373, 27373, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27371, 27371, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27387, 27387, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27383, 27383, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27384, 27384, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27385, 27385, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 27386, 27386, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('YXAF', 31515, 31515, 1, 1, 1, 'Live Treepine Hut 20260613-233535 TreepineOTClothes template 99490', 1);

COMMIT;
