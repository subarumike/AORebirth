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
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768006, 500, 0, 0, 0, 0, 0, 0, 1, '', 99602, 'AtkB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768008, 500, 0, 0, 0, 0, 0, 0, 1, '', 99574, 'MedB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (32768010, 500, 0, 0, 0, 0, 0, 0, 1, '', 99601, 'ToolB');
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
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (37027846, 565, 0, 0, 0, 0, 0, 0, 1, '', 99574, 'MedB');

-- 2064 neut_basic_implants_shop safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266304, 2064, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266309, 2064, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135266314, 2064, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');

-- 2073 neut_advanced_implants_shop safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856128, 2073, 0, 0, 0, 0, 0, 0, 1, '', 297393, 'ICCPhaB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856133, 2073, 0, 0, 0, 0, 0, 0, 1, '', 297394, 'ICCPhaA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (135856138, 2073, 0, 0, 0, 0, 0, 0, 1, '', 297395, 'ICCPhaS');

-- 2096 4holes Fashion safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363457, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99552, 'ICCKeeN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363459, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99551, 'ICCMetN');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (137363462, 2096, 0, 0, 0, 0, 0, 0, 1, '', 99553, 'ICCFixN');

-- 4563 Hardware Dimension - Basic safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299040768, 4563, 0, 0, 0, 0, 0, 0, 1, '', 99601, 'ToolB');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299040771, 4563, 0, 0, 0, 0, 0, 0, 1, '', 99602, 'AtkB');

-- 4564 Hardware Dimension - Advanced safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299106304, 4564, 0, 0, 0, 0, 0, 0, 1, '', 152008, 'ToolA');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (299106307, 4564, 0, 0, 0, 0, 0, 0, 1, '', 151981, 'AtkA');

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

-- 6553 Arete Landing safe statel vendor mappings.
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457414, 6553, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');
INSERT INTO `vendors` (`Id`, `Playfield`, `X`, `Y`, `Z`, `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`, `Name`, `TemplateId`, `Hash`) VALUES (429457415, 6553, 0, 0, 0, 0, 0, 0, 1, '', 297459, 'ICCAmmo');

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
