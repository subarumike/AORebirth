-- ============================================================
-- Uncle Bazzit's Workshop vendors staged SQL
-- Source capture: AOSharp 20260613-184615
-- Target: 4354 Uncle Bazzits Workshop (Dng)
-- Expected coverage: 104 -> 99 (5 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendor rows: 5; MappingType: Captured for every row.

-- VendorId: 285343744; Playfield: 4354 Uncle Bazzits Workshop (Dng); TemplateId: 247744 Maria's Fashion; Statel: 0xC0001102; Capture identity: (VendingMachine:12E4E8EB); Capture: 20260613-184615
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Uncle Bazzit's Workshop AOSharp capture. Capture: 20260613-184615
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 189.677 Y 6.02 Z 146.744; vendortemplate links to existing exact inventory hash Fash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (285343744, 4354, 189.677, 6.02, 146.744, 0, 0, 0, 1, '', 247744, 'UBG2HDY');

-- VendorId: 285343745; Playfield: 4354 Uncle Bazzits Workshop (Dng); TemplateId: 247743 Uncle Bazzit's Miscellany; Statel: 0xC0011102; Capture identity: (VendingMachine:12E4E8EC); Capture: 20260613-184615
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Uncle Bazzit's Workshop AOSharp capture. Capture: 20260613-184615
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 190.031 Y 6.02 Z 144.965; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (285343745, 4354, 190.031, 6.02, 144.965, 0, 0, 0, 1, '', 247743, 'UBQF46P');

-- VendorId: 285343746; Playfield: 4354 Uncle Bazzits Workshop (Dng); TemplateId: 254816 Uncle Bazzit's Floorplans; Statel: 0xC0021102; Capture identity: (VendingMachine:12E4E8ED); Capture: 20260613-184615
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Uncle Bazzit's Workshop AOSharp capture. Capture: 20260613-184615
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 188.385 Y 6.021 Z 143.679; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (285343746, 4354, 188.385, 6.021, 143.679, 0, 0, 0, 1, '', 254816, 'UBMRCLI');

-- VendorId: 285343747; Playfield: 4354 Uncle Bazzits Workshop (Dng); TemplateId: 255998 Uncle Bazzit's Landscaping; Statel: 0xC0031102; Capture identity: (VendingMachine:12E4E8EE); Capture: 20260613-184615
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Uncle Bazzit's Workshop AOSharp capture. Capture: 20260613-184615
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 177.233 Y 6.02 Z 143.604; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (285343747, 4354, 177.233, 6.02, 143.604, 0, 0, 0, 1, '', 255998, 'UBQEFLQ');

-- VendorId: 285343748; Playfield: 4354 Uncle Bazzits Workshop (Dng); TemplateId: 255997 Uncle Bazzit's Furnishings; Statel: 0xC0041102; Capture identity: (VendingMachine:12E4E8EF); Capture: 20260613-184615
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Uncle Bazzit's Workshop AOSharp capture. Capture: 20260613-184615
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 176.295 Y 6.02 Z 145.301; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (285343748, 4354, 176.295, 6.02, 145.301, 0, 0, 0, 1, '', 255997, 'UBNWVTI');

COMMIT;
