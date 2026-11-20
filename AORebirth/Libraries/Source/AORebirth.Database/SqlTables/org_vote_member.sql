CREATE TABLE `org_vote_member` (
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
