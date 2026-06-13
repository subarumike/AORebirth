-- ============================================================
-- Spec SMarket inferred staged vendortemplate inserts
-- Target playfields: 1189 spec_smarket_clan_advanced; 1190 spec_smarket_clan_sup; 1191 spec_smarket_omni_advanced; 1192 spec_smarket_omni_sup
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source captures: 20260613-012810; 20260613-014033
-- Generated from operator-approved inferred reuse of Neutral Basic/Specialty shop inventories.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate SP + 5 Base32(SHA1) chars; collision-safe window.
-- Shop inventory hashes are reused only; no shopinventorytemplates rows are generated.
-- ============================================================
START TRANSACTION;

-- Terminal: Clan Toys and Curiosities
-- MappingType: Inferred
-- Source: NeutralBasic ToysAndCuriosities
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: ClanToysAndCuriosities; TemplateId: 99537; ShopHash: PX4X (reused); Inventory rows: 3; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPJRSDG', 1, 'ClanToysAndCuriosities', 99537, 'PX4X', 1, 1);

-- Terminal: Clan Advanced Cars
-- MappingType: Inferred
-- Source: NeutralBasic AdvancedCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: ClanAdvancedCars; TemplateId: 99541; ShopHash: 7ATH (reused); Inventory rows: 2; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPP7AAL', 1, 'ClanAdvancedCars', 99541, '7ATH', 66, 75);

-- Terminal: Clan Computers
-- MappingType: Inferred
-- Source: NeutralBasic Computers
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: ClanComputers; TemplateId: 99539; ShopHash: I3E4 (reused); Inventory rows: 18; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SP4ZZBC', 1, 'ClanComputers', 99539, 'I3E4', 1, 6);

-- Terminal: Clan Furniture
-- MappingType: Inferred
-- Source: NeutralBasic Furniture
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: ClanFurniture; TemplateId: 120511; ShopHash: 7X7Q (reused); Inventory rows: 16; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPZMWD6', 1, 'ClanFurniture', 120511, '7X7Q', 1, 1);

-- Terminal: Clan Specialist Commerce
-- MappingType: Inferred
-- Source: NeutralBasic SpecialistCommerce
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: ClanSpecialistCommerce; TemplateId: 99538; ShopHash: FBQ3 (reused); Inventory rows: 4; MappingConfidence: High; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPPJAN4', 1, 'ClanSpecialistCommerce', 99538, 'FBQ3', 1, 40);

-- Terminal: Clan Superior Cars
-- MappingType: Inferred
-- Source: NeutralBasic SuperiorCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: ClanSuperiorCars; TemplateId: 99542; ShopHash: FLEW (reused); Inventory rows: 21; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPRXBKP', 1, 'ClanSuperiorCars', 99542, 'FLEW', 81, 200);

-- Terminal: OT Advanced Cars
-- MappingType: Inferred
-- Source: NeutralBasic AdvancedCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: OTAdvancedCars; TemplateId: 99535; ShopHash: 7ATH (reused); Inventory rows: 2; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPG4ODZ', 1, 'OTAdvancedCars', 99535, '7ATH', 66, 75);

-- Terminal: OT Computers
-- MappingType: Inferred
-- Source: NeutralBasic Computers
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: OTComputers; TemplateId: 99500; ShopHash: I3E4 (reused); Inventory rows: 18; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPYDRXU', 1, 'OTComputers', 99500, 'I3E4', 1, 6);

-- Terminal: OT Toys and Curiosities
-- MappingType: Inferred
-- Source: NeutralBasic ToysAndCuriosities
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: OTToysAndCuriosities; TemplateId: 99498; ShopHash: PX4X (reused); Inventory rows: 3; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPW2SA3', 1, 'OTToysAndCuriosities', 99498, 'PX4X', 1, 1);

-- Terminal: OT Furniture
-- MappingType: Inferred
-- Source: NeutralBasic Furniture
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: OTFurniture; TemplateId: 120510; ShopHash: 7X7Q (reused); Inventory rows: 16; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPWZDJ2', 1, 'OTFurniture', 120510, '7X7Q', 1, 1);

-- Terminal: OT Superior Cars
-- MappingType: Inferred
-- Source: NeutralBasic SuperiorCars
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: OTSuperiorCars; TemplateId: 99536; ShopHash: FLEW (reused); Inventory rows: 21; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPTDHFQ', 1, 'OTSuperiorCars', 99536, 'FLEW', 81, 200);

-- Terminal: OT Specialist Commerce
-- MappingType: Inferred
-- Source: NeutralBasic SpecialistCommerce
-- Capture: 20260613-012810 / 20260613-014033
-- Reason: access-restricted / non-capturable
-- NormalizedName: OTSpecialistCommerce; TemplateId: 99499; ShopHash: FBQ3 (reused); Inventory rows: 4; MappingConfidence: Medium; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('SPW7WAJ', 1, 'OTSpecialistCommerce', 99499, 'FBQ3', 1, 40);

COMMIT;
