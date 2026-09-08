-- Add item_instances.Source (ItemSource: Command=0 Loot=1 Quest=2 Vendor=3 Other=4).
-- Existing rows default to Loot (1). Safe to re-run.

SET @col_exists := (
	SELECT COUNT(*)
	FROM INFORMATION_SCHEMA.COLUMNS
	WHERE TABLE_SCHEMA = DATABASE()
		AND TABLE_NAME = 'item_instances'
		AND COLUMN_NAME = 'Source'
);

SET @sql := IF(@col_exists = 0,
	'ALTER TABLE `item_instances` ADD COLUMN `Source` TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER `StackCount`',
	'SELECT 1');

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
