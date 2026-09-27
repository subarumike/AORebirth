-- Per-player quest state: one row per character and quest.
-- QuestId is the Quests.json hash (Source 0, template) or a generatedquests id (Source 1, generated).
-- The quest itself lives in Quests.json or generatedquests. State: 1 active, 2 completed, 3 failed, 4 expired.

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
