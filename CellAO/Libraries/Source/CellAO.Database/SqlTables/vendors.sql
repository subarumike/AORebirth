CREATE TABLE `vendors` (
  `Id` int(32) NOT NULL,
  `Playfield` int(32) NOT NULL DEFAULT '100',
  `X` float NOT NULL DEFAULT '0',
  `Y` float NOT NULL DEFAULT '0',
  `Z` float NOT NULL DEFAULT '0',
  `HeadingX` float NOT NULL DEFAULT '0',
  `HeadingY` float NOT NULL DEFAULT '0',
  `HeadingZ` float NOT NULL DEFAULT '0',
  `HeadingW` float NOT NULL DEFAULT '0',
  `Name` varchar(256) NOT NULL DEFAULT '',
  `TemplateId` int(32) NOT NULL DEFAULT '0',
  `Hash` varchar(7) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1;

--
-- Below is an example set of a few vendors, this must be updated during level design.
--

-- 655 Andromeda safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926081, 655, 0, 0, 0, 0, 0, 0, 1, '', 297290, 'ICCTech');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926082, 655, 0, 0, 0, 0, 0, 0, 1, '', 297423, 'ICCCnt');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926083, 655, 0, 0, 0, 0, 0, 0, 1, '', 297424, 'ICCAccB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926084, 655, 0, 0, 0, 0, 0, 0, 1, '', 297425, 'ICCAccA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926085, 655, 0, 0, 0, 0, 0, 0, 1, '', 297426, 'ICCAccS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926086, 655, 0, 0, 0, 0, 0, 0, 1, '', 297427, 'ICCArmB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926087, 655, 0, 0, 0, 0, 0, 0, 1, '', 297428, 'ICCArmA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926088, 655, 0, 0, 0, 0, 0, 0, 1, '', 297429, 'ICCArmS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926089, 655, 0, 0, 0, 0, 0, 0, 1, '', 297430, 'ICCWepB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926090, 655, 0, 0, 0, 0, 0, 0, 1, '', 297431, 'ICCWepA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926091, 655, 0, 0, 0, 0, 0, 0, 1, '', 297432, 'ICCWepS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926092, 655, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926093, 655, 0, 0, 0, 0, 0, 0, 1, '', 297067, 'ICCGenN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926094, 655, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926095, 655, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(42926096, 655, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');

INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332492, 1180, 197, 5, 203, 0, 0.707108, 0, 0.707106, '', 90562, 'TraCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332491, 1180, 197, 5, 199, 0, 0.707108, 0, 0.707106, '', 90564, 'SolCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332480, 1180, 197, 5.00014, 207, 0, 0.707108, 0, 0.707106, '', 90589, 'AdvCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332481, 1180, 201, 5.00014, 209, 0, 1, 0, -0.00000361999, '', 90588, 'AgeCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332482, 1180, 205, 5.00004, 209, 0, 1, 0, -0.00000361999, '', 90587, 'BurCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332483, 1180, 209, 5.00005, 209, 0, 1, 0, -0.00000361999, '', 90586, 'DocCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332484, 1180, 213, 5.00011, 209, 0, 1, 0, -0.00000361999, '', 90585, 'EnfCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332485, 1180, 217, 5, 207, 0, -0.707103, 0, 0.707111, '', 90579, 'EngCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332486, 1180, 217, 5.00054, 203, 0, -0.707103, 0, 0.707111, '', 90576, 'FixCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332490, 1180, 217, 5, 199, 0, -0.707103, 0, 0.707111, '', 90567, 'NanCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332488, 1180, 217, 5.00021, 195, 0, -0.707103, 0, 0.707111, '', 90571, 'MarCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332489, 1180, 213, 5.0002, 193, 0, 0, 0, 1, '', 90569, 'MetCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77332487, 1180, 209, 5, 193, 0, 0, 0, 1, '', 90574, 'GenCB');

-- 1180 ord_smarket_clan_basic safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332502, 1180, 0, 0, 0, 0, 0, 0, 1, '', 155599, 'BkStB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332522, 1180, 0, 0, 0, 0, 0, 0, 1, '', 155299, 'FNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332523, 1180, 0, 0, 0, 0, 0, 0, 1, '', 155302, 'BNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332524, 1180, 0, 0, 0, 0, 0, 0, 1, '', 155308, 'SNCB');

INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398029, 1181, 177, 5, 167, 0, 0.707108, 0, 0.707106, '', 93043, 'SolCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398028, 1181, 209, 5, 177, 0, 1, 0, -0.00000149012, '', 93041, 'TraCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398027, 1181, 205, 5, 177, 0, 1, 0, -0.00000149012, '', 93043, 'SolCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398026, 1181, 205, 5, 157, 0, 0.00000599027, 0, 1, '', 93044, 'NanCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398025, 1181, 199, 5.0002, 161, 0, 0.707107, 0, 0.707107, '', 93047, 'MetCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398024, 1181, 201, 5.00021, 157, 0, 0.00000599027, 0, 1, '', 93049, 'MarCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398023, 1181, 199, 5, 165, 0, 0.707107, 0, 0.707107, '', 90574, 'GenCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398022, 1181, 209, 5.00054, 157, 0, 0.00000599027, 0, 1, '', 93050, 'FixCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398021, 1181, 213, 5, 157, 0, 0.00000599027, 0, 1, '', 93053, 'EngCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398020, 1181, 215, 5.00011, 161, 0, -0.707104, 0, 0.707109, '', 93055, 'EnfCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398019, 1181, 215, 5.00005, 165, 0, -0.707104, 0, 0.707109, '', 93057, 'DocCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398018, 1181, 215, 5.00004, 169, 0, -0.707104, 0, 0.707109, '', 93058, 'BurCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398017, 1181, 215, 5.00014, 173, 0, -0.707104, 0, 0.707109, '', 93061, 'AgeCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77398016, 1181, 213, 5.00014, 177, 0, 1, 0, -0.00000149012, '', 93062, 'AdvCA');

-- 1181 ord_smarket_clan_advanced safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398038, 1181, 0, 0, 0, 0, 0, 0, 1, '', 155600, 'BkStA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398051, 1181, 0, 0, 0, 0, 0, 0, 1, '', 155300, 'FNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398052, 1181, 0, 0, 0, 0, 0, 0, 1, '', 155303, 'BNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398053, 1181, 0, 0, 0, 0, 0, 0, 1, '', 155309, 'SNCA');

-- 1182 ord_smarket_clan_sup safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463552, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93104, 'AdvCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463553, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93098, 'AgeCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463554, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93102, 'BurCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463555, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93096, 'DocCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463556, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93091, 'EnfCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463557, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93093, 'EngCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463558, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93088, 'FixCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463559, 1182, 0, 0, 0, 0, 0, 0, 1, '', 90574, 'GenCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463560, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93090, 'MarCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463561, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93085, 'MetCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463562, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93086, 'NanCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463563, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93087, 'SolCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463564, 1182, 0, 0, 0, 0, 0, 0, 1, '', 93082, 'TraCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463575, 1182, 0, 0, 0, 0, 0, 0, 1, '', 155301, 'FNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463576, 1182, 0, 0, 0, 0, 0, 0, 1, '', 155307, 'BNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463580, 1182, 0, 0, 0, 0, 0, 0, 1, '', 155310, 'SNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463595, 1182, 0, 0, 0, 0, 0, 0, 1, '', 155601, 'BkStS');

-- 1183 ord_smarket_omni_basic safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529096, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155599, 'BkStB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529098, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90590, 'AdvOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529099, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90580, 'AgeOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529100, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90581, 'BurOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529101, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90582, 'DocOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529102, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90583, 'EnfOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529103, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90577, 'EngOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529104, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90575, 'FixOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529105, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90573, 'GenOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529106, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90570, 'MarOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529107, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90568, 'MetOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529108, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90565, 'NanOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529109, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90563, 'SolOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529110, 1183, 0, 0, 0, 0, 0, 0, 1, '', 90561, 'TraOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529114, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155299, 'FNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529115, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155302, 'BNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529116, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155308, 'SNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529133, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155299, 'FNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529134, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155302, 'BNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529135, 1183, 0, 0, 0, 0, 0, 0, 1, '', 155308, 'SNCB');

-- 1184 ord_smarket_omni_advanced safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594624, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93063, 'AdvOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594625, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93060, 'AgeOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594626, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93059, 'BurOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594627, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93056, 'DocOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594628, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93054, 'EnfOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594629, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93052, 'EngOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594630, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93051, 'FixOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594631, 1184, 0, 0, 0, 0, 0, 0, 1, '', 90573, 'GenOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594632, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93048, 'MarOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594633, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93046, 'MetOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594634, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93045, 'NanOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594635, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93042, 'SolOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594636, 1184, 0, 0, 0, 0, 0, 0, 1, '', 93040, 'TraOA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594643, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155300, 'FNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594644, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155303, 'BNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594645, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155309, 'SNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594653, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155300, 'FNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594654, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155303, 'BNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594655, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155309, 'SNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594689, 1184, 0, 0, 0, 0, 0, 0, 1, '', 99501, 'ContG');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594691, 1184, 0, 0, 0, 0, 0, 0, 1, '', 155600, 'BkStA');

