-- ============================================================
-- Clan Superior staged vendors inserts
-- Target playfield: 1182 ord_smarket_clan_sup
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-232439
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate CS + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: Superior Ranged Weapon Construction Kits; TemplateId: 155283; VendorId: 77463565; Statel: 0xC00D049E; Capture identity: (VendingMachine:12E3AED6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463565, 1182, 167, 13.101, 199, 0, 0, 0, 1, '', 155283, 'CSFCG76');

-- Terminal: Superior Melee Weapon Construction Kits; TemplateId: 155235; VendorId: 77463566; Statel: 0xC00E049E; Capture identity: (VendingMachine:12E3AED7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463566, 1182, 167, 13.101, 204, 0, 0, 0, 1, '', 155235, 'CSGLKFD');

-- Terminal: Superior Implants; TemplateId: 155224; VendorId: 77463570; Statel: 0xC012049E; Capture identity: (VendingMachine:12E3AEDB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463570, 1182, 167, 13.101, 225, 0, 0, 0, 1, '', 155224, 'CSC6V6B');

-- Terminal: Ranged Weapon Components - Superior; TemplateId: 155492; VendorId: 77463571; Statel: 0xC013049E; Capture identity: (VendingMachine:12E3AEDC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463571, 1182, 177, 7.101, 193, 0, 0, 0, 1, '', 155492, 'CSU6YPU');

-- Terminal: Armour and Clothing Components - Superior; TemplateId: 155498; VendorId: 77463574; Statel: 0xC016049E; Capture identity: (VendingMachine:12E3AEDF)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463574, 1182, 177, 7.101, 215, 0, 0, 0, 1, '', 155498, 'CSNB4VR');

-- Terminal: Nano Crystal Components - Superior; TemplateId: 155313; VendorId: 77463577; Statel: 0xC019049E; Capture identity: (VendingMachine:12E3AEE2)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463577, 1182, 197, 7.101, 199, 0, 0, 0, 1, '', 155313, 'CSQSROE');

-- Terminal: Melee Weapon Components - Superior; TemplateId: 155298; VendorId: 77463579; Statel: 0xC01B049E; Capture identity: (VendingMachine:12E3AEE4)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463579, 1182, 177, 7.101, 199, 0, 0, 0, 1, '', 155298, 'CS2LC3A');

-- Terminal: Ranged Weapon Recipes - Superior; TemplateId: 155507; VendorId: 77463581; Statel: 0xC01D049E; Capture identity: (VendingMachine:12E3AEE6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463581, 1182, 207, 13.101, 219, 0, 0, 0, 1, '', 155507, 'CSD2ZJQ');

-- Terminal: Melee Weapon Recipes - Superior; TemplateId: 155504; VendorId: 77463582; Statel: 0xC01E049E; Capture identity: (VendingMachine:12E3AEE7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463582, 1182, 207, 13.101, 212, 0, 0, 0, 1, '', 155504, 'CSAGOLB');

-- Terminal: Clan Superior Attacks; TemplateId: 99534; VendorId: 77463585; Statel: 0xC021049E; Capture identity: (VendingMachine:12E3AEEA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463585, 1182, 177, 6.1, 168, 0, 0, 0, 1, '', 99534, 'CSSD5SY');

-- Terminal: Clan Superior Augmentations; TemplateId: 99518; VendorId: 77463586; Statel: 0xC022049E; Capture identity: (VendingMachine:12E3AEEB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463586, 1182, 177, 6.1, 162, 0, 0, 0, 1, '', 99518, 'CSXKWKP');

-- Terminal: Clan Superior Medical Supplies; TemplateId: 99529; VendorId: 77463587; Statel: 0xC023049E; Capture identity: (VendingMachine:12E3AEEC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463587, 1182, 197, 8.101, 172, 0, 0, 0, 1, '', 99529, 'CSZKPVY');

-- Terminal: Clan Superior Tools; TemplateId: 99530; VendorId: 77463588; Statel: 0xC024049E; Capture identity: (VendingMachine:12E3AEED)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463588, 1182, 177, 6.1, 156, 0, 0, 0, 1, '', 99530, 'CSAUZMP');

-- Terminal: Clan Superior Weapons; TemplateId: 99507; VendorId: 77463589; Statel: 0xC025049E; Capture identity: (VendingMachine:12E3AEEE)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463589, 1182, 182, 6.101, 175, 0, 0, 0, 1, '', 99507, 'CS5JCOM');

-- Terminal: Clan Clothes; TemplateId: 99526; VendorId: 77463590; Statel: 0xC026049E; Capture identity: (VendingMachine:12E3AEEF)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463590, 1182, 197, 6.1, 158, 0, 0, 0, 1, '', 99526, 'CSOO7JG');

-- Terminal: Clan Containers; TemplateId: 99540; VendorId: 77463591; Statel: 0xC027049E; Capture identity: (VendingMachine:12E3AEF0)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463591, 1182, 193, 6.1, 155, 0, 0, 0, 1, '', 99540, 'CSSOA54');

-- Terminal: Clan Superior Armor; TemplateId: 99504; VendorId: 77463592; Statel: 0xC028049E; Capture identity: (VendingMachine:12E3AEF1)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463592, 1182, 177, 6.1, 174, 0, 0, 0, 1, '', 99504, 'CSFKCVG');

-- Terminal: Clan Maps; TemplateId: 117749; VendorId: 77463593; Statel: 0xC029049E; Capture identity: (VendingMachine:12E3AEF2)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463593, 1182, 179, 6.1, 171, 0, 0, 0, 1, '', 117749, 'CSIHQHX');

-- Terminal: Clan Superior Devices; TemplateId: 155608; VendorId: 77463594; Statel: 0xC02A049E; Capture identity: (VendingMachine:12E3AEF3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463594, 1182, 179, 6.101, 159, 0, 0, 0, 1, '', 155608, 'CS3Q3IF');

COMMIT;
