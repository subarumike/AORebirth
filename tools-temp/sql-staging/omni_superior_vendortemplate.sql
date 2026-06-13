-- ============================================================
-- Omni Superior staged vendortemplate inserts
-- Target playfield: 1185 ord_smarket_omni_sup
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-044234
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- ============================================================
START TRANSACTION;

-- Terminal: OT Superior Armor
-- TemplateId: 99477; VendorId(s): 77660173; ShopHash: SI5JBH7GUM; Inventory rows: 29; Capture identity: (VendingMachine:12E3FC84)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSDICGIVPO', 1, 'OTSuperiorArmor', 99477, 'SI5JBH7GUM', 72, 123);

-- Terminal: OT Superior Attacks
-- TemplateId: 99497; VendorId(s): 77660174; ShopHash: SINSYYJNBR; Inventory rows: 8; Capture identity: (VendingMachine:12E3FC85)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSLYXFEL5I', 1, 'OTSuperiorAttacks', 99497, 'SINSYYJNBR', 81, 121);

-- Terminal: OT Superior Augmentations
-- TemplateId: 99486; VendorId(s): 77660175; ShopHash: SILWQ7X37M; Inventory rows: 70; Capture identity: (VendingMachine:12E3FC86)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSBJCYAEGB', 1, 'OTSuperiorAugmentations', 99486, 'SILWQ7X37M', 1, 125);

-- Terminal: OT Superior Medical Supplies
-- TemplateId: 99483; VendorId(s): 77660176; ShopHash: SIPZIOLWU3; Inventory rows: 40; Capture identity: (VendingMachine:12E3FC87)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS4M4YSHH6', 1, 'OTSuperiorMedicalSupplies', 99483, 'SIPZIOLWU3', 70, 125);

-- Terminal: OT Superior Tools
-- TemplateId: 99493; VendorId(s): 77660177; ShopHash: SIHBWJ6AZ3; Inventory rows: 19; Capture identity: (VendingMachine:12E3FC88)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSWKKC5YQA', 1, 'OTSuperiorTools', 99493, 'SIHBWJ6AZ3', 1, 121);

-- Terminal: OT Superior Weapons
-- TemplateId: 99480; VendorId(s): 77660178; ShopHash: SID546XS6U; Inventory rows: 88; Capture identity: (VendingMachine:12E3FC89)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSJDWIX6AP', 1, 'OTSuperiorWeapons', 99480, 'SID546XS6U', 1, 125);

-- Terminal: OT Clothes
-- TemplateId: 99490; VendorId(s): 77660179; ShopHash: SIEQ2HZY6K; Inventory rows: 16; Capture identity: (VendingMachine:12E3FC8A)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS3IYLKOPA', 1, 'OTSuperiorClothes', 99490, 'SIEQ2HZY6K', 1, 1);

-- Terminal: OT Maps
-- TemplateId: 117649; VendorId(s): 77660181; ShopHash: LJI7; Inventory rows: 2; Capture identity: (VendingMachine:12E3FC8C)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSAZP273UR', 1, 'OTSuperiorMaps', 117649, 'LJI7', 1, 30);

-- Terminal: Omni Superior Devices
-- TemplateId: 155609; VendorId(s): 77660183; ShopHash: SIS3LYTOMC; Inventory rows: 27; Capture identity: (VendingMachine:12E3FC8E)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSIQJ65WUV', 1, 'OmniSuperiorDevices', 155609, 'SIS3LYTOMC', 2, 193);

-- Terminal: Melee Weapon Recipes - Superior
-- TemplateId: 155504; VendorId(s): 77660185, 77660188; ShopHash: SIYYITIETC; Inventory rows: 75; Capture identity: (VendingMachine:12E3FC59)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSDHX2VLQ7', 1, 'SuperiorMeleeWeaponRecipes', 155504, 'SIYYITIETC', 1, 1);