-- 1185 ord_smarket_omni_sup safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660160, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93105, 'AdvOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660161, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93099, 'AgeOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660162, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93095, 'BurOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660163, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93097, 'DocOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660164, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93092, 'EnfOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660165, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93094, 'EngOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660166, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93089, 'FixOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660167, 1185, 0, 0, 0, 0, 0, 0, 1, '', 90573, 'GenOB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660168, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93084, 'MarOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660169, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93079, 'MetOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660170, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93080, 'NanOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660171, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93081, 'SolOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660172, 1185, 0, 0, 0, 0, 0, 0, 1, '', 93083, 'TraOS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660180, 1185, 0, 0, 0, 0, 0, 0, 1, '', 99501, 'ContG');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660182, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155601, 'BkStS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660190, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155301, 'FNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660191, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155307, 'BNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660192, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155310, 'SNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660200, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155301, 'FNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660201, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155307, 'BNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660202, 1185, 0, 0, 0, 0, 0, 0, 1, '', 155310, 'SNCS');

-- 500 Parnassos safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768003, 500, 0, 0, 0, 0, 0, 0, 1, '', 99575, 'MedA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768004, 500, 0, 0, 0, 0, 0, 0, 1, '', 99573, 'ICCSpWA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768006, 500, 0, 0, 0, 0, 0, 0, 1, '', 99602, 'AtkB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768008, 500, 0, 0, 0, 0, 0, 0, 1, '', 99574, 'MedB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768010, 500, 0, 0, 0, 0, 0, 0, 1, '', 99601, 'ToolB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768012, 500, 0, 0, 0, 0, 0, 0, 1, '', 99572, 'ICCSpWB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768019, 500, 0, 0, 0, 0, 0, 0, 1, '', 118285, 'ANCC');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768032, 500, 0, 0, 0, 0, 0, 0, 1, '', 99567, 'ICCEngN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768033, 500, 0, 0, 0, 0, 0, 0, 1, '', 99557, 'ICCEnfN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768034, 500, 0, 0, 0, 0, 0, 0, 1, '', 99559, 'ICCDocN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768035, 500, 0, 0, 0, 0, 0, 0, 1, '', 99558, 'ICCBurN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768036, 500, 0, 0, 0, 0, 0, 0, 1, '', 99556, 'ICCAgeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768037, 500, 0, 0, 0, 0, 0, 0, 1, '', 99560, 'ICCAdvN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768050, 500, 0, 0, 0, 0, 0, 0, 1, '', 118286, 'SNCC');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768062, 500, 0, 0, 0, 0, 0, 0, 1, '', 118283, 'SNCO');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768069, 500, 0, 0, 0, 0, 0, 0, 1, '', 155600, 'BkStA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768070, 500, 0, 0, 0, 0, 0, 0, 1, '', 155599, 'BkStB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768071, 500, 0, 0, 0, 0, 0, 0, 1, '', 155601, 'BkStS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768091, 500, 0, 0, 0, 0, 0, 0, 1, '', 155299, 'FNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768092, 500, 0, 0, 0, 0, 0, 0, 1, '', 155300, 'FNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768093, 500, 0, 0, 0, 0, 0, 0, 1, '', 155301, 'FNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768094, 500, 0, 0, 0, 0, 0, 0, 1, '', 155302, 'BNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768095, 500, 0, 0, 0, 0, 0, 0, 1, '', 155303, 'BNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768096, 500, 0, 0, 0, 0, 0, 0, 1, '', 155307, 'BNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768097, 500, 0, 0, 0, 0, 0, 0, 1, '', 155308, 'SNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768098, 500, 0, 0, 0, 0, 0, 0, 1, '', 155309, 'SNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768099, 500, 0, 0, 0, 0, 0, 0, 1, '', 155310, 'SNCS');

-- 565 Newland Desert safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027840, 565, 0, 0, 0, 0, 0, 0, 1, '', 99574, 'MedB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027842, 565, 0, 0, 0, 0, 0, 0, 1, '', 99602, 'AtkB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027843, 565, 0, 0, 0, 0, 0, 0, 1, '', 99572, 'ICCSpWB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027846, 565, 0, 0, 0, 0, 0, 0, 1, '', 99574, 'MedB');

-- 600 Varmint Woods safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (39321612, 600, 0, 0, 0, 0, 0, 0, 1, '', 93063, 'AdvOA');

-- 2060 neut_basic_weapon_shop safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135004160, 2060, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135004161, 2060, 0, 0, 0, 0, 0, 0, 1, '', 297466, 'ICCMAtk');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135004162, 2060, 0, 0, 0, 0, 0, 0, 1, '', 297470, 'ICCSpWS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135004163, 2060, 0, 0, 0, 0, 0, 0, 1, '', 99572, 'ICCSpWB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135004164, 2060, 0, 0, 0, 0, 0, 0, 1, '', 99573, 'ICCSpWA');

-- 2070 neut_advanced_weapons_shop safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135659520, 2070, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135659521, 2070, 0, 0, 0, 0, 0, 0, 1, '', 297466, 'ICCMAtk');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135659522, 2070, 0, 0, 0, 0, 0, 0, 1, '', 297470, 'ICCSpWS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135659523, 2070, 0, 0, 0, 0, 0, 0, 1, '', 99572, 'ICCSpWB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135659524, 2070, 0, 0, 0, 0, 0, 0, 1, '', 99573, 'ICCSpWA');

-- 2064 neut_basic_implants_shop safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266304, 2064, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266309, 2064, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266314, 2064, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');

-- 2073 neut_advanced_implants_shop safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856128, 2073, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856133, 2073, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856138, 2073, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');

-- 2096 4holes Fashion safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363456, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99554, 'MiiHead');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363457, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99552, 'ICCKeeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363458, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99547, 'MiiSwim');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363459, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99551, 'ICCMetN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363460, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99545, 'MiiLeg');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363461, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99548, 'MiiChst');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363462, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99553, 'ICCFixN');

-- 1136 Mir shop clan Miiir fashion safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448896, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99554, 'MiiHead');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448897, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99544, 'MiiFoot');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448898, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99546, 'MiiHand');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448899, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99545, 'MiiLeg');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448900, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99547, 'MiiSwim');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448901, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99548, 'MiiChst');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448902, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99543, 'MiiArm');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74448903, 1136, 0, 0, 0, 0, 0, 0, 1, '', 99550, 'MiiBack');

-- 1137 mir shop omni Miiir fashion safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514432, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99554, 'MiiHead');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514433, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99544, 'MiiFoot');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514434, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99546, 'MiiHand');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514435, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99545, 'MiiLeg');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514436, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99547, 'MiiSwim');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514437, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99548, 'MiiChst');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514438, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99543, 'MiiArm');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (74514439, 1137, 0, 0, 0, 0, 0, 0, 1, '', 99550, 'MiiBack');

-- 4563 Hardware Dimension - Basic safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299040768, 4563, 0, 0, 0, 0, 0, 0, 1, '', 99601, 'ToolB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299040770, 4563, 0, 0, 0, 0, 0, 0, 1, '', 99572, 'ICCSpWB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299040771, 4563, 0, 0, 0, 0, 0, 0, 1, '', 99602, 'AtkB');

-- 4564 Hardware Dimension - Advanced safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299106304, 4564, 0, 0, 0, 0, 0, 0, 1, '', 152008, 'ToolA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299106306, 4564, 0, 0, 0, 0, 0, 0, 1, '', 99573, 'ICCSpWA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299106307, 4564, 0, 0, 0, 0, 0, 0, 1, '', 151981, 'AtkA');

-- 4565 Hardware Dimension - Superior safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299171840, 4565, 0, 0, 0, 0, 0, 0, 1, '', 152012, 'ToolS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299171843, 4565, 0, 0, 0, 0, 0, 0, 1, '', 151982, 'AtkS');

-- 4567 Dimensional Shift - Basic safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299302912, 4567, 0, 0, 0, 0, 0, 0, 1, '', 155308, 'SNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299302915, 4567, 0, 0, 0, 0, 0, 0, 1, '', 155299, 'FNCB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299302916, 4567, 0, 0, 0, 0, 0, 0, 1, '', 155302, 'BNCB');

-- 4568 Dimensional Shift - Advanced safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299368448, 4568, 0, 0, 0, 0, 0, 0, 1, '', 155309, 'SNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299368451, 4568, 0, 0, 0, 0, 0, 0, 1, '', 155300, 'FNCA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299368452, 4568, 0, 0, 0, 0, 0, 0, 1, '', 155303, 'BNCA');

-- 4569 Dimensional Shift - Superior safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299433984, 4569, 0, 0, 0, 0, 0, 0, 1, '', 155310, 'SNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299433987, 4569, 0, 0, 0, 0, 0, 0, 1, '', 155301, 'FNCS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299433988, 4569, 0, 0, 0, 0, 0, 0, 1, '', 155307, 'BNCS');

