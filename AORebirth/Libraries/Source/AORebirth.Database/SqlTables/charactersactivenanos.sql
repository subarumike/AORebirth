CREATE TABLE  `charactersactivenanos` (
	`Id` int(32) NOT NULL AUTO_INCREMENT,
	`CharacterId` int(32) NOT NULL,
	`NanoId` int(32) unsigned NOT NULL,
	`Strain` int(32) unsigned NOT NULL,
	`NanoInstance` int(32) NOT NULL DEFAULT 0,
	`DurationCentiseconds` int(32) NOT NULL DEFAULT 0,
	`ExpiresAtUtcTicks` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
