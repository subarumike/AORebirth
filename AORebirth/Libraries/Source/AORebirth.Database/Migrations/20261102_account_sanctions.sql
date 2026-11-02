-- Additive account sanction ledger: timed suspensions (EndsAtUtc set) and permanent
-- bans (EndsAtUtc NULL). A row blocks login while LiftedAtUtc is NULL and it has not
-- ended; lifting keeps the row as history. Existing login.Flags bans are untouched
-- and still block; this migration never changes an account.
CREATE TABLE IF NOT EXISTS `account_sanctions` (
    `SanctionId` BIGINT NOT NULL AUTO_INCREMENT,
    `AccountId` INT NOT NULL,
    `IssuedAtUtc` DATETIME(6) NOT NULL,
    `EndsAtUtc` DATETIME(6) NULL,
    `IssuedBy` VARCHAR(64) NOT NULL,
    `Reason` VARCHAR(255) NOT NULL DEFAULT '',
    `LiftedAtUtc` DATETIME(6) NULL,
    `LiftedBy` VARCHAR(64) NULL,
    `LiftReason` VARCHAR(255) NULL,
    PRIMARY KEY (`SanctionId`),
    KEY `IX_account_sanctions_active` (`AccountId`, `LiftedAtUtc`, `EndsAtUtc`),
    CONSTRAINT `CK_account_sanctions_ends` CHECK (`EndsAtUtc` IS NULL OR `EndsAtUtc` > `IssuedAtUtc`),
    CONSTRAINT `CK_account_sanctions_lifted` CHECK ((`LiftedAtUtc` IS NULL) = (`LiftedBy` IS NULL))
)
COMMENT='Account suspensions (EndsAtUtc set) and bans (EndsAtUtc NULL); active while not lifted and not ended'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
