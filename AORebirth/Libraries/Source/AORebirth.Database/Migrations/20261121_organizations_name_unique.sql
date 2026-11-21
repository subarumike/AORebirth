-- Organization names: retail allows 40 characters, and a name must be unique (case-insensitive, like the table collation).
-- Back up the database first. The migration stops without changing anything when two organizations already share a name;
-- resolve those rows manually, then run it again. It can be re-run safely.
DROP PROCEDURE IF EXISTS `aorebirth_organizations_name_unique`;
CREATE PROCEDURE `aorebirth_organizations_name_unique`()
BEGIN
	IF EXISTS (SELECT 1 FROM organizations GROUP BY Name HAVING COUNT(*) > 1) THEN
		SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='organizations contains duplicate names; resolve them before adding the unique key';
	END IF;
	ALTER TABLE `organizations` MODIFY COLUMN `Name` varchar(40) NOT NULL;
	IF NOT EXISTS (SELECT INDEX_NAME FROM information_schema.STATISTICS
		WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='organizations' AND NON_UNIQUE=0
		GROUP BY INDEX_NAME HAVING COUNT(*)=1 AND MAX(COLUMN_NAME)='Name') THEN
		ALTER TABLE `organizations` ADD UNIQUE KEY `UX_organizations_name` (`Name`);
	END IF;
END;
CALL `aorebirth_organizations_name_unique`();
DROP PROCEDURE `aorebirth_organizations_name_unique`;
