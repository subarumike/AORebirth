-- ============================================================
-- Omni Advanced staged vendors inserts
-- Target playfield: 1184 ord_smarket_omni_advanced
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260613-002828
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate OA + 5 Base32(SHA1) chars; shop hash 4 Base32(SHA1) chars; collision-safe window.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: Melee Weapon Recipes - Advanced; TemplateId: 155503; VendorId: 77594638; Statel: 0xC00E04A0; Capture identity: (VendingMachine:12E4907E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594638, 1184, 155, 5, 107, 0, 0, 0, 1, '', 155503, 'OALQXGA');

-- Terminal: Ranged Weapon Recipes - Advanced; TemplateId: 155506; VendorId: 77594639; Statel: 0xC00F04A0; Capture identity: (VendingMachine:12E4907F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594639, 1184, 153, 5, 107, 0, 0, 0, 1, '', 155506, 'OAIFSRG');

-- Terminal: Melee Weapon Recipes - Advanced; TemplateId: 155503; VendorId: 77594641; Statel: 0xC01104A0; Capture identity: (VendingMachine:12E49081)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594641, 1184, 153.5, 17, 133.1, 0, 0, 0, 1, '', 155503, 'OALQXGA');

-- Terminal: Ranged Weapon Recipes - Advanced; TemplateId: 155506; VendorId: 77594642; Statel: 0xC01204A0; Capture identity: (VendingMachine:12E49082)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594642, 1184, 155.5, 17, 133.1, 0, 0, 0, 1, '', 155506, 'OAIFSRG');

-- Terminal: Melee Weapon Components - Advanced; TemplateId: 155297; VendorId: 77594646; Statel: 0xC01604A0; Capture identity: (VendingMachine:12E49086)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594646, 1184, 180.8, 13, 133.1, 0, 0, 0, 1, '', 155297, 'OAAC3R2');

-- Terminal: Ranged Weapon Components - Advanced; TemplateId: 155491; VendorId: 77594647; Statel: 0xC01704A0; Capture identity: (VendingMachine:12E49087)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594647, 1184, 178.8, 13, 133.1, 0, 0, 0, 1, '', 155491, 'OABOGYY');

-- Terminal: Melee Weapon Components - Advanced; TemplateId: 155297; VendorId: 77594656; Statel: 0xC02004A0; Capture identity: (VendingMachine:12E49090)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594656, 1184, 177, 5, 131, 0, 0, 0, 1, '', 155297, 'OAAC3R2');

-- Terminal: Ranged Weapon Components - Advanced; TemplateId: 155491; VendorId: 77594657; Statel: 0xC02104A0; Capture identity: (VendingMachine:12E49091)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594657, 1184, 175, 5, 131, 0, 0, 0, 1, '', 155491, 'OABOGYY');

-- Terminal: Advanced Implants; TemplateId: 155223; VendorId: 77594663; Statel: 0xC02704A0; Capture identity: (VendingMachine:12E49097)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594663, 1184, 146.9, 13, 106.1, 0, 0, 0, 1, '', 155223, 'OAG44BS');

-- Terminal: Advanced Melee Weapon Construction Kits; TemplateId: 155234; VendorId: 77594664; Statel: 0xC02804A0; Capture identity: (VendingMachine:12E49098)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594664, 1184, 146.9, 13, 108.1, 0, 0, 0, 1, '', 155234, 'OAXHP7H');

-- Terminal: Advanced Ranged Weapon Construction Kits; TemplateId: 155282; VendorId: 77594665; Statel: 0xC02904A0; Capture identity: (VendingMachine:12E49099)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594665, 1184, 146.9, 13, 110.1, 0, 0, 0, 1, '', 155282, 'OAKGM6T');

-- Terminal: Advanced Implants; TemplateId: 155223; VendorId: 77594669; Statel: 0xC02D04A0; Capture identity: (VendingMachine:12E4909D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594669, 1184, 191, 5.001, 107, 0, 0, 0, 1, '', 155223, 'OAG44BS');

-- Terminal: Advanced Melee Weapon Construction Kits; TemplateId: 155234; VendorId: 77594670; Statel: 0xC02E04A0; Capture identity: (VendingMachine:12E4909E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594670, 1184, 189, 5.001, 107, 0, 0, 0, 1, '', 155234, 'OAXHP7H');

-- Terminal: Advanced Ranged Weapon Construction Kits; TemplateId: 155282; VendorId: 77594671; Statel: 0xC02F04A0; Capture identity: (VendingMachine:12E4909F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594671, 1184, 187, 5.001, 107, 0, 0, 0, 1, '', 155282, 'OAKGM6T');

-- Terminal: OT Advanced Armor; TemplateId: 99386; VendorId: 77594682; Statel: 0xC03A04A0; Capture identity: (VendingMachine:12E490A9)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594682, 1184, 203.2, 5, 111, 0, 0, 0, 1, '', 99386, 'OAL6IVC');

-- Terminal: OT Advanced Attacks; TemplateId: 99496; VendorId: 77594683; Statel: 0xC03B04A0; Capture identity: (VendingMachine:12E490AA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594683, 1184, 203.2, 5, 115, 0, 0, 0, 1, '', 99496, 'OAECAN7');

-- Terminal: OT Advanced Augmentations; TemplateId: 99485; VendorId: 77594684; Statel: 0xC03C04A0; Capture identity: (VendingMachine:12E490AB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594684, 1184, 211.99, 5, 115, 0, 0, 0, 1, '', 99485, 'OAS6ZPM');

-- Terminal: OT Advanced Medical Supplies; TemplateId: 99482; VendorId: 77594685; Statel: 0xC03D04A0; Capture identity: (VendingMachine:12E490AC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594685, 1184, 209.072, 5, 120.803, 0, 0, 0, 1, '', 99482, 'OAW76SU');

-- Terminal: OT Advanced Tools; TemplateId: 99492; VendorId: 77594686; Statel: 0xC03E04A0; Capture identity: (VendingMachine:12E490AD)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594686, 1184, 222.8, 5, 115, 0, 0, 0, 1, '', 99492, 'OAAMXEE');

-- Terminal: OT Advanced Weapons; TemplateId: 99479; VendorId: 77594687; Statel: 0xC03F04A0; Capture identity: (VendingMachine:12E490AE)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594687, 1184, 222.8, 5, 119, 0, 0, 0, 1, '', 99479, 'OAE5BNV');

-- Terminal: OT Clothes; TemplateId: 99490; VendorId: 77594688; Statel: 0xC04004A0; Capture identity: (VendingMachine:12E490AF)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594688, 1184, 213, 5, 113.99, 0, 0, 0, 1, '', 99490, 'OAFBTI6');

-- Terminal: OT Maps; TemplateId: 117649; VendorId: 77594690; Statel: 0xC04204A0; Capture identity: (VendingMachine:12E490B1)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594690, 1184, 219.032, 5, 106.9, 0, 0, 0, 1, '', 117649, 'OAA6B2F');

-- Terminal: Omni Advanced Devices; TemplateId: 155606; VendorId: 77594692; Statel: 0xC04404A0; Capture identity: (VendingMachine:12E490B3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594692, 1184, 214, 5.001, 115, 0, 0, 0, 1, '', 155606, 'OAX2G2O');

COMMIT;
