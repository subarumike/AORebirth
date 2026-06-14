-- Holes in the Wall live capture import (staged)
-- Source: AOSharp capture 20260613-221619
-- Coverage: 127 -> 124 (3 reduction: 2 captured statels + 1 exact-template inferred statel)
START TRANSACTION;

-- VendorId: 51838976; Playfield: 791 Holes in the Wall; TemplateId: 99634 Containers; Statel: 0xC0000317; Capture identity: (VendingMachine:C0000317); Capture: 20260613-221619
-- MappingType: Inferred
-- MappingSource: Captured ShopUpdate inventory on exact statel C0000317 plus current-client statel target metadata. Capture: 20260613-221619
-- MappingConfidence: High
-- Justification: live capture proves exact inventory for the statel identity; target metadata supplies the statel template after the crash prevented VendorFull rows; inventory exactly reuses existing Cont hash.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (51838976, 791, 351.576, 0.208, 778.344, 0, 0, 0, 1, '', 99634, 'HW7LIJB');

-- VendorId: 51838977; Playfield: 791 Holes in the Wall; TemplateId: 151974 Superior Weapons; Statel: 0xC0010317; Capture identity: (VendingMachine:C0010317); Capture: 20260613-221619
-- MappingType: Inferred
-- MappingSource: Captured ShopUpdate inventory on exact statel C0010317 plus current-client statel target metadata. Capture: 20260613-221619
-- MappingConfidence: High
-- Justification: live capture proves exact inventory for the statel identity; target metadata supplies the statel template after the crash prevented VendorFull rows; vendortemplate links to a newly staged exact inventory group.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (51838977, 791, 344.669, 0.504, 780.376, 0, 0, 0, 1, '', 151974, 'HWPZYSE');

-- VendorId: 299171842; Playfield: 4565 Hardware Dimenion - Superior; TemplateId: 151974 Superior Weapons; Statel: 0xC00211D5; Source template capture: C0010317; Capture: 20260613-221619
-- MappingType: Inferred
-- MappingSource: Holes in the Wall capture 20260613-221619 / exact TemplateId 151974
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of the newly validated vendortemplate/shop hash; inventory is fixed by the staged shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299171842, 4565, 36.35, 2.1, 41.5, 0, 0, 0, 1, '', 151974, 'HWPZYSE');

COMMIT;
