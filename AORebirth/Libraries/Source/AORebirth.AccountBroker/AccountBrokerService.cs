namespace AORebirth.AccountBroker
{
    using System;
    using System.Data;
    using System.Security.Cryptography;
    using System.Text;

    using AO.Core.Encryption;
    using AORebirth.Core.Encryption;

    public sealed class AccountBrokerService
    {
        private const int NormalAllowedCharacters = 6;

        private const int NormalAccountFlags = 0;

        private const int NormalExpansions = 127;

        private const int NormalFlags = 0;

        private const int NormalGm = 0;

        private readonly Func<IDbConnection> connectionFactory;

        public AccountBrokerService(Func<IDbConnection> connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            this.connectionFactory = connectionFactory;
        }

        public WebsiteAuthenticationResult AuthenticateWebsiteIdentity(string username, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return FailedAuthentication("INVALID_CREDENTIALS");
            }

            string normalizedUsername;
            try
            {
                normalizedUsername = UsernamePolicy.NormalizeForLegacyLink(username);
            }
            catch (AccountBrokerException)
            {
                return FailedAuthentication("INVALID_CREDENTIALS");
            }

            using (IDbConnection connection = this.OpenConnection())
            using (IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                GameAccountSnapshot gameAccount =
                    this.GetGameAccountByNormalizedUsername(connection, transaction, normalizedUsername);
                if (gameAccount == null || gameAccount.Flags != NormalFlags)
                {
                    transaction.Commit();
                    return FailedAuthentication("INVALID_CREDENTIALS");
                }

                if (!ValidateStoredPassword(password, gameAccount.PasswordHash))
                {
                    transaction.Commit();
                    return FailedAuthentication("INVALID_CREDENTIALS");
                }

                AccountIdentitySnapshot identity =
                    this.GetIdentitySnapshotByGameAccount(connection, transaction, gameAccount.Id);
                if (identity == null)
                {
                    transaction.Commit();
                    return FailedAuthentication("IDENTITY_MAPPING_REQUIRED");
                }

                if (!string.Equals(identity.IdentityStatus, "Active", StringComparison.Ordinal)
                    || !string.Equals(identity.GameMappingState, "Linked", StringComparison.Ordinal))
                {
                    transaction.Commit();
                    return FailedAuthentication("IDENTITY_NOT_ACTIVE");
                }

                transaction.Commit();
                return new WebsiteAuthenticationResult
                {
                    IsAuthenticated = true,
                    Identity = identity
                };
            }
        }

        public AccountIdentitySnapshot GetIdentity(long identityId)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return this.GetIdentitySnapshotByIdentity(connection, null, identityId);
            }
        }

        public AccountProvisioningResult CreateGameAccount(CreateAccountRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                throw new AccountBrokerException("MISSING_IDEMPOTENCY_KEY", "Idempotency key is required.");
            }

            if (request.Password == null)
            {
                throw new AccountBrokerException("MISSING_PASSWORD", "Password is required.");
            }

            string normalizedUsername = UsernamePolicy.NormalizeForNewRegistration(request.Username);
            byte[] idempotencyHash = HashIdempotencyKey(request.IdempotencyKey);

            using (IDbConnection connection = this.OpenConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                ProvisioningJobRow existingJob = this.GetProvisioningJob(connection, transaction, idempotencyHash);
                if (existingJob != null)
                {
                    AccountProvisioningResult resumed = this.ResumeCreateGameAccount(
                        connection,
                    transaction,
                    existingJob,
                    request,
                    normalizedUsername,
                    idempotencyHash);
                    transaction.Commit();
                    return resumed;
                }

                if (this.GetIdentityIdByNormalizedUsername(connection, transaction, normalizedUsername).HasValue)
                {
                    throw new AccountBrokerException("USERNAME_EXISTS", "Normalized username already exists.");
                }

                string normalizedEmail = NormalizeEmail(request.Email);
                if (!string.IsNullOrEmpty(normalizedEmail)
                    && this.GetIdentityIdByNormalizedEmail(connection, transaction, normalizedEmail).HasValue)
                {
                    throw new AccountBrokerException("EMAIL_EXISTS", "Normalized email already exists.");
                }

                long identityId = this.InsertIdentity(
                    connection,
                    transaction,
                    request.Username,
                    normalizedUsername,
                    request.Email,
                    "Reserved");

                this.InsertProvisioningJob(
                    connection,
                    transaction,
                    idempotencyHash,
                    identityId,
                    normalizedUsername,
                    NormalizeEmail(request.Email),
                    "GameAccountPending",
                    20);

                AccountProvisioningResult result = this.CreateOrLinkGameAccount(
                    connection,
                    transaction,
                    identityId,
                    request,
                    normalizedUsername,
                    idempotencyHash,
                    true);

                transaction.Commit();
                return result;
            }
        }

        public IdentityResult CreateLegacyIdentityForExistingGameAccount(int gameAccountId)
        {
            using (IDbConnection connection = this.OpenConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                GameAccountSnapshot gameAccount = this.GetGameAccount(connection, transaction, gameAccountId);
                if (gameAccount == null)
                {
                    throw new AccountBrokerException("GAME_ACCOUNT_NOT_FOUND", "Game account does not exist.");
                }

                string normalizedUsername = UsernamePolicy.NormalizeForLegacyLink(gameAccount.Username);
                long? existingIdentityId =
                    this.GetIdentityIdByNormalizedUsername(connection, transaction, normalizedUsername);
                long identityId = existingIdentityId.HasValue
                    ? existingIdentityId.Value
                    : this.InsertIdentity(
                        connection,
                        transaction,
                        gameAccount.Username,
                        normalizedUsername,
                        null,
                        "Reserved");

                transaction.Commit();
                return new IdentityResult
                {
                    IdentityId = identityId,
                    CanonicalUsername = gameAccount.Username,
                    NormalizedUsername = normalizedUsername
                };
            }
        }

        public void LinkExistingGameAccount(long identityId, int gameAccountId)
        {
            using (IDbConnection connection = this.OpenConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                this.RequireIdentity(connection, transaction, identityId);
                if (this.GetGameAccount(connection, transaction, gameAccountId) == null)
                {
                    throw new AccountBrokerException("GAME_ACCOUNT_NOT_FOUND", "Game account does not exist.");
                }

                long? mappedIdentity = this.GetIdentityIdByGameAccount(connection, transaction, gameAccountId);
                if (mappedIdentity.HasValue)
                {
                    if (mappedIdentity.Value == identityId)
                    {
                        transaction.Commit();
                        return;
                    }

                    throw new AccountBrokerException("GAME_ACCOUNT_MAPPING_CONFLICT", "Game account is already mapped.");
                }

                int? mappedGameAccount = this.GetGameAccountIdByIdentity(connection, transaction, identityId);
                if (mappedGameAccount.HasValue)
                {
                    if (mappedGameAccount.Value == gameAccountId)
                    {
                        transaction.Commit();
                        return;
                    }

                    throw new AccountBrokerException("IDENTITY_MAPPING_CONFLICT", "Identity already has a game account.");
                }

                this.InsertGameMapping(connection, transaction, identityId, gameAccountId);
                transaction.Commit();
            }
        }

        public ExternalMappingResult ReserveExternalMapping(long identityId, string provider, string externalAccountId)
        {
            string normalizedProvider = NormalizeProvider(provider);
            if (string.IsNullOrWhiteSpace(externalAccountId) || externalAccountId.Length > 64)
            {
                throw new AccountBrokerException("INVALID_EXTERNAL_ACCOUNT_ID", "External account id is required.");
            }

            using (IDbConnection connection = this.OpenConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                this.RequireIdentity(connection, transaction, identityId);
                long? existingForExternal =
                    this.GetIdentityIdByExternalAccount(connection, transaction, normalizedProvider, externalAccountId);
                if (existingForExternal.HasValue)
                {
                    if (existingForExternal.Value == identityId)
                    {
                        transaction.Commit();
                        return new ExternalMappingResult
                        {
                            IdentityId = identityId,
                            Provider = normalizedProvider,
                            ExternalAccountId = externalAccountId,
                            MappingState = "Linked"
                        };
                    }

                    throw new AccountBrokerException(
                        "EXTERNAL_MAPPING_CONFLICT",
                        "External account is already mapped to another identity.");
                }

                string existingExternalForIdentity =
                    this.GetExternalAccountIdByIdentityProvider(connection, transaction, identityId, normalizedProvider);
                if (!string.IsNullOrEmpty(existingExternalForIdentity)
                    && existingExternalForIdentity != externalAccountId)
                {
                    throw new AccountBrokerException(
                        "IDENTITY_EXTERNAL_PROVIDER_CONFLICT",
                        "Identity already has a mapping for this provider.");
                }

                if (string.IsNullOrEmpty(existingExternalForIdentity))
                {
                    this.ExecuteNonQuery(
                        connection,
                        transaction,
                        "INSERT INTO account_external_mappings (IdentityId, Provider, ExternalAccountId, MappingState, LinkedAt) VALUES (@identityId, @provider, @externalAccountId, 'Linked', CURRENT_TIMESTAMP(6))",
                        Parameter("@identityId", identityId),
                        Parameter("@provider", normalizedProvider),
                        Parameter("@externalAccountId", externalAccountId));
                }

                transaction.Commit();
                return new ExternalMappingResult
                {
                    IdentityId = identityId,
                    Provider = normalizedProvider,
                    ExternalAccountId = externalAccountId,
                    MappingState = "Linked"
                };
            }
        }

        public GameAccountSnapshot GetGameAccount(int gameAccountId)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return this.GetGameAccount(connection, null, gameAccountId);
            }
        }

        public ProvisioningStatus GetProvisioningStatus(string idempotencyKey)
        {
            byte[] hash = HashIdempotencyKey(idempotencyKey);
            using (IDbConnection connection = this.OpenConnection())
            {
                ProvisioningJobRow job = this.GetProvisioningJob(connection, null, hash);
                if (job == null)
                {
                    return null;
                }

                return new ProvisioningStatus
                {
                    State = job.State,
                    Step = job.Step,
                    IdentityId = job.IdentityId,
                    GameAccountId = job.GameAccountId,
                    UpdatedAt = job.UpdatedAt
                };
            }
        }

        private AccountProvisioningResult ResumeCreateGameAccount(
            IDbConnection connection,
            IDbTransaction transaction,
            ProvisioningJobRow job,
            CreateAccountRequest request,
            string normalizedUsername,
            byte[] idempotencyHash)
        {
            if (!string.Equals(job.RequestedNormalizedUsername, normalizedUsername, StringComparison.Ordinal))
            {
                throw new AccountBrokerException(
                    "IDEMPOTENCY_CONFLICT",
                    "Idempotency key was previously used for a different username.");
            }

            long identityId = job.IdentityId.GetValueOrDefault();
            if (identityId < 1)
            {
                identityId = this.InsertIdentity(
                    connection,
                    transaction,
                    request.Username,
                    normalizedUsername,
                    request.Email,
                    "Reserved");
            }

            if (job.State == "Active")
            {
                int? existingGameAccountId = this.GetGameAccountIdByIdentity(connection, transaction, identityId);
                if (!existingGameAccountId.HasValue)
                {
                    throw new AccountBrokerException(
                        "ACTIVE_JOB_WITHOUT_MAPPING",
                        "Active provisioning job has no game mapping.");
                }

                return new AccountProvisioningResult
                {
                    IdentityId = identityId,
                    GameAccountId = existingGameAccountId.Value,
                    CanonicalUsername = request.Username,
                    NormalizedUsername = normalizedUsername,
                    ProvisioningState = "Active",
                    CreatedGameAccount = false
                };
            }

            return this.CreateOrLinkGameAccount(
                connection,
                transaction,
                identityId,
                request,
                normalizedUsername,
                idempotencyHash,
                false);
        }

        private AccountProvisioningResult CreateOrLinkGameAccount(
            IDbConnection connection,
            IDbTransaction transaction,
            long identityId,
            CreateAccountRequest request,
            string normalizedUsername,
            byte[] idempotencyHash,
            bool firstAttempt)
        {
            GameAccountSnapshot existingByUsername =
                this.GetGameAccountByUsername(connection, transaction, request.Username);
            bool created = false;
            int gameAccountId;
            if (existingByUsername == null)
            {
                string passwordHash = new LoginEncryption().GeneratePasswordHash(request.Password);
                this.ExecuteNonQuery(
                    connection,
                    transaction,
                    "INSERT INTO login (CreationDate, Email, FirstName, LastName, Username, Password, AllowedCharacters, Flags, AccountFlags, Expansions, GM) VALUES (CURRENT_TIMESTAMP(), @email, @firstName, @lastName, @username, @password, @allowedCharacters, @flags, @accountFlags, @expansions, @gm)",
                    Parameter("@email", request.Email ?? string.Empty),
                    Parameter("@firstName", request.FirstName ?? string.Empty),
                    Parameter("@lastName", request.LastName ?? string.Empty),
                    Parameter("@username", request.Username),
                    Parameter("@password", passwordHash),
                    Parameter("@allowedCharacters", NormalAllowedCharacters),
                    Parameter("@flags", NormalFlags),
                    Parameter("@accountFlags", NormalAccountFlags),
                    Parameter("@expansions", NormalExpansions),
                    Parameter("@gm", NormalGm));
                gameAccountId = Convert.ToInt32(this.ExecuteScalar(connection, transaction, "SELECT LAST_INSERT_ID()"));
                created = true;
            }
            else
            {
                if (firstAttempt)
                {
                    throw new AccountBrokerException("GAME_USERNAME_EXISTS", "Game account username already exists.");
                }

                gameAccountId = existingByUsername.Id;
            }

            long? mappedIdentity = this.GetIdentityIdByGameAccount(connection, transaction, gameAccountId);
            if (mappedIdentity.HasValue && mappedIdentity.Value != identityId)
            {
                throw new AccountBrokerException("GAME_ACCOUNT_MAPPING_CONFLICT", "Game account is already mapped.");
            }

            if (!mappedIdentity.HasValue)
            {
                this.InsertGameMapping(connection, transaction, identityId, gameAccountId);
            }

            this.ExecuteNonQuery(
                connection,
                transaction,
                "UPDATE account_provisioning_jobs SET IdentityId=@identityId, RequestedGameAccountId=@gameAccountId, ProvisioningState='Active', ProvisioningStep=60, UpdatedAt=CURRENT_TIMESTAMP(6) WHERE IdempotencyKeyHash=@hash",
                Parameter("@hash", idempotencyHash),
                Parameter("@identityId", identityId),
                Parameter("@gameAccountId", gameAccountId));
            this.ExecuteNonQuery(
                connection,
                transaction,
                "UPDATE account_identities SET IdentityStatus='Active' WHERE IdentityId=@identityId",
                Parameter("@identityId", identityId));

            return new AccountProvisioningResult
            {
                IdentityId = identityId,
                GameAccountId = gameAccountId,
                CanonicalUsername = request.Username,
                NormalizedUsername = normalizedUsername,
                ProvisioningState = "Active",
                CreatedGameAccount = created
            };
        }

        private long InsertIdentity(
            IDbConnection connection,
            IDbTransaction transaction,
            string canonicalUsername,
            string normalizedUsername,
            string email,
            string status)
        {
            this.ExecuteNonQuery(
                connection,
                transaction,
                "INSERT INTO account_identities (IdentityPublicId, CanonicalUsername, NormalizedUsername, CanonicalEmail, NormalizedEmail, IdentityStatus) VALUES (@publicId, @canonicalUsername, @normalizedUsername, @email, @normalizedEmail, @status)",
                Parameter("@publicId", Guid.NewGuid().ToString("D")),
                Parameter("@canonicalUsername", canonicalUsername),
                Parameter("@normalizedUsername", normalizedUsername),
                Parameter("@email", EmptyToNull(email)),
                Parameter("@normalizedEmail", NormalizeEmail(email)),
                Parameter("@status", status));
            return Convert.ToInt64(this.ExecuteScalar(connection, transaction, "SELECT LAST_INSERT_ID()"));
        }

        private void InsertProvisioningJob(
            IDbConnection connection,
            IDbTransaction transaction,
            byte[] idempotencyHash,
            long identityId,
            string normalizedUsername,
            string normalizedEmail,
            string state,
            int step)
        {
            this.ExecuteNonQuery(
                connection,
                transaction,
                "INSERT INTO account_provisioning_jobs (IdempotencyKeyHash, IdentityId, RequestedNormalizedUsername, RequestedNormalizedEmail, ProvisioningState, ProvisioningStep) VALUES (@hash, @identityId, @username, @email, @state, @step)",
                Parameter("@hash", idempotencyHash),
                Parameter("@identityId", identityId),
                Parameter("@username", normalizedUsername),
                Parameter("@email", normalizedEmail),
                Parameter("@state", state),
                Parameter("@step", step));
        }

        private void InsertGameMapping(
            IDbConnection connection,
            IDbTransaction transaction,
            long identityId,
            int gameAccountId)
        {
            this.ExecuteNonQuery(
                connection,
                transaction,
                "INSERT INTO account_game_mappings (IdentityId, GameAccountId, MappingState, LinkedAt) VALUES (@identityId, @gameAccountId, 'Linked', CURRENT_TIMESTAMP(6))",
                Parameter("@identityId", identityId),
                Parameter("@gameAccountId", gameAccountId));
        }

        private IDbConnection OpenConnection()
        {
            IDbConnection connection = this.connectionFactory();
            connection.Open();
            return connection;
        }

        private void RequireIdentity(IDbConnection connection, IDbTransaction transaction, long identityId)
        {
            object value = this.ExecuteScalar(
                connection,
                transaction,
                "SELECT IdentityId FROM account_identities WHERE IdentityId=@identityId",
                Parameter("@identityId", identityId));
            if (value == null || value == DBNull.Value)
            {
                throw new AccountBrokerException("IDENTITY_NOT_FOUND", "Identity does not exist.");
            }
        }

        private long? GetIdentityIdByNormalizedUsername(
            IDbConnection connection,
            IDbTransaction transaction,
            string normalizedUsername)
        {
            return ToNullableInt64(
                this.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT IdentityId FROM account_identities WHERE NormalizedUsername=@normalizedUsername",
                    Parameter("@normalizedUsername", normalizedUsername)));
        }

        private int? GetGameAccountIdByIdentity(IDbConnection connection, IDbTransaction transaction, long identityId)
        {
            return ToNullableInt32(
                this.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT GameAccountId FROM account_game_mappings WHERE IdentityId=@identityId",
                    Parameter("@identityId", identityId)));
        }

        private long? GetIdentityIdByNormalizedEmail(
            IDbConnection connection,
            IDbTransaction transaction,
            string normalizedEmail)
        {
            return ToNullableInt64(
                this.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT IdentityId FROM account_identities WHERE NormalizedEmail=@normalizedEmail",
                    Parameter("@normalizedEmail", normalizedEmail)));
        }

        private long? GetIdentityIdByGameAccount(IDbConnection connection, IDbTransaction transaction, int gameAccountId)
        {
            return ToNullableInt64(
                this.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT IdentityId FROM account_game_mappings WHERE GameAccountId=@gameAccountId",
                    Parameter("@gameAccountId", gameAccountId)));
        }

        private long? GetIdentityIdByExternalAccount(
            IDbConnection connection,
            IDbTransaction transaction,
            string provider,
            string externalAccountId)
        {
            return ToNullableInt64(
                this.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT IdentityId FROM account_external_mappings WHERE Provider=@provider AND ExternalAccountId=@externalAccountId",
                    Parameter("@provider", provider),
                    Parameter("@externalAccountId", externalAccountId)));
        }

        private string GetExternalAccountIdByIdentityProvider(
            IDbConnection connection,
            IDbTransaction transaction,
            long identityId,
            string provider)
        {
            object value = this.ExecuteScalar(
                connection,
                transaction,
                "SELECT ExternalAccountId FROM account_external_mappings WHERE IdentityId=@identityId AND Provider=@provider",
                Parameter("@identityId", identityId),
                Parameter("@provider", provider));
            return value == null || value == DBNull.Value ? null : Convert.ToString(value);
        }

        private GameAccountSnapshot GetGameAccount(IDbConnection connection, IDbTransaction transaction, int id)
        {
            return this.GetSingleGameAccount(
                connection,
                transaction,
                "SELECT Id, Username, Password, Flags, AccountFlags, GM FROM login WHERE Id=@id",
                Parameter("@id", id));
        }

        private GameAccountSnapshot GetGameAccountByUsername(
            IDbConnection connection,
            IDbTransaction transaction,
            string username)
        {
            return this.GetSingleGameAccount(
                connection,
                transaction,
                "SELECT Id, Username, Password, Flags, AccountFlags, GM FROM login WHERE Username=@username",
                Parameter("@username", username));
        }

        private GameAccountSnapshot GetGameAccountByNormalizedUsername(
            IDbConnection connection,
            IDbTransaction transaction,
            string normalizedUsername)
        {
            return this.GetSingleGameAccount(
                connection,
                transaction,
                "SELECT Id, Username, Password, Flags, AccountFlags, GM FROM login WHERE LOWER(Username)=@username",
                Parameter("@username", normalizedUsername));
        }

        private AccountIdentitySnapshot GetIdentitySnapshotByGameAccount(
            IDbConnection connection,
            IDbTransaction transaction,
            int gameAccountId)
        {
            return this.GetSingleIdentitySnapshot(
                connection,
                transaction,
                "SELECT i.IdentityId, i.IdentityPublicId, i.CanonicalUsername, i.NormalizedUsername, i.CanonicalEmail, i.EmailVerifiedAt, i.IdentityStatus, m.GameAccountId, m.MappingState, i.CreatedAt FROM account_identities i INNER JOIN account_game_mappings m ON m.IdentityId=i.IdentityId WHERE m.GameAccountId=@gameAccountId",
                Parameter("@gameAccountId", gameAccountId));
        }

        private AccountIdentitySnapshot GetIdentitySnapshotByIdentity(
            IDbConnection connection,
            IDbTransaction transaction,
            long identityId)
        {
            return this.GetSingleIdentitySnapshot(
                connection,
                transaction,
                "SELECT i.IdentityId, i.IdentityPublicId, i.CanonicalUsername, i.NormalizedUsername, i.CanonicalEmail, i.EmailVerifiedAt, i.IdentityStatus, m.GameAccountId, m.MappingState, i.CreatedAt FROM account_identities i INNER JOIN account_game_mappings m ON m.IdentityId=i.IdentityId WHERE i.IdentityId=@identityId",
                Parameter("@identityId", identityId));
        }

        private AccountIdentitySnapshot GetSingleIdentitySnapshot(
            IDbConnection connection,
            IDbTransaction transaction,
            string sql,
            params IDbDataParameter[] parameters)
        {
            using (IDbCommand command = CreateCommand(connection, transaction, sql, parameters))
            using (IDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                return new AccountIdentitySnapshot
                {
                    IdentityId = Convert.ToInt64(reader["IdentityId"]),
                    IdentityPublicId = Convert.ToString(reader["IdentityPublicId"]),
                    CanonicalUsername = Convert.ToString(reader["CanonicalUsername"]),
                    NormalizedUsername = Convert.ToString(reader["NormalizedUsername"]),
                    CanonicalEmail = reader["CanonicalEmail"] == DBNull.Value
                        ? null
                        : Convert.ToString(reader["CanonicalEmail"]),
                    EmailVerified = reader["EmailVerifiedAt"] != DBNull.Value,
                    IdentityStatus = Convert.ToString(reader["IdentityStatus"]),
                    GameAccountId = Convert.ToInt32(reader["GameAccountId"]),
                    GameMappingState = Convert.ToString(reader["MappingState"]),
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                };
            }
        }

        private GameAccountSnapshot GetSingleGameAccount(
            IDbConnection connection,
            IDbTransaction transaction,
            string sql,
            params IDbDataParameter[] parameters)
        {
            using (IDbCommand command = CreateCommand(connection, transaction, sql, parameters))
            using (IDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                return new GameAccountSnapshot
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Username = Convert.ToString(reader["Username"]),
                    PasswordHash = Convert.ToString(reader["Password"]),
                    Flags = Convert.ToInt32(reader["Flags"]),
                    AccountFlags = Convert.ToInt32(reader["AccountFlags"]),
                    GM = Convert.ToInt32(reader["GM"])
                };
            }
        }

        private ProvisioningJobRow GetProvisioningJob(
            IDbConnection connection,
            IDbTransaction transaction,
            byte[] idempotencyHash)
        {
            using (IDbCommand command = CreateCommand(
                connection,
                transaction,
                "SELECT ProvisioningJobId, IdentityId, RequestedNormalizedUsername, RequestedGameAccountId, ProvisioningState, ProvisioningStep, UpdatedAt FROM account_provisioning_jobs WHERE IdempotencyKeyHash=@hash",
                Parameter("@hash", idempotencyHash)))
            using (IDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    return null;
                }

                return new ProvisioningJobRow
                {
                    JobId = Convert.ToInt64(reader["ProvisioningJobId"]),
                    IdentityId = reader["IdentityId"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["IdentityId"]),
                    RequestedNormalizedUsername = Convert.ToString(reader["RequestedNormalizedUsername"]),
                    GameAccountId = reader["RequestedGameAccountId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["RequestedGameAccountId"]),
                    State = Convert.ToString(reader["ProvisioningState"]),
                    Step = Convert.ToInt32(reader["ProvisioningStep"]),
                    UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"])
                };
            }
        }

        private object ExecuteScalar(
            IDbConnection connection,
            IDbTransaction transaction,
            string sql,
            params IDbDataParameter[] parameters)
        {
            using (IDbCommand command = CreateCommand(connection, transaction, sql, parameters))
            {
                return command.ExecuteScalar();
            }
        }

        private void ExecuteNonQuery(
            IDbConnection connection,
            IDbTransaction transaction,
            string sql,
            params IDbDataParameter[] parameters)
        {
            using (IDbCommand command = CreateCommand(connection, transaction, sql, parameters))
            {
                command.ExecuteNonQuery();
            }
        }

        private static IDbCommand CreateCommand(
            IDbConnection connection,
            IDbTransaction transaction,
            string sql,
            params IDbDataParameter[] parameters)
        {
            IDbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            foreach (IDbDataParameter parameter in parameters)
            {
                IDbDataParameter created = command.CreateParameter();
                created.ParameterName = parameter.ParameterName;
                created.Value = parameter.Value ?? DBNull.Value;
                command.Parameters.Add(created);
            }

            return command;
        }

        private static IDbDataParameter Parameter(string name, object value)
        {
            return new BrokerParameter(name, value);
        }

        private static int? ToNullableInt32(object value)
        {
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        private static long? ToNullableInt64(object value)
        {
            return value == null || value == DBNull.Value ? (long?)null : Convert.ToInt64(value);
        }

        private static byte[] HashIdempotencyKey(string idempotencyKey)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(idempotencyKey ?? string.Empty));
            }
        }

        private static WebsiteAuthenticationResult FailedAuthentication(string code)
        {
            return new WebsiteAuthenticationResult
            {
                IsAuthenticated = false,
                FailureCode = code
            };
        }

        private static bool ValidateStoredPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            try
            {
                return PasswordHash.ValidatePassword(password, storedHash);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string NormalizeEmail(string email)
        {
            return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        }

        private static string NormalizeProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new AccountBrokerException("INVALID_PROVIDER", "Provider is required.");
            }

            string normalized = provider.Trim().ToLowerInvariant();
            for (int index = 0; index < normalized.Length; index++)
            {
                char c = normalized[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == ':' || c == '-'))
                {
                    throw new AccountBrokerException("INVALID_PROVIDER", "Provider contains invalid characters.");
                }
            }

            return normalized;
        }

        private sealed class ProvisioningJobRow
        {
            public long JobId { get; set; }

            public long? IdentityId { get; set; }

            public string RequestedNormalizedUsername { get; set; }

            public int? GameAccountId { get; set; }

            public string State { get; set; }

            public int Step { get; set; }

            public DateTime UpdatedAt { get; set; }
        }

        private sealed class BrokerParameter : IDbDataParameter
        {
            public BrokerParameter(string name, object value)
            {
                this.ParameterName = name;
                this.Value = value;
            }

            public DbType DbType { get; set; }

            public ParameterDirection Direction { get; set; }

            public bool IsNullable { get { return true; } }

            public string ParameterName { get; set; }

            public string SourceColumn { get; set; }

            public DataRowVersion SourceVersion { get; set; }

            public object Value { get; set; }

            public byte Precision { get; set; }

            public byte Scale { get; set; }

            public int Size { get; set; }
        }
    }
}
