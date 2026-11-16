-- Per-member organization tax schedule, upkeep stage and persistent debt.
-- One row per member that has a tax schedule. The row is removed on leave, kick or disband.
CREATE TABLE IF NOT EXISTS `org_member_tax` (
	`char_id` INT(32) PRIMARY KEY,
	`org_id` INT(32) NOT NULL,
	`char_name` VARCHAR(32),
	`next_tax_played` BIGINT(20) NOT NULL,
	`debt` BIGINT(20) NOT NULL,
	`stage` VARCHAR(16) NOT NULL,
	`stage_ends_utc` BIGINT(20) NOT NULL,
	`last_updated` BIGINT(20) NOT NULL,
	CONSTRAINT `CK_org_member_tax_stage` CHECK (`stage` IN ('None', 'Warned', 'Suspended'))
)
COMMENT='Organization tax schedule, upkeep warning/suspension stage and unpaid tax debt per member'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
