-- Broken Shores + Lush Fields live capture import (staged)
-- Source: AOSharp capture 20260613-215211
-- Coverage: 129 -> 127 (2 reduction)
START TRANSACTION;

-- VendorId: 43581442; Playfield: 665 Broken Shores; TemplateId: 99488 OT Advanced Trade Skills; Statel: 0xC0020299; Capture identity: (VendingMachine:C0020299); Capture: 20260613-215211
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from AOSharp capture. Capture: 20260613-215211
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (43581442, 665, 2313.068, 7.2, 2240.402, 0, 0, 0, 1, '', 99488, 'BSNZBQZ');

-- VendorId: 45547520; Playfield: 695 Lush Fields; TemplateId: 99643 Basic Startup Equipment; Statel: 0xC00002B7; Capture identity: (VendingMachine:C00002B7); Capture: 20260613-215211
-- MappingType: Captured
-- MappingSource: Captured direct VendorFull + ShopUpdate from AOSharp capture. Capture: 20260613-215211
-- MappingConfidence: High
-- Justification: target has complete captured template and inventory evidence; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (45547520, 695, 3277, 7, 2938, 0, 0, 0, 1, '', 99643, 'LFUZCHW');

COMMIT;
