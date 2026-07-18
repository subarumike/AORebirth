CREATE TABLE `missionrewardledger` (
    `Id` INT(32) NOT NULL AUTO_INCREMENT,
    `CharacterId` INT(32) NOT NULL,
    `QuestId` VARCHAR(128) NOT NULL,
    `RewardKey` VARCHAR(191) NOT NULL,
    `RewardType` VARCHAR(64) NOT NULL,
    `Status` INT(32) NOT NULL,
    `Attempts` INT(32) NOT NULL DEFAULT 0,
    `EffectReference` VARCHAR(255) NULL,
    `LastError` VARCHAR(1024) NULL,
    `ClaimToken` VARCHAR(64) NULL,
    `ClaimedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `ClaimExpiresAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `AppliedAtUtcTicks` BIGINT(20) NOT NULL DEFAULT 0,
    `CreatedAtUtcTicks` BIGINT(20) NOT NULL,
    `UpdatedAtUtcTicks` BIGINT(20) NOT NULL,
    `Version` BIGINT(20) NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `character_quest_reward` (`CharacterId`, `QuestId`, `RewardKey`),
    INDEX `character_quest` (`CharacterId`, `QuestId`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
