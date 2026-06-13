-- ============================================================
-- Omni Advanced staged vendortemplate inserts
-- Target playfield: 1184 ord_smarket_omni_advanced
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260613-002828
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate OA + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Melee Weapon Recipes - Advanced
-- NormalizedName: AdvancedMeleeWeaponRecipes; TemplateId: 155503; VendorId(s): 77594638, 77594641; ShopHash: IYD4 (new); Inventory rows: 33; Capture identity: (VendingMachine:12E4907E), (VendingMachine:12E49081); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OALQXGA', 1, 'AdvancedMeleeWeaponRecipes', 155503, 'IYD4', 1, 1);

-- Terminal: Ranged Weapon Recipes - Advanced
-- NormalizedName: AdvancedRangedWeaponRecipes; TemplateId: 155506; VendorId(s): 77594639, 77594642; ShopHash: IVM2 (new); Inventory rows: 159; Capture identity: (VendingMachine:12E4907F), (VendingMachine:12E49082); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAIFSRG', 1, 'AdvancedRangedWeaponRecipes', 155506, 'IVM2', 1, 100);

-- Terminal: Melee Weapon Components - Advanced
-- NormalizedName: AdvancedMeleeWeaponComponents; TemplateId: 155297; VendorId(s): 77594646, 77594656; ShopHash: LYQY (new); Inventory rows: 50; Capture identity: (VendingMachine:12E49086), (VendingMachine:12E49090); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAAC3R2', 1, 'AdvancedMeleeWeaponComponents', 155297, 'LYQY', 30, 89);

-- Terminal: Ranged Weapon Components - Advanced
-- NormalizedName: AdvancedRangedWeaponComponents; TemplateId: 155491; VendorId(s): 77594647, 77594657; ShopHash: FJRI (new); Inventory rows: 121; Capture identity: (VendingMachine:12E49087), (VendingMachine:12E49091); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OABOGYY', 1, 'AdvancedRangedWeaponComponents', 155491, 'FJRI', 1, 90);

-- Terminal: Advanced Implants
-- NormalizedName: AdvancedImplants; TemplateId: 155223; VendorId(s): 77594663, 77594669; ShopHash: 2UVY (new); Inventory rows: 38; Capture identity: (VendingMachine:12E49097), (VendingMachine:12E4909D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAG44BS', 1, 'AdvancedImplants', 155223, '2UVY', 30, 89);

-- Terminal: Advanced Melee Weapon Construction Kits
-- NormalizedName: AdvancedMeleeWeaponConstructionKits; TemplateId: 155234; VendorId(s): 77594664, 77594670; ShopHash: GSUE (new); Inventory rows: 38; Capture identity: (VendingMachine:12E49098), (VendingMachine:12E4909E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAXHP7H', 1, 'AdvancedMeleeWeaponConstructionKits', 155234, 'GSUE', 30, 89);

-- Terminal: Advanced Ranged Weapon Construction Kits
-- NormalizedName: AdvancedRangedWeaponConstructionKits; TemplateId: 155282; VendorId(s): 77594665, 77594671; ShopHash: TNCZ (new); Inventory rows: 27; Capture identity: (VendingMachine:12E49099), (VendingMachine:12E4909F); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAKGM6T', 1, 'AdvancedRangedWeaponConstructionKits', 155282, 'TNCZ', 30, 84);

-- Terminal: OT Advanced Armor
-- NormalizedName: OTAdvancedArmor; TemplateId: 99386; VendorId(s): 77594682; ShopHash: L3P7 (new); Inventory rows: 29; Capture identity: (VendingMachine:12E490A9); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAL6IVC', 1, 'OTAdvancedArmor', 99386, 'L3P7', 30, 89);

-- Terminal: OT Advanced Attacks
-- NormalizedName: OTAdvancedAttacks; TemplateId: 99496; VendorId(s): 77594683; ShopHash: TNQE (new); Inventory rows: 7; Capture identity: (VendingMachine:12E490AA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAECAN7', 1, 'OTAdvancedAttacks', 99496, 'TNQE', 30, 100);

-- Terminal: OT Advanced Augmentations
-- NormalizedName: OTAdvancedAugmentations; TemplateId: 99485; VendorId(s): 77594684; ShopHash: JAJA (new); Inventory rows: 70; Capture identity: (VendingMachine:12E490AB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAS6ZPM', 1, 'OTAdvancedAugmentations', 99485, 'JAJA', 1, 90);

-- Terminal: OT Advanced Medical Supplies
-- NormalizedName: OTAdvancedMedicalSupplies; TemplateId: 99482; VendorId(s): 77594685; ShopHash: JTYS (new); Inventory rows: 40; Capture identity: (VendingMachine:12E490AC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAW76SU', 1, 'OTAdvancedMedicalSupplies', 99482, 'JTYS', 20, 90);

-- Terminal: OT Advanced Tools
-- NormalizedName: OTAdvancedTools; TemplateId: 99492; VendorId(s): 77594686; ShopHash: VSQU (new); Inventory rows: 19; Capture identity: (VendingMachine:12E490AD); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAAMXEE', 1, 'OTAdvancedTools', 99492, 'VSQU', 1, 89);

-- Terminal: OT Advanced Weapons
-- NormalizedName: OTAdvancedWeapons; TemplateId: 99479; VendorId(s): 77594687; ShopHash: 7IAS (new); Inventory rows: 88; Capture identity: (VendingMachine:12E490AE); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAE5BNV', 1, 'OTAdvancedWeapons', 99479, '7IAS', 1, 90);

-- Terminal: OT Clothes
-- NormalizedName: OTAdvancedClothes; TemplateId: 99490; VendorId(s): 77594688; ShopHash: 3SQ3 (new); Inventory rows: 15; Capture identity: (VendingMachine:12E490AF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAFBTI6', 1, 'OTAdvancedClothes', 99490, '3SQ3', 1, 1);

-- Terminal: OT Maps
-- NormalizedName: OTAdvancedMaps; TemplateId: 117649; VendorId(s): 77594690; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E490B1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAA6B2F', 1, 'OTAdvancedMaps', 117649, 'LJI7', 1, 30);

-- Terminal: Omni Advanced Devices
-- NormalizedName: OmniAdvancedDevices; TemplateId: 155606; VendorId(s): 77594692; ShopHash: 66ZZ (new); Inventory rows: 26; Capture identity: (VendingMachine:12E490B3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAX2G2O', 1, 'OmniAdvancedDevices', 155606, '66ZZ', 2, 89);

COMMIT;
