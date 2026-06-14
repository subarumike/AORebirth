-- Tower Shop + BS Signup live capture import (staged)
-- Source: AOSharp capture 20260613-223554
-- Coverage: 124 -> 106 (18 reduction)
-- New vendor templates: 18
START TRANSACTION;

-- Terminal: Hovercrafts
-- NormalizedName: TowerShopHovercrafts; TemplateId: 297060; ShopHash: U42F (new); Inventory rows: 31; Capture identity: (VendingMachine:12E504EA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSL6QF5', 1, 'TowerShopHovercrafts', 297060, 'U42F', 15, 200);

-- Terminal: Aircrafts
-- NormalizedName: TowerShopAircrafts; TemplateId: 297059; ShopHash: HCUQ (new); Inventory rows: 17; Capture identity: (VendingMachine:12E504EB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TS6OA23', 1, 'TowerShopAircrafts', 297059, 'HCUQ', 30, 200);

-- Terminal: Watercrafts
-- NormalizedName: TowerShopWatercrafts; TemplateId: 297056; ShopHash: A2ZT (new); Inventory rows: 7; Capture identity: (VendingMachine:12E504EC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSG6QSP', 1, 'TowerShopWatercrafts', 297056, 'A2ZT', 55, 200);

-- Terminal: Basic Towers
-- NormalizedName: TowerShopBasicTowers; TemplateId: 297061; ShopHash: KE42 (new); Inventory rows: 144; Capture identity: (VendingMachine:12E504ED); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSY45Z7', 1, 'TowerShopBasicTowers', 297061, 'KE42', 6, 100);

-- Terminal: Advanced Towers
-- NormalizedName: TowerShopAdvancedTowers; TemplateId: 297064; ShopHash: 35PE (new); Inventory rows: 191; Capture identity: (VendingMachine:12E504EE); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSX2QOJ', 1, 'TowerShopAdvancedTowers', 297064, '35PE', 50, 200);

-- Terminal: Veteran Rewards Vendor
-- NormalizedName: TowerShopVeteranRewardsVendor; TemplateId: 258794; ShopHash: WKXQ (new); Inventory rows: 65; Capture identity: (VendingMachine:12E504F4); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TS3RGJH', 1, 'TowerShopVeteranRewardsVendor', 258794, 'WKXQ', 1, 200);

-- Terminal: Omni City Buildings
-- NormalizedName: TowerShopOmniCityBuildings; TemplateId: 295891; ShopHash: QEO7 (new); Inventory rows: 15; Capture identity: (VendingMachine:12E504E5); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSGCJGV', 1, 'TowerShopOmniCityBuildings', 295891, 'QEO7', 1, 300);

-- Terminal: Social City Buildings
-- NormalizedName: TowerShopSocialCityBuildings; TemplateId: 295893; ShopHash: UFS3 (new); Inventory rows: 19; Capture identity: (VendingMachine:12E504E7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSFHJX6', 1, 'TowerShopSocialCityBuildings', 295893, 'UFS3', 1, 300);

-- Terminal: Furniture
-- NormalizedName: TowerShopFurniture; TemplateId: 297058; ShopHash: EMV2 (new); Inventory rows: 29; Capture identity: (VendingMachine:12E504E8); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSOGO4S', 1, 'TowerShopFurniture', 297058, 'EMV2', 1, 1);

-- Terminal: Decor
-- NormalizedName: TowerShopDecor; TemplateId: 297057; ShopHash: SO4V (new); Inventory rows: 59; Capture identity: (VendingMachine:12E504E9); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSW2LH4', 1, 'TowerShopDecor', 297057, 'SO4V', 1, 1);

-- Terminal: Superior Towers
-- NormalizedName: TowerShopSuperiorTowers; TemplateId: 297063; ShopHash: JHFX (new); Inventory rows: 116; Capture identity: (VendingMachine:12E504EF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSGSFSS', 1, 'TowerShopSuperiorTowers', 297063, 'JHFX', 50, 275);

-- Terminal: Basic Contracts
-- NormalizedName: TowerShopBasicContracts; TemplateId: 297062; ShopHash: QUAT (new); Inventory rows: 350; Capture identity: (VendingMachine:12E504F0); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TSIK3YS', 1, 'TowerShopBasicContracts', 297062, 'QUAT', 1, 100);

-- Terminal: Advanced Contracts
-- NormalizedName: TowerShopAdvancedContracts; TemplateId: 297065; ShopHash: YNWU (new); Inventory rows: 350; Capture identity: (VendingMachine:12E504F1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TS654H3', 1, 'TowerShopAdvancedContracts', 297065, 'YNWU', 101, 200);

-- Terminal: Superior Contracts
-- NormalizedName: TowerShopSuperiorContracts; TemplateId: 297066; ShopHash: GKO6 (new); Inventory rows: 210; Capture identity: (VendingMachine:12E504F2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('TS2DQT2', 1, 'TowerShopSuperiorContracts', 297066, 'GKO6', 201, 250);

-- Terminal: Ofab Meta-Physicist Vendor
-- NormalizedName: BSSignupOfabMetaPhysicistVendor; TemplateId: 266571; ShopHash: 6ZFB (new); Inventory rows: 88; Capture identity: (VendingMachine:C0091777); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OFPMQ3T', 1, 'BSSignupOfabMetaPhysicistVendor', 266571, '6ZFB', 1, 300);

-- Terminal: Ofab General Vendor
-- NormalizedName: BSSignupOfabGeneralVendor; TemplateId: 266579; ShopHash: LS5R (new); Inventory rows: 218; Capture identity: (VendingMachine:C00E1777); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OFVOM25', 1, 'BSSignupOfabGeneralVendor', 266579, 'LS5R', 1, 300);

-- Terminal: Ofab Melee Weapons
-- NormalizedName: BSSignupOfabMeleeWeapons; TemplateId: 266577; ShopHash: DUTX (new); Inventory rows: 60; Capture identity: (VendingMachine:C00F1777); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OFQ2K35', 1, 'BSSignupOfabMeleeWeapons', 266577, 'DUTX', 25, 300);

-- Terminal: Ofab Ranged Weapons
-- NormalizedName: BSSignupOfabRangedWeapons; TemplateId: 266576; ShopHash: XID7 (new); Inventory rows: 78; Capture identity: (VendingMachine:C0101777); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OF4GOBN', 1, 'BSSignupOfabRangedWeapons', 266576, 'XID7', 25, 300);

COMMIT;
