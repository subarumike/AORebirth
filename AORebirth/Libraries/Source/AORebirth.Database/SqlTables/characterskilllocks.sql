CREATE TABLE IF NOT EXISTS `characterskilllocks` (
	`CharacterId` INT(32) NOT NULL,
	`StatId` INT(32) NOT NULL,
	`ExpiresAtUtcTicks` BIGINT(20) NOT NULL,
	PRIMARY KEY (`CharacterId`, `StatId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
