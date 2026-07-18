CREATE TABLE `missionaccountflags` (
    `Id` INT(32) NOT NULL AUTO_INCREMENT,
    `AccountKey` VARCHAR(32) NOT NULL,
    `FlagKey` VARCHAR(128) NOT NULL,
    `Value` VARCHAR(1024) NULL,
    `SourceQuestId` VARCHAR(128) NULL,
    `CreatedAtUtcTicks` BIGINT(20) NOT NULL,
    `UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
    `Version` BIGINT(20) NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `account_flag` (`AccountKey`, `FlagKey`),
    INDEX `account` (`AccountKey`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
