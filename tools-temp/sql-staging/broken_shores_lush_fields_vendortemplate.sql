-- Broken Shores + Lush Fields live capture import (staged)
-- Source: AOSharp capture 20260613-215211
-- Coverage: 129 -> 127 (2 reduction)
-- New vendor templates: 2
START TRANSACTION;

-- Terminal: OT Advanced Trade Skills
-- NormalizedName: BrokenShoresOTAdvancedTradeSkills; TemplateId: 99488; ShopHash: PBPK (new); Inventory rows: 181; Capture identity: (VendingMachine:C0020299); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('BSNZBQZ', 1, 'BrokenShoresOTAdvancedTradeSkills', 99488, 'PBPK', 1, 89);

-- Terminal: Basic Startup Equipment
-- NormalizedName: LushFieldsBasicStartupEquipment; TemplateId: 99643; ShopHash: RCIW (new); Inventory rows: 9; Capture identity: (VendingMachine:C00002B7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('LFUZCHW', 1, 'LushFieldsBasicStartupEquipment', 99643, 'RCIW', 1, 8);

COMMIT;
