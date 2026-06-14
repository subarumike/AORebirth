-- ============================================================
-- Newland + Omni startup staged SQL
-- Source capture: AOSharp 20260613-185338
-- Targets: 565 Newland Desert vendors + 710 Omni-1 Trade startup equipment
-- Expected coverage: 142 -> 133 (9 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate NL/OT + 5 Base32(SHA1); shop hash 4 Base32(SHA1) over numeric-sorted [Low:High:QL] rows; collision-safe window.
-- ============================================================
START TRANSACTION;

-- VendorId: 37027841; Playfield: 565 Newland Desert; TemplateId: 99570 Basic Armor; Statel: 0xC0010235; Capture identity: (VendingMachine:C0010235); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Newland Desert AOSharp capture. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027841, 565, 1549.943, 31.2, 2730.035, 0, 0, 0, 1, '', 99570, 'NLPY36V');

-- VendorId: 37027844; Playfield: 565 Newland Desert; TemplateId: 99643 Basic Startup Equipment; Statel: 0xC0040235; Capture identity: (VendingMachine:C0040235); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Newland Desert AOSharp capture. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027844, 565, 1518.001, 31.2, 2730.07, 0, 0, 0, 1, '', 99643, 'NLLVEHN');

-- VendorId: 37027845; Playfield: 565 Newland Desert; TemplateId: 118287 Basic Nano Clusters; Statel: 0xC0050235; Capture identity: (VendingMachine:C0050235); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Newland Desert AOSharp capture. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027845, 565, 1510.011, 31.2, 2730.056, 0, 0, 0, 1, '', 118287, 'NLD7VOT');

-- VendorId: 37027847; Playfield: 565 Newland Desert; TemplateId: 121035 Food; Statel: 0xC0070235; Capture identity: (VendingMachine:C0070235); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured ShopUpdate on target statel; VendorFull evidence exists for same playfield/template using alternate live identity (VendingMachine:12B90F98). Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027847, 565, 2187.295, 21.3, 1551.807, 0, 0, 0, 1, '', 121035, 'NLV4RLO');

-- VendorId: 37027848; Playfield: 565 Newland Desert; TemplateId: 121037 Drinks; Statel: 0xC0080235; Capture identity: (VendingMachine:C0080235); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured ShopUpdate on target statel; VendorFull evidence exists for same playfield/template using alternate live identity (VendingMachine:12B90F99). Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027848, 565, 2185.245, 21.3, 1552.811, 0, 0, 0, 1, '', 121037, 'NLHROVV');

-- VendorId: 46530560; Playfield: 710 Omni-1 Trade; TemplateId: 99555 OT Basic Startup Equipment; Statel: 0xC00002C6; Capture identity: (VendingMachine:C00002C6); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured four Omni-1 Trade startup terminals; all four inventories are identical and share this template. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (46530560, 710, 246, 8, 413, 0, 0, 0, 1, '', 99555, 'OTRDLME');

-- VendorId: 46530561; Playfield: 710 Omni-1 Trade; TemplateId: 99555 OT Basic Startup Equipment; Statel: 0xC00102C6; Capture identity: (VendingMachine:C00102C6); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured four Omni-1 Trade startup terminals; all four inventories are identical and share this template. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (46530561, 710, 246, 8, 350, 0, 0, 0, 1, '', 99555, 'OTRDLME');

-- VendorId: 46530562; Playfield: 710 Omni-1 Trade; TemplateId: 99555 OT Basic Startup Equipment; Statel: 0xC00202C6; Capture identity: (VendingMachine:C00202C6); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured four Omni-1 Trade startup terminals; all four inventories are identical and share this template. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (46530562, 710, 562, 8, 351, 0, 0, 0, 1, '', 99555, 'OTRDLME');

-- VendorId: 46530563; Playfield: 710 Omni-1 Trade; TemplateId: 99555 OT Basic Startup Equipment; Statel: 0xC00302C6; Capture identity: (VendingMachine:C00302C6); Capture: 20260613-185338
-- MappingType: Captured
-- MappingSource: Captured four Omni-1 Trade startup terminals; all four inventories are identical and share this template. Capture: 20260613-185338
-- MappingConfidence: High
-- Justification: target has complete captured inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (46530563, 710, 562, 8, 413, 0, 0, 0, 1, '', 99555, 'OTRDLME');

COMMIT;
