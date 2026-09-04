-- Unified item_instances table + one-time copy from legacy items / instanceditems.
--
-- Apply after backup. Safe to re-run: skips when item_instances already has rows.
-- Keeps legacy items / instanceditems intact for ZoneEngine.
--
-- Location encoding (ZoneEngine_New target):
--   Character pages / bank: ContainerType = page IdentityType, ContainerInstance = characterId
--   Backpack interior:      ContainerType = Container (0xC749), ContainerInstance = bag InstanceId
-- Legacy carried/bank rows had those two columns inverted; migration swaps them.
-- Stats / pose columns are intentionally not migrated.

CREATE TABLE IF NOT EXISTS `item_instances` (
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

-- Skip copy if already populated.
SET @item_instances_count := (SELECT COUNT(*) FROM `item_instances`);

-- Character-owned page IdentityType values used for swap detection.
-- WeaponPage=101, ArmorPage=102, ImplantPage=103, Inventory=104, Bank=105, SocialPage=115
-- Container=51017 (0xC749) interiors are left as-is.

INSERT INTO `item_instances` (
	`InstanceId`,
	`ContainerType`,
	`ContainerInstance`,
	`ContainerPlacement`,
	`ItemType`,
	`LowId`,
	`HighId`,
	`Quality`,
	`StackCount`
)
SELECT
	`Id`,
	CASE
		WHEN `ContainerType` = 51017 THEN `ContainerType`
		WHEN `ContainerInstance` IN (101, 102, 103, 104, 105, 115) THEN `ContainerInstance`
		ELSE `ContainerType`
	END,
	CASE
		WHEN `ContainerType` = 51017 THEN `ContainerInstance`
		WHEN `ContainerInstance` IN (101, 102, 103, 104, 105, 115) THEN `ContainerType`
		ELSE `ContainerInstance`
	END,
	`ContainerPlacement`,
	`Itemtype`,
	`LowId`,
	`HighId`,
	`Quality`,
	`MultipleCount`
FROM `instanceditems`
WHERE @item_instances_count = 0;

INSERT INTO `item_instances` (
	`ContainerType`,
	`ContainerInstance`,
	`ContainerPlacement`,
	`ItemType`,
	`LowId`,
	`HighId`,
	`Quality`,
	`StackCount`
)
SELECT
	CASE
		WHEN `ContainerType` = 51017 THEN `ContainerType`
		WHEN `ContainerInstance` IN (101, 102, 103, 104, 105, 115) THEN `ContainerInstance`
		ELSE `ContainerType`
	END,
	CASE
		WHEN `ContainerType` = 51017 THEN `ContainerInstance`
		WHEN `ContainerInstance` IN (101, 102, 103, 104, 105, 115) THEN `ContainerType`
		ELSE `ContainerInstance`
	END,
	`ContainerPlacement`,
	0,
	`LowId`,
	`HighId`,
	`Quality`,
	`MultipleCount`
FROM `items`
WHERE @item_instances_count = 0;
