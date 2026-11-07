-- This is used for when a character is kicked from (or left) an organization
-- There is a lockout before next /org invite is executed after it acceptance
CREATE TABLE IF NOT EXISTS `org_lockout` (
    `char_id` INT(32) PRIMARY KEY,
    `char_name` VARCHAR(32),
    `seconds_remaining` BIGINT(20) NOT NULL,
    `last_updated` BIGINT(20) NOT NULL
)
COMMENT='Character lockout for new /org invite after either kick or leave'
DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin
ENGINE=InnoDB;
