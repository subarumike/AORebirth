-- ============================================================
-- Spec SMarket inferred staged vendors inserts
-- Target playfields: 1189 spec_smarket_clan_advanced; 1190 spec_smarket_clan_sup; 1191 spec_smarket_omni_advanced; 1192 spec_smarket_omni_sup
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source captures: 20260613-012810; 20260613-014033
-- Generated from operator-approved inferred reuse of Neutral Basic/Specialty shop inventories.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Coordinates are statel coordinates from vendor-scan-targets.csv.
-- ============================================================
START TRANSACTION;

-- Terminal: Clan Toys and Curiosities; TemplateId: 99537; VendorId: 77922304; Statel: 0xC00004A5
-- MappingType: Inferred
-- Source: NeutralBasic ToysAndCuriosities
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77922304, 1189, 176.5, 5, 133.104, 0, 0, 0, 1, '', 99537, 'SPJRSDG');

-- Terminal: Clan Advanced Cars; TemplateId: 99541; VendorId: 77922305; Statel: 0xC00104A5
-- MappingType: Inferred
-- Source: NeutralBasic AdvancedCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77922305, 1189, 182.532, 5, 133, 0, 0, 0, 1, '', 99541, 'SPP7AAL');

-- Terminal: Clan Computers; TemplateId: 99539; VendorId: 77922306; Statel: 0xC00204A5
-- MappingType: Inferred
-- Source: NeutralBasic Computers
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77922306, 1189, 179.5, 5, 133, 0, 0, 0, 1, '', 99539, 'SP4ZZBC');

-- Terminal: Clan Furniture; TemplateId: 120511; VendorId: 77922307; Statel: 0xC00304A5
-- MappingType: Inferred
-- Source: NeutralBasic Furniture
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77922307, 1189, 185.5, 5, 133, 0, 0, 0, 1, '', 120511, 'SPZMWD6');

-- Terminal: Clan Specialist Commerce; TemplateId: 99538; VendorId: 77987840; Statel: 0xC00004A6
-- MappingType: Inferred
-- Source: NeutralBasic SpecialistCommerce
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77987840, 1190, 176, 5, 133, 0, 0, 0, 1, '', 99538, 'SPPJAN4');

-- Terminal: Clan Superior Cars; TemplateId: 99542; VendorId: 77987841; Statel: 0xC00104A6
-- MappingType: Inferred
-- Source: NeutralBasic SuperiorCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77987841, 1190, 173, 5, 127, 0, 0, 0, 1, '', 99542, 'SPRXBKP');

-- Terminal: Clan Computers; TemplateId: 99539; VendorId: 77987842; Statel: 0xC00204A6
-- MappingType: Inferred
-- Source: NeutralBasic Computers
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77987842, 1190, 185, 5, 127, 0, 0, 0, 1, '', 99539, 'SP4ZZBC');

-- Terminal: Clan Furniture; TemplateId: 120511; VendorId: 77987843; Statel: 0xC00304A6
-- MappingType: Inferred
-- Source: NeutralBasic Furniture
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77987843, 1190, 182, 5, 133, 0, 0, 0, 1, '', 120511, 'SPZMWD6');

-- Terminal: OT Advanced Cars; TemplateId: 99535; VendorId: 78053376; Statel: 0xC00004A7
-- MappingType: Inferred
-- Source: NeutralBasic AdvancedCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78053376, 1191, 168, 5, 143, 0, 0, 0, 1, '', 99535, 'SPG4ODZ');

-- Terminal: OT Computers; TemplateId: 99500; VendorId: 78053377; Statel: 0xC00104A7
-- MappingType: Inferred
-- Source: NeutralBasic Computers
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78053377, 1191, 174, 5, 143, 0, 0, 0, 1, '', 99500, 'SPYDRXU');

-- Terminal: OT Toys and Curiosities; TemplateId: 99498; VendorId: 78053378; Statel: 0xC00204A7
-- MappingType: Inferred
-- Source: NeutralBasic ToysAndCuriosities
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78053378, 1191, 171, 5, 143, 0, 0, 0, 1, '', 99498, 'SPW2SA3');

-- Terminal: OT Furniture; TemplateId: 120510; VendorId: 78053379; Statel: 0xC00304A7
-- MappingType: Inferred
-- Source: NeutralBasic Furniture
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78053379, 1191, 171, 5, 139.7, 0, 0, 0, 1, '', 120510, 'SPWZDJ2');

-- Terminal: OT Superior Cars; TemplateId: 99536; VendorId: 78118912; Statel: 0xC00004A8
-- MappingType: Inferred
-- Source: NeutralBasic SuperiorCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78118912, 1192, 166.3, 5, 146.7, 0, 0, 0, 1, '', 99536, 'SPTDHFQ');

-- Terminal: OT Specialist Commerce; TemplateId: 99499; VendorId: 78118913; Statel: 0xC00104A8
-- MappingType: Inferred
-- Source: NeutralBasic SpecialistCommerce
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78118913, 1192, 163, 5, 141, 0, 0, 0, 1, '', 99499, 'SPW7WAJ');

-- Terminal: OT Computers; TemplateId: 99500; VendorId: 78118914; Statel: 0xC00204A8
-- MappingType: Inferred
-- Source: NeutralBasic Computers
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78118914, 1192, 175, 5, 141, 0, 0, 0, 1, '', 99500, 'SPYDRXU');

-- Terminal: OT Furniture; TemplateId: 120510; VendorId: 78118915; Statel: 0xC00304A8
-- MappingType: Inferred
-- Source: NeutralBasic Furniture
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78118915, 1192, 171.7, 5, 146.7, 0, 0, 0, 1, '', 120510, 'SPWZDJ2');

COMMIT;
