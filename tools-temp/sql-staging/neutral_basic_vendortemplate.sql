-- ============================================================
-- Neutral Basic staged vendortemplate inserts
-- Target playfield: 1193 spec_smarket_neut_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source captures: 20260613-012810; 20260613-014033 (Trader Specialist Commerce)
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate NB + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Computers
-- NormalizedName: NeutralBasicComputers; TemplateId: 99603; VendorId: 78184448; ShopHash: I3E4 (new); Inventory rows: 18; Capture identity: (VendingMachine:12E4ABA8); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBTLELB', 1, 'NeutralBasicComputers', 99603, 'I3E4', 1, 6);

-- Terminal: Advanced Cars
-- NormalizedName: NeutralBasicAdvancedCars; TemplateId: 99635; VendorId: 78184449; ShopHash: 7ATH (new); Inventory rows: 2; Capture identity: (VendingMachine:12E4ABA9); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBBBPWA', 1, 'NeutralBasicAdvancedCars', 99635, '7ATH', 66, 75);

-- Terminal: Furniture
-- NormalizedName: NeutralBasicFurniture; TemplateId: 120512; VendorId: 78184450; ShopHash: 7X7Q (new); Inventory rows: 16; Capture identity: (VendingMachine:12E4ABAA); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NB7LZHA', 1, 'NeutralBasicFurniture', 120512, '7X7Q', 1, 1);

-- Terminal: Toys and Curiosities
-- NormalizedName: NeutralBasicToysAndCuriosities; TemplateId: 151983; VendorId: 78184451; ShopHash: PX4X (new); Inventory rows: 3; Capture identity: (VendingMachine:12E4ABAB); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBM27YC', 1, 'NeutralBasicToysAndCuriosities', 151983, 'PX4X', 1, 1);

-- Terminal: Specialist Commerce
-- NormalizedName: NeutralBasicSpecialistCommerce; TemplateId: 151987; VendorId: 78184452; ShopHash: FBQ3 (new); Inventory rows: 4; Capture identity: (VendingMachine:12E4ABB2); Capture: 20260613-014033; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBCQ762', 1, 'NeutralBasicSpecialistCommerce', 151987, 'FBQ3', 1, 40);

-- Terminal: Superior Cars
-- NormalizedName: NeutralBasicSuperiorCars; TemplateId: 151988; VendorId: 78184453; ShopHash: FLEW (new); Inventory rows: 21; Capture identity: (VendingMachine:12E4ABAD); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NB72WE4', 1, 'NeutralBasicSuperiorCars', 151988, 'FLEW', 81, 200);

COMMIT;
