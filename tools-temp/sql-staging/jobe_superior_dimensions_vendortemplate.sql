-- ============================================================
-- Jobe Superior dimensions vendortemplate staged SQL
-- Source capture: AOSharp 20260614-002319
-- Targets: 4565 Hardware Dimension - Superior and 4569 Dimensional Shift - Superior
-- Expected coverage: 93 -> 89 (4 reduction)
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT VALIDATION
-- Uses INSERT only; no non-insert DML or schema changes.
-- ============================================================
START TRANSACTION;

-- Vendortemplate rows: 4
-- Hash rules: vendortemplate JS + 5 Base32(SHA1); collision-safe window.

-- Terminal: Superior Armor
-- NormalizedName: JobeHardwareSuperiorArmor; TemplateId: 151973; ShopHash: QQIB (new); Inventory rows: 29; Capture identity: (VendingMachine:12E4EE36); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JSH6XAZ', 1, 'JobeHardwareSuperiorArmor', 151973, 'QQIB', 71, 122);

-- Terminal: Superior Equipment for Nano Specialists
-- NormalizedName: JobeHardwareSuperiorNanoSpecialistEquipment; TemplateId: 224079; ShopHash: 4RVR (new); Inventory rows: 32; Capture identity: (VendingMachine:12E4EE39); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JSCKWXE', 1, 'JobeHardwareSuperiorNanoSpecialistEquipment', 224079, '4RVR', 1, 236);

-- Terminal: Costly Regenerative Supplies --- 100-175
-- NormalizedName: JobeDimensionalSuperiorRegenerativeSupplies; TemplateId: 220330; ShopHash: 7QSZ (new); Inventory rows: 18; Capture identity: (VendingMachine:12E5237A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JSNNKW2', 1, 'JobeDimensionalSuperiorRegenerativeSupplies', 220330, '7QSZ', 100, 175);

-- Terminal: Superior Implants
-- NormalizedName: JobeDimensionalSuperiorImplants; TemplateId: 155224; ShopHash: FZZ2 (new); Inventory rows: 37; Capture identity: (VendingMachine:12E5237B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('JSX36TD', 1, 'JobeDimensionalSuperiorImplants', 155224, 'FZZ2', 70, 125);

COMMIT;
