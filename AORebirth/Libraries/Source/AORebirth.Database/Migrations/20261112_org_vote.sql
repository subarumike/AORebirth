-- Organization votes started by the leader or a GM. A stopped vote is kept only to enforce the 30 minute cooldown and is never displayed.
-- Members are snapshotted at start (online or offline); a member who leaves, is kicked or is suspended keeps a vote cast before that.
CREATE TABLE IF NOT EXISTS `org_vote` (
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

-- Choices of a vote, numbered 1 to 10 in the order given by the leader.
CREATE TABLE IF NOT EXISTS `org_vote_choice` (
	`vote_id` INT(32) NOT NULL,
	`choice_no` TINYINT(3) UNSIGNED NOT NULL,
	`label` VARCHAR(64) NOT NULL,
	PRIMARY KEY (`vote_id`, `choice_no`),
	CONSTRAINT `CK_org_vote_choice_no` CHECK (`choice_no` BETWEEN 1 AND 10),
	CONSTRAINT `FK_org_vote_choice_vote` FOREIGN KEY (`vote_id`) REFERENCES `org_vote` (`vote_id`) ON DELETE CASCADE ON UPDATE RESTRICT
)
COMMENT='Choices of an organization vote'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;

-- Eligible members snapshotted when the vote starts. choice_no and cast_at stay NULL until the member votes; a vote is never changed.
CREATE TABLE IF NOT EXISTS `org_vote_member` (
	`vote_id` INT(32) NOT NULL,
	`char_id` INT(32) NOT NULL,
	`char_name` VARCHAR(32) NULL,
	`choice_no` TINYINT(3) UNSIGNED NULL,
	`cast_at` BIGINT(20) NULL,
	PRIMARY KEY (`vote_id`, `char_id`),
	CONSTRAINT `CK_org_vote_member_choice_no` CHECK (`choice_no` IS NULL OR `choice_no` BETWEEN 1 AND 10),
	CONSTRAINT `FK_org_vote_member_vote` FOREIGN KEY (`vote_id`) REFERENCES `org_vote` (`vote_id`) ON DELETE CASCADE ON UPDATE RESTRICT
)
COMMENT='Organization vote electorate snapshot and cast votes'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
