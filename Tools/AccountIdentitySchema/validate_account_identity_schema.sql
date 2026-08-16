-- Validation script for AORebirth Account Broker identity schema.
--
-- Run only against an empty disposable validation database:
--
--   mysql --local-infile=0 <connection-options> <validation-db> < Tools/AccountIdentitySchema/validate_account_identity_schema.sql
--
-- This script intentionally creates, mutates, and drops/recreates only the
-- Account Broker identity tables in the selected validation database. Do not
-- run it against production.

DROP PROCEDURE IF EXISTS `ExpectDuplicateUsernameRejected`;
DROP PROCEDURE IF EXISTS `ExpectDuplicateGameMappingRejected`;
DROP PROCEDURE IF EXISTS `ExpectDuplicateExternalMappingRejected`;
DROP PROCEDURE IF EXISTS `ExpectDuplicateEmailVerificationTokenRejected`;
DROP PROCEDURE IF EXISTS `ExpectInvalidEmailVerificationTokenStateRejected`;
DROP PROCEDURE IF EXISTS `ExpectInvalidProvisioningStateRejected`;

DROP TABLE IF EXISTS `account_provisioning_jobs`;
DROP TABLE IF EXISTS `account_email_verification_tokens`;
DROP TABLE IF EXISTS `account_external_mappings`;
DROP TABLE IF EXISTS `account_game_mappings`;
DROP TABLE IF EXISTS `account_identities`;

SOURCE AORebirth/Libraries/Source/AORebirth.Database/SqlTables/aorebirth_identity.sql

DELIMITER //

CREATE PROCEDURE `ExpectDuplicateUsernameRejected`()
BEGIN
  DECLARE rejected bool DEFAULT FALSE;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET rejected = TRUE;

  INSERT INTO `account_identities`
    (`IdentityPublicId`, `CanonicalUsername`, `NormalizedUsername`, `CanonicalEmail`, `NormalizedEmail`)
  VALUES
    ('00000000-0000-0000-0000-000000000102', 'player', 'player', NULL, NULL);

  IF rejected = FALSE THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'duplicate normalized username was accepted';
  END IF;
END//

CREATE PROCEDURE `ExpectDuplicateGameMappingRejected`()
BEGIN
  DECLARE rejected bool DEFAULT FALSE;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET rejected = TRUE;

  INSERT INTO `account_game_mappings`
    (`IdentityId`, `GameAccountId`, `MappingState`, `LinkedAt`)
  VALUES
    (2, 1001, 'Linked', CURRENT_TIMESTAMP(6));

  IF rejected = FALSE THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'duplicate game account mapping was accepted';
  END IF;
END//

CREATE PROCEDURE `ExpectDuplicateExternalMappingRejected`()
BEGIN
  DECLARE rejected bool DEFAULT FALSE;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET rejected = TRUE;

  INSERT INTO `account_external_mappings`
    (`IdentityId`, `Provider`, `ExternalAccountId`, `MappingState`, `LinkedAt`)
  VALUES
    (2, 'mybb', '42', 'Linked', CURRENT_TIMESTAMP(6));

  IF rejected = FALSE THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'duplicate provider external account mapping was accepted';
  END IF;
END//

CREATE PROCEDURE `ExpectInvalidProvisioningStateRejected`()
BEGIN
  DECLARE rejected bool DEFAULT FALSE;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET rejected = TRUE;

  INSERT INTO `account_provisioning_jobs`
    (`IdempotencyKeyHash`, `IdentityId`, `RequestedNormalizedUsername`, `ProvisioningState`, `ProvisioningStep`)
  VALUES
    (UNHEX(SHA2('bad-state', 256)), 1, 'player', 'Active', 10);

  IF rejected = FALSE THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'invalid provisioning state/step pair was accepted';
  END IF;
END//

CREATE PROCEDURE `ExpectDuplicateEmailVerificationTokenRejected`()
BEGIN
  DECLARE rejected bool DEFAULT FALSE;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET rejected = TRUE;

  INSERT INTO `account_email_verification_tokens`
    (`IdentityId`, `TokenHash`, `ExpiresAt`)
  VALUES
    (2, UNHEX(SHA2('email-token', 256)), TIMESTAMPADD(MINUTE, 30, CURRENT_TIMESTAMP(6)));

  IF rejected = FALSE THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'duplicate email verification token hash was accepted';
  END IF;
