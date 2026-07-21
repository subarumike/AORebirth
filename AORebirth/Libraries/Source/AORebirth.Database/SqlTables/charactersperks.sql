CREATE TABLE  `charactersperks` (
	`Id` int(32) NOT NULL AUTO_INCREMENT,
	`CharacterId` int(32) NOT NULL,
	`PacketId` int(11) NOT NULL,
	PRIMARY KEY (`Id`),
	INDEX `Perks` (`CharacterId`, `PacketId`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
