CREATE TABLE `character_bio_meta` (
	`id_char` INT(32) UNSIGNED NOT NULL PRIMARY KEY,
	`last_updated` DATETIME(6) NOT NULL,
	INDEX `ix_character_bio_meta_last_updated` (`last_updated`)
)
COMMENT='Last UTC change of a published field of a character (bio XML, JSON and HTML)'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
