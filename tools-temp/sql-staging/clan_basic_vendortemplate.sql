-- ============================================================
-- Clan Basic staged vendortemplate inserts
-- Target playfield: 1180 ord_smarket_clan_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- Source capture: 20260612-225855
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- ============================================================
START TRANSACTION;

-- Terminal: Clan Basic Armor
-- NormalizedName: ClanBasicArmor; TemplateId: 99502; VendorId: 77332493; ShopHash: RZ3U (new); Inventory rows: 29; Capture identity: (VendingMachine:12E47537); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBRE2SH', 1, 'ClanBasicArmor', 99502, 'RZ3U', 4, 48);

-- Terminal: Clan Basic Attacks
-- NormalizedName: ClanBasicAttacks; TemplateId: 99532; VendorId: 77332494; ShopHash: LHXL (new); Inventory rows: 7; Capture identity: (VendingMachine:12E47538); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CB63J4Z', 1, 'ClanBasicAttacks', 99532, 'LHXL', 12, 100);

-- Terminal: Clan Basic Augmentations
-- NormalizedName: ClanBasicAugmentations; TemplateId: 99513; VendorId: 77332495; ShopHash: CC4L (new); Inventory rows: 70; Capture identity: (VendingMachine:12E47539); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBSRZ3W', 1, 'ClanBasicAugmentations', 99513, 'CC4L', 1, 20);

