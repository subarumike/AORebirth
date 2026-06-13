-- ============================================================
-- Arete ICC staged SQL
-- Source capture: AOSharp 20260613-172753
-- Target: 6553 Arete Landing core ICC vendors only
-- Expected coverage: 147 -> 142 (5 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate AI + 5 Base32(SHA1); shop hash 4 Base32(SHA1); collision-safe window.
-- Excludes incidental nearby Arete vendors captured in the same session.
-- ============================================================
START TRANSACTION;

-- VendorId: 429457412; Playfield: 6553 Arete Landing; TemplateId: 297320 ICC Basic Implants; Statel: 0xC0041999; Capture identity: (VendingMachine:12D1BF1D); Capture: 20260613-172753
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from Arete ICC AOSharp capture; Capture: 20260613-172753
-- MappingConfidence: High
-- Justification: core Arete ICC target has complete template and inventory evidence; incidental nearby vendors intentionally excluded.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457412, 6553, 3425.5, 9.3, 790.5, 0, 0, 0, 1, '', 297320, 'AIYZS6H');

-- VendorId: 429457411; Playfield: 6553 Arete Landing; TemplateId: 297321 ICC Faded Clusters; Statel: 0xC0031999; Capture identity: (VendingMachine:12D1BF1C); Capture: 20260613-172753
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from Arete ICC AOSharp capture; Capture: 20260613-172753
-- MappingConfidence: High
-- Justification: core Arete ICC target has complete template and inventory evidence; incidental nearby vendors intentionally excluded.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457411, 6553, 3427.5, 9.3, 790.5, 0, 0, 0, 1, '', 297321, 'AIBFIDW');

-- VendorId: 429457410; Playfield: 6553 Arete Landing; TemplateId: 297322 ICC Bright Clusters; Statel: 0xC0021999; Capture identity: (VendingMachine:12D1BF1B); Capture: 20260613-172753
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from Arete ICC AOSharp capture; Capture: 20260613-172753
-- MappingConfidence: High
-- Justification: core Arete ICC target has complete template and inventory evidence; incidental nearby vendors intentionally excluded.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457410, 6553, 3429.5, 9.3, 790.5, 0, 0, 0, 1, '', 297322, 'AIGFHES');

-- VendorId: 429457409; Playfield: 6553 Arete Landing; TemplateId: 297323 ICC Shiny Clusters; Statel: 0xC0011999; Capture identity: (VendingMachine:12D1BF1A); Capture: 20260613-172753
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from Arete ICC AOSharp capture; Capture: 20260613-172753
-- MappingConfidence: High
-- Justification: core Arete ICC target has complete template and inventory evidence; incidental nearby vendors intentionally excluded.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457409, 6553, 3431.5, 9.3, 790.5, 0, 0, 0, 1, '', 297323, 'AIZDXJ7');

-- VendorId: 429457408; Playfield: 6553 Arete Landing; TemplateId: 297325 ICC Pharmacy; Statel: 0xC0001999; Capture identity: (VendingMachine:12D1BF19); Capture: 20260613-172753
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from Arete ICC AOSharp capture; Capture: 20260613-172753
-- MappingConfidence: High
-- Justification: core Arete ICC target has complete template and inventory evidence; incidental nearby vendors intentionally excluded.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457408, 6553, 3421, 9.3, 797.5, 0, 0, 0, 1, '', 297325, 'AIYTWIL');

COMMIT;
