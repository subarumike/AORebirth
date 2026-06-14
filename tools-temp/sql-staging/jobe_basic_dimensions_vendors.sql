-- ============================================================
-- Jobe Basic dimensions vendors staged SQL
-- Source capture: AOSharp 20260614-000058
-- Targets: 4563 Hardware Dimension - Basic and 4567 Dimensional Shift - Basic
-- Expected coverage: 99 -> 96 (3 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendor rows: 3; MappingType: Captured for every row.
-- Note: identical template/coordinate rows in other Jobe tiers are not imported here without direct capture or explicit inference approval.

-- VendorId: 299040769; Playfield: 4563 Hardware Dimension - Basic; TemplateId: 99570 Basic Armor; Statel: 0xC00111D3; Capture identity: (VendingMachine:12E48221); Capture: 20260614-000058
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Basic dimensions AOSharp capture. Capture: 20260614-000058
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 33.65 Y 2.1 Z 41.5; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299040769, 4563, 33.65, 2.1, 41.5, 0, 0, 0, 1, '', 99570, 'JB5P4OH');

-- VendorId: 299302913; Playfield: 4567 Dimensional Shift - Basic; TemplateId: 220329 Costly Regenerative Supplies --- 1-90; Statel: 0xC00111D7; Capture identity: (VendingMachine:12E5134A); Capture: 20260614-000058
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Basic dimensions AOSharp capture. Capture: 20260614-000058
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 28.1 Y 2.1 Z 40.1; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299302913, 4567, 28.1, 2.1, 40.1, 0, 0, 0, 1, '', 220329, 'JBZYZLQ');

-- VendorId: 299302914; Playfield: 4567 Dimensional Shift - Basic; TemplateId: 155222 Basic Implants; Statel: 0xC00211D7; Capture identity: (VendingMachine:12E5134B); Capture: 20260614-000058
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Basic dimensions AOSharp capture. Capture: 20260614-000058
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 46.9 Y 2.1 Z 29.2; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299302914, 4567, 46.9, 2.1, 29.2, 0, 0, 0, 1, '', 155222, 'JBMTLP6');

COMMIT;
