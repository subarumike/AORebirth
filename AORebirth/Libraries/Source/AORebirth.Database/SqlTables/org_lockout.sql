CREATE TABLE `org_lockout` (
	`char_id` INT(32) NOT NULL,
	`char_name` VARCHAR(32) NULL,
	`seconds_remaining` BIGINT(20) NOT NULL,
	`last_updated` BIGINT(20) NOT NULL,
	PRIMARY KEY (`char_id`)
)
COMMENT='Character lockout for new /org invite after either kick or leave'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
