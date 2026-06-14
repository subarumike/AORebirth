-- ============================================================
-- Jobe Superior dimensions vendors staged SQL
-- Source capture: AOSharp 20260614-002319
-- Targets: 4565 Hardware Dimension - Superior and 4569 Dimensional Shift - Superior
-- Expected coverage: 93 -> 89 (4 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendor rows: 4; MappingType: Captured for every row.

-- VendorId: 299171841; Playfield: 4565 Hardware Dimension - Superior; TemplateId: 151973 Superior Armor; Statel: 0xC00111D5; Capture identity: (VendingMachine:12E4EE36); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Superior dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 33.65 Y 2.1 Z 41.5; vendortemplate links to new exact inventory group QQIB.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299171841, 4565, 33.65, 2.1, 41.5, 0, 0, 0, 1, '', 151973, 'JSH6XAZ');

-- VendorId: 299171844; Playfield: 4565 Hardware Dimension - Superior; TemplateId: 224079 Superior Equipment for Nano Specialists; Statel: 0xC00411D5; Capture identity: (VendingMachine:12E4EE39); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Superior dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 33 Y 2.1 Z 29.4; vendortemplate links to new exact inventory group 4RVR.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299171844, 4565, 33, 2.1, 29.4, 0, 0, 0, 1, '', 224079, 'JSCKWXE');

-- VendorId: 299433985; Playfield: 4569 Dimensional Shift - Superior; TemplateId: 220330 Costly Regenerative Supplies --- 100-175; Statel: 0xC00111D9; Capture identity: (VendingMachine:12E5237A); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Superior dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 28.1 Y 2.1 Z 40.1; vendortemplate links to new exact inventory group 7QSZ.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299433985, 4569, 28.1, 2.1, 40.1, 0, 0, 0, 1, '', 220330, 'JSNNKW2');

-- VendorId: 299433986; Playfield: 4569 Dimensional Shift - Superior; TemplateId: 155224 Superior Implants; Statel: 0xC00211D9; Capture identity: (VendingMachine:12E5237B); Capture: 20260614-002319
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Jobe Superior dimensions AOSharp capture. Capture: 20260614-002319
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 46.9 Y 2.1 Z 29.2; vendortemplate links to new exact inventory group FZZ2.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299433986, 4569, 46.9, 2.1, 29.2, 0, 0, 0, 1, '', 155224, 'JSX36TD');

COMMIT;
