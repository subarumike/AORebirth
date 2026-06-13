-- ============================================================
-- Omni Basic implant staged vendors inserts
-- Target playfield: 1183 ord_smarket_omni_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- AOSharp capture: 20260613-005616
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate OB + 5 Base32(SHA1) chars; shop hashes are reused from existing Clan Basic implant inventories.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: Basic Omni-Tek Adventurer Specific Implants; TemplateId: 162158; VendorId: 77529149; Statel: 0xC03D049F; Capture identity: (VendingMachine:12E48CE0)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529149, 1183, 212.6, 5.01, 133.6, 0, 0, 0, 1, '', 162158, 'OBTGSV6');

-- Terminal: Basic Omni-Tek Agent Specific Implants; TemplateId: 162161; VendorId: 77529151; Statel: 0xC03F049F; Capture identity: (VendingMachine:12E48CE1)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529151, 1183, 214, 5.01, 129, 0, 0, 0, 1, '', 162161, 'OBRGJBU');

-- Terminal: Basic Omni-Tek Bureaucrat Specific Implants; TemplateId: 162164; VendorId: 77529153; Statel: 0xC041049F; Capture identity: (VendingMachine:12E48CE2)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529153, 1183, 214, 5.01, 125, 0, 0, 0, 1, '', 162164, 'OBTSB45');

-- Terminal: Basic Omni-Tek Doctor Specific Implants; TemplateId: 162167; VendorId: 77529155; Statel: 0xC043049F; Capture identity: (VendingMachine:12E48CE3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529155, 1183, 214, 5.01, 121, 0, 0, 0, 1, '', 162167, 'OBPBXFK');

-- Terminal: Basic Omni-Tek Enforcer Specific Implants; TemplateId: 162170; VendorId: 77529156; Statel: 0xC044049F; Capture identity: (VendingMachine:12E48CE4)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529156, 1183, 214, 5.01, 118, 0, 0, 0, 1, '', 162170, 'OB53DT5');

-- Terminal: Basic Omni-Tek Engineer Specific Implants; TemplateId: 162173; VendorId: 77529157; Statel: 0xC045049F; Capture identity: (VendingMachine:12E48CE5)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529157, 1183, 214, 5.011, 114, 0, 0, 0, 1, '', 162173, 'OBRJVSX');

-- Terminal: Basic Omni-Tek Fixer Specific Implants; TemplateId: 162176; VendorId: 77529158; Statel: 0xC046049F; Capture identity: (VendingMachine:12E48CE6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529158, 1183, 214, 5.01, 110, 0, 0, 0, 1, '', 162176, 'OBPR5QM');

-- Terminal: Basic Omni-Tek Martial Artist Specific Implants; TemplateId: 162179; VendorId: 77529159; Statel: 0xC047049F; Capture identity: (VendingMachine:12E48CE7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529159, 1183, 209, 5.01, 104, 0, 0, 0, 1, '', 162179, 'OBVDA5Z');

-- Terminal: Basic Omni-Tek Meta-Physicist Specific Implants; TemplateId: 162182; VendorId: 77529160; Statel: 0xC048049F; Capture identity: (VendingMachine:12E48CE8)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529160, 1183, 204, 5.01, 110, 0, 0, 0, 1, '', 162182, 'OBJIWHL');

-- Terminal: Basic Omni-Tek Nanotechnician Specific Implants; TemplateId: 162185; VendorId: 77529161; Statel: 0xC049049F; Capture identity: (VendingMachine:12E48CE9)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529161, 1183, 212.6, 5.01, 106.4, 0, 0, 0, 1, '', 162185, 'OBTYNLZ');

-- Terminal: Basic Omni-Tek Soldier Specific Implants; TemplateId: 162188; VendorId: 77529162; Statel: 0xC04A049F; Capture identity: (VendingMachine:12E48CEA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529162, 1183, 204, 5.01, 126, 0, 0, 0, 1, '', 162188, 'OB4LOZA');

-- Terminal: Basic Omni-Tek Trader Specific Implants; TemplateId: 162191; VendorId: 77529163; Statel: 0xC04B049F; Capture identity: (VendingMachine:12E48CEB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529163, 1183, 209, 5.01, 136, 0, 0, 0, 1, '', 162191, 'OBED7EA');

-- Terminal: Basic Omni-Tek Keeper Specific Implants; TemplateId: 252270; VendorId: 77529164; Statel: 0xC04C049F; Capture identity: (VendingMachine:12E48CEC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529164, 1183, 203.917, 5.01, 130.122, 0, 0, 0, 1, '', 252270, 'OB3DLM5');

COMMIT;
