-- ============================================================
-- Omni Superior staged vendortemplate inserts (schema-compatible v2)
-- Target playfield: 1185 ord_smarket_omni_sup
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-044234
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate OS + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- ============================================================
START TRANSACTION;

-- Terminal: OT Superior Armor
-- TemplateId: 99477; VendorId(s): 77660173; ShopHash: PYFP; Inventory rows: 29; Capture identity: (VendingMachine:12E3FC84); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSLC3UI', 1, 'OTSuperiorArmor', 99477, 'PYFP', 72, 123);

-- Terminal: OT Superior Attacks
-- TemplateId: 99497; VendorId(s): 77660174; ShopHash: WQH5; Inventory rows: 8; Capture identity: (VendingMachine:12E3FC85); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSRA2ZZ', 1, 'OTSuperiorAttacks', 99497, 'WQH5', 81, 121);

-- Terminal: OT Superior Augmentations
-- TemplateId: 99486; VendorId(s): 77660175; ShopHash: HHXC; Inventory rows: 70; Capture identity: (VendingMachine:12E3FC86); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSGQXEO', 1, 'OTSuperiorAugmentations', 99486, 'HHXC', 1, 125);

-- Terminal: OT Superior Medical Supplies
-- TemplateId: 99483; VendorId(s): 77660176; ShopHash: JYPE; Inventory rows: 40; Capture identity: (VendingMachine:12E3FC87); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSCP3HJ', 1, 'OTSuperiorMedicalSupplies', 99483, 'JYPE', 70, 125);

-- Terminal: OT Superior Tools
-- TemplateId: 99493; VendorId(s): 77660177; ShopHash: NTTB; Inventory rows: 19; Capture identity: (VendingMachine:12E3FC88); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSXOL6H', 1, 'OTSuperiorTools', 99493, 'NTTB', 1, 121);

-- Terminal: OT Superior Weapons
-- TemplateId: 99480; VendorId(s): 77660178; ShopHash: 4O66; Inventory rows: 88; Capture identity: (VendingMachine:12E3FC89); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSQC5XR', 1, 'OTSuperiorWeapons', 99480, '4O66', 1, 125);

-- Terminal: OT Clothes
-- TemplateId: 99490; VendorId(s): 77660179; ShopHash: HPBL; Inventory rows: 16; Capture identity: (VendingMachine:12E3FC8A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSNQQWH', 1, 'OTSuperiorClothes', 99490, 'HPBL', 1, 1);

-- Terminal: OT Maps
-- TemplateId: 117649; VendorId(s): 77660181; ShopHash: LJI7; Inventory rows: 2; Capture identity: (VendingMachine:12E3FC8C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSOIVVG', 1, 'OTSuperiorMaps', 117649, 'LJI7', 1, 30);

-- Terminal: Omni Superior Devices
-- TemplateId: 155609; VendorId(s): 77660183; ShopHash: 6LO5; Inventory rows: 27; Capture identity: (VendingMachine:12E3FC8E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OST6OJS', 1, 'OmniSuperiorDevices', 155609, '6LO5', 2, 193);

-- Terminal: Melee Weapon Recipes - Superior
-- TemplateId: 155504; VendorId(s): 77660185, 77660188; ShopHash: OHOO; Inventory rows: 75; Capture identity: (VendingMachine:12E3FC59); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSAGOLB', 1, 'SuperiorMeleeWeaponRecipes', 155504, 'OHOO', 1, 1);

-- Terminal: Ranged Weapon Recipes - Superior
-- TemplateId: 155507; VendorId(s): 77660186, 77660189; ShopHash: CHHQ; Inventory rows: 325; Capture identity: (VendingMachine:12E3FC5A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSD2ZJQ', 1, 'SuperiorRangedWeaponRecipes', 155507, 'CHHQ', 1, 100);

-- Terminal: Melee Weapon Components - Superior
-- TemplateId: 155298; VendorId(s): 77660193; ShopHash: VEHL; Inventory rows: 50; Capture identity: (VendingMachine:12E3FC61); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSFZSPR', 1, 'SuperiorMeleeWeaponComponentsA', 155298, 'VEHL', 70, 199);

-- Terminal: Ranged Weapon Components - Superior
-- TemplateId: 155492; VendorId(s): 77660194; ShopHash: G2RV; Inventory rows: 123; Capture identity: (VendingMachine:12E3FC62); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSS7FOI', 1, 'SuperiorRangedWeaponComponentsA', 155492, 'G2RV', 1, 200);

-- Terminal: Armour and Clothing Components - Superior
-- TemplateId: 155498; VendorId(s): 77660197, 77660207; ShopHash: UZ4T; Inventory rows: 4; Capture identity: (VendingMachine:12E3FC65); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSNB4VR', 1, 'SuperiorArmourClothingComponents', 155498, 'UZ4T', 80, 193);

-- Terminal: Nano Crystal Components - Superior
-- TemplateId: 155313; VendorId(s): 77660198, 77660208; ShopHash: ZP6H; Inventory rows: 60; Capture identity: (VendingMachine:12E3FC66); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSQSROE', 1, 'SuperiorNanoCrystalComponents', 155313, 'ZP6H', 71, 200);

-- Terminal: Melee Weapon Components - Superior
-- TemplateId: 155298; VendorId(s): 77660203; ShopHash: NV6B; Inventory rows: 50; Capture identity: (VendingMachine:12E3FC6B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSBPZXC', 1, 'SuperiorMeleeWeaponComponentsB', 155298, 'NV6B', 71, 199);

-- Terminal: Ranged Weapon Components - Superior
-- TemplateId: 155492; VendorId(s): 77660204; ShopHash: OFJQ; Inventory rows: 123; Capture identity: (VendingMachine:12E3FC6C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSV6ABI', 1, 'SuperiorRangedWeaponComponentsB', 155492, 'OFJQ', 1, 199);

-- Terminal: Superior Implants
-- TemplateId: 155224; VendorId(s): 77660210, 77660216; ShopHash: ITPE; Inventory rows: 38; Capture identity: (VendingMachine:12E3FC72); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSC6V6B', 1, 'SuperiorImplants', 155224, 'ITPE', 71, 121);

-- Terminal: Superior Melee Weapon Construction Kits
-- TemplateId: 155235; VendorId(s): 77660211, 77660217; ShopHash: FVLD; Inventory rows: 39; Capture identity: (VendingMachine:12E3FC73); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSGLKFD', 1, 'SuperiorMeleeWeaponConstructionKits', 155235, 'FVLD', 72, 200);

-- Terminal: Superior Ranged Weapon Construction Kits
-- TemplateId: 155283; VendorId(s): 77660212, 77660218; ShopHash: CTWD; Inventory rows: 27; Capture identity: (VendingMachine:12E3FC74); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSFCG76', 1, 'SuperiorRangedWeaponConstructionKits', 155283, 'CTWD', 72, 193);

COMMIT;
