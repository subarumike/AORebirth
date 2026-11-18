CREATE TABLE `org_online_meta` (
	`id_org` INT(32) UNSIGNED NOT NULL PRIMARY KEY,
	`last_updated` DATETIME(6) NOT NULL,
	INDEX `ix_org_online_meta_last_updated` (`last_updated`)
)
COMMENT='Last UTC logon, logoff or membership change of an organization, for the online lists'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
