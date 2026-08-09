namespace AORebirth.LinuxBuild.Stage6MySqlIntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Net;
    using System.Text;

    using AO.Core.Encryption;

    using AORebirth.Core.Encryption;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    using MySqlConnector;

    using Utility.Config;

    internal static class Program
    {
        private const string AcknowledgementEnvironment = "AO_REBIRTH_STAGE6_DISPOSABLE_ACK";
        private const string AcknowledgementValue = "AO_REBIRTH_STAGE6_DISPOSABLE_ONLY";
        private const string ConfigurationEnvironment = "AO_REBIRTH_CONFIG_PATH";
        private const string ConnectionEnvironment = "AO_REBIRTH_MYSQL_CONNECTION";
        private const string ExpectedDatabase = "aorebirth_chatengine_stage6";
        private const string ExpectedServer = "127.0.0.1";
        private const uint ExpectedPort = 33067;
        private const string ExpectedUser = "aorebirth_stage6";

        private static readonly string[] ExpectedTables =
        {
            "characterstimers",
            "characters",
            "charactersactivenanos",
            "charactersmeshs",
            "charactersuploadednanos",
            "charactersperks",
            "instanceditems",
            "itemnames",
            "items",
            "login",
            "missionaccountflags",
            "missionflags",
            "missionobjectiveobservations",
            "missionobjectiveprogress",
            "missionrewardledger",
            "missionstates",
            "mobdroptable",
            "mobspawns",
            "mobspawnsactivenanos",
            "mobspawnsinventory",
            "mobspawnsmeshs",
            "mobspawnsuploadednanos",
            "mobspawns_stats",
            "mobtemplate",
            "organizations",
            "proxydestinations",
            "receivedmessages",
            "shopinventorytemplates",
            "staticdynels",
            "stats",
            "teleports",
            "tradeskill",
            "vendors",
            "vendortemplate"
        };

        private static int Main(string[] args)
        {
            if (args.Length == 1
                && string.Equals(args[0], "--validate-offline", StringComparison.Ordinal))
            {
                return ValidateOffline();
            }

            if (args.Length != 1
                || !string.Equals(args[0], "--run-disposable", StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    "REFUSED: Stage 6 MySQL integration requires the exact --run-disposable flag.");
                return 2;
            }

            return RunDisposable();
        }

        private static int ValidateOffline()
        {
            try
            {
                const string username = "stage6_offline_account";
                const string password = "Stage6-Offline-Password";
                byte[] salt = CreateSalt(1);
                string serverSalt = ToLowerHex(salt);
                string loginKey = LoginKeyEncoder.Create(username, password, salt);

                var encryption = new LoginEncryption();
                string decodedUsername;
                string decodedSalt;
                string decodedPassword;
                encryption.DecryptLoginKey(
                    loginKey,
                    out decodedUsername,
                    out decodedSalt,
                    out decodedPassword);

                Require(
                    string.Equals(decodedUsername, username, StringComparison.Ordinal),
                    "offline-login-key-username");
                Require(
                    string.Equals(decodedSalt, serverSalt, StringComparison.Ordinal),
                    "offline-login-key-salt");
                Require(
                    string.Equals(decodedPassword, password, StringComparison.Ordinal),
                    "offline-login-key-password");

                string passwordHash = encryption.GeneratePasswordHash(password);
                Require(
                    PasswordHash.ValidatePassword(password, passwordHash),
                    "offline-password-positive");
                Require(
                    !PasswordHash.ValidatePassword(password + "-wrong", passwordHash),
                    "offline-password-negative");
                Require(
                    encryption.IsValidLogin(loginKey, serverSalt, username, passwordHash),
                    "offline-login-positive");
                Require(
                    !encryption.IsValidLogin(loginKey, serverSalt, username, null),
                    "offline-login-null-hash");
                Require(
                    !encryption.IsValidLogin(loginKey, serverSalt, username, string.Empty),
                    "offline-login-empty-hash");
                Require(
                    !encryption.IsValidLogin(loginKey, serverSalt, username, "   "),
                    "offline-login-whitespace-hash");
                Require(
                    !encryption.IsValidLogin(loginKey, serverSalt, username, "malformed"),
                    "offline-login-malformed-hash");

                ValidateConnectionTarget(
                    "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=0.0.0.0;Port=33067;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=3306;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=33067;Database=cellao_codex_clean;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;User ID=root;Password=offline");

                Console.WriteLine(
                    "PASS: Stage 6 offline MySQL target and deterministic login-key contracts; database=closed listeners=0.");
                return 0;
            }
            catch (Stage6ContractException exception)
            {
                Console.Error.WriteLine("FAIL: Stage 6 offline contract code=" + exception.Code + ".");
                return 1;
            }
            catch
            {
                Console.Error.WriteLine("FAIL: Stage 6 offline contract code=unexpected.");
                return 1;
            }
        }

        private static int RunDisposable()
        {
            string failureCode = null;
            bool cleanupFailed = false;
            bool fixtureScopeEstablished = false;
            int loginId = 0;
            int characterId = 0;
            string username = "s6_" + Guid.NewGuid().ToString("N").Substring(0, 20);
            string characterName = "S6" + Guid.NewGuid().ToString("N").Substring(0, 18);

            try
            {
                Require(
                    string.Equals(
                        Environment.GetEnvironmentVariable(AcknowledgementEnvironment),
                        AcknowledgementValue,
                        StringComparison.Ordinal),
                    "missing-exact-disposable-acknowledgement");
                Require(
                    !string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable(ConfigurationEnvironment)),
                    "missing-configuration-path");

                string connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironment);
                Require(!string.IsNullOrWhiteSpace(connectionString), "missing-connection-environment");
                ValidateConnectionTarget(connectionString);
                ValidateProductionConfiguration(connectionString);
                ValidateDatabaseIdentity();
                ValidateSchemaContract();

                var encryption = new LoginEncryption();
                string password = "Stage6-" + Guid.NewGuid().ToString("N");
                byte[] salt = CreateSalt(1);
                string serverSalt = ToLowerHex(salt);
                string validLoginKey = LoginKeyEncoder.Create(username, password, salt);
                VerifyLoginKeySelfCheck(
                    encryption,
                    validLoginKey,
                    username,
                    serverSalt,
                    password);

                Require(
                    LoginDataDao.Instance.GetByUsername(username) == null,
                    "fixture-login-collision");
                Require(
                    CharacterDao.Instance.GetByCharName(characterName) == null,
                    "fixture-character-collision");
                fixtureScopeEstablished = true;

                var login = new DBLoginData
                {
                    AccountFlags = 0,
                    AllowedCharacters = 1,
                    CreationDate = DateTime.UtcNow,
                    Email = username + "@invalid.example",
                    Expansions = 0,
                    FirstName = "Stage",
                    Flags = 0,
                    GM = 0,
                    LastName = "Six",
                    Password = encryption.GeneratePasswordHash(password),
                    Username = username
                };
                int loginRows = LoginDataDao.Instance.Add(login);
                loginId = login.Id;
                Require(loginRows == 1 && loginId > 0, "fixture-login-insert");

                var character = new DBCharacter
                {
                    BuddyList = string.Empty,
                    FirstName = "Stage",
                    HeadingW = 1,
                    HeadingX = 0,
                    HeadingY = 0,
                    HeadingZ = 0,
                    LastName = "Six",
                    Name = characterName,
                    Online = 0,
                    Playfield = 0,
                    Textures0 = 0,
                    Textures1 = 0,
                    Textures2 = 0,
                    Textures3 = 0,
                    Textures4 = 0,
                    Username = username,
                    X = 0,
                    Y = 0,
                    Z = 0
                };
                int characterRows = CharacterDao.Instance.Add(character);
                characterId = character.Id;
                Require(characterRows == 1 && characterId > 0, "fixture-character-insert");

                Require(
                    encryption.IsValidLogin(validLoginKey, serverSalt, username),
                    "database-login-positive");

                string wrongPasswordKey = LoginKeyEncoder.Create(
                    username,
                    password + "-wrong",
                    salt);
                Require(
                    !encryption.IsValidLogin(wrongPasswordKey, serverSalt, username),
                    "database-login-wrong-password");

                Require(
                    !encryption.IsValidLogin(
                        validLoginKey,
                        ToLowerHex(CreateSalt(33)),
                        username),
                    "database-login-wrong-salt");

                Require(
                    encryption.IsCharacterOnAccount(username, unchecked((uint)characterId)),
                    "database-character-positive");
                Require(
                    !encryption.IsCharacterOnAccount(username, 0),
                    "database-character-wrong-id");

                DBCharacter loadedCharacter = CharacterDao.Instance.Get(characterId);
                Require(
                    loadedCharacter != null
                    && string.Equals(loadedCharacter.Username, username, StringComparison.Ordinal)
                    && string.Equals(loadedCharacter.Name, characterName, StringComparison.Ordinal)
                    && loadedCharacter.Online == 0,
                    "database-character-read");

                string missingUsername = "missing_" + Guid.NewGuid().ToString("N").Substring(0, 20);
                string missingLoginKey = LoginKeyEncoder.Create(missingUsername, password, salt);
                bool missingRejected;
                try
                {
                    missingRejected = !encryption.IsValidLogin(
                        missingLoginKey,
                        serverSalt,
                        missingUsername);
                }
                catch
                {
                    throw new Stage6ContractException("database-login-missing-account-threw");
                }

                Require(missingRejected, "database-login-missing-account-accepted");
            }
            catch (Stage6ContractException exception)
            {
                failureCode = exception.Code;
            }
            catch
            {
                failureCode = "unexpected";
            }
            finally
            {
                if (fixtureScopeEstablished)
                {
                    try
                    {
                        CleanupFixture(username, characterName);
                    }
                    catch
                    {
                        cleanupFailed = true;
                    }
                }
            }

            if (cleanupFailed)
            {
                Console.Error.WriteLine(
                    "FAIL: Stage 6 disposable MySQL integration code=fixture-cleanup; manual disposal of the isolated database is required.");
                return 1;
            }

            if (failureCode != null)
            {
                Console.Error.WriteLine(
                    "FAIL: Stage 6 disposable MySQL integration code=" + failureCode + ".");
                return 1;
            }

            Console.WriteLine(
                "PASS: Stage 6 disposable MySQL schema/login acceptance; fixture residue=0 listeners=0.");
            return 0;
        }

        private static void ValidateProductionConfiguration(string connectionString)
        {
            Config configuration = ConfigReadWrite.Instance.CurrentConfig;
            Require(configuration != null, "production-config-null");
            Require(
                string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal),
                "production-config-provider");
            Require(
                string.Equals(
                    configuration.MysqlConnection,
                    connectionString,
                    StringComparison.Ordinal),
                "production-config-environment-overlay");
        }

        private static void ValidateDatabaseIdentity()
        {
            using (IDbConnection connection = Connector.GetConnection())
            {
                Require(connection.State == ConnectionState.Open, "production-connector-not-open");
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT DATABASE()";
                    object value = command.ExecuteScalar();
                    string currentDatabase = value == null || value == DBNull.Value
                                                 ? string.Empty
                                                 : Convert.ToString(value, CultureInfo.InvariantCulture);
                    Require(
                        string.Equals(currentDatabase, ExpectedDatabase, StringComparison.Ordinal),
                        "active-database-identity");
                }

                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT SUBSTRING_INDEX(CURRENT_USER(), '@', 1)";
                    string currentUser = Convert.ToString(
                        command.ExecuteScalar(),
                        CultureInfo.InvariantCulture);
                    Require(
                        string.Equals(currentUser, ExpectedUser, StringComparison.Ordinal),
                        "active-database-user");
                }
            }
        }

        private static void ValidateSchemaContract()
        {
            using (IDbConnection connection = Connector.GetConnection())
            {
                var actualTables = new HashSet<string>(StringComparer.Ordinal);
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT table_name FROM information_schema.tables "
                        + "WHERE table_schema=DATABASE() AND table_type='BASE TABLE'";
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            actualTables.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture));
                        }
                    }
                }

                Require(
                    actualTables.Count == ExpectedTables.Length,
                    "schema-table-count");
                foreach (string tableName in ExpectedTables)
                {
                    Require(actualTables.Contains(tableName), "schema-table-set");
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT 1 FROM `" + tableName + "` LIMIT 0";
                        using (IDataReader reader = command.ExecuteReader())
                        {
                        }
                    }
                }

                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM information_schema.columns "
                        + "WHERE table_schema=DATABASE() AND table_name='characters' "
                        + "AND column_name='Online'") == 1,
                    "schema-characters-online-column");
                Require(
                    ExecuteScalarLong(connection, "SELECT COUNT(*) FROM characters WHERE Online <> 0") == 0,
                    "schema-online-character-residue");

                string[] initiallyEmptyTables =
                {
                    "login",
                    "characters",
                    "stats",
                    "organizations",
                    "receivedmessages"
                };
                foreach (string tableName in initiallyEmptyTables)
                {
                    Require(
                        ExecuteScalarLong(connection, "SELECT COUNT(*) FROM `" + tableName + "`") == 0,
                        "schema-mutable-table-not-empty");
                }
            }
        }

        private static long ExecuteScalarLong(IDbConnection connection, string commandText)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void ValidateConnectionTarget(string connectionString)
        {
            MySqlConnectionStringBuilder builder;
            try
            {
                builder = new MySqlConnectionStringBuilder(connectionString);
            }
            catch
            {
                throw new Stage6ContractException("connection-format");
            }

            Require(
                string.Equals(builder.Server, ExpectedServer, StringComparison.Ordinal),
                "connection-server-not-exact-loopback");
            Require(builder.Port == ExpectedPort, "connection-port-not-isolated");
            Require(
                string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal),
                "connection-database-not-disposable");
            Require(
                string.Equals(builder.UserID, ExpectedUser, StringComparison.Ordinal),
                "connection-user-not-disposable");
            Require(!string.IsNullOrWhiteSpace(builder.Password), "connection-password-empty");
        }

        private static void ExpectTargetRejection(string connectionString)
        {
            try
            {
                ValidateConnectionTarget(connectionString);
            }
            catch (Stage6ContractException)
            {
                return;
            }

            throw new Stage6ContractException("offline-target-guard-accepted-invalid-target");
        }

        private static void VerifyLoginKeySelfCheck(
            LoginEncryption encryption,
            string loginKey,
            string username,
            string serverSalt,
            string password)
        {
            string decodedUsername;
            string decodedSalt;
            string decodedPassword;
            encryption.DecryptLoginKey(
                loginKey,
                out decodedUsername,
                out decodedSalt,
                out decodedPassword);
            Require(
                string.Equals(decodedUsername, username, StringComparison.Ordinal),
                "login-key-self-check-username");
            Require(
                string.Equals(decodedSalt, serverSalt, StringComparison.Ordinal),
                "login-key-self-check-salt");
            Require(
                string.Equals(decodedPassword, password, StringComparison.Ordinal),
                "login-key-self-check-password");
        }

        private static void CleanupFixture(
            string username,
            string characterName)
        {
            int removedCharacters;
            int removedLogins;
            using (IDbConnection connection = Connector.GetConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                removedCharacters = ExecuteFixtureDelete(
                    connection,
                    transaction,
                    "DELETE FROM characters WHERE Username=@username AND Name=@characterName",
                    username,
                    characterName);
                removedLogins = ExecuteFixtureDelete(
                    connection,
                    transaction,
                    "DELETE FROM login WHERE Username=@username",
                    username,
                    characterName);

                transaction.Commit();
            }

            using (IDbConnection connection = Connector.GetConnection())
            {
                Require(
                    CountFixtureRows(
                        connection,
                        "SELECT COUNT(*) FROM characters WHERE Username=@username AND Name=@characterName",
                        username,
                        characterName) == 0,
                    "fixture-character-residue");
                Require(
                    CountFixtureRows(
                        connection,
                        "SELECT COUNT(*) FROM login WHERE Username=@username",
                        username,
                        characterName) == 0,
                    "fixture-login-residue");
            }

            Require(removedCharacters <= 1, "fixture-character-delete-count");
            Require(removedLogins <= 1, "fixture-login-delete-count");
        }

        private static int ExecuteFixtureDelete(
            IDbConnection connection,
            IDbTransaction transaction,
            string commandText,
            string username,
            string characterName)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = commandText;
                AddParameter(command, "@username", username);
                if (commandText.IndexOf("@characterName", StringComparison.Ordinal) >= 0)
                {
                    AddParameter(command, "@characterName", characterName);
                }

                return command.ExecuteNonQuery();
            }
        }

        private static long CountFixtureRows(
            IDbConnection connection,
            string commandText,
            string username,
            string characterName)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                AddParameter(command, "@username", username);
                if (commandText.IndexOf("@characterName", StringComparison.Ordinal) >= 0)
                {
                    AddParameter(command, "@characterName", characterName);
                }

                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static byte[] CreateSalt(int firstValue)
        {
            var salt = new byte[32];
            for (int index = 0; index < salt.Length; index++)
            {
                salt[index] = checked((byte)(firstValue + index));
            }

            return salt;
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void Require(bool condition, string code)
        {
            if (!condition)
            {
                throw new Stage6ContractException(code);
            }
        }

        private sealed class Stage6ContractException : Exception
        {
            internal Stage6ContractException(string code)
                : base("Stage 6 contract failed.")
            {
                this.Code = code;
            }

            internal string Code { get; private set; }
        }

        private static class LoginKeyEncoder
        {
            private const string ClientPublicKeyHex = "2";
            private const string PrimeHex =
                "eca2e8c85d863dcdc26a429a71a9815ad052f6139669dd659f98ae159d313d13c6bf2838e10a69b6478b64a24bd054ba8248e8fa778703b418408249440b2c1edd28853e240d8a7e49540b76d120d3b1ad2878b1b99490eb4a2a5e84caa8a91cecbdb1aa7c816e8be343246f80c637abc653b893fd91686cf8d32d6cfe5f2a6f";
            private const string ServerPrivateKeyHex =
                "7ad852c6494f664e8df21446285ecd6f400cf20e1d872ee96136d7744887424b";

            internal static string Create(string username, string password, byte[] salt)
            {
                byte[] usernameBytes = GetAscii(username, "login-key-username-ascii");
                byte[] passwordBytes = GetAscii(password, "login-key-password-ascii");
                Require(usernameBytes.Length > 0, "login-key-username-empty");
                Require(passwordBytes.Length > 0, "login-key-password-empty");
                Require(Array.IndexOf(usernameBytes, (byte)'|') < 0, "login-key-username-delimiter");
                Require(Array.IndexOf(passwordBytes, (byte)'|') < 0, "login-key-password-delimiter");
                Require(salt != null && salt.Length == 32, "login-key-salt-size");

                for (int index = 0; index < salt.Length; index++)
                {
                    Require(salt[index] != 0, "login-key-salt-zero");
                }

                int dataLength = checked(usernameBytes.Length + 34 + passwordBytes.Length);
                var plaintext = new List<byte>();
                plaintext.AddRange(new byte[8]);
                plaintext.Add((byte)((dataLength >> 24) & 0xff));
                plaintext.Add((byte)((dataLength >> 16) & 0xff));
                plaintext.Add((byte)((dataLength >> 8) & 0xff));
                plaintext.Add((byte)(dataLength & 0xff));
                plaintext.AddRange(usernameBytes);
                plaintext.Add((byte)'|');
                plaintext.AddRange(salt);
                plaintext.Add((byte)'|');
                plaintext.AddRange(passwordBytes);
                while ((plaintext.Count & 7) != 0)
                {
                    plaintext.Add(0);
                }

                uint[] teaKey = CreateTeaKey();
                uint previousLeft = 0;
                uint previousRight = 0;
                var encrypted = new StringBuilder(plaintext.Count * 2);
                byte[] bytes = plaintext.ToArray();
                for (int offset = 0; offset < bytes.Length; offset += 8)
                {
                    uint left = ReadUInt32LittleEndian(bytes, offset) ^ previousLeft;
                    uint right = ReadUInt32LittleEndian(bytes, offset + 4) ^ previousRight;
                    EncryptTeaRound(ref left, ref right, teaKey);
                    encrypted.Append(ToNetworkHex(left));
                    encrypted.Append(ToNetworkHex(right));
                    previousLeft = left;
                    previousRight = right;
                }

                return ClientPublicKeyHex + "-" + encrypted;
            }

            private static byte[] GetAscii(string value, string failureCode)
            {
                Require(value != null, failureCode);
                byte[] bytes = Encoding.ASCII.GetBytes(value);
                Require(
                    string.Equals(Encoding.ASCII.GetString(bytes), value, StringComparison.Ordinal),
                    failureCode);
                return bytes;
            }

            private static uint[] CreateTeaKey()
            {
                var clientPublicKey = new BigInteger(ClientPublicKeyHex, 16);
                var serverPrivateKey = new BigInteger(ServerPrivateKeyHex, 16);
                var prime = new BigInteger(PrimeHex, 16);
                string keyText = clientPublicKey.modPow(serverPrivateKey, prime)
                    .ToString(16)
                    .ToLowerInvariant();
                Require(keyText.Length >= 32, "login-key-shared-secret-too-short");
                if (keyText.Length > 32)
                {
                    keyText = keyText.Substring(0, 32);
                }

                var key = new uint[4];
                for (int index = 0; index < key.Length; index++)
                {
                    int networkWord = Convert.ToInt32(
                        keyText.Substring(index * 8, 8),
                        16);
                    key[index] = unchecked((uint)IPAddress.NetworkToHostOrder(networkWord));
                }

                return key;
            }

            private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
            {
                return (uint)(bytes[offset]
                              | (bytes[offset + 1] << 8)
                              | (bytes[offset + 2] << 16)
                              | (bytes[offset + 3] << 24));
            }

            private static string ToNetworkHex(uint value)
            {
                int networkValue = IPAddress.HostToNetworkOrder(unchecked((int)value));
                return unchecked((uint)networkValue).ToString("x8", CultureInfo.InvariantCulture);
            }

            private static void EncryptTeaRound(ref uint left, ref uint right, uint[] key)
            {
                const uint Delta = 0x9e3779b9;
                uint sum = 0;
                for (int round = 0; round < 32; round++)
                {
                    unchecked
                    {
                        sum += Delta;
                        left += ((right << 4) + key[0])
                                ^ (right + sum)
                                ^ ((right >> 5) + key[1]);
                        right += ((left << 4) + key[2])
                                 ^ (left + sum)
                                 ^ ((left >> 5) + key[3]);
                    }
                }
            }
        }
    }
}
