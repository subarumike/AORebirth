CREATE TABLE `org_stats_meta` (
	`id_org` INT(32) UNSIGNED NOT NULL PRIMARY KEY,
	`last_updated` DATETIME(6) NOT NULL,
	INDEX `ix_org_stats_meta_last_updated` (`last_updated`)
)
COMMENT='Last UTC change of a published field of an organization stats page'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
