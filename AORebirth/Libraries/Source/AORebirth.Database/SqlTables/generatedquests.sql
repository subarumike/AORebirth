-- Generated quests: one row per quest, in Quests.json format, shared by everyone assigned to it.
-- Owns the expiry and optional ACG building generator data. OwnerType: 0 character, 1 team.

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
