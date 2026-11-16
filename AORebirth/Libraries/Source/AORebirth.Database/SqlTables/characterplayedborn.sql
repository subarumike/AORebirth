CREATE TABLE `characterplayedborn` (
	`char_id` INT(32) NOT NULL,
	`char_name` VARCHAR(32) NULL,
	`played_sec` BIGINT(20) NOT NULL DEFAULT 0,
	`created_at` BIGINT(20) NULL,
	PRIMARY KEY (`char_id`)
)
COMMENT='Character played seconds (/played) and creation time in Unix seconds UTC (/born), NULL when never recorded'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
