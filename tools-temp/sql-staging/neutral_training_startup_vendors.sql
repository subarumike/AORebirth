-- ============================================================
-- Neutral Training startup vendors staged SQL
-- Source capture: AOSharp 20260614-002319
-- Target: 954 Neutral Training startup equipment
-- Expected coverage: 29 -> 27 (2 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- VendorId: 62521344; Playfield: 954 Neutral Training; TemplateId: 99643 Basic Startup Equipment; Statel: 0xC00003BA; Capture identity: (VendingMachine:12E4B870); Capture: 20260614-002319
-- MappingType: Captured
-- Evidence: VendorFull + ShopUpdate direct identity match
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (62521344, 954, 227.013, 6, 83.073, 0, 0, 0, 1, '', 99643, 'NT37J3W');

-- VendorId: 62521345; Playfield: 954 Neutral Training; TemplateId: 99643 Basic Startup Equipment; Statel: 0xC00103BA; Capture identity: (VendingMachine:12E4B871); Capture: 20260614-002319
-- MappingType: Captured
-- Evidence: VendorFull + ShopUpdate direct identity match
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (62521345, 954, 150.322, 3.472, 47.923, 0, 0, 0, 1, '', 99643, 'NT37J3W');

COMMIT;
