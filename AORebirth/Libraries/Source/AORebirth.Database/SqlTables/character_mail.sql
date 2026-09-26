CREATE TABLE IF NOT EXISTS `character_mail` (
	`MailId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
	`SenderCharacterId` INT NOT NULL DEFAULT 0,
	`SenderName` VARCHAR(32) NOT NULL,
	`RecipientCharacterId` INT NOT NULL,
	`RecipientName` VARCHAR(32) NOT NULL,
	`Subject` VARCHAR(255) NOT NULL DEFAULT '',
	`Body` MEDIUMTEXT NOT NULL,
	`AcgLow` INT NOT NULL DEFAULT 0,
	`AcgHigh` INT NOT NULL DEFAULT 0,
	`AcgLevel` INT NOT NULL DEFAULT 0,
	`AcgMultipleCount` INT NOT NULL DEFAULT 0,
	`Credits` INT NOT NULL DEFAULT 0,
	`ExpressFlag` TINYINT UNSIGNED NOT NULL DEFAULT 0,
	`IsRead` TINYINT(1) NOT NULL DEFAULT 0,
	`SentAtUtc` DATETIME(6) NOT NULL,
	`ExpiresAtUtc` DATETIME(6) NOT NULL,
	PRIMARY KEY (`MailId`),
	INDEX `IX_character_mail_recipient_expires` (`RecipientCharacterId`, `ExpiresAtUtc`),
	INDEX `IX_character_mail_recipient_unread` (`RecipientCharacterId`, `IsRead`)
)
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
