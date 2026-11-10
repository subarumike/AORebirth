CREATE TABLE `org_anar_pend_kick` (
	`org_id` INT(32) NOT NULL,
	`kicked_id` INT(32) NOT NULL,
	`kicker_id` INT(32) NOT NULL,
	`kicked_name` VARCHAR(32) NULL,
	`kicker_name` VARCHAR(32) NULL,
	`requested_at` BIGINT(20) NOT NULL,
	`last_updated` BIGINT(20) NOT NULL,
	PRIMARY KEY (`org_id`, `kicked_id`, `kicker_id`)
)
COMMENT='Pending /org kick requests in an Anarchism organization: 2 distinct kickers exclude a member, 3 a leader'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
