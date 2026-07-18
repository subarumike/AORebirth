CREATE TABLE `missionobjectiveobservations` (
    `Id` INT(32) NOT NULL AUTO_INCREMENT,
    `CharacterId` INT(32) NOT NULL,
    `QuestId` VARCHAR(128) NOT NULL,
    `ObjectiveId` VARCHAR(128) NOT NULL,
    `ObservationKey` VARCHAR(191) NOT NULL,
    `EventType` VARCHAR(64) NOT NULL,
    `SourceIdentity` VARCHAR(64) NULL,
    `TargetIdentity` VARCHAR(64) NULL,
    `ObservedAtUtcTicks` BIGINT(20) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `character_quest_objective_observation`
        (`CharacterId`, `QuestId`, `ObjectiveId`, `ObservationKey`),
    INDEX `character_quest` (`CharacterId`, `QuestId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
