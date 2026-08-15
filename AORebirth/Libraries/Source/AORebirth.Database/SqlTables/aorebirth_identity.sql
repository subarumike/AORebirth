-- AORebirth Account Broker identity schema.
--
-- This file is repository authority only. It is intended to be applied to a
-- dedicated AORebirth identity database on the same private MySQL server as the
-- game database after Windows validation and explicit production approval.
-- It must not be imported into the MyBB database, and MyBB must not own these
-- tables.

CREATE TABLE `account_identities` (
  `IdentityId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `IdentityPublicId` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `CanonicalUsername` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `NormalizedUsername` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `CanonicalEmail` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `NormalizedEmail` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `EmailVerifiedAt` datetime(6) NULL,
  `IdentityStatus` enum('Reserved','Active','Suspended','Disabled') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Reserved',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`IdentityId`),
  UNIQUE KEY `UX_account_identities_public_id` (`IdentityPublicId`),
  UNIQUE KEY `UX_account_identities_normalized_username` (`NormalizedUsername`),
  UNIQUE KEY `UX_account_identities_normalized_email` (`NormalizedEmail`),
  CONSTRAINT `CK_account_identities_public_id` CHECK (`IdentityPublicId` REGEXP '^[0-9a-fA-F-]{36}$'),
  CONSTRAINT `CK_account_identities_canonical_username` CHECK (`CanonicalUsername` REGEXP '^[A-Za-z0-9]{1,32}$'),
  CONSTRAINT `CK_account_identities_normalized_username` CHECK (`NormalizedUsername` REGEXP '^[a-z0-9]{1,32}$'),
  CONSTRAINT `CK_account_identities_username_normalization` CHECK (`NormalizedUsername` = LOWER(`CanonicalUsername`)),
  CONSTRAINT `CK_account_identities_email_pair` CHECK ((`CanonicalEmail` IS NULL AND `NormalizedEmail` IS NULL) OR (`CanonicalEmail` IS NOT NULL AND `NormalizedEmail` IS NOT NULL)),
  CONSTRAINT `CK_account_identities_email_normalization` CHECK (`NormalizedEmail` IS NULL OR `NormalizedEmail` = LOWER(TRIM(`CanonicalEmail`))),
  CONSTRAINT `CK_account_identities_email_verified_requires_email` CHECK (`EmailVerifiedAt` IS NULL OR `NormalizedEmail` IS NOT NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `account_game_mappings` (
  `IdentityId` bigint unsigned NOT NULL,
  `GameAccountId` int NOT NULL,
  `MappingState` enum('Pending','Linked','Disabled') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Pending',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `LinkedAt` datetime(6) NULL,
  PRIMARY KEY (`IdentityId`),
  UNIQUE KEY `UX_account_game_mappings_game_account` (`GameAccountId`),
  KEY `IX_account_game_mappings_state` (`MappingState`),
  CONSTRAINT `FK_account_game_mappings_identity` FOREIGN KEY (`IdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_account_game_mappings_positive_game_account` CHECK (`GameAccountId` > 0),
  CONSTRAINT `CK_account_game_mappings_linked_at` CHECK ((`MappingState` = 'Linked' AND `LinkedAt` IS NOT NULL) OR (`MappingState` <> 'Linked'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `account_external_mappings` (
  `ExternalMappingId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `IdentityId` bigint unsigned NOT NULL,
  `Provider` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `ExternalAccountId` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `MappingState` enum('Pending','Linked','Disabled') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Pending',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `LinkedAt` datetime(6) NULL,
  PRIMARY KEY (`ExternalMappingId`),
  UNIQUE KEY `UX_account_external_mappings_provider_account` (`Provider`, `ExternalAccountId`),
  UNIQUE KEY `UX_account_external_mappings_identity_provider` (`IdentityId`, `Provider`),
  KEY `IX_account_external_mappings_state` (`MappingState`),
  CONSTRAINT `FK_account_external_mappings_identity` FOREIGN KEY (`IdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_account_external_mappings_provider` CHECK (`Provider` REGEXP '^[a-z0-9_:-]{2,32}$'),
  CONSTRAINT `CK_account_external_mappings_external_id` CHECK (CHAR_LENGTH(`ExternalAccountId`) BETWEEN 1 AND 64),
  CONSTRAINT `CK_account_external_mappings_linked_at` CHECK ((`MappingState` = 'Linked' AND `LinkedAt` IS NOT NULL) OR (`MappingState` <> 'Linked'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `account_provisioning_jobs` (
  `ProvisioningJobId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `IdempotencyKeyHash` binary(32) NOT NULL,
  `IdentityId` bigint unsigned NULL,
  `RequestedNormalizedUsername` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `RequestedNormalizedEmail` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `RequestedGameAccountId` int NULL,
  `RequestedExternalProvider` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `RequestedExternalAccountId` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `ProvisioningState` enum('IdentityReserved','GameAccountPending','GameAccountLinked','ExternalAccountPending','ExternalAccountLinked','Active','ManualReview') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'IdentityReserved',
  `ProvisioningStep` tinyint unsigned NOT NULL DEFAULT 10,
  `AttemptCount` int unsigned NOT NULL DEFAULT 0,
  `LastFailureCode` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `LastFailureDetail` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`ProvisioningJobId`),
  UNIQUE KEY `UX_account_provisioning_jobs_idempotency` (`IdempotencyKeyHash`),
  KEY `IX_account_provisioning_jobs_identity` (`IdentityId`),
  KEY `IX_account_provisioning_jobs_state` (`ProvisioningState`, `ProvisioningStep`),
  KEY `IX_account_provisioning_jobs_username` (`RequestedNormalizedUsername`),
  CONSTRAINT `FK_account_provisioning_jobs_identity` FOREIGN KEY (`IdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_account_provisioning_jobs_username` CHECK (`RequestedNormalizedUsername` REGEXP '^[a-z0-9]{6,32}$'),
  CONSTRAINT `CK_account_provisioning_jobs_game_account` CHECK (`RequestedGameAccountId` IS NULL OR `RequestedGameAccountId` > 0),
  CONSTRAINT `CK_account_provisioning_jobs_external_pair` CHECK ((`RequestedExternalProvider` IS NULL AND `RequestedExternalAccountId` IS NULL) OR (`RequestedExternalProvider` IS NOT NULL AND `RequestedExternalAccountId` IS NOT NULL)),
  CONSTRAINT `CK_account_provisioning_jobs_external_provider` CHECK (`RequestedExternalProvider` IS NULL OR `RequestedExternalProvider` REGEXP '^[a-z0-9_:-]{2,32}$'),
  CONSTRAINT `CK_account_provisioning_jobs_external_id` CHECK (`RequestedExternalAccountId` IS NULL OR CHAR_LENGTH(`RequestedExternalAccountId`) BETWEEN 1 AND 64),
  CONSTRAINT `CK_account_provisioning_jobs_state_step` CHECK (
    (`ProvisioningState` = 'IdentityReserved' AND `ProvisioningStep` = 10) OR
    (`ProvisioningState` = 'GameAccountPending' AND `ProvisioningStep` = 20) OR
    (`ProvisioningState` = 'GameAccountLinked' AND `ProvisioningStep` = 30) OR
    (`ProvisioningState` = 'ExternalAccountPending' AND `ProvisioningStep` = 40) OR
    (`ProvisioningState` = 'ExternalAccountLinked' AND `ProvisioningStep` = 50) OR
    (`ProvisioningState` = 'Active' AND `ProvisioningStep` = 60) OR
    (`ProvisioningState` = 'ManualReview' AND `ProvisioningStep` = 90)
  )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
