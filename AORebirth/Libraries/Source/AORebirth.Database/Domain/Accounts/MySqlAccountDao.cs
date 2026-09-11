namespace AORebirth.Database.Domain.Accounts
{
    using System;
    using System.Data;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Accounts;

    using Dapper;
    using MySqlConnector;

    /// <summary>
    /// MySQL-only legacy game-account persistence. No runtime wiring, hashing, identity workflows
    /// or character mutation. Single writes autocommit as in the legacy account helpers.
    /// </summary>
    public sealed class MySqlAccountDao : IAccountDao
    {
        private const string AccountColumns =
            "Id AS AccountId, CreationDate, Email, FirstName, LastName, Username, "
            + "Password AS PasswordHash, AllowedCharacters, Flags, AccountFlags, Expansions, GM AS GmLevel";

        private readonly Func<IDbConnection> connectionFactory;

        public MySqlAccountDao()
            : this(OpenConfiguredMySqlConnection)
        {
        }

        /// <summary>Explicit connection seam following the mission DAO pattern; each returned connection is owned.</summary>
        public MySqlAccountDao(Func<IDbConnection> connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            this.connectionFactory = connectionFactory;
        }

        public GameAccountAuthenticationData LoadForAuthentication(string username)
        {
            const string Sql =
                "SELECT Id AS AccountId, Username, Password AS PasswordHash, AllowedCharacters, "
                + "Flags, AccountFlags, Expansions, GM AS GmLevel FROM login WHERE Username=@Username";
            using (IDbConnection connection = this.OpenConnection())
            {
                return connection.Query<GameAccountAuthenticationData>(Sql, new { Username = username }).FirstOrDefault();
            }
        }

        public GameAccountData LoadByUsername(string username)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return QueryAccount(connection, username);
            }
        }

        public GameAccountLookupResult LoadByCharacterId(int characterId)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                // Materialize a row rather than a scalar so null Username is not confused with no character.
                CharacterAccountName row = connection.Query<CharacterAccountName>(
                    "SELECT Username FROM characters WHERE Id=@CharacterId",
                    new { CharacterId = characterId }).SingleOrDefault();
                if (row == null)
                {
                    return new GameAccountLookupResult(GameAccountLookupStatus.CharacterNotFound, null, null);
                }

                if (string.IsNullOrEmpty(row.Username))
                {
                    return new GameAccountLookupResult(GameAccountLookupStatus.CharacterUsernameMissing, row.Username, null);
                }

                // Deliberately no transaction/snapshot guarantee; account lookup remains read-only.
                GameAccountData account = QueryAccount(connection, row.Username);
                return new GameAccountLookupResult(
                    account == null ? GameAccountLookupStatus.AccountNotFound : GameAccountLookupStatus.Found,
                    row.Username,
                    account);
            }
        }

        public long CountRegisteredAccounts()
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return connection.Query<long>("SELECT COUNT(*) FROM login").Single();
            }
        }

        public bool UsernameExists(string username)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return connection.Query<int>(
                    "SELECT Id FROM login WHERE Username=@Username", new { Username = username }).Count() == 1;
            }
        }

        public int CreateGameAccount(NewGameAccountData account)
        {
            if (account == null)
            {
                throw new ArgumentNullException("account");
            }

            const string Sql =
                "INSERT INTO login (CreationDate, Email, FirstName, LastName, Username, Password, "
                + "AllowedCharacters, Flags, AccountFlags, Expansions, GM) VALUES "
                + "(@CreationDate, @Email, @FirstName, @LastName, @Username, @PasswordHash, "
                + "@AllowedCharacters, @Flags, @AccountFlags, @Expansions, @GmLevel)";
            using (IDbConnection connection = this.OpenConnection())
            {
                return connection.Execute(Sql, new
                {
                    CreationDate = DateTime.Now,
                    account.Email,
                    account.FirstName,
                    account.LastName,
                    account.Username,
                    account.PasswordHash,
                    account.AllowedCharacters,
                    account.Flags,
                    account.AccountFlags,
                    account.Expansions,
                    account.GmLevel
                });
            }
        }

        public int ChangePassword(string username, string passwordHash)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return connection.Execute(
                    "UPDATE login SET password=@PasswordHash WHERE Username=@Username LIMIT 1",
                    new { Username = username, PasswordHash = passwordHash });
            }
        }

        public int SetExpansions(string username, int expansions)
        {
            using (IDbConnection connection = this.OpenConnection())
            {
                return connection.Execute(
                    "UPDATE login SET Expansions=@Expansions WHERE Username=@Username",
                    new { Username = username, Expansions = expansions });
            }
        }

        private static GameAccountData QueryAccount(IDbConnection connection, string username)
        {
            return connection.Query<GameAccountData>(
                "SELECT " + AccountColumns + " FROM login WHERE Username=@Username",
                new { Username = username }).FirstOrDefault();
        }

        private static IDbConnection OpenConfiguredMySqlConnection()
        {
            IDbConnection connection = Connector.GetConnection();
            if (connection is MySqlConnection)
            {
                return connection;
            }

            if (connection != null)
            {
                connection.Dispose();
            }

            throw new NotSupportedException("Account persistence requires the configured MySQL provider.");
        }

        private IDbConnection OpenConnection()
        {
            IDbConnection connection = this.connectionFactory();
            if (connection == null)
            {
                throw new InvalidOperationException("Account connection factory returned null.");
            }

            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    connection.Open();
                }

                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private sealed class CharacterAccountName
        {
            public string Username { get; set; }
        }
    }
}
