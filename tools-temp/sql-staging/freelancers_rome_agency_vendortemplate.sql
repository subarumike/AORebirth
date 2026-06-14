-- Freelancers Inc. HQ - Rome Agency Shop import
-- Source: AOSharp capture 20260614-022639
-- Coverage: 27 -> 26 (1 reduction)
-- New inventory groups: 1

START TRANSACTION;

-- Vendortemplate rows: 1
-- Hash rules: vendortemplate FR + 5 Base32(SHA1); shop hash 4 Base32(SHA1); collision-safe window.

-- Terminal: Agency Shop
-- NormalizedName: FreelancersRomeAgencyShop; TemplateId: 285348; ShopHash: KNJM (new); Inventory rows: 26; Capture identity: (VendingMachine:C0001B63); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('FRGYZBG', 1, 'FreelancersRomeAgencyShop', 285348, 'KNJM', 1, 189);

COMMIT;