-- 4571 Heavenly Business - Basic safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565056, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155604, 'DevB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565057, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155284, 'GToolB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565058, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155290, 'EMTlB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565059, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155287, 'PCTlB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565060, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155508, 'GRecB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565061, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155499, 'GCmpB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565062, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155493, 'MECmpB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565063, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155314, 'PCCmpB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565064, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155496, 'ACCmpB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299565065, 4571, 0, 0, 0, 0, 0, 0, 1, '', 155311, 'NCCmpB');
-- 4572 Heavenly Business - Advanced safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630592, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155607, 'DevA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630593, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155285, 'GToolA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630594, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155291, 'EMTlA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630595, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155288, 'PCTlA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630596, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155509, 'GRecA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630597, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155500, 'GCmpA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630598, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155494, 'MECmpA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630599, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155488, 'PCCmpA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630600, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155497, 'ACCmpA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299630601, 4572, 0, 0, 0, 0, 0, 0, 1, '', 155312, 'NCCmpA');
-- 4573 Heavenly Business - Superior safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696128, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155610, 'DevS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696129, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155286, 'GToolS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696130, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155292, 'EMTlS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696131, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155289, 'PCTlS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696132, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155510, 'GRecS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696133, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155501, 'GCmpS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696134, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155495, 'MECmpS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696135, 4573, 0, 0, 0, 0, 0, 0, 1, '', 155489, 'PCCmpS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696136, 4573, 0, 0, 0, 0, 0, 0, 1, '', 223502, 'JACmpS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299696137, 4573, 0, 0, 0, 0, 0, 0, 1, '', 223505, 'JNCmpS');
-- 6553 Arete Landing safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457414, 6553, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457415, 6553, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457416, 6553, 0, 0, 0, 0, 0, 0, 1, '', 297466, 'ICCMAtk');

-- Live Neutral Supermarket Basic tradeskill room terminals captured 2026-06-08 from current official client.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725711, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297433, 'ICCChB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725712, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297434, 'ICCChA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725713, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297435, 'ICCChS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725714, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297448, 'ICCEngB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725715, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297449, 'ICCEngA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725716, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297450, 'ICCEngS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725717, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297457, 'ICCFash');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725718, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297443, 'ICCOfB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725719, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297444, 'ICCOfA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725720, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297445, 'ICCOfS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725721, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297442, 'ICCSmS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725722, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297441, 'ICCSmA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725723, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297440, 'ICCSmB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725724, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297454, 'ICCWsB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725725, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297455, 'ICCWsA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725726, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297456, 'ICCWsS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725727, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297451, 'ICCArB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725728, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297452, 'ICCArA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77725729, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297453, 'ICCArS');
-- Live Neutral Supermarket Advanced tradeskill room terminals reuse the same current ICC tradeskill stock as Basic.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791232, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297433, 'ICCChB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791233, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297434, 'ICCChA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791234, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297435, 'ICCChS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791235, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297448, 'ICCEngB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791236, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297449, 'ICCEngA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791237, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297450, 'ICCEngS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791238, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297457, 'ICCFash');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791239, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297443, 'ICCOfB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791240, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297444, 'ICCOfA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791241, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297445, 'ICCOfS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791242, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297442, 'ICCSmS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791243, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297441, 'ICCSmA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791244, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297440, 'ICCSmB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791245, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297454, 'ICCWsB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791246, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297455, 'ICCWsA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791247, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297456, 'ICCWsS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791248, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297451, 'ICCArB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791249, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297452, 'ICCArA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77791250, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297453, 'ICCArS');
-- Live Neutral Supermarket Basic nanos room terminals captured 2026-06-08 from current official client.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725696, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297067, 'ICCGenN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725697, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297070, 'ICCTraN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725698, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297069, 'ICCSolN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725699, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297068, 'ICCShaN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725700, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297071, 'ICCNanN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725701, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99551, 'ICCMetN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725702, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99549, 'ICCMarN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725703, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99552, 'ICCKeeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725704, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99553, 'ICCFixN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725705, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99567, 'ICCEngN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725706, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99557, 'ICCEnfN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725707, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99559, 'ICCDocN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725708, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99558, 'ICCBurN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725709, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99556, 'ICCAgeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725710, 1186, 0, 0, 0, 0, 0, 0, 1, '', 99560, 'ICCAdvN');
-- Live Neutral Supermarket Basic implant room terminals matched to the captured 2026-06-08 ICC implant stock.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725746, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302094, 'ICCAdvI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725747, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302095, 'ICCAgeI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725748, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302096, 'ICCBurI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725749, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302101, 'ICCDocI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725750, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302102, 'ICCEnfI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725751, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302103, 'ICCEngI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725752, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302104, 'ICCFixI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725753, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302106, 'ICCKeeI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725754, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302105, 'ICCMarI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725755, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302097, 'ICCMetI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725756, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302098, 'ICCNanI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725757, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302099, 'ICCSolI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725758, 1186, 0, 0, 0, 0, 0, 0, 1, '', 302100, 'ICCTraI');
-- Live Neutral Supermarket Advanced reuses the same current ICC nanos and main-room stock as the captured Basic Fair Trade stock.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791252, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297067, 'ICCGenN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791253, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297070, 'ICCTraN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791254, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297069, 'ICCSolN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791255, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297068, 'ICCShaN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791256, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297071, 'ICCNanN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791257, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99551, 'ICCMetN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791258, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99549, 'ICCMarN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791259, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99552, 'ICCKeeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791260, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99553, 'ICCFixN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791261, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99567, 'ICCEngN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791262, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99557, 'ICCEnfN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791263, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99559, 'ICCDocN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791264, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99558, 'ICCBurN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791265, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99556, 'ICCAgeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791266, 1187, 0, 0, 0, 0, 0, 0, 1, '', 99560, 'ICCAdvN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791267, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297423, 'ICCCnt');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791268, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297290, 'ICCTech');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791269, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297424, 'ICCAccB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791270, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297425, 'ICCAccA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791271, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297426, 'ICCAccS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791272, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297427, 'ICCArmB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791273, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297428, 'ICCArmA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791274, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297429, 'ICCArmS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791275, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297430, 'ICCWepB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791276, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297431, 'ICCWepA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791277, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297432, 'ICCWepS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791278, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791279, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791280, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791281, 1187, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');
-- Live Neutral Supermarket Advanced implant room terminals captured 2026-06-08 from current official client.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791282, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302094, 'ICCAdvI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791283, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302095, 'ICCAgeI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791284, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302096, 'ICCBurI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791285, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302101, 'ICCDocI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791286, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302102, 'ICCEnfI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791287, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302103, 'ICCEngI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791288, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302104, 'ICCFixI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791289, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302106, 'ICCKeeI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791290, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302105, 'ICCMarI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791291, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302097, 'ICCMetI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791292, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302098, 'ICCNanI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791293, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302099, 'ICCSolI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77791294, 1187, 0, 0, 0, 0, 0, 0, 1, '', 302100, 'ICCTraI');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725731, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297423, 'ICCCnt');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725732, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297290, 'ICCTech');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725733, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297424, 'ICCAccB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725734, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297425, 'ICCAccA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725735, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297426, 'ICCAccS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725736, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297427, 'ICCArmB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725737, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297428, 'ICCArmA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725738, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297429, 'ICCArmS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725739, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297430, 'ICCWepB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725740, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297431, 'ICCWepA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725741, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297432, 'ICCWepS');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725742, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725743, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725744, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES	(77725745, 1186, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');

-- Omni Basic General Shop import
-- Source: AOSharp capture 20260612-012644
-- Coverage reduction: 404 -> 381

-- Terminal: OT Basic Armor; TemplateId: 99383; VendorId: 77529088; Statel: 0xC000049F; Capture identity: (VendingMachine:12E3F4F3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529088, 1183, 192, 5, 139.5, 0, 0, 0, 1, '', 99383, 'OBF3VGA');

-- Terminal: OT Basic Attacks; TemplateId: 99495; VendorId: 77529089; Statel: 0xC001049F; Capture identity: (VendingMachine:12E3F4F4)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529089, 1183, 182, 5, 127.6, 0, 0, 0, 1, '', 99495, 'OBAGPLU');

-- Terminal: OT Basic Augmentations; TemplateId: 99484; VendorId: 77529090; Statel: 0xC002049F; Capture identity: (VendingMachine:12E3F4F5)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529090, 1183, 182, 5, 131.6, 0, 0, 0, 1, '', 99484, 'OBFUYMA');

-- Terminal: OT Basic Medical Supplies; TemplateId: 99481; VendorId: 77529091; Statel: 0xC003049F; Capture identity: (VendingMachine:12E3F4F6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529091, 1183, 182, 5, 135.6, 0, 0, 0, 1, '', 99481, 'OBX7YEB');

-- Terminal: OT Basic Tools; TemplateId: 99491; VendorId: 77529092; Statel: 0xC004049F; Capture identity: (VendingMachine:12E3F4F7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529092, 1183, 192, 5, 135.5, 0, 0, 0, 1, '', 99491, 'OBH6GZY');

-- Terminal: OT Basic Weapons; TemplateId: 99478; VendorId: 77529093; Statel: 0xC005049F; Capture identity: (VendingMachine:12E3F4F8)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529093, 1183, 192, 5, 127.5, 0, 0, 0, 1, '', 99478, 'OBMZVQZ');

-- Terminal: OT Clothes; TemplateId: 99490; VendorId: 77529094; Statel: 0xC006049F; Capture identity: (VendingMachine:12E3F4F9)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529094, 1183, 182, 5, 139.6, 0, 0, 0, 1, '', 99490, 'OBCQTXM');

-- Terminal: OT Maps; TemplateId: 117649; VendorId: 77529095; Statel: 0xC007049F; Capture identity: (VendingMachine:12E3F4FA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529095, 1183, 196.7, 5, 129, 0, 0, 0, 1, '', 117649, 'OBMOJMG');

