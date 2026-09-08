-- Shared InstanceId lease counter for multi-engine-safe allocation.
-- Safe to re-run: creates table/row if missing, then advances past existing item_instances.

CREATE TABLE IF NOT EXISTS `item_instance_id_sequence` (
	`Id` TINYINT NOT NULL,
	`NextInstanceId` INT(32) NOT NULL,
	PRIMARY KEY (`Id`)
)
COMMENT='Shared InstanceId lease counter for ZoneEngine processes'
COLLATE='latin1_general_ci'
ENGINE=InnoDB;

INSERT IGNORE INTO `item_instance_id_sequence` (`Id`, `NextInstanceId`)
VALUES (1, 1);

UPDATE `item_instance_id_sequence` AS seq
SET seq.`NextInstanceId` = GREATEST(
	seq.`NextInstanceId`,
	(SELECT COALESCE(MAX(`InstanceId`), 0) + 1 FROM `item_instances`)
)
WHERE seq.`Id` = 1;
