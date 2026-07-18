CREATE TABLE `missionobjectiveprogress` (
    `Id` INT(32) NOT NULL AUTO_INCREMENT,
    `CharacterId` INT(32) NOT NULL,
    `QuestId` VARCHAR(128) NOT NULL,
    `ObjectiveId` VARCHAR(128) NOT NULL,
    `Progress` INT(32) NOT NULL DEFAULT 0,
    `RequiredCount` INT(32) NOT NULL DEFAULT 0,
    `LastObservationKey` VARCHAR(191) NULL,
    `CreatedAtUtcTicks` BIGINT(20) NOT NULL,
    `UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
    `Version` BIGINT(20) NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `character_quest_objective` (`CharacterId`, `QuestId`, `ObjectiveId`),
    INDEX `character_quest` (`CharacterId`, `QuestId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