-- Terminal: Omni Basic Devices; TemplateId: 155603; VendorId: 77529097; Statel: 0xC009049F; Capture identity: (VendingMachine:12E3F4FC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529097, 1183, 196.7, 5.001, 133, 0, 0, 0, 1, '', 155603, 'OBIUAFT');

-- Terminal: Melee Weapon Recipes - Basic; TemplateId: 155502; VendorId: 77529112; Statel: 0xC018049F; Capture identity: (VendingMachine:12E3F50B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529112, 1183, 117, 5, 123, 0, 0, 0, 1, '', 155502, 'OBGYYPZ');

-- Terminal: Ranged Weapon Recipes - Basic; TemplateId: 155505; VendorId: 77529113; Statel: 0xC019049F; Capture identity: (VendingMachine:12E3F50C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529113, 1183, 115, 5, 123, 0, 0, 0, 1, '', 155505, 'OB6HN4F');

-- Terminal: Melee Weapon Components - Basic; TemplateId: 155296; VendorId: 77529117; Statel: 0xC01D049F; Capture identity: (VendingMachine:12E3F510)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529117, 1183, 142.8, 13, 149.1, 0, 0, 0, 1, '', 155296, 'OBF6KVT');

-- Terminal: Ranged Weapon Components - Basic; TemplateId: 155490; VendorId: 77529118; Statel: 0xC01E049F; Capture identity: (VendingMachine:12E3F511)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529118, 1183, 140.8, 13, 149.1, 0, 0, 0, 1, '', 155490, 'OBJNHBE');

-- Terminal: Basic Implants; TemplateId: 155222; VendorId: 77529124; Statel: 0xC024049F; Capture identity: (VendingMachine:12E3F517)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529124, 1183, 108.9, 13, 122.1, 0, 0, 0, 1, '', 155222, 'OBOFY25');

-- Terminal: Basic Melee Weapon Construction Kits; TemplateId: 155233; VendorId: 77529125; Statel: 0xC025049F; Capture identity: (VendingMachine:12E3F518)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529125, 1183, 108.9, 13, 124.1, 0, 0, 0, 1, '', 155233, 'OBMPNFR');

-- Terminal: Basic Ranged Weapon Construction Kits; TemplateId: 155236; VendorId: 77529126; Statel: 0xC026049F; Capture identity: (VendingMachine:12E3F519)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529126, 1183, 108.9, 13, 126.1, 0, 0, 0, 1, '', 155236, 'OBUHWW4');

-- Terminal: Melee Weapon Recipes - Basic; TemplateId: 155502; VendorId: 77529131; Statel: 0xC02B049F; Capture identity: (VendingMachine:12E3F51E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529131, 1183, 115.5, 17, 149.1, 0, 0, 0, 1, '', 155502, 'OBGYYPZ');

-- Terminal: Ranged Weapon Recipes - Basic; TemplateId: 155505; VendorId: 77529132; Statel: 0xC02C049F; Capture identity: (VendingMachine:12E3F51F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529132, 1183, 117.5, 17, 149.1, 0, 0, 0, 1, '', 155505, 'OB6HN4F');

-- Terminal: Melee Weapon Components - Basic; TemplateId: 155296; VendorId: 77529136; Statel: 0xC030049F; Capture identity: (VendingMachine:12E3F523)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529136, 1183, 139, 5, 147, 0, 0, 0, 1, '', 155296, 'OBF6KVT');

-- Terminal: Ranged Weapon Components - Basic; TemplateId: 155490; VendorId: 77529137; Statel: 0xC031049F; Capture identity: (VendingMachine:12E3F524)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529137, 1183, 137, 5, 147, 0, 0, 0, 1, '', 155490, 'OBJNHBE');

-- Terminal: Basic Implants; TemplateId: 155222; VendorId: 77529143; Statel: 0xC037049F; Capture identity: (VendingMachine:12E3F52A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529143, 1183, 153, 5.001, 123, 0, 0, 0, 1, '', 155222, 'OBOFY25');

-- Terminal: Basic Melee Weapon Construction Kits; TemplateId: 155233; VendorId: 77529144; Statel: 0xC038049F; Capture identity: (VendingMachine:12E3F52B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529144, 1183, 151, 5.001, 123, 0, 0, 0, 1, '', 155233, 'OBMPNFR');

-- Terminal: Basic Ranged Weapon Construction Kits; TemplateId: 155236; VendorId: 77529145; Statel: 0xC039049F; Capture identity: (VendingMachine:12E3F52C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529145, 1183, 149, 5.001, 123, 0, 0, 0, 1, '', 155236, 'OBUHWW4');

-- Omni Superior General Shop import
-- Source: AOSharp capture 20260612-044234
-- Coverage: 351 → 324 (27 reduction)
-- Excludes: 155225 (non-shop statel template)

-- Terminal: OT Superior Armor; TemplateId: 99477; VendorId: 77660173; Statel: 0xC00D04A1; Capture identity: (VendingMachine:12E3FC84)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660173, 1185, 195.35, 5, 124.347, 0, 0, 0, 1, '', 99477, 'OSLC3UI');

-- Terminal: OT Superior Attacks; TemplateId: 99497; VendorId: 77660174; Statel: 0xC00E04A1; Capture identity: (VendingMachine:12E3FC85)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660174, 1185, 195.332, 5, 118.009, 0, 0, 0, 1, '', 99497, 'OSRA2ZZ');

-- Terminal: OT Superior Augmentations; TemplateId: 99486; VendorId: 77660175; Statel: 0xC00F04A1; Capture identity: (VendingMachine:12E3FC86)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660175, 1185, 214.719, 5.01, 128.357, 0, 0, 0, 1, '', 99486, 'OSGQXEO');

-- Terminal: OT Superior Medical Supplies; TemplateId: 99483; VendorId: 77660176; Statel: 0xC01004A1; Capture identity: (VendingMachine:12E3FC87)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660176, 1185, 195.32, 5.001, 111.997, 0, 0, 0, 1, '', 99483, 'OSCP3HJ');

-- Terminal: OT Superior Tools; TemplateId: 99493; VendorId: 77660177; Statel: 0xC01104A1; Capture identity: (VendingMachine:12E3FC88)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660177, 1185, 214.674, 5, 122.313, 0, 0, 0, 1, '', 99493, 'OSXOL6H');

-- Terminal: OT Superior Weapons; TemplateId: 99480; VendorId: 77660178; Statel: 0xC01204A1; Capture identity: (VendingMachine:12E3FC89)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660178, 1185, 214.664, 5, 115.898, 0, 0, 0, 1, '', 99480, 'OSQC5XR');

-- Terminal: OT Clothes; TemplateId: 99490; VendorId: 77660179; Statel: 0xC01304A1; Capture identity: (VendingMachine:12E3FC8A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660179, 1185, 214.619, 5.001, 112.974, 0, 0, 0, 1, '', 99490, 'OSNQQWH');

-- Terminal: OT Maps; TemplateId: 117649; VendorId: 77660181; Statel: 0xC01504A1; Capture identity: (VendingMachine:12E3FC8C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660181, 1185, 198.941, 5.001, 111.316, 0, 0, 0, 1, '', 117649, 'OSOIVVG');

-- Terminal: Omni Superior Devices; TemplateId: 155609; VendorId: 77660183; Statel: 0xC01704A1; Capture identity: (VendingMachine:12E3FC8E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660183, 1185, 210.955, 5.001, 111.364, 0, 0, 0, 1, '', 155609, 'OST6OJS');

-- Terminal: Melee Weapon Recipes - Superior; TemplateId: 155504; VendorId: 77660185; Statel: 0xC01904A1; Capture identity: (VendingMachine:12E3FC59)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660185, 1185, 147, 5, 115, 0, 0, 0, 1, '', 155504, 'OSAGOLB');

-- Terminal: Ranged Weapon Recipes - Superior; TemplateId: 155507; VendorId: 77660186; Statel: 0xC01A04A1; Capture identity: (VendingMachine:12E3FC5A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660186, 1185, 145, 5, 115, 0, 0, 0, 1, '', 155507, 'OSD2ZJQ');

-- Terminal: Melee Weapon Recipes - Superior; TemplateId: 155504; VendorId: 77660188; Statel: 0xC01C04A1; Capture identity: (VendingMachine:12E3FC5C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660188, 1185, 145.5, 17, 141.1, 0, 0, 0, 1, '', 155504, 'OSAGOLB');

-- Terminal: Ranged Weapon Recipes - Superior; TemplateId: 155507; VendorId: 77660189; Statel: 0xC01D04A1; Capture identity: (VendingMachine:12E3FC5D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660189, 1185, 147.5, 17, 141.1, 0, 0, 0, 1, '', 155507, 'OSD2ZJQ');

-- Terminal: Melee Weapon Components - Superior; TemplateId: 155298; VendorId: 77660193; Statel: 0xC02104A1; Capture identity: (VendingMachine:12E3FC61)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660193, 1185, 172.8, 13, 141.1, 0, 0, 0, 1, '', 155298, 'OSFZSPR');

-- Terminal: Ranged Weapon Components - Superior; TemplateId: 155492; VendorId: 77660194; Statel: 0xC02204A1; Capture identity: (VendingMachine:12E3FC62)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660194, 1185, 170.8, 13, 141.1, 0, 0, 0, 1, '', 155492, 'OSS7FOI');

