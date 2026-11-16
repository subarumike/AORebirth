-- Played time (/played) and creation time (/born) per character. Characters created before this migration
-- keep created_at NULL until their first world entry records it.
CREATE TABLE IF NOT EXISTS `characterplayedborn` (
	`char_id` INT(32) PRIMARY KEY,
	`char_name` VARCHAR(32),
	`played_sec` BIGINT(20) NOT NULL DEFAULT 0,
	`created_at` BIGINT(20) NULL
)
COMMENT='Character played seconds (/played) and creation time in Unix seconds UTC (/born), NULL when never recorded'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
