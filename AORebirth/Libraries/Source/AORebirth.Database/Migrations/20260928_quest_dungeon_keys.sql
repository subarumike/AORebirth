-- Mission keys for quest dungeons: each key item instance opens one generated quest's dungeon. Rows are deleted when the quest ends. Additive and safe to re-run.

CREATE TABLE IF NOT EXISTS `questdungeonkeys` (
	`KeyInstanceId` INT(32) NOT NULL,
	`QuestId` VARCHAR(32) NOT NULL,
	`CreatedAtUtcTicks` BIGINT(20) NOT NULL,
	PRIMARY KEY (`KeyInstanceId`),
	INDEX `quest` (`QuestId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
