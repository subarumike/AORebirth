-- Review-only observed live loot seed.
-- Do not run this file directly without checking sample sizes and rates.
-- Generated from passive loot observation CSVs; it does not represent complete live drop tables.

-- Surf Lizard (A000), observed bodies: 7
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00000", 85517, 85517, 1, 1, 0); -- Battered Flak Armor Pants observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00001", 85746, 85746, 1, 1, 0); -- Battered Flak Armor Sleeves observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00002", 85714, 85714, 1, 1, 0); -- Battered Light Combat Body Armor observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00003", 70563, 70563, 1, 1, 0); -- Battered Low-Tech Armor Helmet observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00004", 70560, 70560, 1, 1, 0); -- Battered Low-Tech Female Body Armor observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00005", 42640, 42640, 1, 1, 0); -- Monster Parts observed on 2 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00006", 259987, 259987, 1, 1, 0); -- Yellow Corundum observed on 4 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA00000,OBSA00001,OBSA00002,OBSA00003,OBSA00004,OBSA00005,OBSA00006", `DropSlots`="0,1,2,3,4,5,6", `DropRates`="1429,1429,1429,1429,1429,2857,5714" WHERE `Hash`="A000";

-- Island Reet (A001), observed bodies: 11
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00100", 42640, 42640, 1, 1, 0); -- Monster Parts observed on 8 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00101", 259990, 259990, 1, 1, 0); -- Perfectly Formed Seashell observed on 4 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00102", 248328, 248328, 1, 1, 0); -- Reet Beak observed on 3 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA00100,OBSA00101,OBSA00102", `DropSlots`="0,1,2", `DropRates`="7273,3636,2727" WHERE `Hash`="A001";

-- Shore Snake (A003), observed bodies: 1
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00300", 85714, 85714, 1, 1, 0); -- Battered Light Combat Body Armor observed on 1 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA00300", `DropSlots`="0", `DropRates`="10000" WHERE `Hash`="A003";

-- Beach Leet (A004), observed bodies: 8
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00400", 70562, 70562, 1, 1, 0); -- Battered Low-Tech Armor Gloves observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00401", 42640, 42640, 1, 1, 0); -- Monster Parts observed on 4 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00402", 259990, 259990, 1, 1, 0); -- Perfectly Formed Seashell observed on 2 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA00403", 248323, 248323, 1, 1, 0); -- Spinal Section observed on 4 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA00400,OBSA00401,OBSA00402,OBSA00403", `DropSlots`="0,1,2,3", `DropRates`="1250,5000,2500,5000" WHERE `Hash`="A004";

-- Tropical Stalker (A033), observed bodies: 3
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03300", 85746, 27391, 5, 5, 0); -- Battered Flak Armor Sleeves observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03301", 70560, 85688, 4, 4, 0); -- Battered Low-Tech Female Body Armor observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03302", 259989, 259989, 1, 1, 0); -- Blue Corundum observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03303", 42640, 42641, 4, 4, 0); -- Monster Parts observed on 1 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA03300,OBSA03301,OBSA03302,OBSA03303", `DropSlots`="0,1,2,3", `DropRates`="3333,3333,3333,3333" WHERE `Hash`="A033";

-- Reef Salamander (A034), observed bodies: 2
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03400", 85693, 27389, 3, 3, 0); -- Battered Flak Body Armor observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03401", 70563, 85558, 5, 5, 0); -- Battered Low-Tech Armor Helmet observed on 1 bodies
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03402", 42640, 42641, 4, 4, 0); -- Monster Parts observed on 1 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA03400,OBSA03401,OBSA03402", `DropSlots`="0,1,2", `DropRates`="5000,5000,5000" WHERE `Hash`="A034";

-- Cliff Malle (A035), observed bodies: 1
INSERT INTO `mobdroptable` (`Hash`,`LowID`,`HighID`,`MinQL`,`MaxQL`,`RangeCheck`) VALUES ("OBSA03500", 85533, 85532, 4, 4, 0); -- Battered Light Combat Armor Pants observed on 1 bodies
-- UPDATE `mobtemplate` SET `DropHashes`="OBSA03500", `DropSlots`="0", `DropRates`="10000" WHERE `Hash`="A035";
