-- Clan Basic Startup Equipment import (staged)
-- Source: AOSharp capture 20260613-211234
-- Coverage: 133 -> 129 (4 reduction)
-- Dedup: 4 Old Athen vendors share 1 inventory
START TRANSACTION;

-- VendorId: 35389440; Playfield: 540 Old Athen; TemplateId: 99569 Clan Basic Startup Equipment; Statel: 0xC000021C; Capture identity: (VendingMachine:C000021C); Capture: 20260613-211234
-- MappingType: Captured
-- MappingSource: Captured ShopUpdate inventory on C000021C; missing direct VendorFull correlated by statel template 99569 and identical 9-row inventory with three VendorFull-confirmed terminals.
-- MappingConfidence: High
-- Justification: user-approved correlation rule treats exact template plus identical captured inventory as Captured for the missing VendorFull case.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (35389440, 540, 442, 8.001, 330, 0, 0, 0, 1, '', 99569, 'CBFMV5N');

-- VendorId: 35389442; Playfield: 540 Old Athen; TemplateId: 99569 Clan Basic Startup Equipment; Statel: 0xC002021C; Capture identity: (VendingMachine:C002021C); Capture: 20260613-211234
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Old Athen AOSharp capture. Capture: 20260613-211234
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (35389442, 540, 462, 15, 414, 0, 0, 0, 1, '', 99569, 'CBFMV5N');

-- VendorId: 35389444; Playfield: 540 Old Athen; TemplateId: 99569 Clan Basic Startup Equipment; Statel: 0xC004021C; Capture identity: (VendingMachine:C004021C); Capture: 20260613-211234
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Old Athen AOSharp capture. Capture: 20260613-211234
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (35389444, 540, 406, 15, 378, 0, 0, 0, 1, '', 99569, 'CBFMV5N');

-- VendorId: 35389446; Playfield: 540 Old Athen; TemplateId: 99569 Clan Basic Startup Equipment; Statel: 0xC006021C; Capture identity: (VendingMachine:C006021C); Capture: 20260613-211234
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Old Athen AOSharp capture. Capture: 20260613-211234
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (35389446, 540, 418, 8, 458, 0, 0, 0, 1, '', 99569, 'CBFMV5N');

COMMIT;
