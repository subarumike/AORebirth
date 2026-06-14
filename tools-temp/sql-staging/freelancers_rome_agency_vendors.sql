-- Freelancers Inc. HQ - Rome Agency Shop import
-- Source: AOSharp capture 20260614-022639
-- Coverage: 27 -> 26 (1 reduction)
-- New inventory groups: 1

START TRANSACTION;

-- Vendor rows: 1; MappingType: Captured.

-- VendorId: 459472896; Playfield: 7011 Freelancers Inc. HQ - Rome; TemplateId: 285348 Agency Shop; Statel: 0xC0001B63; Capture identity: (VendingMachine:C0001B63); Capture: 20260614-022639
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from Freelancers Inc. HQ - Rome AOSharp capture. Capture: 20260614-022639
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence at X 93.972 Y 2.01 Z 73.734; vendortemplate links to new exact inventory group KNJM.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (459472896, 7011, 93.972, 2.01, 73.734, 0, 0, 0, 1, '', 285348, 'FRGYZBG');

COMMIT;
