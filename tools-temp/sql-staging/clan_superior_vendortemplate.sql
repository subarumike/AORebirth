-- ============================================================
-- Clan Superior staged vendortemplate inserts
-- Target playfield: 1182 ord_smarket_clan_sup
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-232439
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate CS + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: Superior Ranged Weapon Construction Kits
-- NormalizedName: SuperiorRangedWeaponConstructionKits; TemplateId: 155283; VendorId: 77463565; ShopHash: V5TB (new); Inventory rows: 26; Capture identity: (VendingMachine:12E3AED6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSFCG76', 1, 'SuperiorRangedWeaponConstructionKits', 155283, 'V5TB', 70, 199);

-- Terminal: Superior Melee Weapon Construction Kits
-- NormalizedName: SuperiorMeleeWeaponConstructionKits; TemplateId: 155235; VendorId: 77463566; ShopHash: SYV2 (new); Inventory rows: 37; Capture identity: (VendingMachine:12E3AED7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSGLKFD', 1, 'SuperiorMeleeWeaponConstructionKits', 155235, 'SYV2', 70, 199);

-- Terminal: Superior Implants
-- NormalizedName: SuperiorImplants; TemplateId: 155224; VendorId: 77463570; ShopHash: S47V (new); Inventory rows: 39; Capture identity: (VendingMachine:12E3AEDB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSC6V6B', 1, 'SuperiorImplants', 155224, 'S47V', 71, 123);

-- Terminal: Ranged Weapon Components - Superior
-- NormalizedName: SuperiorRangedWeaponComponents; TemplateId: 155492; VendorId: 77463571; ShopHash: LPFM (new); Inventory rows: 121; Capture identity: (VendingMachine:12E3AEDC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSU6YPU', 1, 'SuperiorRangedWeaponComponents', 155492, 'LPFM', 1, 200);

-- Terminal: Armour and Clothing Components - Superior
-- NormalizedName: SuperiorArmourClothingComponents; TemplateId: 155498; VendorId: 77463574; ShopHash: 4L5J (new); Inventory rows: 4; Capture identity: (VendingMachine:12E3AEDF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSNB4VR', 1, 'SuperiorArmourClothingComponents', 155498, '4L5J', 141, 199);

-- Terminal: Nano Crystal Components - Superior
-- NormalizedName: SuperiorNanoCrystalComponents; TemplateId: 155313; VendorId: 77463577; ShopHash: D7ED (new); Inventory rows: 58; Capture identity: (VendingMachine:12E3AEE2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSQSROE', 1, 'SuperiorNanoCrystalComponents', 155313, 'D7ED', 70, 199);

-- Terminal: Melee Weapon Components - Superior
-- NormalizedName: SuperiorMeleeWeaponComponents; TemplateId: 155298; VendorId: 77463579; ShopHash: 4GMW (new); Inventory rows: 48; Capture identity: (VendingMachine:12E3AEE4); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CS2LC3A', 1, 'SuperiorMeleeWeaponComponents', 155298, '4GMW', 70, 199);

-- Terminal: Ranged Weapon Recipes - Superior
-- NormalizedName: SuperiorRangedWeaponRecipes; TemplateId: 155507; VendorId: 77463581; ShopHash: CHHQ (reused); Inventory rows: 325; Capture identity: (VendingMachine:12E3AEE6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSD2ZJQ', 1, 'SuperiorRangedWeaponRecipes', 155507, 'CHHQ', 1, 100);

-- Terminal: Melee Weapon Recipes - Superior
-- NormalizedName: SuperiorMeleeWeaponRecipes; TemplateId: 155504; VendorId: 77463582; ShopHash: OHOO (reused); Inventory rows: 75; Capture identity: (VendingMachine:12E3AEE7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSAGOLB', 1, 'SuperiorMeleeWeaponRecipes', 155504, 'OHOO', 1, 1);

-- Terminal: Clan Superior Attacks
-- NormalizedName: ClanSuperiorAttacks; TemplateId: 99534; VendorId: 77463585; ShopHash: L2PR (new); Inventory rows: 8; Capture identity: (VendingMachine:12E3AEEA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSSD5SY', 1, 'ClanSuperiorAttacks', 99534, 'L2PR', 73, 121);

-- Terminal: Clan Superior Augmentations
-- NormalizedName: ClanSuperiorAugmentations; TemplateId: 99518; VendorId: 77463586; ShopHash: CCN4 (new); Inventory rows: 70; Capture identity: (VendingMachine:12E3AEEB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSXKWKP', 1, 'ClanSuperiorAugmentations', 99518, 'CCN4', 1, 125);

-- Terminal: Clan Superior Medical Supplies
-- NormalizedName: ClanSuperiorMedicalSupplies; TemplateId: 99529; VendorId: 77463587; ShopHash: JYPE (reused); Inventory rows: 40; Capture identity: (VendingMachine:12E3AEEC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSZKPVY', 1, 'ClanSuperiorMedicalSupplies', 99529, 'JYPE', 70, 125);

-- Terminal: Clan Superior Tools
-- NormalizedName: ClanSuperiorTools; TemplateId: 99530; VendorId: 77463588; ShopHash: 3RVO (new); Inventory rows: 19; Capture identity: (VendingMachine:12E3AEED); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSAUZMP', 1, 'ClanSuperiorTools', 99530, '3RVO', 1, 120);

-- Terminal: Clan Superior Weapons
-- NormalizedName: ClanSuperiorWeapons; TemplateId: 99507; VendorId: 77463589; ShopHash: RQRQ (new); Inventory rows: 88; Capture identity: (VendingMachine:12E3AEEE); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CS5JCOM', 1, 'ClanSuperiorWeapons', 99507, 'RQRQ', 1, 125);

-- Terminal: Clan Clothes
-- NormalizedName: ClanSuperiorClothes; TemplateId: 99526; VendorId: 77463590; ShopHash: BUUA (new); Inventory rows: 21; Capture identity: (VendingMachine:12E3AEEF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSOO7JG', 1, 'ClanSuperiorClothes', 99526, 'BUUA', 1, 1);

-- Terminal: Clan Containers
-- NormalizedName: ClanSuperiorContainers; TemplateId: 99540; VendorId: 77463591; ShopHash: Cont (reused); Inventory rows: 62; Capture identity: (VendingMachine:12E3AEF0); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSSOA54', 1, 'ClanSuperiorContainers', 99540, 'Cont', 1, 1);

-- Terminal: Clan Superior Armor
-- NormalizedName: ClanSuperiorArmor; TemplateId: 99504; VendorId: 77463592; ShopHash: 4ATG (new); Inventory rows: 29; Capture identity: (VendingMachine:12E3AEF1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSFKCVG', 1, 'ClanSuperiorArmor', 99504, '4ATG', 73, 121);

-- Terminal: Clan Maps
-- NormalizedName: ClanSuperiorMaps; TemplateId: 117749; VendorId: 77463593; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E3AEF2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSIHQHX', 1, 'ClanSuperiorMaps', 117749, 'LJI7', 1, 30);

-- Terminal: Clan Superior Devices
-- NormalizedName: ClanSuperiorDevices; TemplateId: 155608; VendorId: 77463594; ShopHash: GVR6 (new); Inventory rows: 26; Capture identity: (VendingMachine:12E3AEF3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CS3Q3IF', 1, 'ClanSuperiorDevices', 155608, 'GVR6', 2, 199);

COMMIT;
