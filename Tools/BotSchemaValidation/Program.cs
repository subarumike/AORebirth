namespace AORebirth.Tools.BotSchemaValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.AccountBroker;
    using AORebirth.BotService;

    using MySqlConnector;

    internal static class Program
    {
        private const string AcknowledgementEnvironment = "AO_REBIRTH_ALLOW_DISPOSABLE_BOT_SCHEMA_VALIDATION";
        private const string ContainerName = "aorebirth-bot-schema-validation";
        private const string DatabaseName = "aorebirth_bot_schema_validation";
        private const string DatabaseUser = "aorebirth_bot_validation";
        private const string Image = "mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d";
        private const string LabelKey = "org.aorebirth.purpose";
        private const string LabelValue = "bot-schema-disposable";
        private const string NetworkName = "aorebirth_bot_schema_validation_internal";
        private const uint Port = 33068;
        private const string VolumeName = "aorebirth_bot_schema_validation_data";
        private static int checks;

        private static int Main(string[] args)
        {
            if (args.Length != 1 || !string.Equals(args[0], "--run-disposable", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("REFUSED: exact --run-disposable argument required.");
                return 2;
            }

            if (!string.Equals(Environment.GetEnvironmentVariable(AcknowledgementEnvironment), "1", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("REFUSED: AO_REBIRTH_ALLOW_DISPOSABLE_BOT_SCHEMA_VALIDATION=1 is required.");
                return 2;
            }

            DisposableMySql disposable = null;
            try
            {
                string repositoryRoot = Directory.GetCurrentDirectory();
                string schemaDirectory = Path.Combine(repositoryRoot, "Tools", "BotSchema");
                string identitySchema = Path.Combine(
                    repositoryRoot,
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Database",
                    "SqlTables",
                    "aorebirth_identity.sql");
                Require(File.Exists(identitySchema), "identity-schema-missing");
                Require(File.Exists(Path.Combine(schemaDirectory, "001_botservice_schema_forward.sql")), "forward-schema-missing");
                Require(File.Exists(Path.Combine(schemaDirectory, "001_botservice_schema_verify.sql")), "verify-schema-missing");
                Require(File.Exists(Path.Combine(schemaDirectory, "001_botservice_schema_rollback_empty.sql")), "rollback-schema-missing");

                disposable = DisposableMySql.Create();
                using (MySqlConnection root = WaitForMySql(disposable.RootConnectionString))
                {
                    string version = ScalarString(root, "SELECT VERSION()");
                    Console.WriteLine(
                        "DISPOSABLE: mysql={0} database={1} target=127.0.0.1:{2} initial_tables=0",
                        version,
                        DatabaseName,
                        Port);
                    ValidateInitialServer(root, version);
                    ExecuteFile(root, identitySchema);
                    ValidateBaseIdentitySchema(root);
                    ProveApprovedSchemaCorrections(root);
                    Require(CountBotTables(root) == 0, "bot-tables-existed-before-forward");

                    ExecuteFile(root, Path.Combine(schemaDirectory, "001_botservice_schema_forward.sql"));
                    ValidateBotSchema(root);
                    RestrictRuntimeAccount(root);
                }

                LifecycleEvidence lifecycle;
                using (MySqlConnection root = Open(disposable.RootConnectionString))
                {
                    SeedIdentities(root);
                    lifecycle = ExerciseRepository(disposable.ApplicationConnectionString, root);
                    ExerciseTransactionRollbacks(disposable.ApplicationConnectionString, root);
                    ExerciseConstraintRejections(root);
                    VerifyCredentialSecrecy(root, lifecycle);
                    ExerciseConcurrentRotation(disposable.ApplicationConnectionString, root);
                    ExerciseDeleteRestrictions(disposable.ApplicationConnectionString, root);
                    ValidateRollbackAndStartupStates(
                        disposable.ApplicationConnectionString,
                        root,
                        schemaDirectory);
                }

                Console.WriteLine(
                    "PASS: disposable BotService MySQL validation checks={0} production_contact=NO secrets_logged=NO",
                    checks);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: disposable BotService MySQL validation code=" + SafeFailure(exception));
                return 1;
            }
            finally
            {
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }

        private static void ValidateInitialServer(MySqlConnection connection, string version)
        {
            Version parsed;
            string numericVersion = version.Split('-')[0];
            Require(Version.TryParse(numericVersion, out parsed) && parsed.Major == 8 && parsed >= new Version(8, 0, 16), "mysql-version-not-supported");
            Require(string.Equals(ScalarString(connection, "SELECT DATABASE()"), DatabaseName, StringComparison.Ordinal), "database-identity-mismatch");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()") == 0, "database-not-initially-empty");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.engines WHERE engine='InnoDB' AND support IN ('YES','DEFAULT')") == 1, "innodb-unavailable");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.collations WHERE collation_name='utf8mb4_0900_ai_ci'") == 1, "required-collation-unavailable");

            Execute(connection, "CREATE TEMPORARY TABLE bot_check_probe (Value int NOT NULL, CONSTRAINT CK_bot_check_probe CHECK (Value > 0))");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_check_probe (Value) VALUES (0)"), "check-constraints-not-enforced");
            Execute(connection, "DROP TEMPORARY TABLE bot_check_probe");
        }

        private static void ValidateBaseIdentitySchema(MySqlConnection connection)
        {
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='account_identities' AND engine='InnoDB'") == 1, "account-identities-engine-mismatch");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='account_identities' AND column_name='IdentityId' AND column_type='bigint unsigned' AND is_nullable='NO' AND column_key='PRI'") == 1, "account-identity-key-incompatible");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='account_identities' AND column_name='IdentityId' AND non_unique=0") >= 1, "account-identity-key-not-indexed");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='organizations'") == 0, "organization-table-unexpected-in-identity-database");
        }

        private static void ProveApprovedSchemaCorrections(MySqlConnection connection)
        {
            Execute(
                connection,
                "CREATE TEMPORARY TABLE bot_original_normalization_probe (DisplayName varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL, NormalizedDisplayName varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL, CHECK (NormalizedDisplayName = LOWER(TRIM(DisplayName))))");
            Execute(connection, "INSERT INTO bot_original_normalization_probe VALUES ('CaseBot','CASEBOT')");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM bot_original_normalization_probe") == 1, "original-normalization-defect-not-reproduced");
            Execute(connection, "DROP TEMPORARY TABLE bot_original_normalization_probe");
            Require(ScalarLong(connection, "SELECT 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' REGEXP '^[0-9a-fA-F-]{36}$'") == 1, "original-uuid-defect-not-reproduced");
            Require(ScalarLong(connection, "SELECT 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' REGEXP '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'") == 0, "corrected-uuid-check-not-strict");
        }

        private static void ValidateBotSchema(MySqlConnection connection)
        {
            Require(CountBotTables(connection) == 4, "four-table-set-mismatch");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('bot_principals','bot_credentials','bot_scopes','bot_audit_events') AND engine='InnoDB' AND table_collation='utf8mb4_0900_ai_ci'") == 4, "table-engine-or-collation-mismatch");
            Require(ScalarString(connection, "SELECT GROUP_CONCAT(table_name ORDER BY table_name) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('bot_principals','bot_credentials','bot_scopes','bot_audit_events')") == "bot_audit_events,bot_credentials,bot_principals,bot_scopes", "table-name-set-mismatch");

            RequireIndex(connection, "bot_principals", "PRIMARY", "BotId", false);
            RequireIndex(connection, "bot_principals", "UX_bot_principals_normalized_display_name", "NormalizedDisplayName", false);
            RequireIndex(connection, "bot_principals", "IX_bot_principals_owner_status", "OwningIdentityId,PrincipalStatus", true);
            RequireIndex(connection, "bot_principals", "IX_bot_principals_organization_status", "OrganizationId,PrincipalStatus", true);
            RequireIndex(connection, "bot_credentials", "PRIMARY", "CredentialId", false);
            RequireIndex(connection, "bot_credentials", "UX_bot_credentials_public_id", "PublicCredentialId", false);
            RequireIndex(connection, "bot_credentials", "UX_bot_credentials_bot_version", "BotId,CredentialVersion", false);
            RequireIndex(connection, "bot_credentials", "IX_bot_credentials_bot_state", "BotId,CredentialState", true);
            RequireIndex(connection, "bot_scopes", "PRIMARY", "BotId,ScopeName", false);
            RequireIndex(connection, "bot_scopes", "IX_bot_scopes_scope_bot", "ScopeName,BotId", true);
            RequireIndex(connection, "bot_scopes", "IX_bot_scopes_granted_by", "GrantedByIdentityId", true);
            RequireIndex(connection, "bot_audit_events", "PRIMARY", "AuditEventId", false);
            RequireIndex(connection, "bot_audit_events", "IX_bot_audit_bot_created", "BotId,CreatedAt", true);
            RequireIndex(connection, "bot_audit_events", "IX_bot_audit_actor_created", "ActorIdentityId,CreatedAt", true);
            RequireIndex(connection, "bot_audit_events", "IX_bot_audit_org_created", "OrganizationId,CreatedAt", true);
            RequireIndex(connection, "bot_audit_events", "IX_bot_audit_event_created", "EventType,CreatedAt", true);

            RequireConstraint(connection, "FK_bot_principals_owner", "FOREIGN KEY");
            RequireConstraint(connection, "FK_bot_credentials_bot", "FOREIGN KEY");
            RequireConstraint(connection, "FK_bot_scopes_bot", "FOREIGN KEY");
            RequireConstraint(connection, "FK_bot_scopes_granted_by", "FOREIGN KEY");
            RequireConstraint(connection, "FK_bot_audit_bot", "FOREIGN KEY");
            RequireConstraint(connection, "FK_bot_audit_actor", "FOREIGN KEY");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() AND constraint_name LIKE 'FK_bot_%' AND delete_rule='RESTRICT' AND update_rule='RESTRICT'") == 6, "foreign-key-restrict-rules-mismatch");

            string[] checksToRequire =
            {
                "CK_bot_principals_id", "CK_bot_principals_org", "CK_bot_principals_name",
                "CK_bot_principals_name_normalization", "CK_bot_principals_version", "CK_bot_principals_disabled_at",
                "CK_bot_credentials_public_id", "CK_bot_credentials_iterations", "CK_bot_credentials_version",
                "CK_bot_credentials_revocation", "CK_bot_scopes_name", "CK_bot_audit_bot_id",
                "CK_bot_audit_session_id", "CK_bot_audit_org"
            };
            foreach (string checkName in checksToRequire)
            {
                RequireConstraint(connection, checkName, "CHECK");
            }

            RequireColumn(connection, "bot_principals", "BotId", "char(36)", "NO");
            RequireColumn(connection, "bot_principals", "OwningIdentityId", "bigint unsigned", "NO");
            RequireColumn(connection, "bot_principals", "OrganizationId", "int unsigned", "YES");
            RequireColumn(connection, "bot_credentials", "CredentialId", "bigint unsigned", "NO");
            Require(ScalarString(connection, "SELECT extra FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='bot_credentials' AND column_name='CredentialId'").Contains("auto_increment"), "credential-id-not-auto-increment");
            RequireColumn(connection, "bot_credentials", "Salt", "binary(16)", "NO");
            RequireColumn(connection, "bot_credentials", "Verifier", "binary(32)", "NO");
            RequireColumn(connection, "bot_audit_events", "BotId", "char(36)", "YES");
            RequireColumn(connection, "bot_audit_events", "ActorIdentityId", "bigint unsigned", "YES");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name IN ('bot_principals','bot_credentials','bot_scopes','bot_audit_events') AND LOWER(column_name) REGEXP 'secret|plaintext|rawcredential'") == 0, "secret-bearing-column-found");
        }

        private static LifecycleEvidence ExerciseRepository(string applicationConnectionString, MySqlConnection root)
        {
            IPersistentBotRepository repository = Repository(applicationConnectionString);
            ((IPersistentBotSchemaValidator)repository).ValidateSchema();
            BotAccountManagementService management = Management(repository);
            BotManagementContext owner = Context(1001);
            BotManagementResult created = management.Create(
                owner,
                new BotManagementCreateRequest
                {
                    DisplayName = "DisposableRelay",
                    OrganizationId = 77,
                    Scopes = BotScope.TellReceive | BotScope.OrganizationRead | BotScope.ChannelJoin,
                    RateLimitProfile = "org-default"
                });
            Require(created.Principal.OwningAccountId == 1001, "created-owner-mismatch");
            Require(repository.FindPrincipal(created.Principal.BotId) != null, "created-principal-not-readable");
            Require(repository.ListPrincipals(1001).Length == 1, "owner-list-mismatch");
            Require(repository.ListPrincipals(1002).Length == 0, "owner-filter-leak");
            ExpectInvalidOperation(() => management.Get(Context(1002), created.Principal.BotId), "cross-owner-get-accepted");

            BotCredentialRecord initialRecord = repository.FindCredential(created.PublicCredentialId);
            PersistentBotCredentialIssuer issuer = new PersistentBotCredentialIssuer();
            Require(initialRecord != null && issuer.Verify(initialRecord, created.OneTimeCredential), "initial-credential-verification-failed");
            Require(!issuer.Verify(initialRecord, MutateSecret(created.OneTimeCredential)), "wrong-credential-accepted");
            BotCredentialAuthenticator authenticator = new BotCredentialAuthenticator(repository, new RecordingBotAuditSink());
            Require(authenticator.Authenticate(created.OneTimeCredential).Succeeded, "storage-backed-authentication-failed");

            management.Disable(owner, created.Principal.BotId);
            Require(!repository.FindPrincipal(created.Principal.BotId).Enabled, "disable-not-persisted");
            management.Enable(owner, created.Principal.BotId);
            Require(repository.FindPrincipal(created.Principal.BotId).Enabled, "enable-not-persisted");
            management.UpdateScopes(owner, created.Principal.BotId, BotScope.TellReceive | BotScope.TellSend | BotScope.ChannelRead);
            Require(repository.FindPrincipal(created.Principal.BotId).Scopes == (BotScope.TellReceive | BotScope.TellSend | BotScope.ChannelRead), "scope-reload-mismatch");
            management.AssignOrganization(owner, created.Principal.BotId, 88);
            Require(repository.FindPrincipal(created.Principal.BotId).OrganizationId == 88, "organization-assignment-not-persisted");

            BotManagementResult rotated = management.RotateCredential(owner, created.Principal.BotId);
            BotCredentialRecord oldRecord = repository.FindCredential(created.PublicCredentialId);
            BotCredentialRecord currentRecord = repository.FindCredential(rotated.PublicCredentialId);
            Require(oldRecord.Revoked, "old-credential-not-revoked-by-rotation");
            Require(!authenticator.Authenticate(created.OneTimeCredential).Succeeded, "old-credential-authenticated-after-rotation");
            Require(issuer.Verify(currentRecord, rotated.OneTimeCredential), "rotated-credential-verification-failed");
            Require(authenticator.Authenticate(rotated.OneTimeCredential).Succeeded, "rotated-storage-authentication-failed");
            Require(ScalarLong(root, "SELECT COUNT(*) FROM bot_credentials WHERE BotId='" + created.Principal.BotId.ToString("D") + "'") == 2, "credential-history-not-retained");

            repository.AppendAudit(
                new BotAuditEvent
                {
                    Kind = BotAuditKind.InboundEventDelivered,
                    BotId = created.Principal.BotId,
                    AccountId = 1001,
                    OrganizationId = 88,
                    Succeeded = true,
                    ReasonCode = "DISPOSABLE_AUDIT",
                    TimestampUtc = DateTime.UtcNow,
                    AuditIdentity = "validation:1001"
                });
            BotAuditEvent[] audit = management.Audit(owner, created.Principal.BotId, 100);
            Require(audit.Length >= 7, "audit-history-incomplete");
            Require(audit[0].TimestampUtc >= audit[audit.Length - 1].TimestampUtc, "audit-order-mismatch");
            repository.AppendAudit(
                new BotAuditEvent
                {
                    Kind = BotAuditKind.AuthenticationFailure,
                    Succeeded = false,
                    ReasonCode = "NULL_SAFE_DISPOSABLE_EVENT",
                    TimestampUtc = DateTime.UtcNow,
                    AuditIdentity = "validation:system"
                });
            Require(ScalarLong(root, "SELECT COUNT(*) FROM bot_audit_events WHERE BotId IS NULL AND ActorIdentityId IS NULL AND ReasonCode='NULL_SAFE_DISPOSABLE_EVENT'") == 1, "null-safe-audit-not-persisted");

            management.RevokeCredentials(owner, created.Principal.BotId);
            Require(repository.FindCurrentCredential(created.Principal.BotId) == null, "revoked-current-credential-remained-active");
            Require(!authenticator.Authenticate(rotated.OneTimeCredential).Succeeded, "revoked-credential-authenticated");
            Require(!repository.FindPrincipal(created.Principal.BotId).Enabled, "revoke-did-not-disable-principal");

            return new LifecycleEvidence
            {
                BotId = created.Principal.BotId,
                InitialCredential = created.OneTimeCredential,
                RotatedCredential = rotated.OneTimeCredential
            };
        }

        private static void ExerciseTransactionRollbacks(string applicationConnectionString, MySqlConnection root)
        {
            IPersistentBotRepository repository = Repository(applicationConnectionString);
            BotAccountManagementService management = Management(repository);
            BotManagementContext owner = Context(1001);

            long principalsBefore = ScalarLong(root, "SELECT COUNT(*) FROM bot_principals");
            long credentialsBefore = ScalarLong(root, "SELECT COUNT(*) FROM bot_credentials");
            long auditsBefore = ScalarLong(root, "SELECT COUNT(*) FROM bot_audit_events");
            Execute(root, "CREATE TRIGGER force_scope_create_failure BEFORE INSERT ON bot_scopes FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced scope failure'");
            ExpectFailure(
                () => management.Create(owner, new BotManagementCreateRequest { DisplayName = "RollbackCreate", Scopes = BotScope.TellReceive }),
                "forced-create-failure-not-raised");
            Execute(root, "DROP TRIGGER force_scope_create_failure");
            Require(ScalarLong(root, "SELECT COUNT(*) FROM bot_principals") == principalsBefore, "create-rollback-left-principal");
            Require(ScalarLong(root, "SELECT COUNT(*) FROM bot_credentials") == credentialsBefore, "create-rollback-left-credential");
            Require(ScalarLong(root, "SELECT COUNT(*) FROM bot_audit_events") == auditsBefore, "create-rollback-left-audit");

            BotManagementResult rotationBot = management.Create(
                owner,
                new BotManagementCreateRequest { DisplayName = "RollbackRotation", Scopes = BotScope.TellReceive });
            Execute(root, "CREATE TRIGGER force_rotation_failure BEFORE INSERT ON bot_credentials FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced rotation failure'");
            ExpectFailure(() => management.RotateCredential(owner, rotationBot.Principal.BotId), "forced-rotation-failure-not-raised");
            Execute(root, "DROP TRIGGER force_rotation_failure");
            Require(repository.FindPrincipal(rotationBot.Principal.BotId).CurrentCredentialVersion == 1, "rotation-rollback-advanced-version");
            Require(!repository.FindCredential(rotationBot.PublicCredentialId).Revoked, "rotation-rollback-revoked-old-credential");

            BotManagementResult scopeBot = management.Create(
                owner,
                new BotManagementCreateRequest { DisplayName = "RollbackScopes", Scopes = BotScope.TellReceive });
            Execute(root, "CREATE TRIGGER force_scope_replace_failure BEFORE INSERT ON bot_scopes FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced scope replace failure'");
            ExpectFailure(() => management.UpdateScopes(owner, scopeBot.Principal.BotId, BotScope.TellSend), "forced-scope-failure-not-raised");
            Execute(root, "DROP TRIGGER force_scope_replace_failure");
            Require(repository.FindPrincipal(scopeBot.Principal.BotId).Scopes == BotScope.TellReceive, "scope-rollback-lost-prior-set");

            BotManagementResult revokeBot = management.Create(
                owner,
                new BotManagementCreateRequest { DisplayName = "RollbackRevoke", Scopes = BotScope.TellReceive });
            Execute(root, "CREATE TRIGGER force_audit_failure BEFORE INSERT ON bot_audit_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced audit failure'");
            ExpectFailure(() => management.RevokeCredentials(owner, revokeBot.Principal.BotId), "forced-revoke-failure-not-raised");
            Execute(root, "DROP TRIGGER force_audit_failure");
            Require(repository.FindPrincipal(revokeBot.Principal.BotId).Enabled, "revoke-rollback-disabled-principal");
            Require(repository.FindCurrentCredential(revokeBot.Principal.BotId) != null, "revoke-rollback-revoked-credential");

            BotManagementResult organizationBot = management.Create(
                owner,
                new BotManagementCreateRequest { DisplayName = "RollbackOrg", OrganizationId = 77, Scopes = BotScope.OrganizationRead });
            Execute(root, "CREATE TRIGGER force_org_audit_failure BEFORE INSERT ON bot_audit_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced org audit failure'");
            ExpectFailure(() => management.AssignOrganization(owner, organizationBot.Principal.BotId, 88), "forced-org-failure-not-raised");
            Execute(root, "DROP TRIGGER force_org_audit_failure");
            Require(repository.FindPrincipal(organizationBot.Principal.BotId).OrganizationId == 77, "organization-rollback-changed-value");
        }

        private static void ExerciseConstraintRejections(MySqlConnection connection)
        {
            const string validBot = "11111111-1111-4111-8111-111111111111";
            string principalColumns = "(BotId,OwningIdentityId,OrganizationId,DisplayName,NormalizedDisplayName,PrincipalStatus,CurrentCredentialVersion,RateLimitProfile,AuditIdentity,CreatedAt,UpdatedAt,DisabledAt)";
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz',1001,NULL,'BadId','badid','Enabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)"), "invalid-bot-id-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('22222222-2222-4222-8222-222222222222',1001,NULL,'','empty','Enabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)"), "empty-display-name-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('33333333-3333-4333-8333-333333333333',1001,NULL,'CaseOnly','CASEONLY','Enabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)"), "mismatched-normalized-name-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('44444444-4444-4444-8444-444444444444',1001,NULL,'ZeroVersion','zeroversion','Enabled',0,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)"), "zero-credential-version-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('55555555-5555-4555-8555-555555555555',1001,NULL,'DisabledNull','disablednull','Disabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)"), "disabled-null-timestamp-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('66666666-6666-4666-8666-666666666666',1001,NULL,'EnabledTimestamp','enabledtimestamp','Enabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))"), "enabled-disabled-timestamp-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('77777777-7777-4777-8777-777777777777',1001,0,'ZeroOrg','zeroorg','Enabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)"), "invalid-organization-id-accepted");

            Execute(connection, "INSERT INTO bot_principals " + principalColumns + " VALUES ('" + validBot + "',1001,NULL,'ConstraintBot','constraintbot','Enabled',1,'default','validation',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),NULL)");
            string credentialColumns = "(BotId,PublicCredentialId,CredentialVersion,Algorithm,Iterations,Salt,Verifier,CredentialState,CreatedAt,RevokedAt,RevocationReason)";
            string validBinary = "UNHEX(REPEAT('01',16)),UNHEX(REPEAT('02',32))";
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG',1,'PBKDF2-SHA256',120000," + validBinary + ",'Active',UTC_TIMESTAMP(6),NULL,NULL)"), "invalid-public-id-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',1,'PBKDF2-SHA256',119999," + validBinary + ",'Active',UTC_TIMESTAMP(6),NULL,NULL)"), "low-iterations-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','cccccccccccccccccccccccccccccccc',1,'PBKDF2-SHA256',120000," + validBinary + ",'Active',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),'bad')"), "active-revocation-metadata-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','dddddddddddddddddddddddddddddddd',1,'PBKDF2-SHA256',120000," + validBinary + ",'Revoked',UTC_TIMESTAMP(6),NULL,NULL)"), "revoked-without-metadata-accepted");
            Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',1,'PBKDF2-SHA256',120000," + validBinary + ",'Active',UTC_TIMESTAMP(6),NULL,NULL)");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',1,'PBKDF2-SHA256',120000," + validBinary + ",'Active',UTC_TIMESTAMP(6),NULL,NULL)"), "duplicate-bot-version-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_credentials " + credentialColumns + " VALUES ('" + validBot + "','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',2,'PBKDF2-SHA256',120000," + validBinary + ",'Active',UTC_TIMESTAMP(6),NULL,NULL)"), "duplicate-public-id-accepted");

            Execute(connection, "INSERT INTO bot_scopes (BotId,ScopeName,GrantedByIdentityId) VALUES ('" + validBot + "','TellReceive',1001)");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_scopes (BotId,ScopeName,GrantedByIdentityId) VALUES ('" + validBot + "','InvalidScope',1001)"), "invalid-scope-accepted");
            ExpectRejected(() => Execute(connection, "INSERT INTO bot_scopes (BotId,ScopeName,GrantedByIdentityId) VALUES ('" + validBot + "','TellReceive',1001)"), "duplicate-scope-accepted");
        }

        private static void VerifyCredentialSecrecy(MySqlConnection connection, LifecycleEvidence evidence)
        {
            string initialSecret = evidence.InitialCredential.Substring(evidence.InitialCredential.LastIndexOf('_') + 1);
            string rotatedSecret = evidence.RotatedCredential.Substring(evidence.RotatedCredential.LastIndexOf('_') + 1);
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM bot_credentials WHERE PublicCredentialId IN (@Initial,@Rotated)",
                new MySqlParameter("@Initial", evidence.InitialCredential), new MySqlParameter("@Rotated", evidence.RotatedCredential)) == 0, "full-credential-persisted");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM bot_credentials WHERE HEX(Salt) IN (@Initial,@Rotated) OR HEX(Verifier) IN (@Initial,@Rotated)",
                new MySqlParameter("@Initial", initialSecret.ToUpperInvariant()), new MySqlParameter("@Rotated", rotatedSecret.ToUpperInvariant())) == 0, "raw-secret-persisted-in-binary-field");
            Require(ScalarLong(connection, "SELECT COUNT(*) FROM bot_audit_events WHERE CONCAT_WS('|',EventType,OperationCode,ReasonCode,AuditIdentity) LIKE @Initial OR CONCAT_WS('|',EventType,OperationCode,ReasonCode,AuditIdentity) LIKE @Rotated",
                new MySqlParameter("@Initial", "%" + initialSecret + "%"), new MySqlParameter("@Rotated", "%" + rotatedSecret + "%")) == 0, "raw-secret-persisted-in-audit");
            Require(!evidence.InitialCredential.Contains(" ") && evidence.InitialCredential.Length == 104, "credential-shape-regressed");
            Require(!new BotCredentialIssue { BotId = evidence.BotId, Version = 1, Credential = evidence.InitialCredential }.ToString().Contains(initialSecret), "credential-diagnostic-leaked-secret");
        }

        private static void ExerciseConcurrentRotation(string applicationConnectionString, MySqlConnection root)
        {
            IPersistentBotRepository repository = Repository(applicationConnectionString);
            BotManagementResult created = Management(repository).Create(
                Context(1001),
                new BotManagementCreateRequest { DisplayName = "ConcurrentRotation", Scopes = BotScope.TellReceive });
            BotPrincipal snapshot = repository.FindPrincipal(created.Principal.BotId);
            BotPrincipal firstPrincipal = snapshot.Copy();
            BotPrincipal secondPrincipal = snapshot.Copy();
            firstPrincipal.CurrentCredentialVersion++;
            secondPrincipal.CurrentCredentialVersion++;
            firstPrincipal.UpdatedAtUtc = DateTime.UtcNow;
            secondPrincipal.UpdatedAtUtc = firstPrincipal.UpdatedAtUtc;
            BotCredentialRecord firstCredential;
            BotCredentialRecord secondCredential;
            new PersistentBotCredentialIssuer().Issue(snapshot.BotId, 2, out firstCredential);
            new PersistentBotCredentialIssuer().Issue(snapshot.BotId, 2, out secondCredential);
            int succeeded = 0;
            int failed = 0;
            ManualResetEventSlim start = new ManualResetEventSlim(false);
            Task first = Task.Run(() => RotateConcurrent(repository, firstPrincipal, firstCredential, start, ref succeeded, ref failed));
            Task second = Task.Run(() => RotateConcurrent(repository, secondPrincipal, secondCredential, start, ref succeeded, ref failed));
            start.Set();
            Task.WaitAll(first, second);
            Require(succeeded == 1 && failed == 1, "concurrent-rotation-outcome-not-deterministic");
            Require(ScalarLong(root, "SELECT COUNT(*) FROM bot_credentials WHERE BotId='" + snapshot.BotId.ToString("D") + "' AND CredentialState='Active'") == 1, "concurrent-rotation-split-brain-active-count");
            Require(repository.FindPrincipal(snapshot.BotId).CurrentCredentialVersion == 2, "concurrent-rotation-principal-version-mismatch");
            Require(ScalarLong(root, "SELECT COUNT(DISTINCT CredentialVersion) FROM bot_credentials WHERE BotId='" + snapshot.BotId.ToString("D") + "' AND CredentialVersion=2") == 1, "concurrent-rotation-duplicate-version");
        }

        private static void RotateConcurrent(
            IPersistentBotRepository repository,
            BotPrincipal principal,
            BotCredentialRecord credential,
            ManualResetEventSlim start,
            ref int succeeded,
            ref int failed)
        {
            start.Wait();
            try
            {
                repository.Rotate(
                    1001,
                    principal,
                    credential,
                    new BotAuditEvent
                    {
                        Kind = BotAuditKind.CredentialRotated,
                        BotId = principal.BotId,
                        AccountId = 1001,
                        Succeeded = true,
                        ReasonCode = "CONCURRENT_ROTATION",
                        TimestampUtc = DateTime.UtcNow,
                        AuditIdentity = "validation:1001"
                    });
                Interlocked.Increment(ref succeeded);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref failed);
            }
            catch (MySqlException)
            {
                Interlocked.Increment(ref failed);
            }
        }

        private static void ExerciseDeleteRestrictions(string applicationConnectionString, MySqlConnection root)
        {
            IPersistentBotRepository repository = Repository(applicationConnectionString);
            BotManagementResult created = Management(repository).Create(
                Context(1002),
                new BotManagementCreateRequest { DisplayName = "DeleteRestriction", Scopes = BotScope.TellReceive });
            Execute(root, "INSERT INTO bot_scopes (BotId,ScopeName,GrantedByIdentityId) VALUES ('" + created.Principal.BotId.ToString("D") + "','ChannelLeave',1003)");
            ExpectRejected(() => Execute(root, "DELETE FROM account_identities WHERE IdentityId=1002"), "owner-delete-not-restricted");
            ExpectRejected(() => Execute(root, "DELETE FROM bot_principals WHERE BotId='" + created.Principal.BotId.ToString("D") + "'"), "bot-delete-not-restricted");
            ExpectRejected(() => Execute(root, "DELETE FROM account_identities WHERE IdentityId=1003"), "scope-grantor-delete-not-restricted");
        }

        private static void ValidateRollbackAndStartupStates(
            string applicationConnectionString,
            MySqlConnection root,
            string schemaDirectory)
        {
            Execute(root, "DELETE FROM bot_audit_events");
            Execute(root, "DELETE FROM bot_scopes");
            Execute(root, "DELETE FROM bot_credentials");
            Execute(root, "DELETE FROM bot_principals");
            Require(ScalarLong(root, "SELECT (SELECT COUNT(*) FROM bot_principals)+(SELECT COUNT(*) FROM bot_credentials)+(SELECT COUNT(*) FROM bot_scopes)+(SELECT COUNT(*) FROM bot_audit_events)") == 0, "rollback-not-pre-data-empty");
            ExecuteFile(root, Path.Combine(schemaDirectory, "001_botservice_schema_rollback_empty.sql"));
            Require(CountBotTables(root) == 0, "rollback-left-bot-tables");
            Require(ScalarLong(root, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'account_%'") == 5, "rollback-touched-base-tables");
            ExpectInvalidOperation(
                () => ((IPersistentBotSchemaValidator)Repository(applicationConnectionString)).ValidateSchema(),
                "missing-schema-startup-accepted");

            ExecuteFile(root, Path.Combine(schemaDirectory, "001_botservice_schema_forward.sql"));
            ValidateBotSchema(root);
            ((IPersistentBotSchemaValidator)Repository(applicationConnectionString)).ValidateSchema();
            Require(true, "correct-schema-startup-failed");

            Execute(root, "ALTER TABLE bot_credentials MODIFY Salt binary(15) NOT NULL");
            ExpectInvalidOperation(
                () => ((IPersistentBotSchemaValidator)Repository(applicationConnectionString)).ValidateSchema(),
                "incompatible-schema-startup-accepted");
            Execute(root, "ALTER TABLE bot_credentials MODIFY Salt binary(16) NOT NULL");
            ((IPersistentBotSchemaValidator)Repository(applicationConnectionString)).ValidateSchema();
            ValidateBotSchema(root);
        }

        private static void SeedIdentities(MySqlConnection connection)
        {
            Execute(
                connection,
                "INSERT INTO account_identities (IdentityId,IdentityPublicId,CanonicalUsername,NormalizedUsername,CanonicalEmail,NormalizedEmail,EmailVerifiedAt,IdentityStatus) VALUES "
                + "(1001,'00000000-0000-0000-0000-000000001001','OwnerOne','ownerone',NULL,NULL,NULL,'Active'),"
                + "(1002,'00000000-0000-0000-0000-000000001002','OwnerTwo','ownertwo',NULL,NULL,NULL,'Active'),"
                + "(1003,'00000000-0000-0000-0000-000000001003','Grantor','grantor',NULL,NULL,NULL,'Active')");
        }

        private static void RestrictRuntimeAccount(MySqlConnection connection)
        {
            Execute(connection, "REVOKE ALL PRIVILEGES, GRANT OPTION FROM '" + DatabaseUser + "'@'%'");
            Execute(connection, "GRANT SELECT, INSERT, UPDATE, DELETE ON `" + DatabaseName + "`.* TO '" + DatabaseUser + "'@'%'");
            Execute(connection, "FLUSH PRIVILEGES");
        }

        private static IPersistentBotRepository Repository(string connectionString)
        {
            return new AdoNetBotRepository(() => new MySqlConnection(connectionString));
        }

        private static BotAccountManagementService Management(IPersistentBotRepository repository)
        {
            return new BotAccountManagementService(
                repository,
                new PersistentBotCredentialIssuer(),
                new ValidationOrganizationAuthority(),
                new DefaultBotScopePolicy());
        }

        private static BotManagementContext Context(long identityId)
        {
            return new BotManagementContext
            {
                AuthenticatedIdentityId = identityId,
                AuditIdentity = "validation:" + identityId.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static MySqlConnection WaitForMySql(string connectionString)
        {
            Exception lastFailure = null;
            for (int attempt = 0; attempt < 90; attempt++)
            {
                try
                {
                    return Open(connectionString);
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                    Thread.Sleep(2000);
                }
            }

            throw new InvalidOperationException("disposable-mysql-readiness-timeout", lastFailure);
        }

        private static MySqlConnection Open(string connectionString)
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        private static void ExecuteFile(MySqlConnection connection, string path)
        {
            Execute(connection, File.ReadAllText(path));
        }

        private static void Execute(MySqlConnection connection, string sql)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.CommandTimeout = 30;
                command.ExecuteNonQuery();
            }
        }

        private static long ScalarLong(MySqlConnection connection, string sql, params MySqlParameter[] parameters)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static string ScalarString(MySqlConnection connection, string sql)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                object result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
            }
        }

        private static long CountBotTables(MySqlConnection connection)
        {
            return ScalarLong(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('bot_principals','bot_credentials','bot_scopes','bot_audit_events')");
        }

        private static void RequireColumn(MySqlConnection connection, string table, string column, string type, string nullable)
        {
            Require(
                ScalarLong(
                    connection,
                    "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='" + table + "' AND column_name='" + column + "' AND column_type='" + type + "' AND is_nullable='" + nullable + "'") == 1,
                "column-metadata-mismatch-" + table + "-" + column);
        }

        private static void RequireConstraint(MySqlConnection connection, string name, string type)
        {
            Require(
                ScalarLong(
                    connection,
                    "SELECT COUNT(*) FROM information_schema.table_constraints WHERE constraint_schema=DATABASE() AND constraint_name='" + name + "' AND constraint_type='" + type + "'") == 1,
                "constraint-missing-" + name);
        }

        private static void RequireIndex(
            MySqlConnection connection,
            string table,
            string name,
            string columns,
            bool nonUnique)
        {
            string actual = ScalarString(
                connection,
                "SELECT CONCAT(non_unique,':',GROUP_CONCAT(column_name ORDER BY seq_in_index)) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='"
                + table + "' AND index_name='" + name + "' GROUP BY non_unique");
            Require(actual == (nonUnique ? "1:" : "0:") + columns, "index-mismatch-" + table + "-" + name);
        }

        private static void ExpectRejected(Action action, string failureCode)
        {
            try
            {
                action();
            }
            catch (MySqlException)
            {
                checks++;
                return;
            }

            throw new InvalidOperationException(failureCode);
        }

        private static void ExpectFailure(Action action, string failureCode)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                checks++;
                return;
            }

            throw new InvalidOperationException(failureCode);
        }

        private static void ExpectInvalidOperation(Action action, string failureCode)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                checks++;
                return;
            }

            throw new InvalidOperationException(failureCode);
        }

        private static void Require(bool condition, string failureCode)
        {
            if (!condition)
            {
                throw new InvalidOperationException(failureCode);
            }

            checks++;
        }

        private static string MutateSecret(string credential)
        {
            char replacement = credential[credential.Length - 1] == '0' ? '1' : '0';
            return credential.Substring(0, credential.Length - 1) + replacement;
        }

        private static string SafeFailure(Exception exception)
        {
            InvalidOperationException invalid = exception as InvalidOperationException;
            if (invalid != null && !string.IsNullOrWhiteSpace(invalid.Message))
            {
                return invalid.Message.Replace(' ', '-').ToLowerInvariant();
            }

            return exception.GetType().Name.ToLowerInvariant();
        }

        private sealed class LifecycleEvidence
        {
            public Guid BotId { get; set; }

            public string InitialCredential { get; set; }

            public string RotatedCredential { get; set; }
        }

        private sealed class ValidationOrganizationAuthority : IBotOrganizationAuthority
        {
            public bool CanAssign(long authenticatedIdentityId, long organizationId)
            {
                return (authenticatedIdentityId == 1001 || authenticatedIdentityId == 1002)
                    && (organizationId == 77 || organizationId == 88);
            }
        }

        private sealed class DisposableMySql : IDisposable
        {
            private string environmentFile;
            private bool containerCreated;
            private bool networkCreated;
            private bool volumeCreated;

            public string ApplicationConnectionString { get; private set; }

            public string RootConnectionString { get; private set; }

            public static DisposableMySql Create()
            {
                DisposableMySql result = new DisposableMySql();
                try
                {
                    RequireDockerResourceAbsent("container", ContainerName);
                    RequireDockerResourceAbsent("network", NetworkName);
                    RequireDockerResourceAbsent("volume", VolumeName);
                    Docker("image", "inspect", Image);
                    RequirePortAvailable();

                    string rootPassword = RandomSecret();
                    string applicationPassword = RandomSecret();
                    result.environmentFile = Path.Combine(Path.GetTempPath(), "aorebirth-bot-schema-" + Guid.NewGuid().ToString("N") + ".env");
                    File.WriteAllLines(
                        result.environmentFile,
                        new[]
                        {
                            "MYSQL_ROOT_PASSWORD=" + rootPassword,
                            "MYSQL_DATABASE=" + DatabaseName,
                            "MYSQL_USER=" + DatabaseUser,
                            "MYSQL_PASSWORD=" + applicationPassword
                        });
                    File.SetAttributes(result.environmentFile, FileAttributes.Hidden | FileAttributes.Temporary);

                    Docker("network", "create", "--label", LabelKey + "=" + LabelValue, NetworkName);
                    result.networkCreated = true;
                    Docker("volume", "create", "--label", LabelKey + "=" + LabelValue, VolumeName);
                    result.volumeCreated = true;
                    Docker(
                        "run",
                        "--detach",
                        "--name",
                        ContainerName,
                        "--label",
                        LabelKey + "=" + LabelValue,
                        "--restart",
                        "no",
                        "--network",
                        NetworkName,
                        "--publish",
                        "127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + ":3306",
                        "--env-file",
                        result.environmentFile,
                        "--volume",
                        VolumeName + ":/var/lib/mysql",
                        Image);
                    result.containerCreated = true;

                    result.RootConnectionString = ConnectionString("root", rootPassword);
                    result.ApplicationConnectionString = ConnectionString(DatabaseUser, applicationPassword);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                try
                {
                    if (this.containerCreated && HasExpectedLabel("container", ContainerName))
                    {
                        Docker("rm", "--force", ContainerName);
                    }

                    if (this.volumeCreated && HasExpectedLabel("volume", VolumeName))
                    {
                        Docker("volume", "rm", VolumeName);
                    }

                    if (this.networkCreated && HasExpectedLabel("network", NetworkName))
                    {
                        Docker("network", "rm", NetworkName);
                    }
                }
                finally
                {
                    if (!string.IsNullOrEmpty(this.environmentFile) && File.Exists(this.environmentFile))
                    {
                        File.SetAttributes(this.environmentFile, FileAttributes.Normal);
                        File.Delete(this.environmentFile);
                    }
                }
            }

            private static string ConnectionString(string user, string password)
            {
                return new MySqlConnectionStringBuilder
                {
                    Server = IPAddress.Loopback.ToString(),
                    Port = Port,
                    Database = DatabaseName,
                    UserID = user,
                    Password = password,
                    SslMode = MySqlSslMode.None,
                    ConnectionTimeout = 3,
                    DefaultCommandTimeout = 30,
                    AllowUserVariables = true
                }.ConnectionString;
            }

            private static void RequirePortAvailable()
            {
                TcpListener listener = new TcpListener(IPAddress.Loopback, checked((int)Port));
                try
                {
                    listener.Start();
                }
                finally
                {
                    listener.Stop();
                }
            }

            private static string RandomSecret()
            {
                byte[] bytes = new byte[36];
                RandomNumberGenerator.Fill(bytes);
                return Convert.ToBase64String(bytes);
            }

            private static void RequireDockerResourceAbsent(string resource, string name)
            {
                ProcessResult result = DockerResult(resource, "inspect", name);
                if (result.ExitCode == 0)
                {
                    throw new InvalidOperationException("disposable-docker-resource-already-exists-" + name);
                }
            }

            private static bool HasExpectedLabel(string resource, string name)
            {
                string format = "{{index .Labels \"" + LabelKey + "\"}}";
                if (resource == "container")
                {
                    format = "{{index .Config.Labels \"" + LabelKey + "\"}}";
                }

                ProcessResult result = DockerResult(resource, "inspect", "--format", format, name);
                return result.ExitCode == 0 && string.Equals(result.Output.Trim(), LabelValue, StringComparison.Ordinal);
            }

            private static void Docker(params string[] arguments)
            {
                ProcessResult result = DockerResult(arguments);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException("docker-command-failed");
                }
            }

            private static ProcessResult DockerResult(params string[] arguments)
            {
                ProcessStartInfo start = new ProcessStartInfo("docker")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                foreach (string argument in arguments)
                {
                    start.ArgumentList.Add(argument);
                }

                using (Process process = Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return new ProcessResult { ExitCode = process.ExitCode, Output = output };
                }
            }

            private sealed class ProcessResult
            {
                public int ExitCode { get; set; }

                public string Output { get; set; }
            }
        }
    }
}
