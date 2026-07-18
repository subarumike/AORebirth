CREATE TABLE `missionstates` (
    `Id` INT(32) NOT NULL AUTO_INCREMENT,
    `CharacterId` INT(32) NOT NULL,
    `QuestId` VARCHAR(128) NOT NULL,
    `State` INT(32) NOT NULL,
    `CurrentStepId` VARCHAR(128) NULL,
    `OfferedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `AcceptedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `CompletedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `FailedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `AbandonedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `CreatedAtUtcTicks` BIGINT(20) NOT NULL,
    `UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
    `Version` BIGINT(20) NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `character_quest` (`CharacterId`, `QuestId`),
    INDEX `character` (`CharacterId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
