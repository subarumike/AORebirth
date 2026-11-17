-- Change tracking for the PORK-compatible bio API (docs/api/character-bio-api.md, sections 5.5, 6 and 12.2).
-- Written by the shared database layer when a published field changes; read by the PorkForAor exporter, which never writes them.
-- A missing row means "never touched": the exporter falls back to its daily sweep.
CREATE TABLE IF NOT EXISTS `character_bio_meta` (
	`id_char` INT(32) UNSIGNED NOT NULL PRIMARY KEY,
	`last_updated` DATETIME(6) NOT NULL,
	INDEX `ix_character_bio_meta_last_updated` (`last_updated`)
)
COMMENT='Last UTC change of a published field of a character (bio XML, JSON and HTML)'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `org_stats_meta` (
	`id_org` INT(32) UNSIGNED NOT NULL PRIMARY KEY,
	`last_updated` DATETIME(6) NOT NULL,
	INDEX `ix_org_stats_meta_last_updated` (`last_updated`)
)
COMMENT='Last UTC change of a published field of an organization stats page'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `org_online_meta` (
	`id_org` INT(32) UNSIGNED NOT NULL PRIMARY KEY,
	`last_updated` DATETIME(6) NOT NULL,
	INDEX `ix_org_online_meta_last_updated` (`last_updated`)
)
COMMENT='Last UTC logon, logoff or membership change of an organization, for the online lists'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
