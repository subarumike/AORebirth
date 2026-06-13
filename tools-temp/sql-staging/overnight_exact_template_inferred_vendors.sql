-- Overnight exact-template inferred vendor import
-- Coverage candidate: 202 -> 171 if all 31 inferred vendor rows verify
-- MappingType: Inferred
-- Rule: vendor-row-only reuse when the target TemplateId has one validated vendortemplate hash and an existing shopinventorytemplates hash; no new templates or inventory rows are introduced.
START TRANSACTION;

-- VendorId: 32768002; Playfield: 500 Parnassos; TemplateId: 99635 Advanced Cars
-- MappingType: Inferred
-- MappingSource: Neutral Basic capture 20260613-012810 / exact TemplateId 99635
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768002, 500, 211.938, 16.4, 162.399, 0, 0, 0, 1, '', 99635, 'NBBBPWA');

-- VendorId: 32768015; Playfield: 500 Parnassos; TemplateId: 99533 Clan Advanced Attacks
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 99533
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768015, 500, 220.751, 16.4, 160.499, 0, 0, 0, 1, '', 99533, 'CAWFVZL');

-- VendorId: 32768016; Playfield: 500 Parnassos; TemplateId: 99517 Clan Advanced Augmentations
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 99517
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768016, 500, 221.824, 16.4, 160.883, 0, 0, 0, 1, '', 99517, 'CAXKPAK');

-- VendorId: 32768018; Playfield: 500 Parnassos; TemplateId: 99509 Clan Advanced Medical Supplies
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 99509
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768018, 500, 224.031, 16.4, 161.614, 0, 0, 0, 1, '', 99509, 'CAKVRD3');

-- VendorId: 32768020; Playfield: 500 Parnassos; TemplateId: 99528 Clan Advanced Tools
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 99528
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768020, 500, 226.065, 16.4, 161.447, 0, 0, 0, 1, '', 99528, 'CA4ANR3');

-- VendorId: 32768022; Playfield: 500 Parnassos; TemplateId: 99506 Clan Advanced Weapons
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 99506
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768022, 500, 225.719, 16.4, 159.308, 0, 0, 0, 1, '', 99506, 'CAIYRLU');

-- VendorId: 32768044; Playfield: 500 Parnassos; TemplateId: 99538 Clan Specialist Commerce
-- MappingType: Inferred
-- MappingSource: Operator-approved NeutralBasic SpecialistCommerce inference from capture 20260613-014033 / exact TemplateId 99538
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768044, 500, 223.588, 16.4, 171.086, 0, 0, 0, 1, '', 99538, 'SPPJAN4');

-- VendorId: 32768045; Playfield: 500 Parnassos; TemplateId: 99504 Clan Superior Armor
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 99504
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768045, 500, 222.449, 16.4, 171.153, 0, 0, 0, 1, '', 99504, 'CSFKCVG');

-- VendorId: 32768046; Playfield: 500 Parnassos; TemplateId: 99534 Clan Superior Attacks
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 99534
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768046, 500, 221.286, 16.4, 171.177, 0, 0, 0, 1, '', 99534, 'CSSD5SY');

-- VendorId: 32768047; Playfield: 500 Parnassos; TemplateId: 99518 Clan Superior Augmentations
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 99518
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768047, 500, 220.105, 16.4, 171.236, 0, 0, 0, 1, '', 99518, 'CSXKWKP');

-- VendorId: 32768049; Playfield: 500 Parnassos; TemplateId: 99529 Clan Superior Medical Supplies
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 99529
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768049, 500, 217.832, 16.4, 171.123, 0, 0, 0, 1, '', 99529, 'CSZKPVY');

-- VendorId: 32768051; Playfield: 500 Parnassos; TemplateId: 99530 Clan Superior Tools
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 99530
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768051, 500, 215.748, 16.4, 171.108, 0, 0, 0, 1, '', 99530, 'CSAUZMP');

-- VendorId: 32768053; Playfield: 500 Parnassos; TemplateId: 99507 Clan Superior Weapons
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 99507
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768053, 500, 215.002, 16.4, 169.185, 0, 0, 0, 1, '', 99507, 'CS5JCOM');

-- VendorId: 32768057; Playfield: 500 Parnassos; TemplateId: 99477 OT Superior Armor
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 99477
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768057, 500, 235.875, 16.4, 178.429, 0, 0, 0, 1, '', 99477, 'OSLC3UI');

-- VendorId: 32768058; Playfield: 500 Parnassos; TemplateId: 99497 OT Superior Attacks
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 99497
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768058, 500, 236.058, 16.4, 179.236, 0, 0, 0, 1, '', 99497, 'OSRA2ZZ');

-- VendorId: 32768059; Playfield: 500 Parnassos; TemplateId: 99486 OT Superior Augmentations
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 99486
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768059, 500, 235.241, 16.4, 175.217, 0, 0, 0, 1, '', 99486, 'OSGQXEO');

-- VendorId: 32768061; Playfield: 500 Parnassos; TemplateId: 99483 OT Superior Medical Supplies
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 99483
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768061, 500, 236.855, 16.4, 181.04, 0, 0, 0, 1, '', 99483, 'OSCP3HJ');

