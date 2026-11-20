CREATE TABLE `org_vote` (
	`vote_id` INT(32) AUTO_INCREMENT PRIMARY KEY,
	`org_id` INT(32) UNSIGNED NOT NULL,
	`description` VARCHAR(255) NOT NULL,
	`duration_minutes` INT(32) NOT NULL,
	`started_utc` BIGINT(20) NOT NULL,
	`ends_utc` BIGINT(20) NOT NULL,
	`closed_utc` BIGINT(20) NULL,
	`status` VARCHAR(16) NOT NULL,
	`stop_reason` VARCHAR(255) NULL,
	`started_by_id` INT(32) NOT NULL,
	`last_updated` BIGINT(20) NOT NULL,
	INDEX `IX_org_vote_org_status` (`org_id`, `status`),
	CONSTRAINT `CK_org_vote_status` CHECK (`status` IN ('Active', 'Finished', 'Stopped')),
	CONSTRAINT `FK_org_vote_organization` FOREIGN KEY (`org_id`) REFERENCES `organizations` (`Id`) ON DELETE CASCADE ON UPDATE RESTRICT
)
COMMENT='Organization votes: description, timing, status and stop reason'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
