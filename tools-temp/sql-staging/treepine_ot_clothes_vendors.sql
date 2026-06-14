-- ============================================================
-- Treepine Hut OT Clothes staged SQL
-- Source capture: AOSharp 20260613-233535
-- Target: 1887 Treepine Hut OT Clothes
-- Expected coverage: 105 -> 104 (1 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- VendorId: 123666433; Playfield: 1887 Treepine Hut; TemplateId: 99490 OT Clothes; Statel: 0xC001075F; Capture identity: (VendingMachine:12E522FB); Capture: 20260613-233535
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Treepine Hut AOSharp capture. Capture: 20260613-233535
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 199.189 Y 5.000 Z 286.698; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (123666433, 1887, 199.189, 5, 286.698, 0, 0, 0, 1, '', 99490, 'TPNFK3D');

COMMIT;
