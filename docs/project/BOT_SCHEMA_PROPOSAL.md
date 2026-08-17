# BOT SCHEMA PROPOSAL — NOT APPLIED

Status: review-only. This Markdown file is deliberately non-executable. No migration, startup schema mutation, deployment, or production bot row is included.

## Placement and conventions

- Target the dedicated AORebirth identity database that owns `account_identities`.
- `OwningIdentityId` references the stable `account_identities.IdentityId` key, not legacy `login.Id`.
- `OrganizationId` stores the stable unsigned `organizations.Id` value. It intentionally has no database foreign key because organizations are owned by the game database, outside the identity database boundary. AccountBroker must validate organization authority against the authoritative organization service before assignment.
- Tables use InnoDB, `utf8mb4`, `utf8mb4_0900_ai_ci`, ASCII binary collation for identifiers and policy names, `datetime(6)` UTC timestamps, explicit state enums, restrictive deletes, named keys, and named checks.
- Bot principals are retained and disabled rather than deleted. Audit rows are append-only. Credential plaintext is never stored.

## Exact proposed DDL

```sql
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
  CONSTRAINT `CK_bot_principals_id` CHECK (`BotId` REGEXP '^[0-9a-fA-F-]{36}$'),
  CONSTRAINT `CK_bot_principals_org` CHECK (`OrganizationId` IS NULL OR `OrganizationId` > 0),
  CONSTRAINT `CK_bot_principals_name` CHECK (CHAR_LENGTH(TRIM(`DisplayName`)) BETWEEN 1 AND 32),
  CONSTRAINT `CK_bot_principals_name_normalization` CHECK (`NormalizedDisplayName` = LOWER(TRIM(`DisplayName`))),
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
  CONSTRAINT `CK_bot_audit_bot_id` CHECK (`BotId` IS NULL OR `BotId` REGEXP '^[0-9a-fA-F-]{36}$'),
  CONSTRAINT `CK_bot_audit_session_id` CHECK (`SessionId` IS NULL OR `SessionId` REGEXP '^[0-9a-fA-F-]{36}$'),
  CONSTRAINT `CK_bot_audit_org` CHECK (`OrganizationId` IS NULL OR `OrganizationId` > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
```

## Migration plan

1. Obtain explicit schema approval and schedule a BotService-disabled maintenance window.
2. Back up the identity database and record its schema fingerprint.
3. Apply the approved DDL manually to an empty disposable MySQL 8 validation database.
4. Run structural, constraint, transaction rollback, credential rotation, scope replacement, and audit append validation there.
5. Apply the approved DDL manually to the identity database with `AO_REBIRTH_BOT_SERVICE_ENABLED=false`.
6. Re-run read-only structural validation, then deploy the host and AccountBroker feature gate while still disabled.
7. Configure the root-readable loopback key and connection environment, enable the feature, then create the first bot through authenticated AccountBroker management.

## Rollback plan

- Before any bot row exists, disable BotService and drop tables in this order: `bot_audit_events`, `bot_scopes`, `bot_credentials`, `bot_principals`.
- After any bot row exists, do not use destructive rollback. Disable BotService and AccountBroker bot management, retain all rows and audit history, restore the application version, and use an approved forward migration.
- Rotation, revoke, and scope replacement are single `READ COMMITTED` transactions. Application failures roll back the entire operation.

## Backfill plan

No backfill is required or permitted. Existing player accounts and organizations remain unchanged. Bot principals are created only after approval through authenticated management, and the raw credential is returned only by create or rotate.
