-- ============================================================
-- Clan Basic staged vendors inserts
-- Target playfield: 1180 ord_smarket_clan_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-225855
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: Clan Basic Armor; TemplateId: 99502; VendorId: 77332493; Statel: 0xC00D049C; Capture identity: (VendingMachine:12E47537)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332493, 1180, 207, 5.01, 183, 0, 0, 0, 1, '', 99502, 'CBRE2SH');

-- Terminal: Clan Basic Attacks; TemplateId: 99532; VendorId: 77332494; Statel: 0xC00E049C; Capture identity: (VendingMachine:12E47538)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332494, 1180, 194.996, 4.609, 162.997, 0, 0, 0, 1, '', 99532, 'CB63J4Z');

-- Terminal: Clan Basic Augmentations; TemplateId: 99513; VendorId: 77332495; Statel: 0xC00F049C; Capture identity: (VendingMachine:12E47539)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332495, 1180, 194.066, 4.6, 181.138, 0, 0, 0, 1, '', 99513, 'CBSRZ3W');

-- Terminal: Clan Basic Medical Supplies; TemplateId: 99508; VendorId: 77332496; Statel: 0xC010049C; Capture identity: (VendingMachine:12E4753A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332496, 1180, 191.887, 4.601, 163.939, 0, 0, 0, 1, '', 99508, 'CBUMBXD');

-- Terminal: Clan Basic Tools; TemplateId: 99527; VendorId: 77332497; Statel: 0xC011049C; Capture identity: (VendingMachine:12E4753B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332497, 1180, 207.154, 4.6, 162.871, 0, 0, 0, 1, '', 99527, 'CBIGA24');

-- Terminal: Clan Basic Weapons; TemplateId: 99505; VendorId: 77332498; Statel: 0xC012049C; Capture identity: (VendingMachine:12E4753C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332498, 1180, 209.829, 4.6, 163.716, 0, 0, 0, 1, '', 99505, 'CBIEXSV');

-- Terminal: Clan Clothes; TemplateId: 99526; VendorId: 77332499; Statel: 0xC013049C; Capture identity: (VendingMachine:12E4753D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332499, 1180, 191.441, 4.6, 180.06, 0, 0, 0, 1, '', 99526, 'CBJY7AT');

-- Terminal: Clan Maps; TemplateId: 117749; VendorId: 77332500; Statel: 0xC014049C; Capture identity: (VendingMachine:12E4753E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332500, 1180, 190.872, 4.6, 177.595, 0, 0, 0, 1, '', 117749, 'CBKAVJ6');

-- Terminal: Clan Basic Devices; TemplateId: 155602; VendorId: 77332501; Statel: 0xC015049C; Capture identity: (VendingMachine:12E4753F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332501, 1180, 190.886, 4.6, 166.795, 0, 0, 0, 1, '', 155602, 'CBGXGWQ');

-- Terminal: Basic Clan Adventurer Specific Implants; TemplateId: 162157; VendorId: 77332503; Statel: 0xC017049C; Capture identity: (VendingMachine:12E47541)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332503, 1180, 233, 5.01, 187, 0, 0, 0, 1, '', 162157, 'CBIDRVY');

-- Terminal: Basic Clan Agent Specific Implants; TemplateId: 162160; VendorId: 77332504; Statel: 0xC018049C; Capture identity: (VendingMachine:12E47542)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332504, 1180, 237, 5.01, 187, 0, 0, 0, 1, '', 162160, 'CBFGTU4');

-- Terminal: Basic Clan Bureaucrat Specific Implants; TemplateId: 162163; VendorId: 77332505; Statel: 0xC019049C; Capture identity: (VendingMachine:12E47543)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332505, 1180, 241, 5.01, 187, 0, 0, 0, 1, '', 162163, 'CBCCGCW');

-- Terminal: Basic Clan Doctor Specific Implants; TemplateId: 162166; VendorId: 77332506; Statel: 0xC01A049C; Capture identity: (VendingMachine:12E47544)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332506, 1180, 241, 5.01, 183, 0, 0, 0, 1, '', 162166, 'CBZ3BNX');

-- Terminal: Basic Clan Enforcer Specific Implants; TemplateId: 162169; VendorId: 77332507; Statel: 0xC01B049C; Capture identity: (VendingMachine:12E47545)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332507, 1180, 241, 5.01, 175, 0, 0, 0, 1, '', 162169, 'CBVTEV5');

