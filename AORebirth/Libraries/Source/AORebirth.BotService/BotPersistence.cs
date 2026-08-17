namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    public interface IPersistentBotRepository : IBotIdentityRepository
    {
        BotPrincipal[] ListPrincipals(long owningAccountId);

        BotPrincipal[] ListEnabledPrincipals();

        BotCredentialRecord FindCurrentCredential(Guid botId);

        BotAuditEvent[] ListAuditEvents(Guid botId, int maximumCount);

        void Create(BotPrincipal principal, BotCredentialRecord credential, BotAuditEvent auditEvent);

        void SetEnabled(long owningAccountId, Guid botId, bool enabled, BotAuditEvent auditEvent);

        void Rotate(long owningAccountId, BotPrincipal principal, BotCredentialRecord credential, BotAuditEvent auditEvent);

        void RevokeAll(long owningAccountId, Guid botId, DateTime revokedAtUtc, BotAuditEvent auditEvent);

        void ReplaceScopes(long owningAccountId, Guid botId, BotScope scopes, BotAuditEvent auditEvent);

        void AssignOrganization(long owningAccountId, Guid botId, long? organizationId, BotAuditEvent auditEvent);

        void AppendAudit(BotAuditEvent auditEvent);
    }

    public sealed class PersistentBotCredentialIssuer
    {
        public const int DefaultIterations = 120000;
        private readonly Func<DateTime> utcNow;

        public PersistentBotCredentialIssuer(Func<DateTime> utcNow = null)
        {
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public BotCredentialIssue Issue(Guid botId, int version, out BotCredentialRecord record)
        {
            if (botId == Guid.Empty || version < 1)
            {
                throw new ArgumentException("A bot id and positive credential version are required.");
            }

            byte[] publicBytes = RandomBytes(16);
            byte[] secretBytes = RandomBytes(32);
            byte[] salt = RandomBytes(16);
            string publicId = ToHex(publicBytes);
            string secret = ToHex(secretBytes);
            DateTime now = this.utcNow();
            record = new BotCredentialRecord
            {
                PublicCredentialId = publicId,
                BotId = botId,
                Version = version,
                Algorithm = "PBKDF2-SHA256",
                Iterations = DefaultIterations,
                Salt = salt,
                Verifier = BotCredentialManager.DeriveVerifier(botId, version, secret, salt, DefaultIterations),
                Revoked = false,
                CreatedAtUtc = now
            };
            return new BotCredentialIssue
            {
                Credential = "bot_v1_" + publicId + "_" + secret,
                PublicCredentialId = publicId,
                BotId = botId,
                Version = version
            };
        }

        public bool Verify(BotCredentialRecord record, string credential)
        {
            if (record == null || record.Revoked)
            {
                return false;
            }

            string publicId;
            string secret;
            if (!BotCredentialManager.TryParse(credential, out publicId, out secret)
                || !string.Equals(publicId, record.PublicCredentialId, StringComparison.Ordinal))
            {
                return false;
            }

            byte[] candidate = BotCredentialManager.DeriveVerifier(
                record.BotId,
                record.Version,
                secret,
                record.Salt,
                record.Iterations);
            return BotCredentialManager.FixedTimeEquals(candidate, record.Verifier);
        }

        private static byte[] RandomBytes(int length)
        {
            byte[] value = new byte[length];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(value);
            }

            return value;
        }

        private static string ToHex(byte[] value)
        {
            StringBuilder builder = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
            {
                builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    public sealed class InMemoryPersistentBotRepository : IPersistentBotRepository
    {
        private readonly object sync = new object();
        private readonly Dictionary<Guid, BotPrincipal> principals = new Dictionary<Guid, BotPrincipal>();
        private readonly Dictionary<string, BotCredentialRecord> credentials =
            new Dictionary<string, BotCredentialRecord>(StringComparer.Ordinal);
        private readonly List<BotAuditEvent> auditEvents = new List<BotAuditEvent>();

        public BotPrincipal FindPrincipal(Guid botId)
        {
            lock (this.sync)
            {
                BotPrincipal principal;
                return this.principals.TryGetValue(botId, out principal) ? principal.Copy() : null;
            }
        }

        public BotCredentialRecord FindCredential(string publicCredentialId)
        {
            lock (this.sync)
            {
                BotCredentialRecord credential;
                return publicCredentialId != null && this.credentials.TryGetValue(publicCredentialId, out credential)
                    ? credential.Copy()
                    : null;
            }
        }

        public BotCredentialRecord FindCurrentCredential(Guid botId)
        {
            lock (this.sync)
            {
                BotPrincipal principal;
                if (!this.principals.TryGetValue(botId, out principal))
                {
                    return null;
                }

                foreach (BotCredentialRecord credential in this.credentials.Values)
                {
                    if (credential.BotId == botId
                        && credential.Version == principal.CurrentCredentialVersion
                        && !credential.Revoked)
                    {
                        return credential.Copy();
                    }
                }

                return null;
            }
        }

        public BotPrincipal[] ListPrincipals(long owningAccountId)
        {
            lock (this.sync)
            {
                List<BotPrincipal> result = new List<BotPrincipal>();
                foreach (BotPrincipal principal in this.principals.Values)
                {
                    if (principal.OwningAccountId == owningAccountId)
                    {
                        result.Add(principal.Copy());
                    }
                }

                return result.ToArray();
            }
        }

        public BotPrincipal[] ListEnabledPrincipals()
        {
            lock (this.sync)
            {
                List<BotPrincipal> result = new List<BotPrincipal>();
                foreach (BotPrincipal principal in this.principals.Values)
                {
                    if (principal.Enabled)
                    {
                        result.Add(principal.Copy());
                    }
                }

                return result.ToArray();
            }
        }

        public BotAuditEvent[] ListAuditEvents(Guid botId, int maximumCount)
        {
            lock (this.sync)
            {
                List<BotAuditEvent> result = new List<BotAuditEvent>();
                for (int index = this.auditEvents.Count - 1; index >= 0 && result.Count < maximumCount; index--)
                {
                    if (this.auditEvents[index].BotId == botId)
                    {
                        result.Add(this.auditEvents[index]);
                    }
                }

                return result.ToArray();
            }
        }

        public void Create(BotPrincipal principal, BotCredentialRecord credential, BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                if (this.principals.ContainsKey(principal.BotId))
                {
                    throw new InvalidOperationException("Bot principal already exists.");
                }

                this.principals.Add(principal.BotId, principal.Copy());
                this.credentials.Add(credential.PublicCredentialId, credential.Copy());
                this.auditEvents.Add(auditEvent);
            }
        }

        public void SetEnabled(long owningAccountId, Guid botId, bool enabled, BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                BotPrincipal principal = this.RequireOwned(owningAccountId, botId);
                principal.Enabled = enabled;
                principal.UpdatedAtUtc = auditEvent.TimestampUtc;
                this.auditEvents.Add(auditEvent);
            }
        }

        public void Rotate(long owningAccountId, BotPrincipal principal, BotCredentialRecord credential, BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                BotPrincipal stored = this.RequireOwned(owningAccountId, principal.BotId);
                if (principal.CurrentCredentialVersion != stored.CurrentCredentialVersion + 1)
                {
                    throw new InvalidOperationException("Credential rotation version is stale.");
                }

                foreach (BotCredentialRecord existing in this.credentials.Values)
                {
                    if (existing.BotId == principal.BotId && !existing.Revoked)
                    {
                        existing.Revoked = true;
                        existing.RevokedAtUtc = auditEvent.TimestampUtc;
                    }
                }

                this.credentials.Add(credential.PublicCredentialId, credential.Copy());
                this.principals[principal.BotId] = principal.Copy();
                this.auditEvents.Add(auditEvent);
            }
        }

        public void RevokeAll(long owningAccountId, Guid botId, DateTime revokedAtUtc, BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                BotPrincipal principal = this.RequireOwned(owningAccountId, botId);
                principal.Enabled = false;
                principal.UpdatedAtUtc = revokedAtUtc;
                foreach (BotCredentialRecord credential in this.credentials.Values)
                {
                    if (credential.BotId == botId && !credential.Revoked)
                    {
                        credential.Revoked = true;
                        credential.RevokedAtUtc = revokedAtUtc;
                    }
                }

                this.auditEvents.Add(auditEvent);
            }
        }

        public void ReplaceScopes(long owningAccountId, Guid botId, BotScope scopes, BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                BotPrincipal principal = this.RequireOwned(owningAccountId, botId);
                principal.Scopes = scopes;
                principal.UpdatedAtUtc = auditEvent.TimestampUtc;
                this.auditEvents.Add(auditEvent);
            }
        }

        public void AssignOrganization(long owningAccountId, Guid botId, long? organizationId, BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                BotPrincipal principal = this.RequireOwned(owningAccountId, botId);
                principal.OrganizationId = organizationId;
                principal.UpdatedAtUtc = auditEvent.TimestampUtc;
                this.auditEvents.Add(auditEvent);
            }
        }

        public void AppendAudit(BotAuditEvent auditEvent)
        {
            lock (this.sync)
            {
                this.auditEvents.Add(auditEvent);
            }
        }

        public void SavePrincipal(BotPrincipal principal)
        {
            lock (this.sync)
            {
                this.principals[principal.BotId] = principal.Copy();
            }
        }

        public void SaveCredential(BotCredentialRecord credential)
        {
            lock (this.sync)
            {
                this.credentials[credential.PublicCredentialId] = credential.Copy();
            }
        }

        public void RevokeCredential(string publicCredentialId, DateTime revokedAtUtc)
        {
            lock (this.sync)
            {
                BotCredentialRecord credential;
                if (this.credentials.TryGetValue(publicCredentialId, out credential))
                {
                    credential.Revoked = true;
                    credential.RevokedAtUtc = revokedAtUtc;
                }
            }
        }

        public void RevokeOtherCredentials(Guid botId, string exceptPublicCredentialId, DateTime revokedAtUtc)
        {
            lock (this.sync)
            {
                foreach (BotCredentialRecord credential in this.credentials.Values)
                {
                    if (credential.BotId == botId
                        && !string.Equals(credential.PublicCredentialId, exceptPublicCredentialId, StringComparison.Ordinal))
                    {
                        credential.Revoked = true;
                        credential.RevokedAtUtc = revokedAtUtc;
                    }
                }
            }
        }

        private BotPrincipal RequireOwned(long owningAccountId, Guid botId)
        {
            BotPrincipal principal;
            if (!this.principals.TryGetValue(botId, out principal) || principal.OwningAccountId != owningAccountId)
            {
                throw new InvalidOperationException("Bot principal was not found for the authenticated owner.");
            }

            return principal;
        }
    }

    public sealed class AdoNetBotRepository : IPersistentBotRepository
    {
        private readonly Func<IDbConnection> connectionFactory;

        public AdoNetBotRepository(Func<IDbConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException("connectionFactory");
        }

        public BotPrincipal FindPrincipal(Guid botId)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return this.ReadPrincipal(connection, null, botId, null);
            }
        }

        public BotCredentialRecord FindCredential(string publicCredentialId)
        {
            using (IDbConnection connection = this.OpenConnection())
            using (IDbCommand command = CreateCommand(connection, null,
                "SELECT PublicCredentialId, BotId, CredentialVersion, Algorithm, Iterations, Salt, Verifier, CredentialState, CreatedAt, RevokedAt "
                + "FROM bot_credentials WHERE PublicCredentialId = @PublicCredentialId"))
            {
                AddParameter(command, "@PublicCredentialId", publicCredentialId);
                return ReadCredential(command);
            }
        }

        public BotCredentialRecord FindCurrentCredential(Guid botId)
        {
            using (IDbConnection connection = this.OpenConnection())
            using (IDbCommand command = CreateCommand(connection, null,
                "SELECT c.PublicCredentialId, c.BotId, c.CredentialVersion, c.Algorithm, c.Iterations, c.Salt, c.Verifier, c.CredentialState, c.CreatedAt, c.RevokedAt "
                + "FROM bot_credentials c INNER JOIN bot_principals p ON p.BotId = c.BotId "
                + "WHERE c.BotId = @BotId AND c.CredentialVersion = p.CurrentCredentialVersion AND c.CredentialState = 'Active'"))
            {
                AddParameter(command, "@BotId", botId.ToString("D"));
                return ReadCredential(command);
            }
        }

        public BotPrincipal[] ListPrincipals(long owningAccountId)
        {
            return this.ListPrincipals("WHERE OwningIdentityId = @OwningIdentityId", owningAccountId);
        }

        public BotPrincipal[] ListEnabledPrincipals()
        {
            return this.ListPrincipals("WHERE PrincipalStatus = 'Enabled'", null);
        }

        public BotAuditEvent[] ListAuditEvents(Guid botId, int maximumCount)
        {
            if (maximumCount < 1 || maximumCount > 500)
            {
                throw new ArgumentOutOfRangeException("maximumCount");
            }

            using (IDbConnection connection = this.OpenConnection())
            using (IDbCommand command = CreateCommand(connection, null,
                "SELECT EventType, BotId, ActorIdentityId, OrganizationId, SessionId, OperationCode, Outcome, ReasonCode, CreatedAt "
                + "FROM bot_audit_events WHERE BotId = @BotId ORDER BY AuditEventId DESC LIMIT "
                + maximumCount.ToString(CultureInfo.InvariantCulture)))
            {
                AddParameter(command, "@BotId", botId.ToString("D"));
                List<BotAuditEvent> result = new List<BotAuditEvent>();
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        BotAuditKind kind;
                        Enum.TryParse(Convert.ToString(reader[0], CultureInfo.InvariantCulture), out kind);
                        BotOperation operation;
                        Enum.TryParse(Convert.ToString(reader[5], CultureInfo.InvariantCulture), out operation);
                        result.Add(new BotAuditEvent
                        {
                            Kind = kind,
                            BotId = ParseNullableGuid(reader[1]),
                            AccountId = ParseNullableInt64(reader[2]),
                            OrganizationId = ParseNullableInt64(reader[3]),
                            SessionId = ParseNullableGuid(reader[4]),
                            Operation = operation,
                            Succeeded = string.Equals(Convert.ToString(reader[6], CultureInfo.InvariantCulture), "Success", StringComparison.Ordinal),
                            ReasonCode = DbString(reader[7]),
                            TimestampUtc = AsUtc(Convert.ToDateTime(reader[8], CultureInfo.InvariantCulture))
                        });
                    }
                }

                return result.ToArray();
            }
        }

        public void Create(BotPrincipal principal, BotCredentialRecord credential, BotAuditEvent auditEvent)
        {
            this.InTransaction((connection, transaction) =>
            {
                this.InsertPrincipal(connection, transaction, principal);
                InsertCredential(connection, transaction, credential);
                ReplaceScopeRows(connection, transaction, principal.BotId, principal.Scopes, principal.OwningAccountId, principal.CreatedAtUtc);
                InsertAudit(connection, transaction, auditEvent);
            });
        }

        public void SetEnabled(long owningAccountId, Guid botId, bool enabled, BotAuditEvent auditEvent)
        {
            this.InTransaction((connection, transaction) =>
            {
                RequireOwned(connection, transaction, owningAccountId, botId);
                Execute(connection, transaction,
                    "UPDATE bot_principals SET PrincipalStatus = @Status, DisabledAt = @DisabledAt, UpdatedAt = @UpdatedAt WHERE BotId = @BotId",
                    new Parameter("@Status", enabled ? "Enabled" : "Disabled"),
                    new Parameter("@DisabledAt", enabled ? null : (object)auditEvent.TimestampUtc),
                    new Parameter("@UpdatedAt", auditEvent.TimestampUtc),
                    new Parameter("@BotId", botId.ToString("D")));
                InsertAudit(connection, transaction, auditEvent);
            });
        }

        public void Rotate(long owningAccountId, BotPrincipal principal, BotCredentialRecord credential, BotAuditEvent auditEvent)
        {
            this.InTransaction((connection, transaction) =>
            {
                BotPrincipal stored = RequireOwned(connection, transaction, owningAccountId, principal.BotId);
                if (principal.CurrentCredentialVersion != stored.CurrentCredentialVersion + 1)
                {
                    throw new InvalidOperationException("Credential rotation version is stale.");
                }

                Execute(connection, transaction,
                    "UPDATE bot_credentials SET CredentialState = 'Superseded', RevokedAt = @RevokedAt, RevocationReason = 'rotation' "
                    + "WHERE BotId = @BotId AND CredentialState = 'Active'",
                    new Parameter("@RevokedAt", auditEvent.TimestampUtc),
                    new Parameter("@BotId", principal.BotId.ToString("D")));
                InsertCredential(connection, transaction, credential);
                Execute(connection, transaction,
                    "UPDATE bot_principals SET CurrentCredentialVersion = @Version, UpdatedAt = @UpdatedAt WHERE BotId = @BotId",
                    new Parameter("@Version", principal.CurrentCredentialVersion),
                    new Parameter("@UpdatedAt", auditEvent.TimestampUtc),
                    new Parameter("@BotId", principal.BotId.ToString("D")));
                InsertAudit(connection, transaction, auditEvent);
            });
        }

        public void RevokeAll(long owningAccountId, Guid botId, DateTime revokedAtUtc, BotAuditEvent auditEvent)
        {
            this.InTransaction((connection, transaction) =>
            {
                RequireOwned(connection, transaction, owningAccountId, botId);
                Execute(connection, transaction,
                    "UPDATE bot_credentials SET CredentialState = 'Revoked', RevokedAt = @RevokedAt, RevocationReason = 'owner_revoke' "
                    + "WHERE BotId = @BotId AND CredentialState = 'Active'",
                    new Parameter("@RevokedAt", revokedAtUtc),
                    new Parameter("@BotId", botId.ToString("D")));
                Execute(connection, transaction,
                    "UPDATE bot_principals SET PrincipalStatus = 'Disabled', DisabledAt = @DisabledAt, UpdatedAt = @UpdatedAt WHERE BotId = @BotId",
                    new Parameter("@DisabledAt", revokedAtUtc),
                    new Parameter("@UpdatedAt", revokedAtUtc),
                    new Parameter("@BotId", botId.ToString("D")));
                InsertAudit(connection, transaction, auditEvent);
            });
        }

        public void ReplaceScopes(long owningAccountId, Guid botId, BotScope scopes, BotAuditEvent auditEvent)
        {
            this.InTransaction((connection, transaction) =>
            {
                RequireOwned(connection, transaction, owningAccountId, botId);
                Execute(connection, transaction, "DELETE FROM bot_scopes WHERE BotId = @BotId", new Parameter("@BotId", botId.ToString("D")));
                ReplaceScopeRows(connection, transaction, botId, scopes, owningAccountId, auditEvent.TimestampUtc);
                Execute(connection, transaction, "UPDATE bot_principals SET UpdatedAt = @UpdatedAt WHERE BotId = @BotId",
                    new Parameter("@UpdatedAt", auditEvent.TimestampUtc), new Parameter("@BotId", botId.ToString("D")));
                InsertAudit(connection, transaction, auditEvent);
            });
        }

        public void AssignOrganization(long owningAccountId, Guid botId, long? organizationId, BotAuditEvent auditEvent)
        {
            this.InTransaction((connection, transaction) =>
            {
                RequireOwned(connection, transaction, owningAccountId, botId);
                Execute(connection, transaction,
                    "UPDATE bot_principals SET OrganizationId = @OrganizationId, UpdatedAt = @UpdatedAt WHERE BotId = @BotId",
                    new Parameter("@OrganizationId", organizationId),
                    new Parameter("@UpdatedAt", auditEvent.TimestampUtc),
                    new Parameter("@BotId", botId.ToString("D")));
                InsertAudit(connection, transaction, auditEvent);
            });
        }

        public void AppendAudit(BotAuditEvent auditEvent)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                InsertAudit(connection, null, auditEvent);
            }
        }

        public void SavePrincipal(BotPrincipal principal)
        {
            throw new NotSupportedException("Use an atomic persistent repository operation.");
        }

        public void SaveCredential(BotCredentialRecord credential)
        {
            throw new NotSupportedException("Use an atomic persistent repository operation.");
        }

        public void RevokeCredential(string publicCredentialId, DateTime revokedAtUtc)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                Execute(connection, null,
                    "UPDATE bot_credentials SET CredentialState = 'Revoked', RevokedAt = @RevokedAt, RevocationReason = 'direct_revoke' "
                    + "WHERE PublicCredentialId = @PublicCredentialId AND CredentialState = 'Active'",
                    new Parameter("@RevokedAt", revokedAtUtc), new Parameter("@PublicCredentialId", publicCredentialId));
            }
        }

        public void RevokeOtherCredentials(Guid botId, string exceptPublicCredentialId, DateTime revokedAtUtc)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                Execute(connection, null,
                    "UPDATE bot_credentials SET CredentialState = 'Superseded', RevokedAt = @RevokedAt, RevocationReason = 'rotation' "
                    + "WHERE BotId = @BotId AND PublicCredentialId <> @ExceptId AND CredentialState = 'Active'",
                    new Parameter("@RevokedAt", revokedAtUtc), new Parameter("@BotId", botId.ToString("D")),
                    new Parameter("@ExceptId", exceptPublicCredentialId));
            }
        }

        private BotPrincipal[] ListPrincipals(string whereClause, long? owningAccountId)
        {
            using (IDbConnection connection = this.OpenConnection())
            using (IDbCommand command = CreateCommand(connection, null,
                "SELECT BotId, DisplayName, OwningIdentityId, OrganizationId, PrincipalStatus, CurrentCredentialVersion, RateLimitProfile, AuditIdentity, CreatedAt, UpdatedAt "
                + "FROM bot_principals " + whereClause + " ORDER BY BotId"))
            {
                if (owningAccountId.HasValue)
                {
                    AddParameter(command, "@OwningIdentityId", owningAccountId.Value);
                }

                List<BotPrincipal> result = new List<BotPrincipal>();
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(MapPrincipal(reader));
                    }
                }

                foreach (BotPrincipal principal in result)
                {
                    principal.Scopes = ReadScopes(connection, null, principal.BotId);
                }

                return result.ToArray();
            }
        }

        private BotPrincipal ReadPrincipal(IDbConnection connection, IDbTransaction transaction, Guid botId, long? owningAccountId)
        {
            string sql = "SELECT BotId, DisplayName, OwningIdentityId, OrganizationId, PrincipalStatus, CurrentCredentialVersion, RateLimitProfile, AuditIdentity, CreatedAt, UpdatedAt "
                + "FROM bot_principals WHERE BotId = @BotId";
            if (owningAccountId.HasValue)
            {
                sql += " AND OwningIdentityId = @OwningIdentityId";
            }

            using (IDbCommand command = CreateCommand(connection, transaction, sql))
            {
                AddParameter(command, "@BotId", botId.ToString("D"));
                if (owningAccountId.HasValue)
                {
                    AddParameter(command, "@OwningIdentityId", owningAccountId.Value);
                }

                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    BotPrincipal principal = MapPrincipal(reader);
                    reader.Close();
                    principal.Scopes = ReadScopes(connection, transaction, botId);
                    return principal;
                }
            }
        }

        private static BotPrincipal MapPrincipal(IDataRecord reader)
        {
            return new BotPrincipal
            {
                BotId = Guid.Parse(Convert.ToString(reader[0], CultureInfo.InvariantCulture)),
                DisplayName = Convert.ToString(reader[1], CultureInfo.InvariantCulture),
                OwningAccountId = Convert.ToInt64(reader[2], CultureInfo.InvariantCulture),
                OrganizationId = ParseNullableInt64(reader[3]),
                Enabled = string.Equals(Convert.ToString(reader[4], CultureInfo.InvariantCulture), "Enabled", StringComparison.Ordinal),
                CurrentCredentialVersion = Convert.ToInt32(reader[5], CultureInfo.InvariantCulture),
                RateLimitProfile = Convert.ToString(reader[6], CultureInfo.InvariantCulture),
                AuditIdentity = Convert.ToString(reader[7], CultureInfo.InvariantCulture),
                CreatedAtUtc = AsUtc(Convert.ToDateTime(reader[8], CultureInfo.InvariantCulture)),
                UpdatedAtUtc = AsUtc(Convert.ToDateTime(reader[9], CultureInfo.InvariantCulture))
            };
        }

        private static BotCredentialRecord ReadCredential(IDbCommand command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                return new BotCredentialRecord
                {
                    PublicCredentialId = Convert.ToString(reader[0], CultureInfo.InvariantCulture),
                    BotId = Guid.Parse(Convert.ToString(reader[1], CultureInfo.InvariantCulture)),
                    Version = Convert.ToInt32(reader[2], CultureInfo.InvariantCulture),
                    Algorithm = Convert.ToString(reader[3], CultureInfo.InvariantCulture),
                    Iterations = Convert.ToInt32(reader[4], CultureInfo.InvariantCulture),
                    Salt = (byte[])reader[5],
                    Verifier = (byte[])reader[6],
                    Revoked = !string.Equals(Convert.ToString(reader[7], CultureInfo.InvariantCulture), "Active", StringComparison.Ordinal),
                    CreatedAtUtc = AsUtc(Convert.ToDateTime(reader[8], CultureInfo.InvariantCulture)),
                    RevokedAtUtc = reader.IsDBNull(9) ? (DateTime?)null : AsUtc(Convert.ToDateTime(reader[9], CultureInfo.InvariantCulture))
                };
            }
        }

        private void InsertPrincipal(IDbConnection connection, IDbTransaction transaction, BotPrincipal principal)
        {
            Execute(connection, transaction,
                "INSERT INTO bot_principals (BotId, OwningIdentityId, OrganizationId, DisplayName, NormalizedDisplayName, PrincipalStatus, CurrentCredentialVersion, RateLimitProfile, AuditIdentity, CreatedAt, UpdatedAt) "
                + "VALUES (@BotId, @Owner, @OrganizationId, @DisplayName, @NormalizedDisplayName, @Status, @Version, @RateLimitProfile, @AuditIdentity, @CreatedAt, @UpdatedAt)",
                new Parameter("@BotId", principal.BotId.ToString("D")),
                new Parameter("@Owner", principal.OwningAccountId),
                new Parameter("@OrganizationId", principal.OrganizationId),
                new Parameter("@DisplayName", principal.DisplayName),
                new Parameter("@NormalizedDisplayName", principal.DisplayName.ToLowerInvariant()),
                new Parameter("@Status", principal.Enabled ? "Enabled" : "Disabled"),
                new Parameter("@Version", principal.CurrentCredentialVersion),
                new Parameter("@RateLimitProfile", principal.RateLimitProfile),
                new Parameter("@AuditIdentity", principal.AuditIdentity),
                new Parameter("@CreatedAt", principal.CreatedAtUtc),
                new Parameter("@UpdatedAt", principal.UpdatedAtUtc));
        }

        private static void InsertCredential(IDbConnection connection, IDbTransaction transaction, BotCredentialRecord credential)
        {
            Execute(connection, transaction,
                "INSERT INTO bot_credentials (BotId, PublicCredentialId, CredentialVersion, Algorithm, Iterations, Salt, Verifier, CredentialState, CreatedAt) "
                + "VALUES (@BotId, @PublicId, @Version, @Algorithm, @Iterations, @Salt, @Verifier, 'Active', @CreatedAt)",
                new Parameter("@BotId", credential.BotId.ToString("D")), new Parameter("@PublicId", credential.PublicCredentialId),
                new Parameter("@Version", credential.Version), new Parameter("@Algorithm", credential.Algorithm),
                new Parameter("@Iterations", credential.Iterations), new Parameter("@Salt", credential.Salt),
                new Parameter("@Verifier", credential.Verifier), new Parameter("@CreatedAt", credential.CreatedAtUtc));
        }

        private static void ReplaceScopeRows(
            IDbConnection connection,
            IDbTransaction transaction,
            Guid botId,
            BotScope scopes,
            long grantedByIdentityId,
            DateTime createdAtUtc)
        {
            foreach (BotScope scope in Enum.GetValues(typeof(BotScope)))
            {
                if (scope != BotScope.None && (scopes & scope) == scope)
                {
                    Execute(connection, transaction,
                        "INSERT INTO bot_scopes (BotId, ScopeName, GrantedByIdentityId, CreatedAt) VALUES (@BotId, @ScopeName, @GrantedBy, @CreatedAt)",
                        new Parameter("@BotId", botId.ToString("D")), new Parameter("@ScopeName", scope.ToString()),
                        new Parameter("@GrantedBy", grantedByIdentityId), new Parameter("@CreatedAt", createdAtUtc));
                }
            }
        }

        private static BotScope ReadScopes(IDbConnection connection, IDbTransaction transaction, Guid botId)
        {
            using (IDbCommand command = CreateCommand(connection, transaction, "SELECT ScopeName FROM bot_scopes WHERE BotId = @BotId"))
            {
                AddParameter(command, "@BotId", botId.ToString("D"));
                BotScope scopes = BotScope.None;
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        BotScope scope;
                        if (Enum.TryParse(Convert.ToString(reader[0], CultureInfo.InvariantCulture), out scope))
                        {
                            scopes |= scope;
                        }
                    }
                }

                return scopes;
            }
        }

        private static void InsertAudit(IDbConnection connection, IDbTransaction transaction, BotAuditEvent auditEvent)
        {
            Execute(connection, transaction,
                "INSERT INTO bot_audit_events (BotId, ActorIdentityId, OrganizationId, SessionId, EventType, OperationCode, Outcome, ReasonCode, AuditIdentity, CreatedAt) "
                + "VALUES (@BotId, @Actor, @OrganizationId, @SessionId, @EventType, @Operation, @Outcome, @ReasonCode, @AuditIdentity, @CreatedAt)",
                new Parameter("@BotId", auditEvent.BotId.HasValue ? (object)auditEvent.BotId.Value.ToString("D") : null),
                new Parameter("@Actor", auditEvent.AccountId), new Parameter("@OrganizationId", auditEvent.OrganizationId),
                new Parameter("@SessionId", auditEvent.SessionId.HasValue ? (object)auditEvent.SessionId.Value.ToString("D") : null),
                new Parameter("@EventType", auditEvent.Kind.ToString()), new Parameter("@Operation", auditEvent.Operation.ToString()),
                new Parameter("@Outcome", auditEvent.Succeeded ? "Success" : "Denied"), new Parameter("@ReasonCode", auditEvent.ReasonCode),
                new Parameter("@AuditIdentity", auditEvent.AuditIdentity), new Parameter("@CreatedAt", auditEvent.TimestampUtc));
        }

        private static BotPrincipal RequireOwned(IDbConnection connection, IDbTransaction transaction, long owningAccountId, Guid botId)
        {
            using (IDbCommand command = CreateCommand(connection, transaction,
                "SELECT BotId, DisplayName, OwningIdentityId, OrganizationId, PrincipalStatus, CurrentCredentialVersion, RateLimitProfile, AuditIdentity, CreatedAt, UpdatedAt "
                + "FROM bot_principals WHERE BotId = @BotId AND OwningIdentityId = @Owner FOR UPDATE"))
            {
                AddParameter(command, "@BotId", botId.ToString("D"));
                AddParameter(command, "@Owner", owningAccountId);
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("Bot principal was not found for the authenticated owner.");
                    }

                    return MapPrincipal(reader);
                }
            }
        }

        private void InTransaction(Action<IDbConnection, IDbTransaction> action)
        {
            using (IDbConnection connection = this.OpenConnection())
            using (IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    action(connection, transaction);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private IDbConnection OpenConnection()
        {
            IDbConnection connection = this.connectionFactory();
            if (connection == null)
            {
                throw new InvalidOperationException("The bot database connection factory returned null.");
            }

            connection.Open();
            return connection;
        }

        private static IDbCommand CreateCommand(IDbConnection connection, IDbTransaction transaction, string sql)
        {
            IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            return command;
        }

        private static void Execute(IDbConnection connection, IDbTransaction transaction, string sql, params Parameter[] parameters)
        {
            using (IDbCommand command = CreateCommand(connection, transaction, sql))
            {
                foreach (Parameter parameter in parameters)
                {
                    AddParameter(command, parameter.Name, parameter.Value);
                }

                command.ExecuteNonQuery();
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static long? ParseNullableInt64(object value)
        {
            return value == null || value == DBNull.Value ? (long?)null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static Guid? ParseNullableGuid(object value)
        {
            string text = DbString(value);
            return string.IsNullOrEmpty(text) ? (Guid?)null : Guid.Parse(text);
        }

        private static string DbString(object value)
        {
            return value == null || value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static DateTime AsUtc(DateTime value)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private sealed class Parameter
        {
            public Parameter(string name, object value)
            {
                this.Name = name;
                this.Value = value;
            }

            public string Name { get; private set; }

            public object Value { get; private set; }
        }
    }
}
