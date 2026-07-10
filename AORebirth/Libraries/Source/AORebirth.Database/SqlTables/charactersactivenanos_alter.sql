ALTER TABLE `charactersactivenanos`
  ADD COLUMN `NanoInstance` int(32) NOT NULL DEFAULT 0 AFTER `Strain`,
  ADD COLUMN `DurationCentiseconds` int(32) NOT NULL DEFAULT 0 AFTER `NanoInstance`,
  ADD COLUMN `ExpiresAtUtcTicks` bigint(20) NOT NULL DEFAULT 0 AFTER `DurationCentiseconds`;
