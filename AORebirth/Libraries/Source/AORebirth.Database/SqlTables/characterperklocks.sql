CREATE TABLE IF NOT EXISTS `characterperklocks` (
	`CharacterId` INT(32) NOT NULL,
	`PerkId` INT(32) NOT NULL,
	`ExpiresAtUtcTicks` BIGINT(20) NOT NULL,
	PRIMARY KEY (`CharacterId`, `PerkId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
