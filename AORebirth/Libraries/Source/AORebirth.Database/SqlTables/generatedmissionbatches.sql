CREATE TABLE IF NOT EXISTS generatedmissionbatches (
 OwnerId INT NOT NULL, OwnerType INT NOT NULL, BatchIdentity VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 RollSeed INT NOT NULL, ResponseNonce INT NOT NULL, Fee INT NOT NULL,
 TerminalType INT NOT NULL, TerminalInstance INT NOT NULL, TerminalPlayfield INT NOT NULL,
 LevelSlider INT NOT NULL, GoodBadSlider INT NOT NULL, OrderChaosSlider INT NOT NULL,
 OpenHiddenSlider INT NOT NULL, PhysicalMysticalSlider INT NOT NULL, HeadOnStealthSlider INT NOT NULL,
 MoneyExperienceSlider INT NOT NULL, OfferedAtUtcTicks BIGINT NOT NULL, ExpiresAtUtcTicks BIGINT NOT NULL,
 CashBefore INT NOT NULL, CashAfter INT NOT NULL,
 PRIMARY KEY (OwnerId,BatchIdentity), UNIQUE KEY batch_identity (BatchIdentity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
