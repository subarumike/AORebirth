-- ============================================================
-- Jobe Advanced dimensions vendors staged SQL
-- Source capture: AOSharp 20260614-002319
-- Targets: 4564 Hardware Dimension - Advanced and 4568 Dimensional Shift - Advanced
-- Expected coverage: 96 -> 93 (3 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendor rows: 3; MappingType: Captured for every row.

-- VendorId: 299106305; Playfield: 4564 Hardware Dimension - Advanced; TemplateId: 99571 Advanced Armor; Statel: 0xC00111D4; Capture identity: (VendingMachine:12E5137B); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Advanced dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 33.65 Y 2.1 Z 41.5; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299106305, 4564, 33.65, 2.1, 41.5, 0, 0, 0, 1, '', 99571, 'JARLAWF');

-- VendorId: 299368449; Playfield: 4568 Dimensional Shift - Advanced; TemplateId: 220329 Costly Regenerative Supplies --- 1-90; Statel: 0xC00111D8; Capture identity: (VendingMachine:12E53D47); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Advanced dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 28.1 Y 2.1 Z 40.1; vendortemplate links to existing exact inventory hash HMIZ.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299368449, 4568, 28.1, 2.1, 40.1, 0, 0, 0, 1, '', 220329, 'JASODU6');

-- VendorId: 299368450; Playfield: 4568 Dimensional Shift - Advanced; TemplateId: 155223 Advanced Implants; Statel: 0xC00211D8; Capture identity: (VendingMachine:12E53D48); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Advanced dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 46.9 Y 2.1 Z 29.2; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299368450, 4568, 46.9, 2.1, 29.2, 0, 0, 0, 1, '', 155223, 'JAVMXFQ');

COMMIT;