-- Terminal: Armour and Clothing Components - Superior; TemplateId: 155498; VendorId: 77660197; Statel: 0xC02504A1; Capture identity: (VendingMachine:12E3FC65)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660197, 1185, 164.8, 13, 141.1, 0, 0, 0, 1, '', 155498, 'OSNB4VR');

-- Terminal: Nano Crystal Components - Superior; TemplateId: 155313; VendorId: 77660198; Statel: 0xC02604A1; Capture identity: (VendingMachine:12E3FC66)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660198, 1185, 162.8, 13, 141.1, 0, 0, 0, 1, '', 155313, 'OSQSROE');

-- Terminal: Melee Weapon Components - Superior; TemplateId: 155298; VendorId: 77660203; Statel: 0xC02B04A1; Capture identity: (VendingMachine:12E3FC6B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660203, 1185, 169, 5, 139, 0, 0, 0, 1, '', 155298, 'OSBPZXC');

-- Terminal: Ranged Weapon Components - Superior; TemplateId: 155492; VendorId: 77660204; Statel: 0xC02C04A1; Capture identity: (VendingMachine:12E3FC6C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660204, 1185, 167, 5, 139, 0, 0, 0, 1, '', 155492, 'OSV6ABI');

-- Terminal: Armour and Clothing Components - Superior; TemplateId: 155498; VendorId: 77660207; Statel: 0xC02F04A1; Capture identity: (VendingMachine:12E3FC6F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660207, 1185, 161, 5, 139, 0, 0, 0, 1, '', 155498, 'OSNB4VR');

-- Terminal: Nano Crystal Components - Superior; TemplateId: 155313; VendorId: 77660208; Statel: 0xC03004A1; Capture identity: (VendingMachine:12E3FC70)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660208, 1185, 159, 5, 139, 0, 0, 0, 1, '', 155313, 'OSQSROE');

-- Terminal: Superior Implants; TemplateId: 155224; VendorId: 77660210; Statel: 0xC03204A1; Capture identity: (VendingMachine:12E3FC72)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660210, 1185, 138.9, 13, 114.1, 0, 0, 0, 1, '', 155224, 'OSC6V6B');

-- Terminal: Superior Melee Weapon Construction Kits; TemplateId: 155235; VendorId: 77660211; Statel: 0xC03304A1; Capture identity: (VendingMachine:12E3FC73)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660211, 1185, 138.9, 13, 116.1, 0, 0, 0, 1, '', 155235, 'OSGLKFD');

-- Terminal: Superior Ranged Weapon Construction Kits; TemplateId: 155283; VendorId: 77660212; Statel: 0xC03404A1; Capture identity: (VendingMachine:12E3FC74)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660212, 1185, 138.9, 13, 118.1, 0, 0, 0, 1, '', 155283, 'OSFCG76');

-- Terminal: Superior Implants; TemplateId: 155224; VendorId: 77660216; Statel: 0xC03804A1; Capture identity: (VendingMachine:12E3FC78)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660216, 1185, 183, 5.001, 115, 0, 0, 0, 1, '', 155224, 'OSC6V6B');

-- Terminal: Superior Melee Weapon Construction Kits; TemplateId: 155235; VendorId: 77660217; Statel: 0xC03904A1; Capture identity: (VendingMachine:12E3FC79)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660217, 1185, 181, 5.001, 115, 0, 0, 0, 1, '', 155235, 'OSGLKFD');

-- Terminal: Superior Ranged Weapon Construction Kits; TemplateId: 155283; VendorId: 77660218; Statel: 0xC03A04A1; Capture identity: (VendingMachine:12E3FC7A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77660218, 1185, 179, 5.001, 115, 0, 0, 0, 1, '', 155283, 'OSFCG76');

-- Clan Basic General Shop import
-- Source: AOSharp capture 20260612-225855
-- Coverage: 324 -> 295 (29 reduction)
-- Excludes: 155225 (non-shop statel template)
-- Reuses existing shop inventory hashes: G4XZ, HYDQ, LJI7, R5R7

-- Terminal: Clan Basic Armor; TemplateId: 99502; VendorId: 77332493; Statel: 0xC00D049C; Capture identity: (VendingMachine:12E47537)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332493, 1180, 207, 5.01, 183, 0, 0, 0, 1, '', 99502, 'CBRE2SH');

-- Terminal: Clan Basic Attacks; TemplateId: 99532; VendorId: 77332494; Statel: 0xC00E049C; Capture identity: (VendingMachine:12E47538)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332494, 1180, 194.996, 4.609, 162.997, 0, 0, 0, 1, '', 99532, 'CB63J4Z');

-- Terminal: Clan Basic Augmentations; TemplateId: 99513; VendorId: 77332495; Statel: 0xC00F049C; Capture identity: (VendingMachine:12E47539)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332495, 1180, 194.066, 4.6, 181.138, 0, 0, 0, 1, '', 99513, 'CBSRZ3W');

-- Terminal: Clan Basic Medical Supplies; TemplateId: 99508; VendorId: 77332496; Statel: 0xC010049C; Capture identity: (VendingMachine:12E4753A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332496, 1180, 191.887, 4.601, 163.939, 0, 0, 0, 1, '', 99508, 'CBUMBXD');

-- Terminal: Clan Basic Tools; TemplateId: 99527; VendorId: 77332497; Statel: 0xC011049C; Capture identity: (VendingMachine:12E4753B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332497, 1180, 207.154, 4.6, 162.871, 0, 0, 0, 1, '', 99527, 'CBIGA24');

-- Terminal: Clan Basic Weapons; TemplateId: 99505; VendorId: 77332498; Statel: 0xC012049C; Capture identity: (VendingMachine:12E4753C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332498, 1180, 209.829, 4.6, 163.716, 0, 0, 0, 1, '', 99505, 'CBIEXSV');

-- Terminal: Clan Clothes; TemplateId: 99526; VendorId: 77332499; Statel: 0xC013049C; Capture identity: (VendingMachine:12E4753D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332499, 1180, 191.441, 4.6, 180.06, 0, 0, 0, 1, '', 99526, 'CBJY7AT');

-- Terminal: Clan Maps; TemplateId: 117749; VendorId: 77332500; Statel: 0xC014049C; Capture identity: (VendingMachine:12E4753E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332500, 1180, 190.872, 4.6, 177.595, 0, 0, 0, 1, '', 117749, 'CBKAVJ6');

-- Terminal: Clan Basic Devices; TemplateId: 155602; VendorId: 77332501; Statel: 0xC015049C; Capture identity: (VendingMachine:12E4753F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332501, 1180, 190.886, 4.6, 166.795, 0, 0, 0, 1, '', 155602, 'CBGXGWQ');

-- Terminal: Basic Clan Adventurer Specific Implants; TemplateId: 162157; VendorId: 77332503; Statel: 0xC017049C; Capture identity: (VendingMachine:12E47541)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332503, 1180, 233, 5.01, 187, 0, 0, 0, 1, '', 162157, 'CBIDRVY');

-- Terminal: Basic Clan Agent Specific Implants; TemplateId: 162160; VendorId: 77332504; Statel: 0xC018049C; Capture identity: (VendingMachine:12E47542)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332504, 1180, 237, 5.01, 187, 0, 0, 0, 1, '', 162160, 'CBFGTU4');

-- Terminal: Basic Clan Bureaucrat Specific Implants; TemplateId: 162163; VendorId: 77332505; Statel: 0xC019049C; Capture identity: (VendingMachine:12E47543)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332505, 1180, 241, 5.01, 187, 0, 0, 0, 1, '', 162163, 'CBCCGCW');

-- Terminal: Basic Clan Doctor Specific Implants; TemplateId: 162166; VendorId: 77332506; Statel: 0xC01A049C; Capture identity: (VendingMachine:12E47544)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332506, 1180, 241, 5.01, 183, 0, 0, 0, 1, '', 162166, 'CBZ3BNX');

-- Terminal: Basic Clan Enforcer Specific Implants; TemplateId: 162169; VendorId: 77332507; Statel: 0xC01B049C; Capture identity: (VendingMachine:12E47545)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332507, 1180, 241, 5.01, 175, 0, 0, 0, 1, '', 162169, 'CBVTEV5');

-- Terminal: Basic Clan Engineer Specific Implants; TemplateId: 162172; VendorId: 77332508; Statel: 0xC01C049C; Capture identity: (VendingMachine:12E47546)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332508, 1180, 241, 5.01, 171, 0, 0, 0, 1, '', 162172, 'CBHGCGP');

-- Terminal: Basic Clan Fixer Specific Implants; TemplateId: 162175; VendorId: 77332509; Statel: 0xC01D049C; Capture identity: (VendingMachine:12E47547)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332509, 1180, 237, 5.01, 171, 0, 0, 0, 1, '', 162175, 'CBYDGZL');

-- Terminal: Basic Clan Martial Artist Specific Implants; TemplateId: 162178; VendorId: 77332510; Statel: 0xC01E049C; Capture identity: (VendingMachine:12E47548)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332510, 1180, 229, 5.01, 171, 0, 0, 0, 1, '', 162178, 'CBXEBL5');

-- Terminal: Basic Clan Meta-Physicist Specific Implants; TemplateId: 162181; VendorId: 77332511; Statel: 0xC01F049C; Capture identity: (VendingMachine:12E47549)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332511, 1180, 225, 5.01, 171, 0, 0, 0, 1, '', 162181, 'CB2ELTH');

