-- AORebirth BotService schema migration 001.
-- Approved schema with only the documented normalization and UUID constraint corrections.
-- This file is never executed by application startup.

CREATE TABLE `bot_principals` (
  `BotId` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `OwningIdentityId` bigint unsigned NOT NULL,
  `OrganizationId` int unsigned NULL,
  `DisplayName` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `NormalizedDisplayName` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `PrincipalStatus` enum('Enabled','Disabled') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Enabled',
  `CurrentCredentialVersion` int unsigned NOT NULL DEFAULT 1,
  `RateLimitProfile` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'default',
  `AuditIdentity` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `DisabledAt` datetime(6) NULL,
  PRIMARY KEY (`BotId`),
  UNIQUE KEY `UX_bot_principals_normalized_display_name` (`NormalizedDisplayName`),
  KEY `IX_bot_principals_owner_status` (`OwningIdentityId`, `PrincipalStatus`),
  KEY `IX_bot_principals_organization_status` (`OrganizationId`, `PrincipalStatus`),
  CONSTRAINT `FK_bot_principals_owner` FOREIGN KEY (`OwningIdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_bot_principals_id` CHECK (`BotId` REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'),
  CONSTRAINT `CK_bot_principals_org` CHECK (`OrganizationId` IS NULL OR `OrganizationId` > 0),
  CONSTRAINT `CK_bot_principals_name` CHECK (CHAR_LENGTH(TRIM(`DisplayName`)) BETWEEN 1 AND 32),
  CONSTRAINT `CK_bot_principals_name_normalization` CHECK (BINARY `NormalizedDisplayName` = BINARY LOWER(TRIM(`DisplayName`))),
  CONSTRAINT `CK_bot_principals_version` CHECK (`CurrentCredentialVersion` > 0),
  CONSTRAINT `CK_bot_principals_disabled_at` CHECK ((`PrincipalStatus` = 'Disabled' AND `DisabledAt` IS NOT NULL) OR (`PrincipalStatus` = 'Enabled' AND `DisabledAt` IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `bot_credentials` (
  `CredentialId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BotId` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `PublicCredentialId` char(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `CredentialVersion` int unsigned NOT NULL,
  `Algorithm` enum('PBKDF2-SHA256') CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `Iterations` int unsigned NOT NULL,
  `Salt` binary(16) NOT NULL,
  `Verifier` binary(32) NOT NULL,
  `CredentialState` enum('Active','Superseded','Revoked') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Active',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `RevokedAt` datetime(6) NULL,
  `RevocationReason` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  PRIMARY KEY (`CredentialId`),
  UNIQUE KEY `UX_bot_credentials_public_id` (`PublicCredentialId`),
  UNIQUE KEY `UX_bot_credentials_bot_version` (`BotId`, `CredentialVersion`),
  KEY `IX_bot_credentials_bot_state` (`BotId`, `CredentialState`),
  CONSTRAINT `FK_bot_credentials_bot` FOREIGN KEY (`BotId`) REFERENCES `bot_principals` (`BotId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_bot_credentials_public_id` CHECK (`PublicCredentialId` REGEXP '^[0-9a-f]{32}$'),
  CONSTRAINT `CK_bot_credentials_iterations` CHECK (`Iterations` >= 120000),
  CONSTRAINT `CK_bot_credentials_version` CHECK (`CredentialVersion` > 0),
  CONSTRAINT `CK_bot_credentials_revocation` CHECK ((`CredentialState` = 'Active' AND `RevokedAt` IS NULL AND `RevocationReason` IS NULL) OR (`CredentialState` <> 'Active' AND `RevokedAt` IS NOT NULL AND `RevocationReason` IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `bot_scopes` (
  `BotId` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `ScopeName` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `GrantedByIdentityId` bigint unsigned NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`BotId`, `ScopeName`),
  KEY `IX_bot_scopes_scope_bot` (`ScopeName`, `BotId`),
  KEY `IX_bot_scopes_granted_by` (`GrantedByIdentityId`),
  CONSTRAINT `FK_bot_scopes_bot` FOREIGN KEY (`BotId`) REFERENCES `bot_principals` (`BotId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_bot_scopes_granted_by` FOREIGN KEY (`GrantedByIdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_bot_scopes_name` CHECK (`ScopeName` IN ('TellReceive','TellSend','OrganizationRead','OrganizationSend','ChannelJoin','ChannelLeave','ChannelRead','ChannelSend','RosterRead','CommandReceive','CommandExecute'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE `bot_audit_events` (
  `AuditEventId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BotId` char(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `ActorIdentityId` bigint unsigned NULL,
  `OrganizationId` int unsigned NULL,
  `SessionId` char(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `EventType` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `OperationCode` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Unknown',
  `Outcome` enum('Success','Denied','Failed') CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `ReasonCode` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `AuditIdentity` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`AuditEventId`),
  KEY `IX_bot_audit_bot_created` (`BotId`, `CreatedAt`),
  KEY `IX_bot_audit_actor_created` (`ActorIdentityId`, `CreatedAt`),
  KEY `IX_bot_audit_org_created` (`OrganizationId`, `CreatedAt`),
  KEY `IX_bot_audit_event_created` (`EventType`, `CreatedAt`),
  CONSTRAINT `FK_bot_audit_bot` FOREIGN KEY (`BotId`) REFERENCES `bot_principals` (`BotId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_bot_audit_actor` FOREIGN KEY (`ActorIdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_bot_audit_bot_id` CHECK (`BotId` IS NULL OR `BotId` REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'),
  CONSTRAINT `CK_bot_audit_session_id` CHECK (`SessionId` IS NULL OR `SessionId` REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'),
  CONSTRAINT `CK_bot_audit_org` CHECK (`OrganizationId` IS NULL OR `OrganizationId` > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
