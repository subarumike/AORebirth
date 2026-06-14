-- ============================================================
-- Omni Training Startup Shop staged SQL
-- Source capture: AOSharp 20260613-231115
-- Target: 950 Omni Training Startup Shop!
-- Expected coverage: 106 -> 105 (1 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- VendorId: 62259200; Playfield: 950 Omni Training; TemplateId: 100035 Startup Shop!; Statel: 0xC00003B6; Capture identity: (VendingMachine:12E530CC); Capture: 20260613-231115
-- MappingType: Captured
-- MappingSource: VendorFull captured after Omni Training playfield entry; ShopUpdate captured by opening Startup Shop!. Capture: 20260613-231115
-- MappingConfidence: High
-- Justification: target metadata maps template 100035 to statel 0xC00003B6 at X 60 Y 14 Z 50; capture has matching template, position, and seven-row inventory.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (62259200, 950, 60, 14, 50, 0, 0, 0, 1, '', 100035, 'OTY56RU');

COMMIT;
