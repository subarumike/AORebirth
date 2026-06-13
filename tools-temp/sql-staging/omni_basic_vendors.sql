-- ============================================================
-- Omni Basic staged vendors inserts
-- Target playfield: 1183 ord_smarket_omni_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-012644
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: OT Basic Armor; TemplateId: 99383; VendorId: 77529088; Statel: 0xC000049F; Capture identity: (VendingMachine:12E3F4F3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529088, 1183, 192, 5, 139.5, 0, 0, 0, 1, '', 99383, 'OBF3VGA');

-- Terminal: OT Basic Attacks; TemplateId: 99495; VendorId: 77529089; Statel: 0xC001049F; Capture identity: (VendingMachine:12E3F4F4)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529089, 1183, 182, 5, 127.6, 0, 0, 0, 1, '', 99495, 'OBAGPLU');

-- Terminal: OT Basic Augmentations; TemplateId: 99484; VendorId: 77529090; Statel: 0xC002049F; Capture identity: (VendingMachine:12E3F4F5)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529090, 1183, 182, 5, 131.6, 0, 0, 0, 1, '', 99484, 'OBFUYMA');

-- Terminal: OT Basic Medical Supplies; TemplateId: 99481; VendorId: 77529091; Statel: 0xC003049F; Capture identity: (VendingMachine:12E3F4F6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529091, 1183, 182, 5, 135.6, 0, 0, 0, 1, '', 99481, 'OBX7YEB');

-- Terminal: OT Basic Tools; TemplateId: 99491; VendorId: 77529092; Statel: 0xC004049F; Capture identity: (VendingMachine:12E3F4F7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529092, 1183, 192, 5, 135.5, 0, 0, 0, 1, '', 99491, 'OBH6GZY');

-- Terminal: OT Basic Weapons; TemplateId: 99478; VendorId: 77529093; Statel: 0xC005049F; Capture identity: (VendingMachine:12E3F4F8)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529093, 1183, 192, 5, 127.5, 0, 0, 0, 1, '', 99478, 'OBMZVQZ');

-- Terminal: OT Clothes; TemplateId: 99490; VendorId: 77529094; Statel: 0xC006049F; Capture identity: (VendingMachine:12E3F4F9)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529094, 1183, 182, 5, 139.6, 0, 0, 0, 1, '', 99490, 'OBCQTXM');

-- Terminal: OT Maps; TemplateId: 117649; VendorId: 77529095; Statel: 0xC007049F; Capture identity: (VendingMachine:12E3F4FA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529095, 1183, 196.7, 5, 129, 0, 0, 0, 1, '', 117649, 'OBMOJMG');

-- Terminal: Omni Basic Devices; TemplateId: 155603; VendorId: 77529097; Statel: 0xC009049F; Capture identity: (VendingMachine:12E3F4FC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529097, 1183, 196.7, 5.001, 133, 0, 0, 0, 1, '', 155603, 'OBIUAFT');

-- Terminal: Melee Weapon Recipes - Basic; TemplateId: 155502; VendorId: 77529112; Statel: 0xC018049F; Capture identity: (VendingMachine:12E3F50B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529112, 1183, 117, 5, 123, 0, 0, 0, 1, '', 155502, 'OBGYYPZ');

-- Terminal: Ranged Weapon Recipes - Basic; TemplateId: 155505; VendorId: 77529113; Statel: 0xC019049F; Capture identity: (VendingMachine:12E3F50C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529113, 1183, 115, 5, 123, 0, 0, 0, 1, '', 155505, 'OB6HN4F');

-- Terminal: Melee Weapon Components - Basic; TemplateId: 155296; VendorId: 77529117; Statel: 0xC01D049F; Capture identity: (VendingMachine:12E3F510)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529117, 1183, 142.8, 13, 149.1, 0, 0, 0, 1, '', 155296, 'OBF6KVT');

-- Terminal: Ranged Weapon Components - Basic; TemplateId: 155490; VendorId: 77529118; Statel: 0xC01E049F; Capture identity: (VendingMachine:12E3F511)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529118, 1183, 140.8, 13, 149.1, 0, 0, 0, 1, '', 155490, 'OBJNHBE');

-- Terminal: Basic Implants; TemplateId: 155222; VendorId: 77529124; Statel: 0xC024049F; Capture identity: (VendingMachine:12E3F517)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529124, 1183, 108.9, 13, 122.1, 0, 0, 0, 1, '', 155222, 'OBOFY25');

-- Terminal: Basic Melee Weapon Construction Kits; TemplateId: 155233; VendorId: 77529125; Statel: 0xC025049F; Capture identity: (VendingMachine:12E3F518)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529125, 1183, 108.9, 13, 124.1, 0, 0, 0, 1, '', 155233, 'OBMPNFR');

-- Terminal: Basic Ranged Weapon Construction Kits; TemplateId: 155236; VendorId: 77529126; Statel: 0xC026049F; Capture identity: (VendingMachine:12E3F519)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529126, 1183, 108.9, 13, 126.1, 0, 0, 0, 1, '', 155236, 'OBUHWW4');

-- Terminal: Melee Weapon Recipes - Basic; TemplateId: 155502; VendorId: 77529131; Statel: 0xC02B049F; Capture identity: (VendingMachine:12E3F51E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529131, 1183, 115.5, 17, 149.1, 0, 0, 0, 1, '', 155502, 'OBGYYPZ');

-- Terminal: Ranged Weapon Recipes - Basic; TemplateId: 155505; VendorId: 77529132; Statel: 0xC02C049F; Capture identity: (VendingMachine:12E3F51F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529132, 1183, 117.5, 17, 149.1, 0, 0, 0, 1, '', 155505, 'OB6HN4F');

-- Terminal: Melee Weapon Components - Basic; TemplateId: 155296; VendorId: 77529136; Statel: 0xC030049F; Capture identity: (VendingMachine:12E3F523)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529136, 1183, 139, 5, 147, 0, 0, 0, 1, '', 155296, 'OBF6KVT');

-- Terminal: Ranged Weapon Components - Basic; TemplateId: 155490; VendorId: 77529137; Statel: 0xC031049F; Capture identity: (VendingMachine:12E3F524)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529137, 1183, 137, 5, 147, 0, 0, 0, 1, '', 155490, 'OBJNHBE');

-- Terminal: Basic Implants; TemplateId: 155222; VendorId: 77529143; Statel: 0xC037049F; Capture identity: (VendingMachine:12E3F52A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529143, 1183, 153, 5.001, 123, 0, 0, 0, 1, '', 155222, 'OBOFY25');

-- Terminal: Basic Melee Weapon Construction Kits; TemplateId: 155233; VendorId: 77529144; Statel: 0xC038049F; Capture identity: (VendingMachine:12E3F52B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529144, 1183, 151, 5.001, 123, 0, 0, 0, 1, '', 155233, 'OBMPNFR');

-- Terminal: Basic Ranged Weapon Construction Kits; TemplateId: 155236; VendorId: 77529145; Statel: 0xC039049F; Capture identity: (VendingMachine:12E3F52C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529145, 1183, 149, 5.001, 123, 0, 0, 0, 1, '', 155236, 'OBUHWW4');

COMMIT;
