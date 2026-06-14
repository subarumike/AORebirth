-- Clan Basic Startup Equipment import (staged)
-- Source: AOSharp capture 20260613-211234
-- Coverage: 133 -> 129 (4 reduction)
-- Terminal: Clan Basic Startup Equipment
-- NormalizedName: ClanBasicStartupEquipment; TemplateId: 99569; ShopHash: VZMO (new); Inventory rows: 9
START TRANSACTION;

INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBFMV5N', 1, 'ClanBasicStartupEquipment', 99569, 'VZMO', 1, 1);

COMMIT;