-- Terminal: Ranged Weapon Recipes - Superior
-- TemplateId: 155507; VendorId(s): 77660186, 77660189; ShopHash: SICNDOMXXR; Inventory rows: 325; Capture identity: (VendingMachine:12E3FC5A)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS3QJTUFWR', 1, 'SuperiorRangedWeaponRecipes', 155507, 'SICNDOMXXR', 1, 100);

-- Terminal: Melee Weapon Components - Superior
-- TemplateId: 155298; VendorId(s): 77660193; ShopHash: SIJMYYJCTB; Inventory rows: 50; Capture identity: (VendingMachine:12E3FC61)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSPKU3XEFS', 1, 'SuperiorMeleeWeaponComponentsA', 155298, 'SIJMYYJCTB', 70, 199);

-- Terminal: Ranged Weapon Components - Superior
-- TemplateId: 155492; VendorId(s): 77660194; ShopHash: SIDEH6NGAN; Inventory rows: 123; Capture identity: (VendingMachine:12E3FC62)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSCLJWTK54', 1, 'SuperiorRangedWeaponComponentsA', 155492, 'SIDEH6NGAN', 1, 200);

-- Terminal: Armour and Clothing Components - Superior
-- TemplateId: 155498; VendorId(s): 77660197, 77660207; ShopHash: SIADDO57ME; Inventory rows: 4; Capture identity: (VendingMachine:12E3FC65)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS7PWVFK6R', 1, 'SuperiorArmourClothingComponents', 155498, 'SIADDO57ME', 80, 193);

-- Terminal: Nano Crystal Components - Superior
-- TemplateId: 155313; VendorId(s): 77660198, 77660208; ShopHash: SIAY7ZSSJX; Inventory rows: 60; Capture identity: (VendingMachine:12E3FC66)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS7J42QVHY', 1, 'SuperiorNanoCrystalComponents', 155313, 'SIAY7ZSSJX', 71, 200);

-- Terminal: Melee Weapon Components - Superior
-- TemplateId: 155298; VendorId(s): 77660203; ShopHash: SIYISUGK3V; Inventory rows: 50; Capture identity: (VendingMachine:12E3FC6B)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSSZ54MXPM', 1, 'SuperiorMeleeWeaponComponentsB', 155298, 'SIYISUGK3V', 71, 199);

-- Terminal: Ranged Weapon Components - Superior
-- TemplateId: 155492; VendorId(s): 77660204; ShopHash: SI5LRPTKN4; Inventory rows: 123; Capture identity: (VendingMachine:12E3FC6C)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS6OKLRJ6E', 1, 'SuperiorRangedWeaponComponentsB', 155492, 'SI5LRPTKN4', 1, 199);

-- Terminal: Superior Implants
-- TemplateId: 155224; VendorId(s): 77660210, 77660216; ShopHash: SI3ASAQHHA; Inventory rows: 38; Capture identity: (VendingMachine:12E3FC72)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OS5BZVLP5S', 1, 'SuperiorImplants', 155224, 'SI3ASAQHHA', 71, 121);

-- Terminal: Superior Melee Weapon Construction Kits
-- TemplateId: 155235; VendorId(s): 77660211, 77660217; ShopHash: SIJ4SPL3PW; Inventory rows: 39; Capture identity: (VendingMachine:12E3FC73)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSZ5QYJ2QF', 1, 'SuperiorMeleeWeaponConstructionKits', 155235, 'SIJ4SPL3PW', 72, 200);

-- Terminal: Superior Ranged Weapon Construction Kits
-- TemplateId: 155283; VendorId(s): 77660212, 77660218; ShopHash: SI4OV4V6OD; Inventory rows: 27; Capture identity: (VendingMachine:12E3FC74)
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSMRQE3XPA', 1, 'SuperiorRangedWeaponConstructionKits', 155283, 'SI4OV4V6OD', 72, 193);

COMMIT;
