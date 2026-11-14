CREATE TABLE `org_vote_choice` (
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
