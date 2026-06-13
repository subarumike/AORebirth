-- ============================================================
-- Omni Basic implant staged vendortemplate inserts
-- Target playfield: 1183 ord_smarket_omni_basic
-- REVIEW STAGING ONLY - DO NOT IMPORT WITHOUT APPROVAL
-- AOSharp capture: 20260613-005616
-- Generated from AOSharp capture evidence in Cellao-AORebirth.
-- Uses INSERT only; no non-insert DML or existing table edits.
-- Hash rules: vendortemplate OB + 5 Base32(SHA1) chars; shop hashes are reused from existing Clan Basic implant inventories.
-- ============================================================
START TRANSACTION;

-- Terminal: Basic Omni-Tek Adventurer Specific Implants
-- NormalizedName: BasicOmniTekAdventurerImplants; TemplateId: 162158; VendorId: 77529149; ShopHash: 5M5F (reused); Inventory rows: 96; Capture identity: (VendingMachine:12E48CE0); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBTGSV6', 1, 'BasicOmniTekAdventurerImplants', 162158, '5M5F', 10, 100);

-- Terminal: Basic Omni-Tek Agent Specific Implants
-- NormalizedName: BasicOmniTekAgentImplants; TemplateId: 162161; VendorId: 77529151; ShopHash: 7LZ3 (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CE1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBRGJBU', 1, 'BasicOmniTekAgentImplants', 162161, '7LZ3', 10, 100);

-- Terminal: Basic Omni-Tek Bureaucrat Specific Implants
-- NormalizedName: BasicOmniTekBureaucratImplants; TemplateId: 162164; VendorId: 77529153; ShopHash: O3KI (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CE2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBTSB45', 1, 'BasicOmniTekBureaucratImplants', 162164, 'O3KI', 10, 100);

-- Terminal: Basic Omni-Tek Doctor Specific Implants
-- NormalizedName: BasicOmniTekDoctorImplants; TemplateId: 162167; VendorId: 77529155; ShopHash: 6YPW (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CE3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBPBXFK', 1, 'BasicOmniTekDoctorImplants', 162167, '6YPW', 10, 100);

-- Terminal: Basic Omni-Tek Enforcer Specific Implants
-- NormalizedName: BasicOmniTekEnforcerImplants; TemplateId: 162170; VendorId: 77529156; ShopHash: RNWW (reused); Inventory rows: 96; Capture identity: (VendingMachine:12E48CE4); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OB53DT5', 1, 'BasicOmniTekEnforcerImplants', 162170, 'RNWW', 10, 100);

-- Terminal: Basic Omni-Tek Engineer Specific Implants
-- NormalizedName: BasicOmniTekEngineerImplants; TemplateId: 162173; VendorId: 77529157; ShopHash: RO4Q (reused); Inventory rows: 72; Capture identity: (VendingMachine:12E48CE5); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBRJVSX', 1, 'BasicOmniTekEngineerImplants', 162173, 'RO4Q', 10, 100);

-- Terminal: Basic Omni-Tek Fixer Specific Implants
-- NormalizedName: BasicOmniTekFixerImplants; TemplateId: 162176; VendorId: 77529158; ShopHash: SBQ6 (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CE6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBPR5QM', 1, 'BasicOmniTekFixerImplants', 162176, 'SBQ6', 10, 100);

-- Terminal: Basic Omni-Tek Martial Artist Specific Implants
-- NormalizedName: BasicOmniTekMartialArtistImplants; TemplateId: 162179; VendorId: 77529159; ShopHash: JWHR (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CE7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBVDA5Z', 1, 'BasicOmniTekMartialArtistImplants', 162179, 'JWHR', 10, 100);

-- Terminal: Basic Omni-Tek Meta-Physicist Specific Implants
-- NormalizedName: BasicOmniTekMetaPhysicistImplants; TemplateId: 162182; VendorId: 77529160; ShopHash: 5BUX (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CE8); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBJIWHL', 1, 'BasicOmniTekMetaPhysicistImplants', 162182, '5BUX', 10, 100);

-- Terminal: Basic Omni-Tek Nanotechnician Specific Implants
-- NormalizedName: BasicOmniTekNanotechnicianImplants; TemplateId: 162185; VendorId: 77529161; ShopHash: A32J (reused); Inventory rows: 72; Capture identity: (VendingMachine:12E48CE9); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBTYNLZ', 1, 'BasicOmniTekNanotechnicianImplants', 162185, 'A32J', 10, 100);

-- Terminal: Basic Omni-Tek Soldier Specific Implants
-- NormalizedName: BasicOmniTekSoldierImplants; TemplateId: 162188; VendorId: 77529162; ShopHash: 6MQN (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CEA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OB4LOZA', 1, 'BasicOmniTekSoldierImplants', 162188, '6MQN', 10, 100);

-- Terminal: Basic Omni-Tek Trader Specific Implants
-- NormalizedName: BasicOmniTekTraderImplants; TemplateId: 162191; VendorId: 77529163; ShopHash: KVVT (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CEB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBED7EA', 1, 'BasicOmniTekTraderImplants', 162191, 'KVVT', 10, 100);

-- Terminal: Basic Omni-Tek Keeper Specific Implants
-- NormalizedName: BasicOmniTekKeeperImplants; TemplateId: 252270; VendorId: 77529164; ShopHash: KV75 (reused); Inventory rows: 78; Capture identity: (VendingMachine:12E48CEC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OB3DLM5', 1, 'BasicOmniTekKeeperImplants', 252270, 'KV75', 10, 100);

COMMIT;
