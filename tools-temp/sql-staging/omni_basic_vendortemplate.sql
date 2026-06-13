-- ============================================================
-- Omni Basic staged vendortemplate inserts
-- Target playfield: 1183 ord_smarket_omni_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-012644
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- ============================================================
START TRANSACTION;

-- Terminal: Basic Implants
-- TemplateId: 155222; VendorId(s): 77529124, 77529143; ShopHash: BV22; Inventory rows: 38
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBOFY25', 1, 'BasicImplants', 155222, 'BV22', 2, 50);

-- Terminal: Melee Weapon Components - Basic
-- TemplateId: 155296; VendorId(s): 77529117, 77529136; ShopHash: XSPR; Inventory rows: 49
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBF6KVT', 1, 'BasicMeleeWeaponComponents', 155296, 'XSPR', 2, 50);

-- Terminal: Basic Melee Weapon Construction Kits
-- TemplateId: 155233; VendorId(s): 77529125, 77529144; ShopHash: QKCF; Inventory rows: 38
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBMPNFR', 1, 'BasicMeleeWeaponConstructionKits', 155233, 'QKCF', 2, 50);

-- Terminal: Melee Weapon Recipes - Basic
-- TemplateId: 155502; VendorId(s): 77529112, 77529131; ShopHash: R5R7; Inventory rows: 21
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBGYYPZ', 1, 'BasicMeleeWeaponRecipes', 155502, 'R5R7', 1, 1);

-- Terminal: Ranged Weapon Components - Basic
-- TemplateId: 155490; VendorId(s): 77529118, 77529137; ShopHash: F5CG; Inventory rows: 118
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBJNHBE', 1, 'BasicRangedWeaponComponents', 155490, 'F5CG', 1, 50);

-- Terminal: Basic Ranged Weapon Construction Kits
-- TemplateId: 155236; VendorId(s): 77529126, 77529145; ShopHash: XWMV; Inventory rows: 27
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBUHWW4', 1, 'BasicRangedWeaponConstructionKits', 155236, 'XWMV', 2, 50);

-- Terminal: Ranged Weapon Recipes - Basic
-- TemplateId: 155505; VendorId(s): 77529113, 77529132; ShopHash: HYDQ; Inventory rows: 95
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OB6HN4F', 1, 'BasicRangedWeaponRecipes', 155505, 'HYDQ', 1, 100);

-- Terminal: OT Basic Armor
-- TemplateId: 99383; VendorId(s): 77529088; ShopHash: EPFF; Inventory rows: 29
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBF3VGA', 1, 'OTBasicArmor', 99383, 'EPFF', 3, 49);

-- Terminal: OT Basic Attacks
-- TemplateId: 99495; VendorId(s): 77529089; ShopHash: ZWCP; Inventory rows: 10
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBAGPLU', 1, 'OTBasicAttacks', 99495, 'ZWCP', 1, 100);

-- Terminal: OT Basic Augmentations
-- TemplateId: 99484; VendorId(s): 77529090; ShopHash: ZTPP; Inventory rows: 70
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBFUYMA', 1, 'OTBasicAugmentations', 99484, 'ZTPP', 1, 42);

-- Terminal: OT Clothes
-- TemplateId: 99490; VendorId(s): 77529094; ShopHash: IMXL; Inventory rows: 19
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBCQTXM', 1, 'OTBasicClothes', 99490, 'IMXL', 1, 1);

-- Terminal: OT Maps
-- TemplateId: 117649; VendorId(s): 77529095; ShopHash: LJI7; Inventory rows: 2
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBMOJMG', 1, 'OTBasicMaps', 117649, 'LJI7', 1, 30);

-- Terminal: OT Basic Medical Supplies
-- TemplateId: 99481; VendorId(s): 77529091; ShopHash: G4XZ; Inventory rows: 40
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBX7YEB', 1, 'OTBasicMedicalSupplies', 99481, 'G4XZ', 1, 20);

-- Terminal: OT Basic Tools
-- TemplateId: 99491; VendorId(s): 77529092; ShopHash: ZUI3; Inventory rows: 19
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBH6GZY', 1, 'OTBasicTools', 99491, 'ZUI3', 1, 42);

-- Terminal: OT Basic Weapons
-- TemplateId: 99478; VendorId(s): 77529093; ShopHash: JPBP; Inventory rows: 88
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBMZVQZ', 1, 'OTBasicWeapons', 99478, 'JPBP', 1, 50);

-- Terminal: Omni Basic Devices
-- TemplateId: 155603; VendorId(s): 77529097; ShopHash: FRZN; Inventory rows: 27
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBIUAFT', 1, 'OmniBasicDevices', 155603, 'FRZN', 1, 50);

COMMIT;
