-- ============================================================
-- Omni Training Startup Shop staged SQL
-- Source capture: AOSharp 20260613-231115
-- Target: 950 Omni Training Startup Shop!
-- Expected coverage: 106 -> 105 (1 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: shop hash 4 Base32(SHA1) over numeric-sorted [Low:High:QL] rows; collision-safe window.
-- ============================================================
START TRANSACTION;

-- ShopHash: AMJX
-- Terminal: Startup Shop!
-- NormalizedName: OmniTrainingStartupShop; TemplateId: 100035; Capture identity: (VendingMachine:12E530CC); Inventory rows: 7; Shop window: 0
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 31837, 31837, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 291082, 291082, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 291043, 291043, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 95577, 95577, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 28564, 28564, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 161699, 161699, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);
INSERT INTO `shopinventorytemplates` (`HASH`, `lowID`, `highID`, `minQL`, `maxQL`, `multiplecount`, `admindescription`, `active`) VALUES ('AMJX', 99228, 99228, 1, 1, 1, 'Live Omni Training 20260613-231115 OmniTrainingStartupShop template 100035', 1);

COMMIT;
