CREATE TABLE `vendortemplate` (
  `Hash` varchar(7) NOT NULL,
  `Lvl` int(32) NOT NULL DEFAULT '1',
  `Name` varchar(256) NOT NULL DEFAULT "",
  `ItemTemplate` int(32) NOT NULL DEFAULT '0',
  `ShopInvHash` varchar(4) NOT NULL,
  `MinQl` int(32) DEFAULT '1',
  `MaxQl` int(32) DEFAULT '1',
  `Id` int(32) unsigned NOT NULL AUTO_INCREMENT,
  `Buy` float(3,2) NOT NULL default '0.05', -- Price Modifiere Sell item to Shop
  `Sell` float(3,2) NOT NULL default '1.00', -- Price Modifiere Buy item from Shop
  `Skill` int(3) DEFAULT '161', -- Skill that change the Basic Price of a Item CompLiter(161) for Machines, Psychology(162) for Humans (SL Garden Shops)
  PRIMARY KEY (`ID`,`Hash`) USING BTREE
) ENGINE=MyISAM DEFAULT CHARSET=latin1;

--
-- This list is not complete and should be filled up.
--

insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvNB', 1, 'Basic Adventurer Crystals', 43580, 'AdvN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeNB', 1, 'Basic Agent Crystals', 43579, 'AgeN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurNB', 1, 'Basic Bureaucrat Crystals', 43578, 'BurN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocNB', 1, 'Basic Doctor Crystals', 43577, 'DocN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfNB', 1, 'Basic Enforcer Crystals', 43581, 'EnfN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngNB', 1, 'Basic Engineer Crystals', 43576, 'EngN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixNB', 1, 'Basic Fixer Crystals', 43571, 'FixN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GenNB', 1, 'Basic General Crystals', 43569, 'GenN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('KeeNB', 1, 'Basic Keeper Crystals', 222945, 'KeeN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarNB', 1, 'Basic Martial Artist Crystals', 43575, 'MarN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetNB', 1, 'Basic Meta-Physicist Crystals', 43574, 'MetN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanNB', 1, 'Basic Nanotechnician Crystals', 43573, 'NanN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ShaNB', 1, 'Basic Shade Crystals', 222946, 'ShaN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolNB', 1, 'Basic Soldier Crystals', 43572, 'SolN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraNB', 1, 'Basic Trader Crystals', 43570, 'TraN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvNA', 1, 'Advanced Adventurer Crystals', 46522, 'AdvN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeNA', 1, 'Advanced Agent Crystals', 46520, 'AgeN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurNA', 1, 'Advanced Bureaucrat Crystals', 46506, 'BurN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocNA', 1, 'Advanced Doctor Crystals', 46340, 'DocN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfNA', 1, 'Advanced Enforcer Crystals', 46517, 'EnfN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngNA', 1, 'Advanced Engineer Crystals', 46339, 'EngN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixNA', 1, 'Advanced Fixer Crystals', 46519, 'FixN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('KeeNA', 1, 'Advanced Keeper Crystals', 222947, 'KeeN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarNA', 1, 'Advanced Martial Artists Crystals', 46510, 'MarN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetNA', 1, 'Advanced Meta-Physicist Crystals', 46338, 'MetN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanNA', 1, 'Advanced Nanotechnician Crystals', 46337, 'NanN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ShaNA', 1, 'Advanced Shade Crystals', 222948, 'ShaN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolNA', 1, 'Advanced Soldier Crystals', 46521, 'SolN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraNA', 1, 'Advanced Trader Crystals', 46518, 'TraN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvNS', 1, 'Superior Adventurer Crystals', 46516, 'AdvN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeNS', 1, 'Superior Agent Crystals', 46515, 'AgeN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurNS', 1, 'Superior Bureaucrat Crystals', 46507, 'BurN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocNS', 1, 'Superior Doctor Crystals', 46336, 'DocN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfNS', 1, 'Superior Enforcer Crystals', 46511, 'EnfN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngNS', 1, 'Superior Engineer Crystals', 46335, 'EngN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixNS', 1, 'Superior Fixer Crystals', 46512, 'FixN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('KeeNS', 1, 'Superior Keeper Crystals', 222949, 'KeeN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarNS', 1, 'Superior Martial Artist Crystals', 46509, 'MarN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetNS', 1, 'Superior Meta-Physicist Crystals', 46334, 'MetN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanNS', 1, 'Superior Nanotechnician Crystals', 46341, 'NanN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ShaNS', 1, 'Superior Shade Crystals', 222950, 'ShaN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolNS', 1, 'Superior Soldier Crystals', 46514, 'SolN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraNS', 1, 'Superior Trader Crystals', 46513, 'TraN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvCA', 1, 'Advanced Clan Adventurer Crystals', 93062, 'AdvN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeCA', 1, 'Advanced Clan Agent Crystals', 93061, 'AgeN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurCA', 1, 'Advanced Clan Bureaucrat Crystals', 93058, 'BurN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocCA', 1, 'Advanced Clan Doctor Crystals', 93057, 'DocN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfCA', 1, 'Advanced Clan Enforcer Crystals', 93055, 'EnfN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngCA', 1, 'Advanced Clan Engineer Crystals', 93053, 'EngN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixCA', 1, 'Advanced Clan Fixer Crystals', 93050, 'FixN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarCA', 1, 'Advanced Clan Martial Artists Crystals', 93049, 'MarN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetCA', 1, 'Advanced Clan Meta-Physicist Crystals', 93047, 'MetN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanCA', 1, 'Advanced Clan Nanotechnician Crystals', 93044, 'NanN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolCA', 1, 'Advanced Clan Soldier Crystals', 93043, 'SolN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraCA', 1, 'Advanced Clan Trader Crystals', 93041, 'TraN', 40, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvCB', 1, 'Basic Clan Adventurer Crystals', 90589, 'AdvN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeCB', 1, 'Basic Clan Agent Crystals', 90588, 'AgeN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurCB', 1, 'Basic Clan Bureaucrat Crystals', 90587, 'BurN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocCB', 1, 'Basic Clan Doctor Crystals', 90586, 'DocN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfCB', 1, 'Basic Clan Enforcer Crystals', 90585, 'EnfN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngCB', 1, 'Basic Clan Engineer Crystals', 90579, 'EngN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixCB', 1, 'Basic Clan Fixer Crystals', 90576, 'FixN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GenCB', 1, 'Basic Clan General Crystals', 90574, 'GenN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarCB', 1, 'Basic Clan Martial Artist Crystals', 90571, 'MarN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetCB', 1, 'Basic Clan Meta-Physicist Crystals', 90569, 'MetN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanCB', 1, 'Basic Clan Nanotechnician Crystals', 90567, 'NanN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolCB', 1, 'Basic Clan Soldier Crystals', 90564, 'SolN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraCB', 1, 'Basic Clan Trader Crystals', 90562, 'TraN', 1, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvCS', 1, 'Superior Clan Adventurer Crystals', 93104, 'AdvN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeCS', 1, 'Superior Clan Agent Crystals', 93098, 'AgeN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurCS', 1, 'Superior Clan Bureaucrat Crystals', 93102, 'BurN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocCS', 1, 'Superior Clan Doctor Crystals', 93096, 'DocN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfCS', 1, 'Superior Clan Enforcer Crystals', 93091, 'EnfN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngCS', 1, 'Superior Clan Engineer Crystals', 93093, 'EngN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixCS', 1, 'Superior Clan Fixer Crystals', 93088, 'FixN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarCS', 1, 'Superior Clan Martial Artist Crystals', 93090, 'MarN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetCS', 1, 'Superior Clan Meta-Physicist Crystals', 93085, 'MetN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanCS', 1, 'Superior Clan Nanotechnician Crystals', 93086, 'NanN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolCS', 1, 'Superior Clan Soldier Crystals', 93087, 'SolN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraCS', 1, 'Superior Clan Trader Crystals', 93082, 'TraN', 80, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvOA', 1, 'Advanced Omni-Tek Adventurer Crystals', 93063, 'AdvN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeOA', 1, 'Advanced Omni-Tek Agent Crystals', 93060, 'AgeN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurOA', 1, 'Advanced Omni-Tek Bureaucrat Crystals', 93059, 'BurN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocOA', 1, 'Advanced Omni-Tek Doctor Crystals', 93056, 'DocN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfOA', 1, 'Advanced Omni-Tek Enforcer Crystals', 93054, 'EnfN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngOA', 1, 'Advanced Omni-Tek Engineer Crystals', 93052, 'EngN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixOA', 1, 'Advanced Omni-Tek Fixer Crystals', 93051, 'FixN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarOA', 1, 'Advanced Omni-Tek Martial Artists Crystals', 93048, 'MarN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetOA', 1, 'Advanced Omni-Tek Meta-Physicist Crystals', 93046, 'MetN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanOA', 1, 'Advanced Omni-Tek Nanotechnician Crystals', 93045, 'NanN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolOA', 1, 'Advanced Omni-Tek Soldier Crystals', 93042, 'SolN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraOA', 1, 'Advanced Omni-Tek Trader Crystals', 93040, 'TraN', 40, 100, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvOB', 1, 'Basic Omni-Tek Adventurer Crystals', 90590, 'AdvN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeOB', 1, 'Basic Omni-Tek Agent Crystals', 90580, 'AgeN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurOB', 1, 'Basic Omni-Tek Bureaucrat Crystals', 90581, 'BurN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocOB', 1, 'Basic Omni-Tek Doctor Crystals', 90582, 'DocN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfOB', 1, 'Basic Omni-Tek Enforcer Crystals', 90583, 'EnfN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngOB', 1, 'Basic Omni-Tek Engineer Crystals', 90577, 'EngN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixOB', 1, 'Basic Omni-Tek Fixer Crystals', 90575, 'FixN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GenOB', 1, 'Basic Omni-Tek General Crystals', 90573, 'GenN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarOB', 1, 'Basic Omni-Tek Martial Artist Crystals', 90570, 'MarN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetOB', 1, 'Basic Omni-Tek Meta-Physicist Crystals', 90568, 'MetN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanOB', 1, 'Basic Omni-Tek Nanotechnician Crystals', 90565, 'NanN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolOB', 1, 'Basic Omni-Tek Soldier Crystals', 90563, 'SolN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraOB', 1, 'Basic Omni-Tek Trader Crystals', 90561, 'TraN', 1, 50, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AdvOS', 1, 'Superior Omni-Tek Adventurer Crystals', 93105, 'AdvN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AgeOS', 1, 'Superior Omni-Tek Agent Crystals', 93099, 'AgeN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BurOS', 1, 'Superior Omni-Tek Bureaucrat Crystals', 93095, 'BurN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DocOS', 1, 'Superior Omni-Tek Doctor Crystals', 93097, 'DocN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EnfOS', 1, 'Superior Omni-Tek Enforcer Crystals', 93092, 'EnfN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EngOS', 1, 'Superior Omni-Tek Engineer Crystals', 93094, 'EngN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FixOS', 1, 'Superior Omni-Tek Fixer Crystals', 93089, 'FixN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MarOS', 1, 'Superior Omni-Tek Martial Artist Crystals', 93084, 'MarN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MetOS', 1, 'Superior Omni-Tek Meta-Physicist Crystals', 93079, 'MetN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NanOS', 1, 'Superior Omni-Tek Nanotechnician Crystals', 93080, 'NanN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SolOS', 1, 'Superior Omni-Tek Soldier Crystals', 93081, 'SolN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('TraOS', 1, 'Superior Omni-Tek Trader Crystals', 93083, 'TraN', 80, 120, 0.05, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BNCO', 1, 'OT Basic Nano Clusters', 118281, 'NaCl', 1, 50, 0.06, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ANCO', 1, 'OT Advanced Nano Clusters', 118282, 'NaCl', 50, 90, 0.06, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SNCO', 1, 'OT Superior Nano Clusters', 118283, 'NaCl', 90, 200, 0.06, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BNCC', 1, 'Clan Basic Nano Clusters', 118284, 'NaCl', 1, 50, 0.04 ,1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ANCC', 1, 'Clan Advanced Nano Clusters', 118285, 'NaCl', 50, 90, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SNCC', 1, 'Clan Superior Nano Clusters', 118286, 'NaCl', 90, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FNCB', 1, 'Faded Nano Clusters - Basic', 155299, 'CluF', 1, 50, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FNCA', 1, 'Faded Nano Clusters - Advanced', 155300, 'CluF', 50, 90, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('FNCS', 1, 'Faded Nano Clusters - Superior', 155301, 'CluF', 90, 200, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BNCB', 1, 'Bright Nano Clusters - Basic', 155302, 'CluB', 1, 50, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BNCA', 1, 'Bright Nano Clusters - Advanced', 155303, 'CluB', 50, 90, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BNCS', 1, 'Bright Nano Clusters - Superior', 155307, 'CluB', 90, 200, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SNCB', 1, 'Shining Nano Clusters - Basic', 155308, 'CluS', 1, 50, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SNCA', 1, 'Shining Nano Clusters - Advanced', 155309, 'CluS', 50, 90, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('SNCS', 1, 'Shining Nano Clusters - Superior', 155310, 'CluS', 90, 200, 0.04, 1, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MedS', 1, 'Superior Medic Supplies', 151975, 'Med', 70, 125, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MedA', 1, 'Advanced Medic Supplies', 99575, 'Med', 20, 90, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MedB', 1, 'Basic Medic Supplies', 99574, 'Med', 1, 20, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ToolS', 1, 'Superior Tools', 152012, 'Tool', 70, 200, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ToolA', 1, 'Advanced Tools', 152008, 'Tool', 40, 125, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ToolB', 1, 'Basic Tools', 99601, 'Tool', 1, 50, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AtkS', 1, 'Superior Attacks', 151982, 'Atk', 70, 200, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AtkA', 1, 'Advanced Attacks', 151981, 'Atk', 30, 125, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('AtkB', 1, 'Basic Attacks', 99602, 'Atk', 1, 50, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BkStS', 1, 'Superior Bookstore', 155601, 'BkSt', 100, 200, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BkStA', 1, 'Advanced Bookstore', 155600, 'BkSt', 50, 100, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('BkStB', 1, 'Basic Bookstore', 155599, 'BkSt', 1, 50, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ContG', 1, 'Containers', 99501, 'Cont', 1, 1, 0.04, 1.00, 161);
-- Live Neutral Supermarket Advanced ICC implant room terminals captured 2026-06-08 from current official client.
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAdvI', 1, 'Basic ICC Adventurer Specific Implants', 302094, 'AdvI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAgeI', 1, 'Basic ICC Agent Specific Implants', 302095, 'AgeI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCBurI', 1, 'Basic ICC Bureaucrat Specific Implants', 302096, 'BurI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCDocI', 1, 'Basic ICC Doctor Specific Implants', 302101, 'DocI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEnfI', 1, 'Basic ICC Enforcer Specific Implants', 302102, 'EnfI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEngI', 1, 'Basic ICC Engineer Specific Implants', 302103, 'EngI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCFixI', 1, 'Basic ICC Fixer Specific Implants', 302104, 'FixI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCKeeI', 1, 'Basic ICC Keeper Specific Implants', 302106, 'KeeI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCMarI', 1, 'Basic ICC Martial Artist Specific Implants', 302105, 'MarI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCMetI', 1, 'Basic ICC Meta-Physicist Specific Implants', 302097, 'MetI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCNanI', 1, 'Basic ICC Nanotechnician Specific Implants', 302098, 'NanI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSolI', 1, 'Basic ICC Soldier Specific Implants', 302099, 'SolI', 10, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCTraI', 1, 'Basic ICC Trader Specific Implants', 302100, 'TraI', 10, 125, 0.04, 1.05, 161);
-- Live Neutral Supermarket Basic ICC nanos room terminals captured 2026-06-08 from current official client.
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCGenN', 1, 'ICC General Nano Programs', 297067, 'GenN', 1, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCTraN', 1, 'ICC Trader Nano Programs', 297070, 'TraN', 1, 127, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSolN', 1, 'ICC Soldier Nano Programs', 297069, 'SolN', 1, 119, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCShaN', 1, 'ICC Shade Nano Programs', 297068, 'ShaN', 1, 122, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCNanN', 1, 'ICC Nano-Technician Nano Programs', 297071, 'NanN', 1, 119, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCMetN', 1, 'ICC Meta-Physicist Nano Programs', 99551, 'MetN', 1, 116, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCMarN', 1, 'ICC Martial Artist Nano Programs', 99549, 'MarN', 1, 113, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCKeeN', 1, 'ICC Keeper Nano Programs', 99552, 'KeeN', 1, 124, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCFixN', 1, 'ICC Fixer Nano Programs', 99553, 'FixN', 1, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEngN', 1, 'ICC Engineer Nano Programs', 99567, 'EngN', 1, 119, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEnfN', 1, 'ICC Enforcer Nano Programs', 99557, 'EnfN', 1, 120, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCDocN', 1, 'ICC Doctor Nano Programs', 99559, 'DocN', 1, 119, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCBurN', 1, 'ICC Bureaucrat Nano Programs', 99558, 'BurN', 1, 142, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAgeN', 1, 'ICC Agent Nano Programs', 99556, 'AgeN', 1, 119, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAdvN', 1, 'ICC Adventurer Nano Programs', 99560, 'AdvN', 1, 125, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCTech', 1, 'ICC Tech Supplies', 297290, '0T95', 1, 300, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAmmo', 1, 'ICC Ammunition', 297459, 'Ammo', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCPhaB', 1, 'Basic ICC Pharmacy', 297393, 'Med', 5, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCPhaA', 1, 'Advanced ICC Pharmacy', 297394, 'Med', 110, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCPhaS', 1, 'Superior ICC Pharmacy', 297395, 'PC1H', 200, 300, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCCnt', 1, 'ICC Containers', 297423, 'Cont', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAccB', 1, 'Basic ICC Accessories', 297424, 'AcB', 1, 109, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAccA', 1, 'Advanced ICC Accessories', 297425, 'AcA', 110, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCAccS', 1, 'Superior ICC Accessories', 297426, 'AcS', 200, 300, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCArmB', 1, 'Basic ICC Armor', 297427, 'IAB', 1, 109, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCArmA', 1, 'Advanced ICC Armor', 297428, 'IAA', 110, 208, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCArmS', 1, 'Superior ICC Armor', 297429, 'IAS', 175, 220, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCWepB', 1, 'Basic ICC Weapons', 297430, 'WpB', 1, 109, 0.04, 0.56, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCWepA', 1, 'Advanced ICC Weapons', 297431, 'WpA', 110, 208, 0.04, 0.56, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCWepS', 1, 'Superior ICC Weapons', 297432, 'WpS', 175, 300, 0.04, 0.56, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCMAtk', 1, 'ICC Martial Arts Attacks', 297466, 'MAtk', 8, 300, 0.04, 1.00, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSpWB', 1, 'Basic ICC Special Weapons', 99572, 'SpWB', 1, 93, 0.04, 0.56, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSpWA', 1, 'Advanced ICC Special Weapons', 99573, 'SpWA', 101, 200, 0.04, 0.56, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSpWS', 1, 'Superior ICC Special Weapons', 297470, 'SpWS', 200, 297, 0.04, 0.56, 161);
-- Live Heavenly Business Basic tradeskill terminals captured 2026-06-10 from current official client.
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DevB', 1, 'Basic Devices', 155604, 'DevB', 1, 45, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GToolB', 1, 'General Tools and Bases - Basic', 155284, 'GTBB', 1, 49, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EMTlB', 1, 'Tools for Electrical and Mechanical Engineering - Basic', 155290, 'EMTB', 10, 49, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('PCTlB', 1, 'Pharmacy and Chemistry Tools and Bases - Basic', 155287, 'PCTB', 7, 49, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GRecB', 1, 'General Recipes - Basic', 155508, 'GReB', 1, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GCmpB', 1, 'General Components - Basic', 155499, 'GCmB', 1, 46, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MECmpB', 1, 'Mechanical and Electrical Engineering Components - Basic', 155493, 'MECB', 3, 49, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('PCCmpB', 1, 'Pharmacy and Chemistry Components - Basic', 155314, 'PCCB', 3, 50, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ACCmpB', 1, 'Armour and Clothing Components - Basic', 155496, 'ACCB', 9, 37, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NCCmpB', 1, 'Nano Crystal Components - Basic', 155311, 'NCCB', 2, 50, 0.04, 1.05, 161);
-- Live Heavenly Business Advanced tradeskill terminals captured 2026-06-11 from current official client.
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DevA', 1, 'Advanced Devices', 155607, 'DevA', 2, 83, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GToolA', 1, 'General Tools and Bases - Advanced', 155285, 'GTBA', 1, 89, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EMTlA', 1, 'Tools for Electrical and Mechanical Engineering - Advanced', 155291, 'EMTA', 10, 89, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('PCTlA', 1, 'Pharmacy and Chemistry Tools and Bases - Advanced', 155288, 'PCTA', 10, 89, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GRecA', 1, 'General Recipes - Advanced', 155509, 'GReA', 1, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GCmpA', 1, 'General Components - Advanced', 155500, 'GCmA', 1, 71, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MECmpA', 1, 'Mechanical and Electrical Engineering Components - Advanced', 155494, 'MECA', 10, 89, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('PCCmpA', 1, 'Pharmacy and Chemistry Components - Advanced', 155488, 'PCCA', 10, 89, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ACCmpA', 1, 'Armour and Clothing Components - Advanced', 155497, 'ACCA', 40, 83, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('NCCmpA', 1, 'Nano Crystal Components - Advanced', 155312, 'NCCA', 31, 89, 0.04, 1.05, 161);
-- Live Heavenly Business Superior tradeskill terminals captured 2026-06-11 from current official client.
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('DevS', 1, 'Superior Devices', 155610, 'DevS', 2, 199, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GToolS', 1, 'General Tools and Bases - Superior', 155286, 'GTBS', 1, 199, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('EMTlS', 1, 'Tools for Electrical and Mechanical Engineering - Superior', 155292, 'EMTS', 10, 137, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('PCTlS', 1, 'Pharmacy and Chemistry Tools and Bases - Superior', 155289, 'PCTS', 10, 199, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GRecS', 1, 'General Recipes - Superior', 155510, 'GReS', 1, 100, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('GCmpS', 1, 'General Components - Superior', 155501, 'GCmS', 1, 167, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MECmpS', 1, 'Mechanical and Electrical Engineering Components - Superior', 155495, 'MECS', 10, 199, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('PCCmpS', 1, 'Pharmacy and Chemistry Components - Superior', 155489, 'PCCS', 10, 199, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('JACmpS', 1, 'Jobe Armour and Clothing Components - Superior', 223502, 'JACS', 88, 150, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('JNCmpS', 1, 'Jobe Nano Crystal Components - Superior', 223505, 'JNCS', 74, 199, 0.04, 1.05, 161);
-- Live Neutral Supermarket tradeskill room vendor templates captured 2026-06-08 from current official client.
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCChB', 1, 'Basic ICC Chemical Supplies', 297433, 'ChB', 1, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCChA', 1, 'Advanced ICC Chemical Supplies', 297434, 'ChA', 1, 209, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCChS', 1, 'Superior ICC Chemical Supplies', 297435, 'ChS', 1, 300, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEngB', 1, 'Basic ICC Engineering Supplies', 297448, 'EnB', 1, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEngA', 1, 'Advanced ICC Engineering Supplies', 297449, 'EnA', 1, 208, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCEngS', 1, 'Superior ICC Engineering Supplies', 297450, 'EnS', 1, 300, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCFash', 1, 'ICC Fashion Supplies', 297457, 'Fash', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiArm', 1, 'Miiir Armwear', 99543, 'MArm', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiFoot', 1, 'Miiir Footwear', 99544, 'MFot', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiLeg', 1, 'Miiir Legwear', 99545, 'MLeg', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiHand', 1, 'Miiir Handwear', 99546, 'MHan', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiSwim', 1, 'Miiir Swimwear', 99547, 'MSwm', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiChst', 1, 'Miiir Chestwear', 99548, 'MChs', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiBack', 1, 'Miiir Backwear', 99550, 'MBak', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('MiiHead', 1, 'Miiir Headwear', 99554, 'MHea', 1, 1, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCOfB', 1, 'Basic ICC Office Supplies', 297443, 'OfcB', 1, 94, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCOfA', 1, 'Advanced ICC Office Supplies', 297444, 'OfcA', 1, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCOfS', 1, 'Superior ICC Office Supplies', 297445, 'OfcS', 1, 200, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSmB', 1, 'Basic ICC Smithing Supplies', 297440, 'SmB', 1, 104, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSmA', 1, 'Advanced ICC Smithing Supplies', 297441, 'SmA', 10, 205, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCSmS', 1, 'Superior ICC Smithing Supplies', 297442, 'SmS', 10, 255, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCWsB', 1, 'Basic ICC Weapon Supplies', 297454, 'WsB', 1, 109, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCWsA', 1, 'Advanced ICC Weapon Supplies', 297455, 'WsA', 1, 208, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCWsS', 1, 'Superior ICC Weapon Supplies', 297456, 'WsS', 1, 300, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCArB', 1, 'Basic ICC Architecture Supplies', 297451, 'ArB', 6, 201, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCArA', 1, 'Advanced ICC Architecture Supplies', 297452, 'ArA', 75, 206, 0.04, 1.05, 161);
insert into `vendortemplate` (`HASH`,`lvl`,`Name`,`itemtemplate`,`ShopInvHash`,`minQL`,`maxQL`,`buy`,`sell`,`skill`) VALUES ('ICCArS', 1, 'Superior ICC Architecture Supplies', 297453, 'ArS', 75, 300, 0.04, 1.05, 161);

-- Omni Basic General Shop import
-- Source: AOSharp capture 20260612-012644
-- Coverage reduction: 404 -> 381

-- Terminal: Basic Implants
-- TemplateId: 155222; VendorId(s): 77529124, 77529143; ShopHash: BV22; Inventory rows: 38
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBOFY25', 1, 'BasicImplants', 155222, 'BV22', 2, 50);

-- Terminal: Melee Weapon Components - Basic
-- TemplateId: 155296; VendorId(s): 77529117, 77529136; ShopHash: XSPR; Inventory rows: 49
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBF6KVT', 1, 'BasicMeleeWeaponComponents', 155296, 'XSPR', 2, 50);

-- Terminal: Basic Melee Weapon Construction Kits
-- TemplateId: 155233; VendorId(s): 77529125, 77529144; ShopHash: QKCF; Inventory rows: 38
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBMPNFR', 1, 'BasicMeleeWeaponConstructionKits', 155233, 'QKCF', 2, 50);

-- Terminal: Melee Weapon Recipes - Basic
-- TemplateId: 155502; VendorId(s): 77529112, 77529131; ShopHash: R5R7; Inventory rows: 21
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBGYYPZ', 1, 'BasicMeleeWeaponRecipes', 155502, 'R5R7', 1, 1);

-- Terminal: Ranged Weapon Components - Basic
-- TemplateId: 155490; VendorId(s): 77529118, 77529137; ShopHash: F5CG; Inventory rows: 118
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBJNHBE', 1, 'BasicRangedWeaponComponents', 155490, 'F5CG', 1, 50);

-- Terminal: Basic Ranged Weapon Construction Kits
-- TemplateId: 155236; VendorId(s): 77529126, 77529145; ShopHash: XWMV; Inventory rows: 27
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBUHWW4', 1, 'BasicRangedWeaponConstructionKits', 155236, 'XWMV', 2, 50);

-- Terminal: Ranged Weapon Recipes - Basic
-- TemplateId: 155505; VendorId(s): 77529113, 77529132; ShopHash: HYDQ; Inventory rows: 95
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OB6HN4F', 1, 'BasicRangedWeaponRecipes', 155505, 'HYDQ', 1, 100);

-- Terminal: OT Basic Armor
-- TemplateId: 99383; VendorId(s): 77529088; ShopHash: EPFF; Inventory rows: 29
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBF3VGA', 1, 'OTBasicArmor', 99383, 'EPFF', 3, 49);

-- Terminal: OT Basic Attacks
-- TemplateId: 99495; VendorId(s): 77529089; ShopHash: ZWCP; Inventory rows: 10
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBAGPLU', 1, 'OTBasicAttacks', 99495, 'ZWCP', 1, 100);

-- Terminal: OT Basic Augmentations
-- TemplateId: 99484; VendorId(s): 77529090; ShopHash: ZTPP; Inventory rows: 70
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBFUYMA', 1, 'OTBasicAugmentations', 99484, 'ZTPP', 1, 42);

-- Terminal: OT Clothes
-- TemplateId: 99490; VendorId(s): 77529094; ShopHash: IMXL; Inventory rows: 19
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBCQTXM', 1, 'OTBasicClothes', 99490, 'IMXL', 1, 1);

-- Terminal: OT Maps
-- TemplateId: 117649; VendorId(s): 77529095; ShopHash: LJI7; Inventory rows: 2
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBMOJMG', 1, 'OTBasicMaps', 117649, 'LJI7', 1, 30);

-- Terminal: OT Basic Medical Supplies
-- TemplateId: 99481; VendorId(s): 77529091; ShopHash: G4XZ; Inventory rows: 40
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBX7YEB', 1, 'OTBasicMedicalSupplies', 99481, 'G4XZ', 1, 20);

-- Terminal: OT Basic Tools
-- TemplateId: 99491; VendorId(s): 77529092; ShopHash: ZUI3; Inventory rows: 19
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBH6GZY', 1, 'OTBasicTools', 99491, 'ZUI3', 1, 42);

-- Terminal: OT Basic Weapons
-- TemplateId: 99478; VendorId(s): 77529093; ShopHash: JPBP; Inventory rows: 88
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBMZVQZ', 1, 'OTBasicWeapons', 99478, 'JPBP', 1, 50);

-- Terminal: Omni Basic Devices
-- TemplateId: 155603; VendorId(s): 77529097; ShopHash: FRZN; Inventory rows: 27
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OBIUAFT', 1, 'OmniBasicDevices', 155603, 'FRZN', 1, 50);

-- Omni Superior General Shop import
-- Source: AOSharp capture 20260612-044234
-- Coverage: 351 → 324 (27 reduction)
-- Excludes: 155225 (non-shop statel template)

-- Terminal: OT Superior Armor
-- TemplateId: 99477; VendorId(s): 77660173; ShopHash: PYFP; Inventory rows: 29; Capture identity: (VendingMachine:12E3FC84); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSLC3UI', 1, 'OTSuperiorArmor', 99477, 'PYFP', 72, 123);

-- Terminal: OT Superior Attacks
-- TemplateId: 99497; VendorId(s): 77660174; ShopHash: WQH5; Inventory rows: 8; Capture identity: (VendingMachine:12E3FC85); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSRA2ZZ', 1, 'OTSuperiorAttacks', 99497, 'WQH5', 81, 121);

-- Terminal: OT Superior Augmentations
-- TemplateId: 99486; VendorId(s): 77660175; ShopHash: HHXC; Inventory rows: 70; Capture identity: (VendingMachine:12E3FC86); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSGQXEO', 1, 'OTSuperiorAugmentations', 99486, 'HHXC', 1, 125);

-- Terminal: OT Superior Medical Supplies
-- TemplateId: 99483; VendorId(s): 77660176; ShopHash: JYPE; Inventory rows: 40; Capture identity: (VendingMachine:12E3FC87); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSCP3HJ', 1, 'OTSuperiorMedicalSupplies', 99483, 'JYPE', 70, 125);

-- Terminal: OT Superior Tools
-- TemplateId: 99493; VendorId(s): 77660177; ShopHash: NTTB; Inventory rows: 19; Capture identity: (VendingMachine:12E3FC88); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSXOL6H', 1, 'OTSuperiorTools', 99493, 'NTTB', 1, 121);

-- Terminal: OT Superior Weapons
-- TemplateId: 99480; VendorId(s): 77660178; ShopHash: 4O66; Inventory rows: 88; Capture identity: (VendingMachine:12E3FC89); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSQC5XR', 1, 'OTSuperiorWeapons', 99480, '4O66', 1, 125);

-- Terminal: OT Clothes
-- TemplateId: 99490; VendorId(s): 77660179; ShopHash: HPBL; Inventory rows: 16; Capture identity: (VendingMachine:12E3FC8A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSNQQWH', 1, 'OTSuperiorClothes', 99490, 'HPBL', 1, 1);

-- Terminal: OT Maps
-- TemplateId: 117649; VendorId(s): 77660181; ShopHash: LJI7; Inventory rows: 2; Capture identity: (VendingMachine:12E3FC8C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSOIVVG', 1, 'OTSuperiorMaps', 117649, 'LJI7', 1, 30);

-- Terminal: Omni Superior Devices
-- TemplateId: 155609; VendorId(s): 77660183; ShopHash: 6LO5; Inventory rows: 27; Capture identity: (VendingMachine:12E3FC8E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OST6OJS', 1, 'OmniSuperiorDevices', 155609, '6LO5', 2, 193);

-- Terminal: Melee Weapon Recipes - Superior
-- TemplateId: 155504; VendorId(s): 77660185, 77660188; ShopHash: OHOO; Inventory rows: 75; Capture identity: (VendingMachine:12E3FC59); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSAGOLB', 1, 'SuperiorMeleeWeaponRecipes', 155504, 'OHOO', 1, 1);

-- Terminal: Ranged Weapon Recipes - Superior
-- TemplateId: 155507; VendorId(s): 77660186, 77660189; ShopHash: CHHQ; Inventory rows: 325; Capture identity: (VendingMachine:12E3FC5A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSD2ZJQ', 1, 'SuperiorRangedWeaponRecipes', 155507, 'CHHQ', 1, 100);

-- Terminal: Melee Weapon Components - Superior
-- TemplateId: 155298; VendorId(s): 77660193; ShopHash: VEHL; Inventory rows: 50; Capture identity: (VendingMachine:12E3FC61); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSFZSPR', 1, 'SuperiorMeleeWeaponComponentsA', 155298, 'VEHL', 70, 199);

-- Terminal: Ranged Weapon Components - Superior
-- TemplateId: 155492; VendorId(s): 77660194; ShopHash: G2RV; Inventory rows: 123; Capture identity: (VendingMachine:12E3FC62); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSS7FOI', 1, 'SuperiorRangedWeaponComponentsA', 155492, 'G2RV', 1, 200);

-- Terminal: Armour and Clothing Components - Superior
-- TemplateId: 155498; VendorId(s): 77660197, 77660207; ShopHash: UZ4T; Inventory rows: 4; Capture identity: (VendingMachine:12E3FC65); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSNB4VR', 1, 'SuperiorArmourClothingComponents', 155498, 'UZ4T', 80, 193);

-- Terminal: Nano Crystal Components - Superior
-- TemplateId: 155313; VendorId(s): 77660198, 77660208; ShopHash: ZP6H; Inventory rows: 60; Capture identity: (VendingMachine:12E3FC66); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSQSROE', 1, 'SuperiorNanoCrystalComponents', 155313, 'ZP6H', 71, 200);

-- Terminal: Melee Weapon Components - Superior
-- TemplateId: 155298; VendorId(s): 77660203; ShopHash: NV6B; Inventory rows: 50; Capture identity: (VendingMachine:12E3FC6B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSBPZXC', 1, 'SuperiorMeleeWeaponComponentsB', 155298, 'NV6B', 71, 199);

-- Terminal: Ranged Weapon Components - Superior
-- TemplateId: 155492; VendorId(s): 77660204; ShopHash: OFJQ; Inventory rows: 123; Capture identity: (VendingMachine:12E3FC6C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSV6ABI', 1, 'SuperiorRangedWeaponComponentsB', 155492, 'OFJQ', 1, 199);

-- Terminal: Superior Implants
-- TemplateId: 155224; VendorId(s): 77660210, 77660216; ShopHash: ITPE; Inventory rows: 38; Capture identity: (VendingMachine:12E3FC72); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSC6V6B', 1, 'SuperiorImplants', 155224, 'ITPE', 71, 121);

-- Terminal: Superior Melee Weapon Construction Kits
-- TemplateId: 155235; VendorId(s): 77660211, 77660217; ShopHash: FVLD; Inventory rows: 39; Capture identity: (VendingMachine:12E3FC73); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSGLKFD', 1, 'SuperiorMeleeWeaponConstructionKits', 155235, 'FVLD', 72, 200);

-- Terminal: Superior Ranged Weapon Construction Kits
-- TemplateId: 155283; VendorId(s): 77660212, 77660218; ShopHash: CTWD; Inventory rows: 27; Capture identity: (VendingMachine:12E3FC74); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OSFCG76', 1, 'SuperiorRangedWeaponConstructionKits', 155283, 'CTWD', 72, 193);

-- Clan Basic General Shop import
-- Source: AOSharp capture 20260612-225855
-- Coverage: 324 -> 295 (29 reduction)
-- Excludes: 155225 (non-shop statel template)
-- Reuses existing shop inventory hashes: G4XZ, HYDQ, LJI7, R5R7

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


-- Clan Superior General Shop import
-- Source: AOSharp capture 20260612-232439
-- Coverage: 295 → 276 (19 reduction)
-- Excludes: 155225 (non-shop statel template)
-- Reuses existing shop inventory hashes: LJI7, CHHQ, OHOO, JYPE, Cont


-- Terminal: Superior Ranged Weapon Construction Kits
-- NormalizedName: SuperiorRangedWeaponConstructionKits; TemplateId: 155283; VendorId: 77463565; ShopHash: V5TB (new); Inventory rows: 26; Capture identity: (VendingMachine:12E3AED6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSFCG76', 1, 'SuperiorRangedWeaponConstructionKits', 155283, 'V5TB', 70, 199);

-- Terminal: Superior Melee Weapon Construction Kits
-- NormalizedName: SuperiorMeleeWeaponConstructionKits; TemplateId: 155235; VendorId: 77463566; ShopHash: SYV2 (new); Inventory rows: 37; Capture identity: (VendingMachine:12E3AED7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSGLKFD', 1, 'SuperiorMeleeWeaponConstructionKits', 155235, 'SYV2', 70, 199);

-- Terminal: Superior Implants
-- NormalizedName: SuperiorImplants; TemplateId: 155224; VendorId: 77463570; ShopHash: S47V (new); Inventory rows: 39; Capture identity: (VendingMachine:12E3AEDB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSC6V6B', 1, 'SuperiorImplants', 155224, 'S47V', 71, 123);

-- Terminal: Ranged Weapon Components - Superior
-- NormalizedName: SuperiorRangedWeaponComponents; TemplateId: 155492; VendorId: 77463571; ShopHash: LPFM (new); Inventory rows: 121; Capture identity: (VendingMachine:12E3AEDC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSU6YPU', 1, 'SuperiorRangedWeaponComponents', 155492, 'LPFM', 1, 200);

-- Terminal: Armour and Clothing Components - Superior
-- NormalizedName: SuperiorArmourClothingComponents; TemplateId: 155498; VendorId: 77463574; ShopHash: 4L5J (new); Inventory rows: 4; Capture identity: (VendingMachine:12E3AEDF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSNB4VR', 1, 'SuperiorArmourClothingComponents', 155498, '4L5J', 141, 199);

-- Terminal: Nano Crystal Components - Superior
-- NormalizedName: SuperiorNanoCrystalComponents; TemplateId: 155313; VendorId: 77463577; ShopHash: D7ED (new); Inventory rows: 58; Capture identity: (VendingMachine:12E3AEE2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSQSROE', 1, 'SuperiorNanoCrystalComponents', 155313, 'D7ED', 70, 199);

-- Terminal: Melee Weapon Components - Superior
-- NormalizedName: SuperiorMeleeWeaponComponents; TemplateId: 155298; VendorId: 77463579; ShopHash: 4GMW (new); Inventory rows: 48; Capture identity: (VendingMachine:12E3AEE4); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CS2LC3A', 1, 'SuperiorMeleeWeaponComponents', 155298, '4GMW', 70, 199);

-- Terminal: Ranged Weapon Recipes - Superior
-- NormalizedName: SuperiorRangedWeaponRecipes; TemplateId: 155507; VendorId: 77463581; ShopHash: CHHQ (reused); Inventory rows: 325; Capture identity: (VendingMachine:12E3AEE6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSD2ZJQ', 1, 'SuperiorRangedWeaponRecipes', 155507, 'CHHQ', 1, 100);

-- Terminal: Melee Weapon Recipes - Superior
-- NormalizedName: SuperiorMeleeWeaponRecipes; TemplateId: 155504; VendorId: 77463582; ShopHash: OHOO (reused); Inventory rows: 75; Capture identity: (VendingMachine:12E3AEE7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSAGOLB', 1, 'SuperiorMeleeWeaponRecipes', 155504, 'OHOO', 1, 1);

-- Terminal: Clan Superior Attacks
-- NormalizedName: ClanSuperiorAttacks; TemplateId: 99534; VendorId: 77463585; ShopHash: L2PR (new); Inventory rows: 8; Capture identity: (VendingMachine:12E3AEEA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSSD5SY', 1, 'ClanSuperiorAttacks', 99534, 'L2PR', 73, 121);

-- Terminal: Clan Superior Augmentations
-- NormalizedName: ClanSuperiorAugmentations; TemplateId: 99518; VendorId: 77463586; ShopHash: CCN4 (new); Inventory rows: 70; Capture identity: (VendingMachine:12E3AEEB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSXKWKP', 1, 'ClanSuperiorAugmentations', 99518, 'CCN4', 1, 125);

-- Terminal: Clan Superior Medical Supplies
-- NormalizedName: ClanSuperiorMedicalSupplies; TemplateId: 99529; VendorId: 77463587; ShopHash: JYPE (reused); Inventory rows: 40; Capture identity: (VendingMachine:12E3AEEC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSZKPVY', 1, 'ClanSuperiorMedicalSupplies', 99529, 'JYPE', 70, 125);

-- Terminal: Clan Superior Tools
-- NormalizedName: ClanSuperiorTools; TemplateId: 99530; VendorId: 77463588; ShopHash: 3RVO (new); Inventory rows: 19; Capture identity: (VendingMachine:12E3AEED); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSAUZMP', 1, 'ClanSuperiorTools', 99530, '3RVO', 1, 120);

-- Terminal: Clan Superior Weapons
-- NormalizedName: ClanSuperiorWeapons; TemplateId: 99507; VendorId: 77463589; ShopHash: RQRQ (new); Inventory rows: 88; Capture identity: (VendingMachine:12E3AEEE); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CS5JCOM', 1, 'ClanSuperiorWeapons', 99507, 'RQRQ', 1, 125);

-- Terminal: Clan Clothes
-- NormalizedName: ClanSuperiorClothes; TemplateId: 99526; VendorId: 77463590; ShopHash: BUUA (new); Inventory rows: 21; Capture identity: (VendingMachine:12E3AEEF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSOO7JG', 1, 'ClanSuperiorClothes', 99526, 'BUUA', 1, 1);

-- Terminal: Clan Containers
-- NormalizedName: ClanSuperiorContainers; TemplateId: 99540; VendorId: 77463591; ShopHash: Cont (reused); Inventory rows: 62; Capture identity: (VendingMachine:12E3AEF0); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSSOA54', 1, 'ClanSuperiorContainers', 99540, 'Cont', 1, 1);

-- Terminal: Clan Superior Armor
-- NormalizedName: ClanSuperiorArmor; TemplateId: 99504; VendorId: 77463592; ShopHash: 4ATG (new); Inventory rows: 29; Capture identity: (VendingMachine:12E3AEF1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSFKCVG', 1, 'ClanSuperiorArmor', 99504, '4ATG', 73, 121);

-- Terminal: Clan Maps
-- NormalizedName: ClanSuperiorMaps; TemplateId: 117749; VendorId: 77463593; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E3AEF2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CSIHQHX', 1, 'ClanSuperiorMaps', 117749, 'LJI7', 1, 30);

-- Terminal: Clan Superior Devices
-- NormalizedName: ClanSuperiorDevices; TemplateId: 155608; VendorId: 77463594; ShopHash: GVR6 (new); Inventory rows: 26; Capture identity: (VendingMachine:12E3AEF3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CS3Q3IF', 1, 'ClanSuperiorDevices', 155608, 'GVR6', 2, 199);


-- Omni Advanced General Shop import
-- Source: AOSharp capture 20260613-002828
-- Coverage: 276 -> 253 (23 reduction)
-- Excludes: 155225 (non-shop statel template)
-- Reuses existing shop inventory hash: LJI7


-- Terminal: Melee Weapon Recipes - Advanced
-- NormalizedName: AdvancedMeleeWeaponRecipes; TemplateId: 155503; VendorId(s): 77594638, 77594641; ShopHash: IYD4 (new); Inventory rows: 33; Capture identity: (VendingMachine:12E4907E), (VendingMachine:12E49081); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OALQXGA', 1, 'AdvancedMeleeWeaponRecipes', 155503, 'IYD4', 1, 1);

-- Terminal: Ranged Weapon Recipes - Advanced
-- NormalizedName: AdvancedRangedWeaponRecipes; TemplateId: 155506; VendorId(s): 77594639, 77594642; ShopHash: IVM2 (new); Inventory rows: 159; Capture identity: (VendingMachine:12E4907F), (VendingMachine:12E49082); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAIFSRG', 1, 'AdvancedRangedWeaponRecipes', 155506, 'IVM2', 1, 100);

-- Terminal: Melee Weapon Components - Advanced
-- NormalizedName: AdvancedMeleeWeaponComponents; TemplateId: 155297; VendorId(s): 77594646, 77594656; ShopHash: LYQY (new); Inventory rows: 50; Capture identity: (VendingMachine:12E49086), (VendingMachine:12E49090); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAAC3R2', 1, 'AdvancedMeleeWeaponComponents', 155297, 'LYQY', 30, 89);

-- Terminal: Ranged Weapon Components - Advanced
-- NormalizedName: AdvancedRangedWeaponComponents; TemplateId: 155491; VendorId(s): 77594647, 77594657; ShopHash: FJRI (new); Inventory rows: 121; Capture identity: (VendingMachine:12E49087), (VendingMachine:12E49091); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OABOGYY', 1, 'AdvancedRangedWeaponComponents', 155491, 'FJRI', 1, 90);

-- Terminal: Advanced Implants
-- NormalizedName: AdvancedImplants; TemplateId: 155223; VendorId(s): 77594663, 77594669; ShopHash: 2UVY (new); Inventory rows: 38; Capture identity: (VendingMachine:12E49097), (VendingMachine:12E4909D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAG44BS', 1, 'AdvancedImplants', 155223, '2UVY', 30, 89);

-- Terminal: Advanced Melee Weapon Construction Kits
-- NormalizedName: AdvancedMeleeWeaponConstructionKits; TemplateId: 155234; VendorId(s): 77594664, 77594670; ShopHash: GSUE (new); Inventory rows: 38; Capture identity: (VendingMachine:12E49098), (VendingMachine:12E4909E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAXHP7H', 1, 'AdvancedMeleeWeaponConstructionKits', 155234, 'GSUE', 30, 89);

-- Terminal: Advanced Ranged Weapon Construction Kits
-- NormalizedName: AdvancedRangedWeaponConstructionKits; TemplateId: 155282; VendorId(s): 77594665, 77594671; ShopHash: TNCZ (new); Inventory rows: 27; Capture identity: (VendingMachine:12E49099), (VendingMachine:12E4909F); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAKGM6T', 1, 'AdvancedRangedWeaponConstructionKits', 155282, 'TNCZ', 30, 84);

-- Terminal: OT Advanced Armor
-- NormalizedName: OTAdvancedArmor; TemplateId: 99386; VendorId(s): 77594682; ShopHash: L3P7 (new); Inventory rows: 29; Capture identity: (VendingMachine:12E490A9); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAL6IVC', 1, 'OTAdvancedArmor', 99386, 'L3P7', 30, 89);

-- Terminal: OT Advanced Attacks
-- NormalizedName: OTAdvancedAttacks; TemplateId: 99496; VendorId(s): 77594683; ShopHash: TNQE (new); Inventory rows: 7; Capture identity: (VendingMachine:12E490AA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAECAN7', 1, 'OTAdvancedAttacks', 99496, 'TNQE', 30, 100);

-- Terminal: OT Advanced Augmentations
-- NormalizedName: OTAdvancedAugmentations; TemplateId: 99485; VendorId(s): 77594684; ShopHash: JAJA (new); Inventory rows: 70; Capture identity: (VendingMachine:12E490AB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAS6ZPM', 1, 'OTAdvancedAugmentations', 99485, 'JAJA', 1, 90);

-- Terminal: OT Advanced Medical Supplies
-- NormalizedName: OTAdvancedMedicalSupplies; TemplateId: 99482; VendorId(s): 77594685; ShopHash: JTYS (new); Inventory rows: 40; Capture identity: (VendingMachine:12E490AC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAW76SU', 1, 'OTAdvancedMedicalSupplies', 99482, 'JTYS', 20, 90);

-- Terminal: OT Advanced Tools
-- NormalizedName: OTAdvancedTools; TemplateId: 99492; VendorId(s): 77594686; ShopHash: VSQU (new); Inventory rows: 19; Capture identity: (VendingMachine:12E490AD); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAAMXEE', 1, 'OTAdvancedTools', 99492, 'VSQU', 1, 89);

-- Terminal: OT Advanced Weapons
-- NormalizedName: OTAdvancedWeapons; TemplateId: 99479; VendorId(s): 77594687; ShopHash: 7IAS (new); Inventory rows: 88; Capture identity: (VendingMachine:12E490AE); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAE5BNV', 1, 'OTAdvancedWeapons', 99479, '7IAS', 1, 90);

-- Terminal: OT Clothes
-- NormalizedName: OTAdvancedClothes; TemplateId: 99490; VendorId(s): 77594688; ShopHash: 3SQ3 (new); Inventory rows: 15; Capture identity: (VendingMachine:12E490AF); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAFBTI6', 1, 'OTAdvancedClothes', 99490, '3SQ3', 1, 1);

-- Terminal: OT Maps
-- NormalizedName: OTAdvancedMaps; TemplateId: 117649; VendorId(s): 77594690; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E490B1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAA6B2F', 1, 'OTAdvancedMaps', 117649, 'LJI7', 1, 30);

-- Terminal: Omni Advanced Devices
-- NormalizedName: OmniAdvancedDevices; TemplateId: 155606; VendorId(s): 77594692; ShopHash: 66ZZ (new); Inventory rows: 26; Capture identity: (VendingMachine:12E490B3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('OAX2G2O', 1, 'OmniAdvancedDevices', 155606, '66ZZ', 2, 89);


-- Omni Basic Implant Terminals import
-- Source: AOSharp capture 20260613-005616
-- Coverage: 253 -> 240 (13 reduction)
-- Reuse: existing implant shop hashes


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

-- Neutral Basic General/Specialty Shop import
-- Source: AOSharp captures 20260613-012810 and 20260613-014033
-- Coverage: 240 -> 234 (6 reduction)
-- Note: Specialist Commerce required Trader access.


-- Terminal: Computers
-- NormalizedName: NeutralBasicComputers; TemplateId: 99603; VendorId: 78184448; ShopHash: I3E4 (new); Inventory rows: 18; Capture identity: (VendingMachine:12E4ABA8); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBTLELB', 1, 'NeutralBasicComputers', 99603, 'I3E4', 1, 6);

-- Terminal: Advanced Cars
-- NormalizedName: NeutralBasicAdvancedCars; TemplateId: 99635; VendorId: 78184449; ShopHash: 7ATH (new); Inventory rows: 2; Capture identity: (VendingMachine:12E4ABA9); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBBBPWA', 1, 'NeutralBasicAdvancedCars', 99635, '7ATH', 66, 75);

-- Terminal: Furniture
-- NormalizedName: NeutralBasicFurniture; TemplateId: 120512; VendorId: 78184450; ShopHash: 7X7Q (new); Inventory rows: 16; Capture identity: (VendingMachine:12E4ABAA); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NB7LZHA', 1, 'NeutralBasicFurniture', 120512, '7X7Q', 1, 1);

-- Terminal: Toys and Curiosities
-- NormalizedName: NeutralBasicToysAndCuriosities; TemplateId: 151983; VendorId: 78184451; ShopHash: PX4X (new); Inventory rows: 3; Capture identity: (VendingMachine:12E4ABAB); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBM27YC', 1, 'NeutralBasicToysAndCuriosities', 151983, 'PX4X', 1, 1);

-- Terminal: Specialist Commerce
-- NormalizedName: NeutralBasicSpecialistCommerce; TemplateId: 151987; VendorId: 78184452; ShopHash: FBQ3 (new); Inventory rows: 4; Capture identity: (VendingMachine:12E4ABB2); Capture: 20260613-014033; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NBCQ762', 1, 'NeutralBasicSpecialistCommerce', 151987, 'FBQ3', 1, 40);

-- Terminal: Superior Cars
-- NormalizedName: NeutralBasicSuperiorCars; TemplateId: 151988; VendorId: 78184453; ShopHash: FLEW (new); Inventory rows: 21; Capture identity: (VendingMachine:12E4ABAD); Capture: 20260613-012810; VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NB72WE4', 1, 'NeutralBasicSuperiorCars', 151988, 'FLEW', 81, 200);

-- spec_smarket specialty import (inferred)
-- Coverage: 234 -> 218 (16 reduction)
-- Reuse: existing shop hashes only
-- No new inventory groups

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

-- ============================================================
-- Clan Advanced General Shop import
-- Source: AOSharp capture 20260613-034740
-- Coverage: 218 -> 202 (16 reduction)
-- Reuse: Cont, IVM2, IYD4, JTYS, LJI7
-- Vendor templates: 16
-- ============================================================
-- Terminal: Clan Advanced Attacks
-- NormalizedName: ClanAdvancedAttacks; TemplateId: 99533; VendorId: 77398030; ShopHash: CWW5 (new); Inventory rows: 8; Capture identity: (VendingMachine:12E4B059); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAWFVZL', 1, 'ClanAdvancedAttacks', 99533, 'CWW5', 32, 88);

-- Terminal: Clan Advanced Augmentations
-- NormalizedName: ClanAdvancedAugmentations; TemplateId: 99517; VendorId: 77398031; ShopHash: VNZ5 (new); Inventory rows: 70; Capture identity: (VendingMachine:12E4B05A); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAXKPAK', 1, 'ClanAdvancedAugmentations', 99517, 'VNZ5', 1, 90);

-- Terminal: Clan Advanced Medical Supplies
-- NormalizedName: ClanAdvancedMedicalSupplies; TemplateId: 99509; VendorId: 77398032; ShopHash: JTYS (reused); Inventory rows: 40; Capture identity: (VendingMachine:12E4B05B); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAKVRD3', 1, 'ClanAdvancedMedicalSupplies', 99509, 'JTYS', 20, 90);

-- Terminal: Clan Advanced Tools
-- NormalizedName: ClanAdvancedTools; TemplateId: 99528; VendorId: 77398033; ShopHash: 6BWM (new); Inventory rows: 19; Capture identity: (VendingMachine:12E4B05C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CA4ANR3', 1, 'ClanAdvancedTools', 99528, '6BWM', 1, 119);

-- Terminal: Clan Advanced Weapons
-- NormalizedName: ClanAdvancedWeapons; TemplateId: 99506; VendorId: 77398034; ShopHash: 4GTH (new); Inventory rows: 88; Capture identity: (VendingMachine:12E4B05D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAIYRLU', 1, 'ClanAdvancedWeapons', 99506, '4GTH', 1, 90);

-- Terminal: Clan Clothes
-- NormalizedName: ClanAdvancedClothes; TemplateId: 99526; VendorId: 77398035; ShopHash: VECC (new); Inventory rows: 21; Capture identity: (VendingMachine:12E4B05E); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAOIGJE', 1, 'ClanAdvancedClothes', 99526, 'VECC', 1, 1);

-- Terminal: Clan Containers
-- NormalizedName: ClanAdvancedContainers; TemplateId: 99540; VendorId: 77398036; ShopHash: Cont (reused); Inventory rows: 62; Capture identity: (VendingMachine:12E4B05F); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAE4PJN', 1, 'ClanAdvancedContainers', 99540, 'Cont', 1, 1);

-- Terminal: Clan Maps
-- NormalizedName: ClanAdvancedMaps; TemplateId: 117749; VendorId: 77398037; ShopHash: LJI7 (reused); Inventory rows: 2; Capture identity: (VendingMachine:12E4B060); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CANXN6U', 1, 'ClanAdvancedMaps', 117749, 'LJI7', 1, 30);

-- Terminal: Clan Advanced Devices
-- NormalizedName: ClanAdvancedDevices; TemplateId: 155605; VendorId: 77398039; ShopHash: SDYB (new); Inventory rows: 27; Capture identity: (VendingMachine:12E4B062); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CASMUGY', 1, 'ClanAdvancedDevices', 155605, 'SDYB', 2, 85);

-- Terminal: Advanced Ranged Weapon Construction Kits
-- NormalizedName: AdvancedRangedWeaponConstructionKits; TemplateId: 155282; VendorId: 77398040; ShopHash: 2S24 (new); Inventory rows: 27; Capture identity: (VendingMachine:12E4B063); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAKGM6T', 1, 'AdvancedRangedWeaponConstructionKits', 155282, '2S24', 33, 87);

-- Terminal: Advanced Melee Weapon Construction Kits
-- NormalizedName: AdvancedMeleeWeaponConstructionKits; TemplateId: 155234; VendorId: 77398042; ShopHash: 4R2H (new); Inventory rows: 38; Capture identity: (VendingMachine:12E4B065); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAXHP7H', 1, 'AdvancedMeleeWeaponConstructionKits', 155234, '4R2H', 30, 89);

-- Terminal: Advanced Implants
-- NormalizedName: AdvancedImplants; TemplateId: 155223; VendorId: 77398044; ShopHash: EHGI (new); Inventory rows: 38; Capture identity: (VendingMachine:12E4B067); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAG44BS', 1, 'AdvancedImplants', 155223, 'EHGI', 30, 89);

-- Terminal: Melee Weapon Components - Advanced
-- NormalizedName: AdvancedMeleeWeaponComponents; TemplateId: 155297; VendorId: 77398049; ShopHash: M4DY (new); Inventory rows: 50; Capture identity: (VendingMachine:12E4B06C); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAAC3R2', 1, 'AdvancedMeleeWeaponComponents', 155297, 'M4DY', 30, 88);

-- Terminal: Ranged Weapon Components - Advanced
-- NormalizedName: AdvancedRangedWeaponComponents; TemplateId: 155491; VendorId: 77398050; ShopHash: B6S3 (new); Inventory rows: 119; Capture identity: (VendingMachine:12E4B06D); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CABOGYY', 1, 'AdvancedRangedWeaponComponents', 155491, 'B6S3', 1, 90);

-- Terminal: Melee Weapon Recipes - Advanced
-- NormalizedName: AdvancedMeleeWeaponRecipes; TemplateId: 155503; VendorId: 77398056; ShopHash: IYD4 (reused); Inventory rows: 33; Capture identity: (VendingMachine:12E4B073); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CALQXGA', 1, 'AdvancedMeleeWeaponRecipes', 155503, 'IYD4', 1, 1);

-- Terminal: Ranged Weapon Recipes - Advanced
-- NormalizedName: AdvancedRangedWeaponRecipes; TemplateId: 155506; VendorId: 77398058; ShopHash: IVM2 (reused); Inventory rows: 159; Capture identity: (VendingMachine:12E4B075); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('CAIFSRG', 1, 'AdvancedRangedWeaponRecipes', 155506, 'IVM2', 1, 100);

-- ============================================================
-- Neutral ICC implant/cluster import
-- Source: AOSharp capture 20260613-170220
-- Captured: 2064 neut_basic_implants_shop
-- Exact-template reuse: 2073 neut_advanced_implants_shop
-- Coverage: 171 -> 147 (24 reduction)
-- Vendor templates: 12
-- ============================================================
-- Terminal: Basic ICC Implants
-- NormalizedName: NeutralBasicICCImplants; TemplateId: 297396; ShopHash: XZXX (new); Inventory rows: 143; Capture identity: (VendingMachine:12D3D4F0); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NITV2DU', 1, 'NeutralBasicICCImplants', 297396, 'XZXX', 5, 100);

-- Terminal: Basic ICC Faded Clusters
-- NormalizedName: NeutralBasicICCFadedClusters; TemplateId: 297399; ShopHash: MSBV (new); Inventory rows: 274; Capture identity: (VendingMachine:12D3D4F1); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIIOYTU', 1, 'NeutralBasicICCFadedClusters', 297399, 'MSBV', 25, 100);

-- Terminal: Basic ICC Bright Clusters
-- NormalizedName: NeutralBasicICCBrightClusters; TemplateId: 297402; ShopHash: KMMP (new); Inventory rows: 274; Capture identity: (VendingMachine:12D3D4F2); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NI4JLQO', 1, 'NeutralBasicICCBrightClusters', 297402, 'KMMP', 25, 100);

-- Terminal: Basic ICC Shiny Clusters
-- NormalizedName: NeutralBasicICCShinyClusters; TemplateId: 297405; ShopHash: LQBF (new); Inventory rows: 274; Capture identity: (VendingMachine:12D3D4F3); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIKTHHA', 1, 'NeutralBasicICCShinyClusters', 297405, 'LQBF', 25, 100);

-- Terminal: Advanced ICC Implants
-- NormalizedName: NeutralAdvancedICCImplants; TemplateId: 297397; ShopHash: XNTQ (new); Inventory rows: 130; Capture identity: (VendingMachine:12D3D4F5); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIKUGMU', 1, 'NeutralAdvancedICCImplants', 297397, 'XNTQ', 110, 200);

-- Terminal: Advanced ICC Faded Clusters
-- NormalizedName: NeutralAdvancedICCFadedClusters; TemplateId: 297400; ShopHash: VUI3 (new); Inventory rows: 108; Capture identity: (VendingMachine:12D3D4F6); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIJIZJC', 1, 'NeutralAdvancedICCFadedClusters', 297400, 'VUI3', 200, 200);

-- Terminal: Advanced ICC Bright Clusters
-- NormalizedName: NeutralAdvancedICCBrightClusters; TemplateId: 297403; ShopHash: LZWI (new); Inventory rows: 108; Capture identity: (VendingMachine:12D3D4F7); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NI2ZU2M', 1, 'NeutralAdvancedICCBrightClusters', 297403, 'LZWI', 200, 200);

-- Terminal: Advanced ICC Shiny Clusters
-- NormalizedName: NeutralAdvancedICCShinyClusters; TemplateId: 297406; ShopHash: YRLY (new); Inventory rows: 108; Capture identity: (VendingMachine:12D3D4F8); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIXARAY', 1, 'NeutralAdvancedICCShinyClusters', 297406, 'YRLY', 200, 200);

-- Terminal: Refined ICC Implants
-- NormalizedName: NeutralRefinedICCImplants; TemplateId: 297398; ShopHash: AN3B (new); Inventory rows: 130; Capture identity: (VendingMachine:12D3D4FA); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIGQ25C', 1, 'NeutralRefinedICCImplants', 297398, 'AN3B', 210, 300);

-- Terminal: Refined ICC Faded Clusters
-- NormalizedName: NeutralRefinedICCFadedClusters; TemplateId: 297401; ShopHash: AQDG (new); Inventory rows: 109; Capture identity: (VendingMachine:12D3D4FB); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NICEGHE', 1, 'NeutralRefinedICCFadedClusters', 297401, 'AQDG', 300, 300);

-- Terminal: Refined ICC Bright Clusters
-- NormalizedName: NeutralRefinedICCBrightClusters; TemplateId: 297404; ShopHash: CF73 (new); Inventory rows: 109; Capture identity: (VendingMachine:12D3D4FC); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NIVUZHQ', 1, 'NeutralRefinedICCBrightClusters', 297404, 'CF73', 300, 300);

-- Terminal: Refined ICC Shiny Clusters
-- NormalizedName: NeutralRefinedICCShinyClusters; TemplateId: 297407; ShopHash: N5PM (new); Inventory rows: 109; Capture identity: (VendingMachine:12D3D4FD); VT window: 0
INSERT INTO `vendortemplate` (`HASH`, `lvl`, `Name`, `itemtemplate`, `ShopInvHash`, `minQL`, `maxQL`) VALUES ('NILZLFQ', 1, 'NeutralRefinedICCShinyClusters', 297407, 'N5PM', 300, 300);
