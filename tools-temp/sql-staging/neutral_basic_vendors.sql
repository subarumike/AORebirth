-- ============================================================
-- Neutral Basic staged vendors inserts
-- Target playfield: 1193 spec_smarket_neut_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source captures: 20260613-012810; 20260613-014033 (Trader Specialist Commerce)
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: Computers; TemplateId: 99603; VendorId: 78184448; Statel: 0xC00004A9; Capture identity: (VendingMachine:12E4ABA8); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184448, 1193, 199, 5, 129, 0, 0, 0, 1, '', 99603, 'NBTLELB');

-- Terminal: Advanced Cars; TemplateId: 99635; VendorId: 78184449; Statel: 0xC00104A9; Capture identity: (VendingMachine:12E4ABA9); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184449, 1193, 193, 5.001, 123, 0, 0, 0, 1, '', 99635, 'NBBBPWA');

-- Terminal: Furniture; TemplateId: 120512; VendorId: 78184450; Statel: 0xC00204A9; Capture identity: (VendingMachine:12E4ABAA); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184450, 1193, 203, 5, 129, 0, 0, 0, 1, '', 120512, 'NB7LZHA');

-- Terminal: Toys and Curiosities; TemplateId: 151983; VendorId: 78184451; Statel: 0xC00304A9; Capture identity: (VendingMachine:12E4ABAB); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184451, 1193, 209, 5, 123, 0, 0, 0, 1, '', 151983, 'NBM27YC');

-- Terminal: Specialist Commerce; TemplateId: 151987; VendorId: 78184452; Statel: 0xC00404A9; Capture identity: (VendingMachine:12E4ABB2); Capture: 20260613-014033
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184452, 1193, 209, 5, 127, 0, 0, 0, 1, '', 151987, 'NBCQ762');

-- Terminal: Superior Cars; TemplateId: 151988; VendorId: 78184453; Statel: 0xC00504A9; Capture identity: (VendingMachine:12E4ABAD); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184453, 1193, 193, 5, 127, 0, 0, 0, 1, '', 151988, 'NB72WE4');

COMMIT;
