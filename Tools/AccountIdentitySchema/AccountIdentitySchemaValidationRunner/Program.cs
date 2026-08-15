namespace AccountIdentitySchemaValidationRunner
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    using MySqlConnector;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string connectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    Console.WriteLine("FAIL AO_REBIRTH_MYSQL_CONNECTION is missing.");
                    return 2;
                }

                string repositoryRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
                string schemaPath = Path.Combine(
                    repositoryRoot,
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Database",
                    "SqlTables",
                    "aorebirth_identity.sql");
                if (!File.Exists(schemaPath))
                {
                    Console.WriteLine("FAIL schema file missing.");
                    return 2;
                }

                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    ResetIdentityTables(connection);
                    ApplySchema(connection, File.ReadAllText(schemaPath));
                    ValidateTables(connection);
                    RunValidationScenario(connection);
                }

                Console.WriteLine(
                    "AORebirth account identity schema validation PASS | IdentityRows 3 | GameMappingRows 1 | ExternalMappingRows 1 | ProvisioningState GameAccountLinked");
                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("FAIL " + exception.GetType().Name + ": " + exception.Message);
                return 1;
            }
        }

        private static void ResetIdentityTables(MySqlConnection connection)
        {
            Execute(connection, "DROP TABLE IF EXISTS `account_provisioning_jobs`");
            Execute(connection, "DROP TABLE IF EXISTS `account_external_mappings`");
            Execute(connection, "DROP TABLE IF EXISTS `account_game_mappings`");
            Execute(connection, "DROP TABLE IF EXISTS `account_identities`");
        }

        private static void ApplySchema(MySqlConnection connection, string schemaSql)
        {
            foreach (string statement in schemaSql.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = statement.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                Execute(connection, trimmed);
            }
        }

        private static void ValidateTables(MySqlConnection connection)
        {
            string tables = Convert.ToString(
                Scalar(
                    connection,
                    "SELECT GROUP_CONCAT(table_name ORDER BY table_name SEPARATOR ',') FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('account_identities','account_game_mappings','account_external_mappings','account_provisioning_jobs')"));
            if (tables != "account_external_mappings,account_game_mappings,account_identities,account_provisioning_jobs")
            {
                throw new InvalidOperationException("identity table set mismatch: " + tables);
            }
        }

        private static void RunValidationScenario(MySqlConnection connection)
        {
            Execute(
                connection,
                "INSERT INTO `account_identities` (`IdentityPublicId`, `CanonicalUsername`, `NormalizedUsername`, `CanonicalEmail`, `NormalizedEmail`, `EmailVerifiedAt`, `IdentityStatus`) VALUES ('00000000-0000-0000-0000-000000000101', 'Player', 'player', 'Player@example.com', 'player@example.com', CURRENT_TIMESTAMP(6), 'Reserved'), ('00000000-0000-0000-0000-000000000201', 'Second', 'second', NULL, NULL, NULL, 'Reserved'), ('00000000-0000-0000-0000-000000000301', 'Old', 'old', NULL, NULL, NULL, 'Reserved')");

            ExpectRejected(
                connection,
                "INSERT INTO `account_identities` (`IdentityPublicId`, `CanonicalUsername`, `NormalizedUsername`, `CanonicalEmail`, `NormalizedEmail`) VALUES ('00000000-0000-0000-0000-000000000102', 'player', 'player', NULL, NULL)",
                "duplicate normalized username was accepted");

            Execute(
                connection,
                "INSERT INTO `account_game_mappings` (`IdentityId`, `GameAccountId`, `MappingState`, `LinkedAt`) VALUES (1, 1001, 'Linked', CURRENT_TIMESTAMP(6))");

            ExpectRejected(
                connection,
                "INSERT INTO `account_game_mappings` (`IdentityId`, `GameAccountId`, `MappingState`, `LinkedAt`) VALUES (2, 1001, 'Linked', CURRENT_TIMESTAMP(6))",
                "duplicate game account mapping was accepted");

            Execute(
                connection,
                "INSERT INTO `account_external_mappings` (`IdentityId`, `Provider`, `ExternalAccountId`, `MappingState`, `LinkedAt`) VALUES (1, 'mybb', '42', 'Linked', CURRENT_TIMESTAMP(6))");

            ExpectRejected(
                connection,
                "INSERT INTO `account_external_mappings` (`IdentityId`, `Provider`, `ExternalAccountId`, `MappingState`, `LinkedAt`) VALUES (2, 'mybb', '42', 'Linked', CURRENT_TIMESTAMP(6))",
                "duplicate provider external account mapping was accepted");

            Execute(
                connection,
                "INSERT INTO `account_provisioning_jobs` (`IdempotencyKeyHash`, `IdentityId`, `RequestedNormalizedUsername`, `RequestedNormalizedEmail`, `RequestedGameAccountId`, `ProvisioningState`, `ProvisioningStep`) VALUES (@hash, 1, 'player', 'player@example.com', NULL, 'IdentityReserved', 10)",
                Parameter("@hash", Hash("player-registration")));

            Execute(
                connection,
                "UPDATE `account_provisioning_jobs` SET `RequestedGameAccountId` = 1001, `ProvisioningState` = 'GameAccountLinked', `ProvisioningStep` = 30 WHERE `ProvisioningJobId` = 1 AND `ProvisioningStep` < 30");

            Execute(
                connection,
                "UPDATE `account_provisioning_jobs` SET `ProvisioningState` = 'IdentityReserved', `ProvisioningStep` = 10 WHERE `ProvisioningJobId` = 1 AND `ProvisioningStep` < 10");

            ExpectRejected(
                connection,
                "INSERT INTO `account_provisioning_jobs` (`IdempotencyKeyHash`, `IdentityId`, `RequestedNormalizedUsername`, `ProvisioningState`, `ProvisioningStep`) VALUES (@hash, 1, 'player', 'Active', 10)",
                "invalid provisioning state/step pair was accepted",
                Parameter("@hash", Hash("bad-state")));

            string state = Convert.ToString(
                Scalar(
                    connection,
                    "SELECT `ProvisioningState` FROM `account_provisioning_jobs` WHERE `ProvisioningJobId` = 1"));
            if (state != "GameAccountLinked")
            {
                throw new InvalidOperationException("provisioning state mismatch: " + state);
            }
        }

        private static void ExpectRejected(MySqlConnection connection, string sql, string failureMessage, params MySqlParameter[] parameters)
        {
            try
            {
                Execute(connection, sql, parameters);
            }
            catch (MySqlException)
            {
                return;
            }

            throw new InvalidOperationException(failureMessage);
        }

        private static byte[] Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        private static MySqlParameter Parameter(string name, object value)
        {
            return new MySqlParameter(name, value);
        }

        private static object Scalar(MySqlConnection connection, string sql, params MySqlParameter[] parameters)
        {
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                return command.ExecuteScalar();
            }
        }

        private static void Execute(MySqlConnection connection, string sql, params MySqlParameter[] parameters)
        {
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                command.ExecuteNonQuery();
            }
        }
    }
}
