namespace AccountBrokerValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    using AO.Core.Encryption;
    using AORebirth.AccountBroker;

    using MySqlConnector;

    internal static class Program
    {
        private const string ServerPrivateKey =
            "7ad852c6494f664e8df21446285ecd6f400cf20e1d872ee96136d7744887424b";

        private const string Prime =
            "eca2e8c85d863dcdc26a429a71a9815ad052f6139669dd659f98ae159d313d13c6bf2838e10a69b6478b64a24bd054ba8248e8fa778703b418408249440b2c1edd28853e240d8a7e49540b76d120d3b1ad2878b1b99490eb4a2a5e84caa8a91cecbdb1aa7c816e8be343246f80c637abc653b893fd91686cf8d32d6cfe5f2a6f";

        private const string ClientPublicKey =
            "8f2d7c34a0b9e8d6c5f4a3928170615049382716f5e4d3c2b1a09876543210fedcba98765432100123456789abcdef";

        private const string ServerSalt =
            "00112233445566778899aabbccddeeff102132435465768798a9babbdcedfe0f";

        private static readonly string[] TestUsernames =
        {
            "BrokerA1",
            "BrokerB1",
            "BrokerC1",
            "BrokerD1",
            "BrokerE1",
            "Old5",
            "Old6"
        };

        private static int Main()
        {
            string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("FAIL AO_REBIRTH_MYSQL_CONNECTION is missing.");
                return 1;
            }

            var failures = new List<string>();
            var broker = new AccountBrokerService(() => new MySqlConnection(connectionString));

            try
            {
                Cleanup(connectionString);
                RunValidations(connectionString, broker, failures);
            }
            catch (Exception ex)
            {
                failures.Add(FormatException(ex));
            }
            finally
            {
                try
                {
                    Cleanup(connectionString);
                }
                catch (Exception cleanup)
                {
                    failures.Add("cleanup failed: " + FormatException(cleanup));
                }
            }

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Console.WriteLine("FAIL " + failure);
                }

                return 1;
            }

            Console.WriteLine("PASS AccountBrokerValidation 31/31");
            return 0;
        }

        private static string FormatException(Exception exception)
        {
            var messages = new List<string>();
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                messages.Add(current.GetType().Name + ": " + current.Message);
            }

            return string.Join(" | ", messages);
        }

        private static void RunValidations(
            string connectionString,
            AccountBrokerService broker,
            IList<string> failures)
        {
            string password = "Broker-Password-123!";
            AccountProvisioningResult created = broker.CreateGameAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = "new-account",
                    Username = "BrokerA1",
                    Password = password,
                    Email = "BrokerA1@example.com",
                    FirstName = "Broker",
                    LastName = "A"
                });
            Expect("new account active", "Active", created.ProvisioningState, failures);
            GameAccountSnapshot game = broker.GetGameAccount(created.GameAccountId);
            Expect("new account username", "BrokerA1", game.Username, failures);
            Expect("new account flags", 0, game.Flags, failures);
            Expect("new account accountflags", 0, game.AccountFlags, failures);
            Expect("new account gm", 0, game.GM, failures);
            Expect("correct generated password validates", true, ValidatePassword("BrokerA1", password, game.PasswordHash), failures);
            Expect("wrong generated password rejects", false, ValidatePassword("BrokerA1", "WrongPassword", game.PasswordHash), failures);

            AccountProvisioningResult retry = broker.CreateGameAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = "new-account",
                    Username = "BrokerA1",
                    Password = password,
                    Email = "BrokerA1@example.com"
                });
            Expect("retry same identity", created.IdentityId, retry.IdentityId, failures);
            Expect("retry same game account", created.GameAccountId, retry.GameAccountId, failures);
            Expect("one login row after retry", 1, CountLoginRows(connectionString, "BrokerA1"), failures);
            ForumSsoIdentity activeForumIdentity = broker.GetForumSsoIdentityByPublicId(
                GetIdentityPublicId(connectionString, created.IdentityId));
            Expect("forum sso active identity username", "BrokerA1", activeForumIdentity.CanonicalUsername, failures);
            ExternalMappingResult activeForumMapping = broker.ConfirmForumExternalMapping(activeForumIdentity.IdentityPublicId, "99");
            Expect("forum confirm active mybb uid", "99", activeForumMapping.ExternalAccountId, failures);
            Expect("forum sso active existing mybb", "99", broker.GetForumSsoIdentityByPublicId(activeForumIdentity.IdentityPublicId).ExistingMybbUid, failures);

            ExpectBrokerException(
                "case-equivalent duplicate rejected",
                "USERNAME_EXISTS",
                () => broker.CreateGameAccount(
                    new CreateAccountRequest
                    {
                        IdempotencyKey = "case-duplicate",
                        Username = "brokera1",
                        Password = password
                    }),
                failures);
            ExpectBrokerException(
                "invalid new username rejected",
                "INVALID_USERNAME",
                () => broker.CreateGameAccount(
                    new CreateAccountRequest
                    {
                        IdempotencyKey = "invalid-username",
                        Username = "Bad!",
                        Password = password
                    }),
                failures);

            InsertFixtureGameAccount(connectionString, "Old5", "Legacy-Password-1!");
            int old5Id = GetLoginId(connectionString, "Old5");
            string old5Hash = broker.GetGameAccount(old5Id).PasswordHash;
            string old5NameBefore = broker.GetGameAccount(old5Id).Username;
            InsertFixtureCharacter(connectionString, "Old5");
            IdentityResult legacyIdentity = broker.CreateLegacyIdentityForExistingGameAccount(old5Id);
            Expect("legacy short identity normalized", "old5", legacyIdentity.NormalizedUsername, failures);
            broker.LinkExistingGameAccount(legacyIdentity.IdentityId, old5Id);
            broker.LinkExistingGameAccount(legacyIdentity.IdentityId, old5Id);
            Expect("legacy password unchanged", old5Hash, broker.GetGameAccount(old5Id).PasswordHash, failures);
            Expect("legacy username unchanged", old5NameBefore, broker.GetGameAccount(old5Id).Username, failures);
            Expect("legacy character owner unchanged", 1, CountCharacters(connectionString, "Old5"), failures);

            InsertFixtureGameAccount(connectionString, "Old6", "Legacy-Password-2!");
            int old6Id = GetLoginId(connectionString, "Old6");
            IdentityResult otherIdentity = broker.CreateLegacyIdentityForExistingGameAccount(old6Id);
            ExpectBrokerException(
                "conflicting existing game mapping rejected",
                "GAME_ACCOUNT_MAPPING_CONFLICT",
                () => broker.LinkExistingGameAccount(otherIdentity.IdentityId, old5Id),
                failures);

            ExternalMappingResult external = broker.ReserveExternalMapping(legacyIdentity.IdentityId, "mybb", "42");
            Expect("mybb provider accepted", "mybb", external.Provider, failures);
            broker.ReserveExternalMapping(legacyIdentity.IdentityId, "mybb", "42");
            ExpectBrokerException(
                "duplicate mybb uid rejected for other identity",
                "EXTERNAL_MAPPING_CONFLICT",
                () => broker.ReserveExternalMapping(otherIdentity.IdentityId, "mybb", "42"),
                failures);
            ExpectBrokerException(
                "same identity second mybb uid rejected",
                "IDENTITY_EXTERNAL_PROVIDER_CONFLICT",
                () => broker.ReserveExternalMapping(legacyIdentity.IdentityId, "mybb", "43"),
                failures);

            SimulateInterruptedAfterIdentity(connectionString);
            AccountProvisioningResult resumedIdentity = broker.CreateGameAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = "interrupted-after-identity",
                    Username = "BrokerB1",
                    Password = password
                });
            Expect("resume after identity active", "Active", resumedIdentity.ProvisioningState, failures);

            SimulateInterruptedAfterJob(connectionString);
            AccountProvisioningResult resumedJob = broker.CreateGameAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = "interrupted-after-job",
                    Username = "BrokerC1",
                    Password = password
                });
            Expect("resume after job active", "Active", resumedJob.ProvisioningState, failures);

            SimulateInterruptedAfterGameAccount(connectionString);
            AccountProvisioningResult resumedGame = broker.CreateGameAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = "interrupted-after-game",
                    Username = "BrokerD1",
                    Password = password
                });
            Expect("resume after game active", "Active", resumedGame.ProvisioningState, failures);
            Expect("resume after game did not duplicate login", 1, CountLoginRows(connectionString, "BrokerD1"), failures);

            SimulateInterruptedAfterMapping(connectionString);
            AccountProvisioningResult resumedMapping = broker.CreateGameAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = "interrupted-after-mapping",
                    Username = "BrokerE1",
                    Password = password
                });
            Expect("resume after mapping active", "Active", resumedMapping.ProvisioningState, failures);

            Expect("active cannot regress silently", 60, broker.GetProvisioningStatus("interrupted-after-mapping").Step, failures);
        }

        private static void Cleanup(string connectionString)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                Execute(connection, "DELETE FROM account_external_mappings");
                Execute(connection, "DELETE FROM account_game_mappings");
                Execute(connection, "DELETE FROM account_provisioning_jobs");
                Execute(connection, "DELETE FROM account_identities");
                Execute(connection, "DELETE FROM characters WHERE Username IN ('BrokerA1','BrokerB1','BrokerC1','BrokerD1','BrokerE1','Old5','Old6')");
                Execute(connection, "DELETE FROM login WHERE Username IN ('BrokerA1','BrokerB1','BrokerC1','BrokerD1','BrokerE1','Old5','Old6')");
            }
        }

        private static void SimulateInterruptedAfterIdentity(string connectionString)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                long identityId = InsertIdentity(connection, "BrokerB1", "brokerb1");
                InsertJob(connection, "interrupted-after-identity", identityId, "brokerb1", "IdentityReserved", 10, null);
            }
        }

        private static void SimulateInterruptedAfterJob(string connectionString)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                long identityId = InsertIdentity(connection, "BrokerC1", "brokerc1");
                InsertJob(connection, "interrupted-after-job", identityId, "brokerc1", "GameAccountPending", 20, null);
            }
        }

        private static void SimulateInterruptedAfterGameAccount(string connectionString)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                long identityId = InsertIdentity(connection, "BrokerD1", "brokerd1");
                InsertFixtureGameAccount(connectionString, "BrokerD1", "Broker-Password-123!");
                int gameAccountId = GetLoginId(connectionString, "BrokerD1");
                InsertJob(connection, "interrupted-after-game", identityId, "brokerd1", "GameAccountPending", 20, gameAccountId);
            }
        }

        private static void SimulateInterruptedAfterMapping(string connectionString)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                long identityId = InsertIdentity(connection, "BrokerE1", "brokere1");
                InsertFixtureGameAccount(connectionString, "BrokerE1", "Broker-Password-123!");
                int gameAccountId = GetLoginId(connectionString, "BrokerE1");
                Execute(
                    connection,
                    "INSERT INTO account_game_mappings (IdentityId, GameAccountId, MappingState, LinkedAt) VALUES (@identityId, @gameAccountId, 'Linked', CURRENT_TIMESTAMP(6))",
                    Parameter("@identityId", identityId),
                    Parameter("@gameAccountId", gameAccountId));
                InsertJob(connection, "interrupted-after-mapping", identityId, "brokere1", "GameAccountLinked", 30, gameAccountId);
            }
        }

        private static long InsertIdentity(MySqlConnection connection, string canonical, string normalized)
        {
            Execute(
                connection,
                "INSERT INTO account_identities (IdentityPublicId, CanonicalUsername, NormalizedUsername, IdentityStatus) VALUES (@publicId, @canonical, @normalized, 'Reserved')",
                Parameter("@publicId", Guid.NewGuid().ToString("D")),
                Parameter("@canonical", canonical),
                Parameter("@normalized", normalized));
            return Convert.ToInt64(Scalar(connection, "SELECT LAST_INSERT_ID()"));
        }

        private static void InsertJob(
            MySqlConnection connection,
            string idempotencyKey,
            long identityId,
            string normalizedUsername,
            string state,
            int step,
            int? gameAccountId)
        {
            Execute(
                connection,
                "INSERT INTO account_provisioning_jobs (IdempotencyKeyHash, IdentityId, RequestedNormalizedUsername, RequestedGameAccountId, ProvisioningState, ProvisioningStep) VALUES (@hash, @identityId, @username, @gameAccountId, @state, @step)",
                Parameter("@hash", HashIdempotencyKey(idempotencyKey)),
                Parameter("@identityId", identityId),
                Parameter("@username", normalizedUsername),
                Parameter("@gameAccountId", gameAccountId.HasValue ? (object)gameAccountId.Value : DBNull.Value),
                Parameter("@state", state),
                Parameter("@step", step));
        }

        private static void InsertFixtureGameAccount(string connectionString, string username, string password)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string hash = new LoginEncryption().GeneratePasswordHash(password);
                Execute(
                    connection,
                    "INSERT INTO login (CreationDate, Email, FirstName, LastName, Username, Password, AllowedCharacters, Flags, AccountFlags, Expansions, GM) VALUES (CURRENT_TIMESTAMP(), '', '', '', @username, @password, 6, 0, 0, 127, 0)",
                    Parameter("@username", username),
                    Parameter("@password", hash));
            }
        }

        private static void InsertFixtureCharacter(string connectionString, string username)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                Execute(
                    connection,
                    "INSERT INTO characters (Username, Name, FirstName, LastName, playfield, X, Y, Z, HeadingX, HeadingY, HeadingZ, HeadingW, Online, BuddyList) VALUES (@username, @name, '', '', 655, 0, 0, 0, 0, 0, 0, 1, 0, '')",
                    Parameter("@username", username),
                    Parameter("@name", username + "Char"));
            }
        }

        private static int GetLoginId(string connectionString, string username)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                return Convert.ToInt32(
                    Scalar(
                        connection,
                        "SELECT Id FROM login WHERE Username=@username",
                        Parameter("@username", username)));
            }
        }

        private static string GetIdentityPublicId(string connectionString, long identityId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                return Convert.ToString(
                    Scalar(
                        connection,
                        "SELECT IdentityPublicId FROM account_identities WHERE IdentityId=@identityId",
                        Parameter("@identityId", identityId)));
            }
        }

        private static int CountLoginRows(string connectionString, string username)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                return Convert.ToInt32(
                    Scalar(
                        connection,
                        "SELECT COUNT(*) FROM login WHERE Username=@username",
                        Parameter("@username", username)));
            }
        }

        private static int CountCharacters(string connectionString, string username)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                return Convert.ToInt32(
                    Scalar(
                        connection,
                        "SELECT COUNT(*) FROM characters WHERE Username=@username",
                        Parameter("@username", username)));
            }
        }

        private static bool ValidatePassword(string account, string password, string storedHash)
        {
            string loginKey = CreateLoginKey(account, password, ServerSalt);
            return new LoginEncryption().IsValidLogin(loginKey, ServerSalt, account, storedHash);
        }

        private static void Expect<T>(string name, T expected, T actual, IList<string> failures)
        {
            if (!object.Equals(expected, actual))
            {
                failures.Add(name + " expected " + expected + " actual " + actual);
            }
        }

        private static void ExpectBrokerException(
            string name,
            string expectedCode,
            Action action,
            IList<string> failures)
        {
            try
            {
                action();
                failures.Add(name + " expected exception " + expectedCode);
            }
            catch (AccountBrokerException ex)
            {
                if (ex.Code != expectedCode)
                {
                    failures.Add(name + " expected " + expectedCode + " actual " + ex.Code);
                }
            }
        }

        private static object Scalar(MySqlConnection connection, string sql, params MySqlParameter[] parameters)
        {
            using (var command = new MySqlCommand(sql, connection))
            {
                foreach (MySqlParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                return command.ExecuteScalar();
            }
        }

        private static void Execute(MySqlConnection connection, string sql, params MySqlParameter[] parameters)
        {
            using (var command = new MySqlCommand(sql, connection))
            {
                foreach (MySqlParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                command.ExecuteNonQuery();
            }
        }

        private static MySqlParameter Parameter(string name, object value)
        {
            return new MySqlParameter(name, value ?? DBNull.Value);
        }

        private static byte[] HashIdempotencyKey(string idempotencyKey)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(idempotencyKey));
            }
        }

        private static string CreateLoginKey(string username, string password, string serverSalt)
        {
            string teaKey = ComputeTeaKey();
            string plaintext = CreatePlaintext(username, password, serverSalt);
            string encryptedBlock = EncryptTea(plaintext, teaKey);
            return ClientPublicKey + "-" + encryptedBlock;
        }

        private static string ComputeTeaKey()
        {
            var clientPublicKey = new BigInteger(ClientPublicKey, 16);
            var serverPrivateKey = new BigInteger(ServerPrivateKey, 16);
            var prime = new BigInteger(Prime, 16);
            string teaKey = clientPublicKey.modPow(serverPrivateKey, prime).ToString(16).ToLowerInvariant();
            if (teaKey.Length < 32)
            {
                teaKey = teaKey.PadLeft(32, '0');
            }
            else if (teaKey.Length > 32)
            {
                teaKey = teaKey.Substring(0, 32);
            }

            return teaKey;
        }

        private static string CreatePlaintext(string username, string password, string serverSalt)
        {
            string saltBytes = SaltHexToBytes(serverSalt);
            int dataLength = username.Length + password.Length + 34;
            var sb = new StringBuilder();
            sb.Append("AOLOGIN!");
            sb.Append(IntToBigEndianString(dataLength));
            sb.Append(username);
            sb.Append('|');
            sb.Append(saltBytes);
            sb.Append('|');
            sb.Append(password);
            while (sb.Length % 8 != 0)
            {
                sb.Append('\0');
            }

            return sb.ToString();
        }

        private static string SaltHexToBytes(string serverSalt)
        {
            var sb = new StringBuilder(32);
            for (int index = 0; index < serverSalt.Length; index += 8)
            {
                uint value = uint.Parse(
                    serverSalt.Substring(index, 8),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                sb.Append((char)((value >> 24) & 0xFF));
                sb.Append((char)((value >> 16) & 0xFF));
                sb.Append((char)((value >> 8) & 0xFF));
                sb.Append((char)(value & 0xFF));
            }

            return sb.ToString();
        }

        private static string IntToBigEndianString(int value)
        {
            var sb = new StringBuilder(4);
            sb.Append((char)((value >> 24) & 0xFF));
            sb.Append((char)((value >> 16) & 0xFF));
            sb.Append((char)((value >> 8) & 0xFF));
            sb.Append((char)(value & 0xFF));
            return sb.ToString();
        }

        private static string EncryptTea(string plaintext, string key)
        {
            uint[] keyInt = ConvertHexKeyToUInts(key);
            uint[] previous = { 0U, 0U };
            var encrypted = new StringBuilder();

            for (int index = 0; index < plaintext.Length; index += 8)
            {
                uint[] block =
                {
                    PlaintextToUInt(plaintext, index) ^ previous[0],
                    PlaintextToUInt(plaintext, index + 4) ^ previous[1]
                };

                EncryptTeaRound(block, keyInt);
                encrypted.Append(UIntToNetworkHex(block[0]));
                encrypted.Append(UIntToNetworkHex(block[1]));
                previous[0] = block[0];
                previous[1] = block[1];
            }

            return encrypted.ToString();
        }

        private static uint[] ConvertHexKeyToUInts(string key)
        {
            return new[]
            {
                ConvertHexToUInt(key.Substring(0, 8)),
                ConvertHexToUInt(key.Substring(8, 8)),
                ConvertHexToUInt(key.Substring(16, 8)),
                ConvertHexToUInt(key.Substring(24, 8))
            };
        }

        private static uint PlaintextToUInt(string input, int index)
        {
            return (uint)input[index]
                   | ((uint)input[index + 1] << 8)
                   | ((uint)input[index + 2] << 16)
                   | ((uint)input[index + 3] << 24);
        }

        private static uint ConvertHexToUInt(string hexInput)
        {
            return (uint)IPAddress.NetworkToHostOrder(Convert.ToInt32(hexInput, 16));
        }

        private static string UIntToNetworkHex(uint value)
        {
            int networkValue = IPAddress.HostToNetworkOrder(unchecked((int)value));
            return unchecked((uint)networkValue).ToString("x8", CultureInfo.InvariantCulture);
        }

        private static void EncryptTeaRound(uint[] data, uint[] key)
        {
            uint n = 32;
            uint sum = 0;
            const uint Delta = 0x9e3779b9;

            while (n-- > 0)
            {
                sum += Delta;
                data[0] += ((data[1] << 4) + key[0]) ^ (data[1] + sum) ^ ((data[1] >> 5) + key[1]);
                data[1] += ((data[0] << 4) + key[2]) ^ (data[0] + sum) ^ ((data[0] >> 5) + key[3]);
            }
        }
    }
}
