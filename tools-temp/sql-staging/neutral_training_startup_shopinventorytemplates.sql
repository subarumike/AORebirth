-- ============================================================
-- Neutral Training startup staged SQL
-- Source capture: AOSharp 20260614-002319
-- Targets: 954 Neutral Training startup equipment
-- Expected coverage: 29 -> 27 (2 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: shop hash 4 Base32(SHA1) over numeric-sorted [Low:High:QL] rows; collision-safe window.
-- ============================================================
START TRANSACTION;

-- ShopHash: WHBW
-- Terminal: Basic Startup Equipment
-- NormalizedName: NeutralTrainingBasicStartupEquipment; TemplateId: 99643; Capture identities: (VendingMachine:12E4B870), (VendingMachine:12E4B871); Inventory rows: 9; Shop window: 0
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 31837, 31837, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 291082, 291082, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 291043, 291043, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 95577, 95577, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 81757, 81756, 6, 6, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 81753, 99727, 2, 2, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 28564, 28564, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 161699, 161699, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('WHBW', 99228, 99228, 1, 1, 1, 'Live Neutral Training 20260614-002319 NeutralTrainingBasicStartupEquipment template 99643', 1);

COMMIT;
