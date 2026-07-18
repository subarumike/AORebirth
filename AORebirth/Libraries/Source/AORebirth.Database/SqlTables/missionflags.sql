CREATE TABLE `missionflags` (
    `Id` INT(32) NOT NULL AUTO_INCREMENT,
    `CharacterId` INT(32) NOT NULL,
    `QuestId` VARCHAR(128) NOT NULL,
    `FlagKey` VARCHAR(128) NOT NULL,
    `Value` VARCHAR(1024) NULL,
    `CreatedAtUtcTicks` BIGINT(20) NOT NULL,
    `UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
    `Version` BIGINT(20) NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `character_quest_flag` (`CharacterId`, `QuestId`, `FlagKey`),
    INDEX `character_quest` (`CharacterId`, `QuestId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