-- Terminal: Basic Clan Engineer Specific Implants; TemplateId: 162172; VendorId: 77332508; Statel: 0xC01C049C; Capture identity: (VendingMachine:12E47546)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332508, 1180, 241, 5.01, 171, 0, 0, 0, 1, '', 162172, 'CBHGCGP');

-- Terminal: Basic Clan Fixer Specific Implants; TemplateId: 162175; VendorId: 77332509; Statel: 0xC01D049C; Capture identity: (VendingMachine:12E47547)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332509, 1180, 237, 5.01, 171, 0, 0, 0, 1, '', 162175, 'CBYDGZL');

-- Terminal: Basic Clan Martial Artist Specific Implants; TemplateId: 162178; VendorId: 77332510; Statel: 0xC01E049C; Capture identity: (VendingMachine:12E47548)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332510, 1180, 229, 5.01, 171, 0, 0, 0, 1, '', 162178, 'CBXEBL5');

-- Terminal: Basic Clan Meta-Physicist Specific Implants; TemplateId: 162181; VendorId: 77332511; Statel: 0xC01F049C; Capture identity: (VendingMachine:12E47549)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332511, 1180, 225, 5.01, 171, 0, 0, 0, 1, '', 162181, 'CB2ELTH');

-- Terminal: Basic Clan Nanotechnician Specific Implants; TemplateId: 162184; VendorId: 77332512; Statel: 0xC020049C; Capture identity: (VendingMachine:12E4754A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332512, 1180, 233, 5.01, 171, 0, 0, 0, 1, '', 162184, 'CBLEZ7U');

-- Terminal: Basic Clan Soldier Specific Implants; TemplateId: 162187; VendorId: 77332513; Statel: 0xC021049C; Capture identity: (VendingMachine:12E4754B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332513, 1180, 225, 5.01, 187, 0, 0, 0, 1, '', 162187, 'CBMK5IN');

-- Terminal: Basic Clan Trader Specific Implants; TemplateId: 162190; VendorId: 77332514; Statel: 0xC022049C; Capture identity: (VendingMachine:12E4754C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332514, 1180, 229, 5.01, 187, 0, 0, 0, 1, '', 162190, 'CBDWQH6');

-- Terminal: Basic Clan Keeper Specific Implants; TemplateId: 252271; VendorId: 77332515; Statel: 0xC023049C; Capture identity: (VendingMachine:12E4754D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332515, 1180, 221.114, 5.01, 171.109, 0, 0, 0, 1, '', 252271, 'CBHJMPZ');

-- Terminal: Basic Implants; TemplateId: 155222; VendorId: 77332516; Statel: 0xC024049C; Capture identity: (VendingMachine:12E4754E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332516, 1180, 133, 11, 153, 0, 0, 0, 1, '', 155222, 'CBOFY25');

-- Terminal: Basic Melee Weapon Construction Kits; TemplateId: 155233; VendorId: 77332517; Statel: 0xC025049C; Capture identity: (VendingMachine:12E4754F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332517, 1180, 154, 11.012, 153, 0, 0, 0, 1, '', 155233, 'CBMPNFR');

-- Terminal: Basic Ranged Weapon Construction Kits; TemplateId: 155236; VendorId: 77332518; Statel: 0xC026049C; Capture identity: (VendingMachine:12E47550)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332518, 1180, 159, 11, 153, 0, 0, 0, 1, '', 155236, 'CBUHWW4');

-- Terminal: Melee Weapon Components - Basic; TemplateId: 155296; VendorId: 77332525; Statel: 0xC02D049C; Capture identity: (VendingMachine:12E47557)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332525, 1180, 159, 5, 163, 0, 0, 0, 1, '', 155296, 'CBF6KVT');

-- Terminal: Ranged Weapon Components - Basic; TemplateId: 155490; VendorId: 77332526; Statel: 0xC02E049C; Capture identity: (VendingMachine:12E47558)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332526, 1180, 166, 5.001, 163, 0, 0, 0, 1, '', 155490, 'CBJNHBE');

-- Terminal: Melee Weapon Recipes - Basic; TemplateId: 155502; VendorId: 77332532; Statel: 0xC034049C; Capture identity: (VendingMachine:12E4755E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332532, 1180, 146, 11, 193, 0, 0, 0, 1, '', 155502, 'CBGYYPZ');

-- Terminal: Ranged Weapon Recipes - Basic; TemplateId: 155505; VendorId: 77332534; Statel: 0xC036049C; Capture identity: (VendingMachine:12E47560)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332534, 1180, 139.016, 11, 192.999, 0, 0, 0, 1, '', 155505, 'CB6HN4F');

COMMIT;
