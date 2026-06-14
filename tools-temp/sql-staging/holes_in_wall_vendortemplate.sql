-- Holes in the Wall live capture import (staged)
-- Source: AOSharp capture 20260613-221619
-- Coverage: 127 -> 124 (3 reduction: 2 captured statels + 1 exact-template inferred statel)
-- New vendor templates: 2
START TRANSACTION;

-- Terminal: Containers
-- NormalizedName: HolesInWallContainers; TemplateId: 99634; ShopHash: Cont (reused); Inventory rows: 62; Capture identity: (VendingMachine:C0000317); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('HW7LIJB', 1, 'HolesInWallContainers', 99634, 'Cont', 1, 1);

-- Terminal: Superior Weapons
-- NormalizedName: HolesInWallSuperiorWeapons; TemplateId: 151974; ShopHash: FZT5 (new); Inventory rows: 87; Capture identity: (VendingMachine:C0010317); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('HWPZYSE', 1, 'HolesInWallSuperiorWeapons', 151974, 'FZT5', 1, 125);

COMMIT;