-- Terminal: Basic Clan Nanotechnician Specific Implants; TemplateId: 162184; VendorId: 77332512; Statel: 0xC020049C; Capture identity: (VendingMachine:12E4754A)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332512, 1180, 233, 5.01, 171, 0, 0, 0, 1, '', 162184, 'CBLEZ7U');

-- Terminal: Basic Clan Soldier Specific Implants; TemplateId: 162187; VendorId: 77332513; Statel: 0xC021049C; Capture identity: (VendingMachine:12E4754B)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332513, 1180, 225, 5.01, 187, 0, 0, 0, 1, '', 162187, 'CBMK5IN');

-- Terminal: Basic Clan Trader Specific Implants; TemplateId: 162190; VendorId: 77332514; Statel: 0xC022049C; Capture identity: (VendingMachine:12E4754C)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332514, 1180, 229, 5.01, 187, 0, 0, 0, 1, '', 162190, 'CBDWQH6');

-- Terminal: Basic Clan Keeper Specific Implants; TemplateId: 252271; VendorId: 77332515; Statel: 0xC023049C; Capture identity: (VendingMachine:12E4754D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332515, 1180, 221.114, 5.01, 171.109, 0, 0, 0, 1, '', 252271, 'CBHJMPZ');

-- Terminal: Basic Implants; TemplateId: 155222; VendorId: 77332516; Statel: 0xC024049C; Capture identity: (VendingMachine:12E4754E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332516, 1180, 133, 11, 153, 0, 0, 0, 1, '', 155222, 'CBOFY25');

-- Terminal: Basic Melee Weapon Construction Kits; TemplateId: 155233; VendorId: 77332517; Statel: 0xC025049C; Capture identity: (VendingMachine:12E4754F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332517, 1180, 154, 11.012, 153, 0, 0, 0, 1, '', 155233, 'CBMPNFR');

-- Terminal: Basic Ranged Weapon Construction Kits; TemplateId: 155236; VendorId: 77332518; Statel: 0xC026049C; Capture identity: (VendingMachine:12E47550)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332518, 1180, 159, 11, 153, 0, 0, 0, 1, '', 155236, 'CBUHWW4');

-- Terminal: Melee Weapon Components - Basic; TemplateId: 155296; VendorId: 77332525; Statel: 0xC02D049C; Capture identity: (VendingMachine:12E47557)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332525, 1180, 159, 5, 163, 0, 0, 0, 1, '', 155296, 'CBF6KVT');

-- Terminal: Ranged Weapon Components - Basic; TemplateId: 155490; VendorId: 77332526; Statel: 0xC02E049C; Capture identity: (VendingMachine:12E47558)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332526, 1180, 166, 5.001, 163, 0, 0, 0, 1, '', 155490, 'CBJNHBE');

-- Terminal: Melee Weapon Recipes - Basic; TemplateId: 155502; VendorId: 77332532; Statel: 0xC034049C; Capture identity: (VendingMachine:12E4755E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332532, 1180, 146, 11, 193, 0, 0, 0, 1, '', 155502, 'CBGYYPZ');

-- Terminal: Ranged Weapon Recipes - Basic; TemplateId: 155505; VendorId: 77332534; Statel: 0xC036049C; Capture identity: (VendingMachine:12E47560)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77332534, 1180, 139.016, 11, 192.999, 0, 0, 0, 1, '', 155505, 'CB6HN4F');


-- Clan Superior General Shop import
-- Source: AOSharp capture 20260612-232439
-- Coverage: 295 → 276 (19 reduction)
-- Excludes: 155225 (non-shop statel template)
-- Reuses existing shop inventory hashes: LJI7, CHHQ, OHOO, JYPE, Cont


-- Terminal: Superior Ranged Weapon Construction Kits; TemplateId: 155283; VendorId: 77463565; Statel: 0xC00D049E; Capture identity: (VendingMachine:12E3AED6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463565, 1182, 167, 13.101, 199, 0, 0, 0, 1, '', 155283, 'CSFCG76');

-- Terminal: Superior Melee Weapon Construction Kits; TemplateId: 155235; VendorId: 77463566; Statel: 0xC00E049E; Capture identity: (VendingMachine:12E3AED7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463566, 1182, 167, 13.101, 204, 0, 0, 0, 1, '', 155235, 'CSGLKFD');

-- Terminal: Superior Implants; TemplateId: 155224; VendorId: 77463570; Statel: 0xC012049E; Capture identity: (VendingMachine:12E3AEDB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463570, 1182, 167, 13.101, 225, 0, 0, 0, 1, '', 155224, 'CSC6V6B');

-- Terminal: Ranged Weapon Components - Superior; TemplateId: 155492; VendorId: 77463571; Statel: 0xC013049E; Capture identity: (VendingMachine:12E3AEDC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463571, 1182, 177, 7.101, 193, 0, 0, 0, 1, '', 155492, 'CSU6YPU');

-- Terminal: Armour and Clothing Components - Superior; TemplateId: 155498; VendorId: 77463574; Statel: 0xC016049E; Capture identity: (VendingMachine:12E3AEDF)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463574, 1182, 177, 7.101, 215, 0, 0, 0, 1, '', 155498, 'CSNB4VR');

-- Terminal: Nano Crystal Components - Superior; TemplateId: 155313; VendorId: 77463577; Statel: 0xC019049E; Capture identity: (VendingMachine:12E3AEE2)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463577, 1182, 197, 7.101, 199, 0, 0, 0, 1, '', 155313, 'CSQSROE');

-- Terminal: Melee Weapon Components - Superior; TemplateId: 155298; VendorId: 77463579; Statel: 0xC01B049E; Capture identity: (VendingMachine:12E3AEE4)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463579, 1182, 177, 7.101, 199, 0, 0, 0, 1, '', 155298, 'CS2LC3A');

-- Terminal: Ranged Weapon Recipes - Superior; TemplateId: 155507; VendorId: 77463581; Statel: 0xC01D049E; Capture identity: (VendingMachine:12E3AEE6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463581, 1182, 207, 13.101, 219, 0, 0, 0, 1, '', 155507, 'CSD2ZJQ');

-- Terminal: Melee Weapon Recipes - Superior; TemplateId: 155504; VendorId: 77463582; Statel: 0xC01E049E; Capture identity: (VendingMachine:12E3AEE7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463582, 1182, 207, 13.101, 212, 0, 0, 0, 1, '', 155504, 'CSAGOLB');

-- Terminal: Clan Superior Attacks; TemplateId: 99534; VendorId: 77463585; Statel: 0xC021049E; Capture identity: (VendingMachine:12E3AEEA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463585, 1182, 177, 6.1, 168, 0, 0, 0, 1, '', 99534, 'CSSD5SY');

-- Terminal: Clan Superior Augmentations; TemplateId: 99518; VendorId: 77463586; Statel: 0xC022049E; Capture identity: (VendingMachine:12E3AEEB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463586, 1182, 177, 6.1, 162, 0, 0, 0, 1, '', 99518, 'CSXKWKP');

-- Terminal: Clan Superior Medical Supplies; TemplateId: 99529; VendorId: 77463587; Statel: 0xC023049E; Capture identity: (VendingMachine:12E3AEEC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463587, 1182, 197, 8.101, 172, 0, 0, 0, 1, '', 99529, 'CSZKPVY');

-- Terminal: Clan Superior Tools; TemplateId: 99530; VendorId: 77463588; Statel: 0xC024049E; Capture identity: (VendingMachine:12E3AEED)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463588, 1182, 177, 6.1, 156, 0, 0, 0, 1, '', 99530, 'CSAUZMP');

-- Terminal: Clan Superior Weapons; TemplateId: 99507; VendorId: 77463589; Statel: 0xC025049E; Capture identity: (VendingMachine:12E3AEEE)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463589, 1182, 182, 6.101, 175, 0, 0, 0, 1, '', 99507, 'CS5JCOM');

-- Terminal: Clan Clothes; TemplateId: 99526; VendorId: 77463590; Statel: 0xC026049E; Capture identity: (VendingMachine:12E3AEEF)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463590, 1182, 197, 6.1, 158, 0, 0, 0, 1, '', 99526, 'CSOO7JG');

-- Terminal: Clan Containers; TemplateId: 99540; VendorId: 77463591; Statel: 0xC027049E; Capture identity: (VendingMachine:12E3AEF0)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463591, 1182, 193, 6.1, 155, 0, 0, 0, 1, '', 99540, 'CSSOA54');

-- Terminal: Clan Superior Armor; TemplateId: 99504; VendorId: 77463592; Statel: 0xC028049E; Capture identity: (VendingMachine:12E3AEF1)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463592, 1182, 177, 6.1, 174, 0, 0, 0, 1, '', 99504, 'CSFKCVG');

-- Terminal: Clan Maps; TemplateId: 117749; VendorId: 77463593; Statel: 0xC029049E; Capture identity: (VendingMachine:12E3AEF2)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463593, 1182, 179, 6.1, 171, 0, 0, 0, 1, '', 117749, 'CSIHQHX');

-- Terminal: Clan Superior Devices; TemplateId: 155608; VendorId: 77463594; Statel: 0xC02A049E; Capture identity: (VendingMachine:12E3AEF3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77463594, 1182, 179, 6.101, 159, 0, 0, 0, 1, '', 155608, 'CS3Q3IF');