END//

CREATE PROCEDURE `ExpectInvalidEmailVerificationTokenStateRejected`()
BEGIN
  DECLARE rejected bool DEFAULT FALSE;
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION SET rejected = TRUE;

  INSERT INTO `account_email_verification_tokens`
    (`IdentityId`, `TokenHash`, `TokenState`, `ExpiresAt`, `UsedAt`)
  VALUES
    (1, UNHEX(SHA2('bad-used-state', 256)), 'Used', TIMESTAMPADD(MINUTE, 30, CURRENT_TIMESTAMP(6)), NULL);

  IF rejected = FALSE THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'used email verification token without UsedAt was accepted';
  END IF;
END//

DELIMITER ;

INSERT INTO `account_identities`
  (`IdentityPublicId`, `CanonicalUsername`, `NormalizedUsername`, `CanonicalEmail`, `NormalizedEmail`, `EmailVerifiedAt`, `IdentityStatus`)
VALUES
  ('00000000-0000-0000-0000-000000000101', 'Player', 'player', 'Player@example.com', 'player@example.com', CURRENT_TIMESTAMP(6), 'Reserved'),
  ('00000000-0000-0000-0000-000000000201', 'Second', 'second', NULL, NULL, NULL, 'Reserved'),
  ('00000000-0000-0000-0000-000000000301', 'Old', 'old', NULL, NULL, NULL, 'Reserved');

CALL `ExpectDuplicateUsernameRejected`();

INSERT INTO `account_game_mappings`
  (`IdentityId`, `GameAccountId`, `MappingState`, `LinkedAt`)
VALUES
  (1, 1001, 'Linked', CURRENT_TIMESTAMP(6));

CALL `ExpectDuplicateGameMappingRejected`();

INSERT INTO `account_external_mappings`
  (`IdentityId`, `Provider`, `ExternalAccountId`, `MappingState`, `LinkedAt`)
VALUES
  (1, 'mybb', '42', 'Linked', CURRENT_TIMESTAMP(6));

CALL `ExpectDuplicateExternalMappingRejected`();

INSERT INTO `account_email_verification_tokens`
  (`IdentityId`, `TokenHash`, `ExpiresAt`)
VALUES
  (1, UNHEX(SHA2('email-token', 256)), TIMESTAMPADD(MINUTE, 30, CURRENT_TIMESTAMP(6)));

CALL `ExpectDuplicateEmailVerificationTokenRejected`();
CALL `ExpectInvalidEmailVerificationTokenStateRejected`();

INSERT INTO `account_provisioning_jobs`
  (`IdempotencyKeyHash`, `IdentityId`, `RequestedNormalizedUsername`, `RequestedNormalizedEmail`, `RequestedGameAccountId`, `ProvisioningState`, `ProvisioningStep`)
VALUES
  (UNHEX(SHA2('player-registration', 256)), 1, 'player', 'player@example.com', NULL, 'IdentityReserved', 10);

UPDATE `account_provisioning_jobs`
SET `RequestedGameAccountId` = 1001,
    `ProvisioningState` = 'GameAccountLinked',
    `ProvisioningStep` = 30
WHERE `ProvisioningJobId` = 1
  AND `ProvisioningStep` < 30;

UPDATE `account_provisioning_jobs`
SET `ProvisioningState` = 'IdentityReserved',
    `ProvisioningStep` = 10
WHERE `ProvisioningJobId` = 1
  AND `ProvisioningStep` < 10;

CALL `ExpectInvalidProvisioningStateRejected`();

SELECT
  'AORebirth account identity schema validation PASS' AS `Result`,
  (SELECT COUNT(*) FROM `account_identities`) AS `IdentityRows`,
  (SELECT COUNT(*) FROM `account_game_mappings`) AS `GameMappingRows`,
  (SELECT COUNT(*) FROM `account_external_mappings`) AS `ExternalMappingRows`,
  (SELECT COUNT(*) FROM `account_email_verification_tokens`) AS `EmailVerificationTokenRows`,
  (SELECT `ProvisioningState` FROM `account_provisioning_jobs` WHERE `ProvisioningJobId` = 1) AS `ProvisioningState`;
