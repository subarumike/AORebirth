-- Read-only operator evidence. The governed runner enforces these conditions before execution.
SELECT VERSION() AS `MySqlVersion`, DATABASE() AS `TargetDatabase`, @@default_storage_engine AS `DefaultEngine`;
SELECT `ENGINE`, `TABLE_COLLATION` FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='account_identities';
SELECT `COLUMN_TYPE`, `IS_NULLABLE`, `COLUMN_KEY` FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='account_identities' AND column_name='IdentityId';
SELECT COUNT(*) AS `ExistingBotTableCount` FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('bot_principals','bot_credentials','bot_scopes','bot_audit_events');
SELECT COUNT(*) AS `RequiredCollationCount` FROM information_schema.collations WHERE collation_name='utf8mb4_0900_ai_ci';
SELECT `SUPPORT` FROM information_schema.engines WHERE engine='InnoDB';
