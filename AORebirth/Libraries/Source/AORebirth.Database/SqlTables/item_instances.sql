CREATE TABLE `item_instances` (
	`InstanceId` INT(32) NOT NULL AUTO_INCREMENT,
	`ContainerType` INT(32) NOT NULL,
	`ContainerInstance` INT(32) NOT NULL,
	`ContainerPlacement` INT(32) NOT NULL,
	`ItemType` INT(32) NOT NULL DEFAULT 0,
	`LowId` INT(32) NOT NULL,
	`HighId` INT(32) NOT NULL,
	`Quality` INT(32) NOT NULL,
	`StackCount` INT(32) NOT NULL,
	PRIMARY KEY (`InstanceId`),
	UNIQUE INDEX `UX_item_instances_location` (`ContainerType`, `ContainerInstance`, `ContainerPlacement`),
	INDEX `IX_item_instances_parent` (`ContainerType`, `ContainerInstance`)
)
COMMENT='Unified item instances (carried pages, bank, backpack interiors)'
COLLATE='latin1_general_ci'
ENGINE=InnoDB;