-- VendorId: 32768063; Playfield: 500 Parnassos; TemplateId: 99493 OT Superior Tools
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 99493
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768063, 500, 235.449, 16.4, 176.047, 0, 0, 0, 1, '', 99493, 'OSXOL6H');

-- VendorId: 32768065; Playfield: 500 Parnassos; TemplateId: 99480 OT Superior Weapons
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 99480
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768065, 500, 236.236, 16.4, 180.016, 0, 0, 0, 1, '', 99480, 'OSQC5XR');

-- VendorId: 32768072; Playfield: 500 Parnassos; TemplateId: 155602 Clan Basic Devices
-- MappingType: Inferred
-- MappingSource: Clan Basic capture 20260612-225855 / exact TemplateId 155602
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768072, 500, 547.938, 37.001, 320.321, 0, 0, 0, 1, '', 155602, 'CBGXGWQ');

-- VendorId: 32768073; Playfield: 500 Parnassos; TemplateId: 155605 Clan Advanced Devices
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 155605
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768073, 500, 546.3, 37.001, 320.333, 0, 0, 0, 1, '', 155605, 'CASMUGY');

-- VendorId: 32768074; Playfield: 500 Parnassos; TemplateId: 155608 Clan Superior Devices
-- MappingType: Inferred
-- MappingSource: Clan Superior capture 20260612-232439 / exact TemplateId 155608
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768074, 500, 544.645, 37.001, 320.312, 0, 0, 0, 1, '', 155608, 'CS3Q3IF');

-- VendorId: 32768078; Playfield: 500 Parnassos; TemplateId: 155603 Omni Basic Devices
-- MappingType: Inferred
-- MappingSource: Omni Basic capture 20260612-012644 / exact TemplateId 155603
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768078, 500, 530.567, 37.001, 320.3, 0, 0, 0, 1, '', 155603, 'OBIUAFT');

-- VendorId: 32768079; Playfield: 500 Parnassos; TemplateId: 155606 Omni Advanced Devices
-- MappingType: Inferred
-- MappingSource: Omni Advanced capture 20260613-002828 / exact TemplateId 155606
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768079, 500, 529, 37.001, 320.3, 0, 0, 0, 1, '', 155606, 'OAX2G2O');

-- VendorId: 32768080; Playfield: 500 Parnassos; TemplateId: 155609 Omni Superior Devices
-- MappingType: Inferred
-- MappingSource: Omni Superior capture 20260612-044234 / exact TemplateId 155609
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768080, 500, 527.4, 37.001, 320.344, 0, 0, 0, 1, '', 155609, 'OST6OJS');

-- VendorId: 39321600; Playfield: 600 Varmint Woods; TemplateId: 99479 OT Advanced Weapons
-- MappingType: Inferred
-- MappingSource: Omni Advanced capture 20260613-002828 / exact TemplateId 99479
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (39321600, 600, 3856.8, 20.7, 1934.4, 0, 0, 0, 1, '', 99479, 'OAE5BNV');

-- VendorId: 39321601; Playfield: 600 Varmint Woods; TemplateId: 99482 OT Advanced Medical Supplies
-- MappingType: Inferred
-- MappingSource: Omni Advanced capture 20260613-002828 / exact TemplateId 99482
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (39321601, 600, 3858, 20.727, 1934.4, 0, 0, 0, 1, '', 99482, 'OAW76SU');

-- VendorId: 42926080; Playfield: 655 Andromeda; TemplateId: 151987 Specialist Commerce
-- MappingType: Inferred
-- MappingSource: Neutral Basic capture 20260613-014033 / exact TemplateId 151987
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (42926080, 655, 3253.364, 36.959, 793.912, 0, 0, 0, 1, '', 151987, 'NBCQ762');

-- VendorId: 43581440; Playfield: 665 Broken Shores; TemplateId: 99528 Clan Advanced Tools
-- MappingType: Inferred
-- MappingSource: Clan Advanced capture 20260613-034740 / exact TemplateId 99528
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (43581440, 665, 730.652, 22.01, 1543.597, 0, 0, 0, 1, '', 99528, 'CA4ANR3');

-- VendorId: 43581443; Playfield: 665 Broken Shores; TemplateId: 99527 Clan Basic Tools
-- MappingType: Inferred
-- MappingSource: Clan Basic capture 20260612-225855 / exact TemplateId 99527
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (43581443, 665, 928.852, 49.4, 3713.883, 0, 0, 0, 1, '', 99527, 'CBIGA24');

-- VendorId: 123666432; Playfield: 1887 Treepine Hut; TemplateId: 99386 OT Advanced Armor
-- MappingType: Inferred
-- MappingSource: Omni Advanced capture 20260613-002828 / exact TemplateId 99386
-- MappingConfidence: High
-- Justification: exact TemplateId reuse of a validated vendortemplate/shop hash; inventory is fixed by existing shopinventorytemplates hash; no live-only packet behavior is inferred.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (123666432, 1887, 209.691, 5, 286.698, 0, 0, 0, 1, '', 99386, 'OAL6IVC');

COMMIT;