-- Terminal: Clan Basic Medical Supplies
-- NormalizedName: ClanBasicMedicalSupplies; TemplateId: 99508; VendorId: 77332496; ShopHash: G4XZ (reused); Inventory rows: 40; Capture identity: (VendingMachine:12E4753A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBUMBXD', 1, 'ClanBasicMedicalSupplies', 99508, 'G4XZ', 1, 20);

-- Terminal: Clan Basic Tools
-- NormalizedName: ClanBasicTools; TemplateId: 99527; VendorId: 77332497; ShopHash: PN3R (new); Inventory rows: 19; Capture identity: (VendingMachine:12E4753B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBIGA24', 1, 'ClanBasicTools', 99527, 'PN3R', 1, 50);

-- Terminal: Clan Basic Weapons
-- NormalizedName: ClanBasicWeapons; TemplateId: 99505; VendorId: 77332498; ShopHash: EH5X (new); Inventory rows: 88; Capture identity: (VendingMachine:12E4753C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBIEXSV', 1, 'ClanBasicWeapons', 99505, 'EH5X', 1, 50);

-- Terminal: Clan Clothes
-- NormalizedName: ClanBasicClothes; TemplateId: 99526; VendorId: 77332499; ShopHash: 35AQ (new); Inventory rows: 24; Capture identity: (VendingMachine:12E4753D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBJY7AT', 1, 'ClanBasicClothes', 99526, '35AQ', 1, 1);

-- Terminal: Clan Maps
-- NormalizedName: ClanBasicMaps; TemplateId: 117749; VendorId: 77332500; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E4753E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBKAVJ6', 1, 'ClanBasicMaps', 117749, 'LJI7', 1, 30);

-- Terminal: Clan Basic Devices
-- NormalizedName: ClanBasicDevices; TemplateId: 155602; VendorId: 77332501; ShopHash: 3XKL (new); Inventory rows: 26; Capture identity: (VendingMachine:12E4753F); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBGXGWQ', 1, 'ClanBasicDevices', 155602, '3XKL', 1, 49);

-- Terminal: Basic Clan Adventurer Specific Implants
-- NormalizedName: BasicClanAdventurerImplants; TemplateId: 162157; VendorId: 77332503; ShopHash: 5M5F (new); Inventory rows: 96; Capture identity: (VendingMachine:12E47541); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBIDRVY', 1, 'BasicClanAdventurerImplants', 162157, '5M5F', 10, 100);

-- Terminal: Basic Clan Agent Specific Implants
-- NormalizedName: BasicClanAgentImplants; TemplateId: 162160; VendorId: 77332504; ShopHash: 7LZ3 (new); Inventory rows: 78; Capture identity: (VendingMachine:12E47542); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBFGTU4', 1, 'BasicClanAgentImplants', 162160, '7LZ3', 10, 100);

-- Terminal: Basic Clan Bureaucrat Specific Implants
-- NormalizedName: BasicClanBureaucratImplants; TemplateId: 162163; VendorId: 77332505; ShopHash: O3KI (new); Inventory rows: 78; Capture identity: (VendingMachine:12E47543); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBCCGCW', 1, 'BasicClanBureaucratImplants', 162163, 'O3KI', 10, 100);

-- Terminal: Basic Clan Doctor Specific Implants
-- NormalizedName: BasicClanDoctorImplants; TemplateId: 162166; VendorId: 77332506; ShopHash: 6YPW (new); Inventory rows: 78; Capture identity: (VendingMachine:12E47544); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBZ3BNX', 1, 'BasicClanDoctorImplants', 162166, '6YPW', 10, 100);

-- Terminal: Basic Clan Enforcer Specific Implants
-- NormalizedName: BasicClanEnforcerImplants; TemplateId: 162169; VendorId: 77332507; ShopHash: RNWW (new); Inventory rows: 96; Capture identity: (VendingMachine:12E47545); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBVTEV5', 1, 'BasicClanEnforcerImplants', 162169, 'RNWW', 10, 100);

-- Terminal: Basic Clan Engineer Specific Implants
-- NormalizedName: BasicClanEngineerImplants; TemplateId: 162172; VendorId: 77332508; ShopHash: RO4Q (new); Inventory rows: 72; Capture identity: (VendingMachine:12E47546); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBHGCGP', 1, 'BasicClanEngineerImplants', 162172, 'RO4Q', 10, 100);

-- Terminal: Basic Clan Fixer Specific Implants
-- NormalizedName: BasicClanFixerImplants; TemplateId: 162175; VendorId: 77332509; ShopHash: SBQ6 (new); Inventory rows: 78; Capture identity: (VendingMachine:12E47547); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBYDGZL', 1, 'BasicClanFixerImplants', 162175, 'SBQ6', 10, 100);

-- Terminal: Basic Clan Martial Artist Specific Implants
-- NormalizedName: BasicClanMartialArtistImplants; TemplateId: 162178; VendorId: 77332510; ShopHash: JWHR (new); Inventory rows: 78; Capture identity: (VendingMachine:12E47548); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBXEBL5', 1, 'BasicClanMartialArtistImplants', 162178, 'JWHR', 10, 100);

-- Terminal: Basic Clan Meta-Physicist Specific Implants
-- NormalizedName: BasicClanMetaPhysicistImplants; TemplateId: 162181; VendorId: 77332511; ShopHash: 5BUX (new); Inventory rows: 78; Capture identity: (VendingMachine:12E47549); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CB2ELTH', 1, 'BasicClanMetaPhysicistImplants', 162181, '5BUX', 10, 100);

-- Terminal: Basic Clan Nanotechnician Specific Implants
-- NormalizedName: BasicClanNanotechnicianImplants; TemplateId: 162184; VendorId: 77332512; ShopHash: A32J (new); Inventory rows: 72; Capture identity: (VendingMachine:12E4754A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBLEZ7U', 1, 'BasicClanNanotechnicianImplants', 162184, 'A32J', 10, 100);

-- Terminal: Basic Clan Soldier Specific Implants
-- NormalizedName: BasicClanSoldierImplants; TemplateId: 162187; VendorId: 77332513; ShopHash: 6MQN (new); Inventory rows: 78; Capture identity: (VendingMachine:12E4754B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBMK5IN', 1, 'BasicClanSoldierImplants', 162187, '6MQN', 10, 100);

-- Terminal: Basic Clan Trader Specific Implants
-- NormalizedName: BasicClanTraderImplants; TemplateId: 162190; VendorId: 77332514; ShopHash: KVVT (new); Inventory rows: 78; Capture identity: (VendingMachine:12E4754C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBDWQH6', 1, 'BasicClanTraderImplants', 162190, 'KVVT', 10, 100);

-- Terminal: Basic Clan Keeper Specific Implants
-- NormalizedName: BasicClanKeeperImplants; TemplateId: 252271; VendorId: 77332515; ShopHash: KV75 (new); Inventory rows: 78; Capture identity: (VendingMachine:12E4754D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBHJMPZ', 1, 'BasicClanKeeperImplants', 252271, 'KV75', 10, 100);

-- Terminal: Basic Implants
-- NormalizedName: BasicImplants; TemplateId: 155222; VendorId: 77332516; ShopHash: 2QDW (new); Inventory rows: 39; Capture identity: (VendingMachine:12E4754E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBOFY25', 1, 'BasicImplants', 155222, '2QDW', 1, 50);

-- Terminal: Basic Melee Weapon Construction Kits
-- NormalizedName: BasicMeleeWeaponConstructionKits; TemplateId: 155233; VendorId: 77332517; ShopHash: AEWP (new); Inventory rows: 39; Capture identity: (VendingMachine:12E4754F); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBMPNFR', 1, 'BasicMeleeWeaponConstructionKits', 155233, 'AEWP', 1, 50);

-- Terminal: Basic Ranged Weapon Construction Kits
-- NormalizedName: BasicRangedWeaponConstructionKits; TemplateId: 155236; VendorId: 77332518; ShopHash: IBLB (new); Inventory rows: 27; Capture identity: (VendingMachine:12E47550); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBUHWW4', 1, 'BasicRangedWeaponConstructionKits', 155236, 'IBLB', 1, 50);

-- Terminal: Melee Weapon Components - Basic
-- NormalizedName: BasicMeleeWeaponComponents; TemplateId: 155296; VendorId: 77332525; ShopHash: CZ6M (new); Inventory rows: 50; Capture identity: (VendingMachine:12E47557); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBF6KVT', 1, 'BasicMeleeWeaponComponents', 155296, 'CZ6M', 1, 50);

-- Terminal: Ranged Weapon Components - Basic
-- NormalizedName: BasicRangedWeaponComponents; TemplateId: 155490; VendorId: 77332526; ShopHash: 2KLE (new); Inventory rows: 119; Capture identity: (VendingMachine:12E47558); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBJNHBE', 1, 'BasicRangedWeaponComponents', 155490, '2KLE', 1, 50);

-- Terminal: Melee Weapon Recipes - Basic
-- NormalizedName: BasicMeleeWeaponRecipes; TemplateId: 155502; VendorId: 77332532; ShopHash: R5R7 (reused); Inventory rows: 21; Capture identity: (VendingMachine:12E4755E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CBGYYPZ', 1, 'BasicMeleeWeaponRecipes', 155502, 'R5R7', 1, 1);

-- Terminal: Ranged Weapon Recipes - Basic
-- NormalizedName: BasicRangedWeaponRecipes; TemplateId: 155505; VendorId: 77332534; ShopHash: HYDQ (reused); Inventory rows: 95; Capture identity: (VendingMachine:12E47560); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CB6HN4F', 1, 'BasicRangedWeaponRecipes', 155505, 'HYDQ', 1, 100);

COMMIT;
