-- AORebirth Account Broker email verification token table.
--
-- Apply only after a production database backup and explicit deployment
-- approval. This migration adds the broker-owned token table required by the
-- production email verification flow. It does not modify login passwords,
-- account mappings, characters, MyBB tables, or existing identity rows.

CREATE TABLE IF NOT EXISTS `account_email_verification_tokens` (
  `EmailVerificationTokenId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `IdentityId` bigint unsigned NOT NULL,
  `TokenHash` binary(32) NOT NULL,
  `TokenState` enum('Active','Superseded','Used','Expired') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Active',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `ExpiresAt` datetime(6) NOT NULL,
  `UsedAt` datetime(6) NULL,
  PRIMARY KEY (`EmailVerificationTokenId`),
  UNIQUE KEY `UX_account_email_verification_tokens_hash` (`TokenHash`),
  KEY `IX_account_email_verification_tokens_identity_state` (`IdentityId`, `TokenState`, `ExpiresAt`),
  CONSTRAINT `FK_account_email_verification_tokens_identity` FOREIGN KEY (`IdentityId`) REFERENCES `account_identities` (`IdentityId`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `CK_account_email_verification_tokens_expires` CHECK (`ExpiresAt` > `CreatedAt`),
  CONSTRAINT `CK_account_email_verification_tokens_used_at` CHECK ((`TokenState` = 'Used' AND `UsedAt` IS NOT NULL) OR (`TokenState` <> 'Used' AND `UsedAt` IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