-- Omni Advanced General Shop import
-- Source: AOSharp capture 20260613-002828
-- Coverage: 276 -> 253 (23 reduction)
-- Excludes: 155225 (non-shop statel template)
-- Reuses existing shop inventory hash: LJI7


-- Terminal: Melee Weapon Recipes - Advanced; TemplateId: 155503; VendorId: 77594638; Statel: 0xC00E04A0; Capture identity: (VendingMachine:12E4907E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594638, 1184, 155, 5, 107, 0, 0, 0, 1, '', 155503, 'OALQXGA');

-- Terminal: Ranged Weapon Recipes - Advanced; TemplateId: 155506; VendorId: 77594639; Statel: 0xC00F04A0; Capture identity: (VendingMachine:12E4907F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594639, 1184, 153, 5, 107, 0, 0, 0, 1, '', 155506, 'OAIFSRG');

-- Terminal: Melee Weapon Recipes - Advanced; TemplateId: 155503; VendorId: 77594641; Statel: 0xC01104A0; Capture identity: (VendingMachine:12E49081)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594641, 1184, 153.5, 17, 133.1, 0, 0, 0, 1, '', 155503, 'OALQXGA');

-- Terminal: Ranged Weapon Recipes - Advanced; TemplateId: 155506; VendorId: 77594642; Statel: 0xC01204A0; Capture identity: (VendingMachine:12E49082)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594642, 1184, 155.5, 17, 133.1, 0, 0, 0, 1, '', 155506, 'OAIFSRG');

-- Terminal: Melee Weapon Components - Advanced; TemplateId: 155297; VendorId: 77594646; Statel: 0xC01604A0; Capture identity: (VendingMachine:12E49086)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594646, 1184, 180.8, 13, 133.1, 0, 0, 0, 1, '', 155297, 'OAAC3R2');

-- Terminal: Ranged Weapon Components - Advanced; TemplateId: 155491; VendorId: 77594647; Statel: 0xC01704A0; Capture identity: (VendingMachine:12E49087)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594647, 1184, 178.8, 13, 133.1, 0, 0, 0, 1, '', 155491, 'OABOGYY');

-- Terminal: Melee Weapon Components - Advanced; TemplateId: 155297; VendorId: 77594656; Statel: 0xC02004A0; Capture identity: (VendingMachine:12E49090)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594656, 1184, 177, 5, 131, 0, 0, 0, 1, '', 155297, 'OAAC3R2');

-- Terminal: Ranged Weapon Components - Advanced; TemplateId: 155491; VendorId: 77594657; Statel: 0xC02104A0; Capture identity: (VendingMachine:12E49091)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594657, 1184, 175, 5, 131, 0, 0, 0, 1, '', 155491, 'OABOGYY');

-- Terminal: Advanced Implants; TemplateId: 155223; VendorId: 77594663; Statel: 0xC02704A0; Capture identity: (VendingMachine:12E49097)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594663, 1184, 146.9, 13, 106.1, 0, 0, 0, 1, '', 155223, 'OAG44BS');

-- Terminal: Advanced Melee Weapon Construction Kits; TemplateId: 155234; VendorId: 77594664; Statel: 0xC02804A0; Capture identity: (VendingMachine:12E49098)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594664, 1184, 146.9, 13, 108.1, 0, 0, 0, 1, '', 155234, 'OAXHP7H');

-- Terminal: Advanced Ranged Weapon Construction Kits; TemplateId: 155282; VendorId: 77594665; Statel: 0xC02904A0; Capture identity: (VendingMachine:12E49099)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594665, 1184, 146.9, 13, 110.1, 0, 0, 0, 1, '', 155282, 'OAKGM6T');

-- Terminal: Advanced Implants; TemplateId: 155223; VendorId: 77594669; Statel: 0xC02D04A0; Capture identity: (VendingMachine:12E4909D)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594669, 1184, 191, 5.001, 107, 0, 0, 0, 1, '', 155223, 'OAG44BS');

-- Terminal: Advanced Melee Weapon Construction Kits; TemplateId: 155234; VendorId: 77594670; Statel: 0xC02E04A0; Capture identity: (VendingMachine:12E4909E)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594670, 1184, 189, 5.001, 107, 0, 0, 0, 1, '', 155234, 'OAXHP7H');

-- Terminal: Advanced Ranged Weapon Construction Kits; TemplateId: 155282; VendorId: 77594671; Statel: 0xC02F04A0; Capture identity: (VendingMachine:12E4909F)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594671, 1184, 187, 5.001, 107, 0, 0, 0, 1, '', 155282, 'OAKGM6T');

-- Terminal: OT Advanced Armor; TemplateId: 99386; VendorId: 77594682; Statel: 0xC03A04A0; Capture identity: (VendingMachine:12E490A9)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594682, 1184, 203.2, 5, 111, 0, 0, 0, 1, '', 99386, 'OAL6IVC');

-- Terminal: OT Advanced Attacks; TemplateId: 99496; VendorId: 77594683; Statel: 0xC03B04A0; Capture identity: (VendingMachine:12E490AA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594683, 1184, 203.2, 5, 115, 0, 0, 0, 1, '', 99496, 'OAECAN7');

-- Terminal: OT Advanced Augmentations; TemplateId: 99485; VendorId: 77594684; Statel: 0xC03C04A0; Capture identity: (VendingMachine:12E490AB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594684, 1184, 211.99, 5, 115, 0, 0, 0, 1, '', 99485, 'OAS6ZPM');

-- Terminal: OT Advanced Medical Supplies; TemplateId: 99482; VendorId: 77594685; Statel: 0xC03D04A0; Capture identity: (VendingMachine:12E490AC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594685, 1184, 209.072, 5, 120.803, 0, 0, 0, 1, '', 99482, 'OAW76SU');

-- Terminal: OT Advanced Tools; TemplateId: 99492; VendorId: 77594686; Statel: 0xC03E04A0; Capture identity: (VendingMachine:12E490AD)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594686, 1184, 222.8, 5, 115, 0, 0, 0, 1, '', 99492, 'OAAMXEE');

-- Terminal: OT Advanced Weapons; TemplateId: 99479; VendorId: 77594687; Statel: 0xC03F04A0; Capture identity: (VendingMachine:12E490AE)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594687, 1184, 222.8, 5, 119, 0, 0, 0, 1, '', 99479, 'OAE5BNV');

-- Terminal: OT Clothes; TemplateId: 99490; VendorId: 77594688; Statel: 0xC04004A0; Capture identity: (VendingMachine:12E490AF)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594688, 1184, 213, 5, 113.99, 0, 0, 0, 1, '', 99490, 'OAFBTI6');

-- Terminal: OT Maps; TemplateId: 117649; VendorId: 77594690; Statel: 0xC04204A0; Capture identity: (VendingMachine:12E490B1)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594690, 1184, 219.032, 5, 106.9, 0, 0, 0, 1, '', 117649, 'OAA6B2F');

-- Terminal: Omni Advanced Devices; TemplateId: 155606; VendorId: 77594692; Statel: 0xC04404A0; Capture identity: (VendingMachine:12E490B3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77594692, 1184, 214, 5.001, 115, 0, 0, 0, 1, '', 155606, 'OAX2G2O');


-- Omni Basic Implant Terminals import
-- Source: AOSharp capture 20260613-005616
-- Coverage: 253 -> 240 (13 reduction)
-- Reuse: existing implant shop hashes


-- Terminal: Basic Omni-Tek Adventurer Specific Implants; TemplateId: 162158; VendorId: 77529149; Statel: 0xC03D049F; Capture identity: (VendingMachine:12E48CE0)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529149, 1183, 212.6, 5.01, 133.6, 0, 0, 0, 1, '', 162158, 'OBTGSV6');

-- Terminal: Basic Omni-Tek Agent Specific Implants; TemplateId: 162161; VendorId: 77529151; Statel: 0xC03F049F; Capture identity: (VendingMachine:12E48CE1)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529151, 1183, 214, 5.01, 129, 0, 0, 0, 1, '', 162161, 'OBRGJBU');

-- Terminal: Basic Omni-Tek Bureaucrat Specific Implants; TemplateId: 162164; VendorId: 77529153; Statel: 0xC041049F; Capture identity: (VendingMachine:12E48CE2)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529153, 1183, 214, 5.01, 125, 0, 0, 0, 1, '', 162164, 'OBTSB45');

-- Terminal: Basic Omni-Tek Doctor Specific Implants; TemplateId: 162167; VendorId: 77529155; Statel: 0xC043049F; Capture identity: (VendingMachine:12E48CE3)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529155, 1183, 214, 5.01, 121, 0, 0, 0, 1, '', 162167, 'OBPBXFK');

-- Terminal: Basic Omni-Tek Enforcer Specific Implants; TemplateId: 162170; VendorId: 77529156; Statel: 0xC044049F; Capture identity: (VendingMachine:12E48CE4)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529156, 1183, 214, 5.01, 118, 0, 0, 0, 1, '', 162170, 'OB53DT5');

-- Terminal: Basic Omni-Tek Engineer Specific Implants; TemplateId: 162173; VendorId: 77529157; Statel: 0xC045049F; Capture identity: (VendingMachine:12E48CE5)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529157, 1183, 214, 5.011, 114, 0, 0, 0, 1, '', 162173, 'OBRJVSX');

-- Terminal: Basic Omni-Tek Fixer Specific Implants; TemplateId: 162176; VendorId: 77529158; Statel: 0xC046049F; Capture identity: (VendingMachine:12E48CE6)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529158, 1183, 214, 5.01, 110, 0, 0, 0, 1, '', 162176, 'OBPR5QM');

