-- Player quests: generated quest definitions and per-character quest state. Additive and safe to re-run.

CREATE TABLE IF NOT EXISTS `generatedquests` (
	`QuestId` VARCHAR(32) NOT NULL,
	`OwnerType` INT(32) NOT NULL,
	`OwnerId` INT(32) NOT NULL,
	`DefinitionJson` MEDIUMTEXT NOT NULL,
	`AcgBuildingGeneratorJson` MEDIUMTEXT NULL,
	`CreatedAtUtcTicks` BIGINT(20) NOT NULL,
	`ExpiresAtUtcTicks` BIGINT(20) NOT NULL,
	`UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
	PRIMARY KEY (`QuestId`),
	INDEX `owner` (`OwnerType`, `OwnerId`),
	INDEX `expires` (`ExpiresAtUtcTicks`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `characterquests` (
	`CharacterId` INT(32) NOT NULL,
	`QuestId` VARCHAR(32) NOT NULL,
	`Source` INT(32) NOT NULL,
	`State` INT(32) NOT NULL,
	`Progress` INT(32) NOT NULL DEFAULT 0,
	`RequiredCount` INT(32) NOT NULL DEFAULT 0,
	`AssignedAtUtcTicks` BIGINT(20) NOT NULL,
	`UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
	PRIMARY KEY (`CharacterId`, `QuestId`),
	INDEX `quest` (`QuestId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
