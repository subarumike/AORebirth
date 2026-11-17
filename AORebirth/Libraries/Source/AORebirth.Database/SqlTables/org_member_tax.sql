CREATE TABLE `org_member_tax` (
	`char_id` INT(32) NOT NULL,
	`org_id` INT(32) NOT NULL,
	`char_name` VARCHAR(32) NULL,
	`next_tax_played` BIGINT(20) NOT NULL,
	`debt` BIGINT(20) NOT NULL,
	`stage` VARCHAR(16) NOT NULL,
	`stage_ends_utc` BIGINT(20) NOT NULL,
	`last_updated` BIGINT(20) NOT NULL,
	PRIMARY KEY (`char_id`),
	CONSTRAINT `CK_org_member_tax_stage` CHECK (`stage` IN ('None', 'Warned', 'Suspended'))
)
COMMENT='Organization tax schedule, upkeep warning/suspension stage and unpaid tax debt per member'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
