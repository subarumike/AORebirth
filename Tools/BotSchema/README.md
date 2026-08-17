# AORebirth BotService production migration package

Status: ready for a separate production approval. Not applied to production.

## Files and order

1. `001_botservice_schema_preflight.sql`: read-only operator evidence.
2. `001_botservice_schema_forward.sql`: approved four-table forward migration.
3. `001_botservice_schema_verify.sql`: post-apply metadata evidence.
4. `001_botservice_schema_rollback_empty.sql`: destructive rollback permitted only before any bot row exists.

## Governing rules

- Keep BotService disabled and AccountBroker bot-management routes unregistered.
- Back up and fingerprint the identity database before migration.
- Require MySQL 8.0.16 or newer, InnoDB, `utf8mb4_0900_ai_ci`, and `account_identities.IdentityId bigint unsigned` as an indexed non-null key.
- Supply the database name explicitly. Never infer the target from a default connection.
- Use a root-owned mode `0600` environment file and never place credentials on a command line or in logs.
- Refuse forward application if any bot table already exists. Incompatible or partial schemas require review, not repair in place.
- No application startup path executes these SQL files.

## Production execution order after separate approval

1. Confirm backup and identity-database schema fingerprint.
2. Keep `AO_REBIRTH_BOT_SERVICE_ENABLED=false`.
3. Run the read-only preflight and retain its output.
4. Apply `001_botservice_schema_forward.sql` once.
5. Run `001_botservice_schema_verify.sql` and compare with disposable-validation evidence.
6. Deploy the already-built storage-aware BotService host while disabled.
7. Configure the root-readable MySQL connection and loopback HMAC key.
8. Start BotService and verify private ChatEngine connectivity.
9. Keep AccountBroker bot management gated.
10. Create one disposable smoke bot through authenticated management, validate, then revoke it under the separately approved production test plan.
11. Enable the management surface only after the smoke gate passes.

Expected locking is limited to four new `CREATE TABLE` metadata operations and foreign-key metadata checks against `account_identities`. There is no base-table rewrite or backfill. Reserve a five-minute maintenance window; expected active DDL time is seconds on a healthy MySQL 8 server.

Rollback is allowed only while all four bot tables contain zero rows. After any bot data exists, disable BotService and use an approved forward correction rather than dropping retained credentials or audit history.
