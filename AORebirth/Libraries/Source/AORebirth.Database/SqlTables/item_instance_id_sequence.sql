CREATE TABLE `item_instance_id_sequence` (
	`Id` TINYINT NOT NULL,
	`NextInstanceId` INT(32) NOT NULL,
	PRIMARY KEY (`Id`)
)
COMMENT='Shared InstanceId lease counter for ZoneEngine processes'
COLLATE='latin1_general_ci'
ENGINE=InnoDB;

INSERT INTO `item_instance_id_sequence` (`Id`, `NextInstanceId`)
VALUES (1, 1);
