-- AORebirth Account Broker password reset token table.
--
-- Apply only after a production database backup and explicit deployment
-- approval. This migration adds the broker-owned token table required by the
-- password recovery flow. It does not modify login passwords, account
-- mappings, characters, MyBB tables, or existing identity rows.

CREATE TABLE IF NOT EXISTS `account_password_reset_tokens` (
  `PasswordResetTokenId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `IdentityId` bigint unsigned NOT NULL,
  `EmailHash` binary(32) NOT NULL,
  `TokenHash` binary(32) NOT NULL,
  `TokenState` enum('Active','Superseded','Used','Expired') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Active',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `ExpiresAt` datetime(6) NOT NULL,
  `UsedAt` datetime(6) NULL,
  PRIMARY KEY (`PasswordResetTokenId`),
  UNIQUE KEY `UX_account_password_reset_tokens_hash` (`TokenHash`),
  KEY `IX_account_password_reset_tokens_identity_state` (`IdentityId`, `TokenState`, `ExpiresAt`),
  KEY `IX_account_password_reset_tokens_expiry` (`TokenState`, `ExpiresAt`),
  CONSTRAINT `FK_account_password_reset_tokens_identity` FOREIGN KEY (`IdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_account_password_reset_tokens_expires` CHECK (`ExpiresAt` > `CreatedAt`),
  CONSTRAINT `CK_account_password_reset_tokens_used_at` CHECK ((`TokenState` = 'Used' AND `UsedAt` IS NOT NULL) OR (`TokenState` <> 'Used' AND `UsedAt` IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