-- Terminal: Basic Omni-Tek Martial Artist Specific Implants; TemplateId: 162179; VendorId: 77529159; Statel: 0xC047049F; Capture identity: (VendingMachine:12E48CE7)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529159, 1183, 209, 5.01, 104, 0, 0, 0, 1, '', 162179, 'OBVDA5Z');

-- Terminal: Basic Omni-Tek Meta-Physicist Specific Implants; TemplateId: 162182; VendorId: 77529160; Statel: 0xC048049F; Capture identity: (VendingMachine:12E48CE8)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529160, 1183, 204, 5.01, 110, 0, 0, 0, 1, '', 162182, 'OBJIWHL');

-- Terminal: Basic Omni-Tek Nanotechnician Specific Implants; TemplateId: 162185; VendorId: 77529161; Statel: 0xC049049F; Capture identity: (VendingMachine:12E48CE9)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529161, 1183, 212.6, 5.01, 106.4, 0, 0, 0, 1, '', 162185, 'OBTYNLZ');

-- Terminal: Basic Omni-Tek Soldier Specific Implants; TemplateId: 162188; VendorId: 77529162; Statel: 0xC04A049F; Capture identity: (VendingMachine:12E48CEA)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529162, 1183, 204, 5.01, 126, 0, 0, 0, 1, '', 162188, 'OB4LOZA');

-- Terminal: Basic Omni-Tek Trader Specific Implants; TemplateId: 162191; VendorId: 77529163; Statel: 0xC04B049F; Capture identity: (VendingMachine:12E48CEB)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529163, 1183, 209, 5.01, 136, 0, 0, 0, 1, '', 162191, 'OBED7EA');

-- Terminal: Basic Omni-Tek Keeper Specific Implants; TemplateId: 252270; VendorId: 77529164; Statel: 0xC04C049F; Capture identity: (VendingMachine:12E48CEC)
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77529164, 1183, 203.917, 5.01, 130.122, 0, 0, 0, 1, '', 252270, 'OB3DLM5');

-- Neutral Basic General/Specialty Shop import
-- Source: AOSharp captures 20260613-012810 and 20260613-014033
-- Coverage: 240 -> 234 (6 reduction)
-- Note: Specialist Commerce required Trader access.


-- Terminal: Computers; TemplateId: 99603; VendorId: 78184448; Statel: 0xC00004A9; Capture identity: (VendingMachine:12E4ABA8); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184448, 1193, 199, 5, 129, 0, 0, 0, 1, '', 99603, 'NBTLELB');

-- Terminal: Advanced Cars; TemplateId: 99635; VendorId: 78184449; Statel: 0xC00104A9; Capture identity: (VendingMachine:12E4ABA9); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184449, 1193, 193, 5.001, 123, 0, 0, 0, 1, '', 99635, 'NBBBPWA');

-- Terminal: Furniture; TemplateId: 120512; VendorId: 78184450; Statel: 0xC00204A9; Capture identity: (VendingMachine:12E4ABAA); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184450, 1193, 203, 5, 129, 0, 0, 0, 1, '', 120512, 'NB7LZHA');

-- Terminal: Toys and Curiosities; TemplateId: 151983; VendorId: 78184451; Statel: 0xC00304A9; Capture identity: (VendingMachine:12E4ABAB); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184451, 1193, 209, 5, 123, 0, 0, 0, 1, '', 151983, 'NBM27YC');

-- Terminal: Specialist Commerce; TemplateId: 151987; VendorId: 78184452; Statel: 0xC00404A9; Capture identity: (VendingMachine:12E4ABB2); Capture: 20260613-014033
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184452, 1193, 209, 5, 127, 0, 0, 0, 1, '', 151987, 'NBCQ762');

-- Terminal: Superior Cars; TemplateId: 151988; VendorId: 78184453; Statel: 0xC00504A9; Capture identity: (VendingMachine:12E4ABAD); Capture: 20260613-012810
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (78184453, 1193, 193, 5, 127, 0, 0, 0, 1, '', 151988, 'NB72WE4');

-- spec_smarket specialty import (inferred)
-- Coverage: 234 -> 218 (16 reduction)
-- Reuse: existing shop hashes only
-- No new inventory groups

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

-- ============================================================
-- Clan Advanced General Shop import
-- Source: AOSharp capture 20260613-034740
-- Coverage: 218 -> 202 (16 reduction)
-- Reuse: Cont, IVM2, IYD4, JTYS, LJI7
-- Vendor rows: 16
-- ============================================================
-- Terminal: Clan Advanced Attacks; TemplateId: 99533; VendorId: 77398030; Statel: 0xC00E049D; Capture identity: (VendingMachine:12E4B059); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398030, 1181, 177, 5, 171, 0, 0, 0, 1, '', 99533, 'CAWFVZL');

-- Terminal: Clan Advanced Augmentations; TemplateId: 99517; VendorId: 77398031; Statel: 0xC00F049D; Capture identity: (VendingMachine:12E4B05A); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398031, 1181, 177, 5, 175, 0, 0, 0, 1, '', 99517, 'CAXKPAK');

-- Terminal: Clan Advanced Medical Supplies; TemplateId: 99509; VendorId: 77398032; Statel: 0xC010049D; Capture identity: (VendingMachine:12E4B05B); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398032, 1181, 183, 5, 177, 0, 0, 0, 1, '', 99509, 'CAKVRD3');

-- Terminal: Clan Advanced Tools; TemplateId: 99528; VendorId: 77398033; Statel: 0xC011049D; Capture identity: (VendingMachine:12E4B05C); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398033, 1181, 185, 5, 170.6, 0, 0, 0, 1, '', 99528, 'CA4ANR3');

-- Terminal: Clan Advanced Weapons; TemplateId: 99506; VendorId: 77398034; Statel: 0xC012049D; Capture identity: (VendingMachine:12E4B05D); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398034, 1181, 197, 5, 176, 0, 0, 0, 1, '', 99506, 'CAIYRLU');

-- Terminal: Clan Clothes; TemplateId: 99526; VendorId: 77398035; Statel: 0xC013049D; Capture identity: (VendingMachine:12E4B05E); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398035, 1181, 187.017, 5, 169, 0, 0, 0, 1, '', 99526, 'CAOIGJE');

-- Terminal: Clan Containers; TemplateId: 99540; VendorId: 77398036; Statel: 0xC014049D; Capture identity: (VendingMachine:12E4B05F); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398036, 1181, 197, 5, 167, 0, 0, 0, 1, '', 99540, 'CAE4PJN');

-- Terminal: Clan Maps; TemplateId: 117749; VendorId: 77398037; Statel: 0xC015049D; Capture identity: (VendingMachine:12E4B060); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398037, 1181, 192.743, 5, 162.102, 0, 0, 0, 1, '', 117749, 'CANXN6U');

-- Terminal: Clan Advanced Devices; TemplateId: 155605; VendorId: 77398039; Statel: 0xC017049D; Capture identity: (VendingMachine:12E4B062); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398039, 1181, 189, 5.001, 170.6, 0, 0, 0, 1, '', 155605, 'CASMUGY');

-- Terminal: Advanced Ranged Weapon Construction Kits; TemplateId: 155282; VendorId: 77398040; Statel: 0xC018049D; Capture identity: (VendingMachine:12E4B063); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398040, 1181, 167, 11.001, 201, 0, 0, 0, 1, '', 155282, 'CAKGM6T');

-- Terminal: Advanced Melee Weapon Construction Kits; TemplateId: 155234; VendorId: 77398042; Statel: 0xC01A049D; Capture identity: (VendingMachine:12E4B065); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398042, 1181, 167, 11.001, 206, 0, 0, 0, 1, '', 155234, 'CAXHP7H');

-- Terminal: Advanced Implants; TemplateId: 155223; VendorId: 77398044; Statel: 0xC01C049D; Capture identity: (VendingMachine:12E4B067); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398044, 1181, 167, 11.001, 227, 0, 0, 0, 1, '', 155223, 'CAG44BS');

-- Terminal: Melee Weapon Components - Advanced; TemplateId: 155297; VendorId: 77398049; Statel: 0xC021049D; Capture identity: (VendingMachine:12E4B06C); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398049, 1181, 177, 5.001, 201, 0, 0, 0, 1, '', 155297, 'CAAC3R2');

-- Terminal: Ranged Weapon Components - Advanced; TemplateId: 155491; VendorId: 77398050; Statel: 0xC022049D; Capture identity: (VendingMachine:12E4B06D); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398050, 1181, 177, 5, 195, 0, 0, 0, 1, '', 155491, 'CABOGYY');

-- Terminal: Melee Weapon Recipes - Advanced; TemplateId: 155503; VendorId: 77398056; Statel: 0xC028049D; Capture identity: (VendingMachine:12E4B073); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398056, 1181, 207, 11.001, 214, 0, 0, 0, 1, '', 155503, 'CALQXGA');

-- Terminal: Ranged Weapon Recipes - Advanced; TemplateId: 155506; VendorId: 77398058; Statel: 0xC02A049D; Capture identity: (VendingMachine:12E4B075); Capture: 20260613-034740
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (77398058, 1181, 207, 11.001, 221, 0, 0, 0, 1, '', 155506, 'CAIFSRG');
