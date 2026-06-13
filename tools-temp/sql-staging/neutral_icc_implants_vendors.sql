-- ============================================================
-- Neutral ICC implant/cluster staged SQL
-- Source capture: AOSharp 20260613-170220
-- Captured interior: 2064 neut_basic_implants_shop
-- Exact-template reuse target: 2073 neut_advanced_implants_shop
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- Hash rules: vendortemplate NI + 5 Base32(SHA1); shop hash 4 Base32(SHA1); collision-safe window.
-- ============================================================
START TRANSACTION;

-- VendorId: 135266305; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297396 Basic ICC Implants
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266305, 2064, 186.965, 5.01, 155.058, 0, 0, 0, 1, '', 297396, 'NITV2DU');

-- VendorId: 135856129; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297396 Basic ICC Implants
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856129, 2073, 200.965, 5.01, 199, 0, 0, 0, 1, '', 297396, 'NITV2DU');

-- VendorId: 135266306; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297399 Basic ICC Faded Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266306, 2064, 186.965, 5.01, 159, 0, 0, 0, 1, '', 297399, 'NIIOYTU');

-- VendorId: 135856130; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297399 Basic ICC Faded Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856130, 2073, 200.965, 5.01, 201, 0, 0, 0, 1, '', 297399, 'NIIOYTU');

-- VendorId: 135266307; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297402 Basic ICC Bright Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266307, 2064, 186.965, 5.01, 161, 0, 0, 0, 1, '', 297402, 'NI4JLQO');

-- VendorId: 135856131; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297402 Basic ICC Bright Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856131, 2073, 200.965, 5.01, 203, 0, 0, 0, 1, '', 297402, 'NI4JLQO');

-- VendorId: 135266308; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297405 Basic ICC Shiny Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266308, 2064, 186.965, 5.01, 163, 0, 0, 0, 1, '', 297405, 'NIKTHHA');

-- VendorId: 135856132; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297405 Basic ICC Shiny Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856132, 2073, 200.965, 5.01, 205, 0, 0, 0, 1, '', 297405, 'NIKTHHA');

-- VendorId: 135266310; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297397 Advanced ICC Implants
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266310, 2064, 187, 5.003, 147, 0, 0, 0, 1, '', 297397, 'NIKUGMU');

-- VendorId: 135856134; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297397 Advanced ICC Implants
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856134, 2073, 201, 5.003, 189, 0, 0, 0, 1, '', 297397, 'NIKUGMU');

-- VendorId: 135266311; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297400 Advanced ICC Faded Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266311, 2064, 187, 5.003, 149, 0, 0, 0, 1, '', 297400, 'NIJIZJC');

-- VendorId: 135856135; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297400 Advanced ICC Faded Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856135, 2073, 201, 5.003, 191, 0, 0, 0, 1, '', 297400, 'NIJIZJC');

-- VendorId: 135266312; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297403 Advanced ICC Bright Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266312, 2064, 187, 5.003, 151.021, 0, 0, 0, 1, '', 297403, 'NI2ZU2M');

-- VendorId: 135856136; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297403 Advanced ICC Bright Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856136, 2073, 201, 5.003, 193.021, 0, 0, 0, 1, '', 297403, 'NI2ZU2M');

-- VendorId: 135266313; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297406 Advanced ICC Shiny Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266313, 2064, 187, 5.003, 153, 0, 0, 0, 1, '', 297406, 'NIXARAY');

-- VendorId: 135856137; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297406 Advanced ICC Shiny Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856137, 2073, 201, 5.003, 195, 0, 0, 0, 1, '', 297406, 'NIXARAY');

-- VendorId: 135266315; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297398 Refined ICC Implants
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266315, 2064, 195, 5.003, 151, 0, 0, 0, 1, '', 297398, 'NIGQ25C');

-- VendorId: 135856139; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297398 Refined ICC Implants
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856139, 2073, 209, 5.003, 193, 0, 0, 0, 1, '', 297398, 'NIGQ25C');

-- VendorId: 135266316; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297401 Refined ICC Faded Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266316, 2064, 195, 5.003, 149, 0, 0, 0, 1, '', 297401, 'NICEGHE');

-- VendorId: 135856140; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297401 Refined ICC Faded Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856140, 2073, 209, 5.003, 191, 0, 0, 0, 1, '', 297401, 'NICEGHE');

-- VendorId: 135266317; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297404 Refined ICC Bright Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266317, 2064, 195, 5.003, 147, 0, 0, 0, 1, '', 297404, 'NIVUZHQ');

-- VendorId: 135856141; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297404 Refined ICC Bright Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856141, 2073, 209, 5.003, 189, 0, 0, 0, 1, '', 297404, 'NIVUZHQ');

-- VendorId: 135266318; Playfield: 2064 neut_basic_implants_shop; TemplateId: 297407 Refined ICC Shiny Clusters
-- MappingType: Captured
-- MappingSource: Captured VendorFull + ShopUpdate from 2064 AOSharp capture; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266318, 2064, 195, 5.003, 145, 0, 0, 0, 1, '', 297407, 'NILZLFQ');

-- VendorId: 135856142; Playfield: 2073 neut_advanced_implants_shop; TemplateId: 297407 Refined ICC Shiny Clusters
-- MappingType: Inferred
-- MappingSource: Exact TemplateId reuse from captured 2064 template evidence; same template family already reused by ICC pharmacy anchors across 2064/2073; Capture: 20260613-170220
-- MappingConfidence: High
-- Justification: fixed TemplateId inventory captured once; vendor row binds statel position to validated vendortemplate/shop hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856142, 2073, 209, 5.003, 187, 0, 0, 0, 1, '', 297407, 'NILZLFQ');

COMMIT;
