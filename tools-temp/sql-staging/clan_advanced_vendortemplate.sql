-- ============================================================
-- Clan Advanced staged vendortemplate inserts
-- Target playfield: 1181 ord_smarket_clan_advanced
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260613-034740
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate CA + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Clan Advanced Attacks
-- NormalizedName: ClanAdvancedAttacks; TemplateId: 99533; VendorId: 77398030; ShopHash: CWW5 (new); Inventory rows: 8; Capture identity: (VendingMachine:12E4B059); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAWFVZL', 1, 'ClanAdvancedAttacks', 99533, 'CWW5', 32, 88);

-- Terminal: Clan Advanced Augmentations
-- NormalizedName: ClanAdvancedAugmentations; TemplateId: 99517; VendorId: 77398031; ShopHash: VNZ5 (new); Inventory rows: 70; Capture identity: (VendingMachine:12E4B05A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAXKPAK', 1, 'ClanAdvancedAugmentations', 99517, 'VNZ5', 1, 90);

-- Terminal: Clan Advanced Medical Supplies
-- NormalizedName: ClanAdvancedMedicalSupplies; TemplateId: 99509; VendorId: 77398032; ShopHash: JTYS (reused); Inventory rows: 40; Capture identity: (VendingMachine:12E4B05B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAKVRD3', 1, 'ClanAdvancedMedicalSupplies', 99509, 'JTYS', 20, 90);

-- Terminal: Clan Advanced Tools
-- NormalizedName: ClanAdvancedTools; TemplateId: 99528; VendorId: 77398033; ShopHash: 6BWM (new); Inventory rows: 19; Capture identity: (VendingMachine:12E4B05C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CA4ANR3', 1, 'ClanAdvancedTools', 99528, '6BWM', 1, 119);

-- Terminal: Clan Advanced Weapons
-- NormalizedName: ClanAdvancedWeapons; TemplateId: 99506; VendorId: 77398034; ShopHash: 4GTH (new); Inventory rows: 88; Capture identity: (VendingMachine:12E4B05D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAIYRLU', 1, 'ClanAdvancedWeapons', 99506, '4GTH', 1, 90);

-- Terminal: Clan Clothes
-- NormalizedName: ClanAdvancedClothes; TemplateId: 99526; VendorId: 77398035; ShopHash: VECC (new); Inventory rows: 21; Capture identity: (VendingMachine:12E4B05E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAOIGJE', 1, 'ClanAdvancedClothes', 99526, 'VECC', 1, 1);

-- Terminal: Clan Containers
-- NormalizedName: ClanAdvancedContainers; TemplateId: 99540; VendorId: 77398036; ShopHash: Cont (reused); Inventory rows: 62; Capture identity: (VendingMachine:12E4B05F); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAE4PJN', 1, 'ClanAdvancedContainers', 99540, 'Cont', 1, 1);

-- Terminal: Clan Maps
-- NormalizedName: ClanAdvancedMaps; TemplateId: 117749; VendorId: 77398037; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E4B060); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CANXN6U', 1, 'ClanAdvancedMaps', 117749, 'LJI7', 1, 30);

-- Terminal: Clan Advanced Devices
-- NormalizedName: ClanAdvancedDevices; TemplateId: 155605; VendorId: 77398039; ShopHash: SDYB (new); Inventory rows: 27; Capture identity: (VendingMachine:12E4B062); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CASMUGY', 1, 'ClanAdvancedDevices', 155605, 'SDYB', 2, 85);

-- Terminal: Advanced Ranged Weapon Construction Kits
-- NormalizedName: AdvancedRangedWeaponConstructionKits; TemplateId: 155282; VendorId: 77398040; ShopHash: 2S24 (new); Inventory rows: 27; Capture identity: (VendingMachine:12E4B063); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAKGM6T', 1, 'AdvancedRangedWeaponConstructionKits', 155282, '2S24', 33, 87);

-- Terminal: Advanced Melee Weapon Construction Kits
-- NormalizedName: AdvancedMeleeWeaponConstructionKits; TemplateId: 155234; VendorId: 77398042; ShopHash: 4R2H (new); Inventory rows: 38; Capture identity: (VendingMachine:12E4B065); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAXHP7H', 1, 'AdvancedMeleeWeaponConstructionKits', 155234, '4R2H', 30, 89);

-- Terminal: Advanced Implants
-- NormalizedName: AdvancedImplants; TemplateId: 155223; VendorId: 77398044; ShopHash: EHGI (new); Inventory rows: 38; Capture identity: (VendingMachine:12E4B067); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAG44BS', 1, 'AdvancedImplants', 155223, 'EHGI', 30, 89);

-- Terminal: Melee Weapon Components - Advanced
-- NormalizedName: AdvancedMeleeWeaponComponents; TemplateId: 155297; VendorId: 77398049; ShopHash: M4DY (new); Inventory rows: 50; Capture identity: (VendingMachine:12E4B06C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAAC3R2', 1, 'AdvancedMeleeWeaponComponents', 155297, 'M4DY', 30, 88);

-- Terminal: Ranged Weapon Components - Advanced
-- NormalizedName: AdvancedRangedWeaponComponents; TemplateId: 155491; VendorId: 77398050; ShopHash: B6S3 (new); Inventory rows: 119; Capture identity: (VendingMachine:12E4B06D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CABOGYY', 1, 'AdvancedRangedWeaponComponents', 155491, 'B6S3', 1, 90);

-- Terminal: Melee Weapon Recipes - Advanced
-- NormalizedName: AdvancedMeleeWeaponRecipes; TemplateId: 155503; VendorId: 77398056; ShopHash: IYD4 (reused); Inventory rows: 33; Capture identity: (VendingMachine:12E4B073); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CALQXGA', 1, 'AdvancedMeleeWeaponRecipes', 155503, 'IYD4', 1, 1);

-- Terminal: Ranged Weapon Recipes - Advanced
-- NormalizedName: AdvancedRangedWeaponRecipes; TemplateId: 155506; VendorId: 77398058; ShopHash: IVM2 (reused); Inventory rows: 159; Capture identity: (VendingMachine:12E4B075); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAIFSRG', 1, 'AdvancedRangedWeaponRecipes', 155506, 'IVM2', 1, 100);

COMMIT;
