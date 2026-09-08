namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using MySqlConnector;

    using Utility;

    /// <summary>
    /// Ensures the MySQL database exists, loads missing SqlTables, then applies Migrations.
    /// </summary>
    internal static class DatabaseMigrationRunner
    {
        private const string MigrationsFolderName = "Migrations";

        private const int UnknownDatabaseErrorNumber = 1049;

        private const int AccessDeniedDatabaseErrorNumber = 1044;

        private const int AccessDeniedErrorNumber = 1045;

        private const string EnsureTrackingTableSql =
            "CREATE TABLE IF NOT EXISTS `schema_migrations` ("
            + "`MigrationName` VARCHAR(255) NOT NULL,"
            + "`AppliedAtUtc` DATETIME(6) NOT NULL,"
            + "PRIMARY KEY (`MigrationName`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci";

        public static bool TryApplyPendingMigrations()
        {
            string migrationsRoot = Path.Combine(AppContext.BaseDirectory, MigrationsFolderName);
            bool migrationsFolderExists = Directory.Exists(migrationsRoot);
            string[] migrationFiles = migrationsFolderExists
                ? Directory.GetFiles(migrationsRoot, "*.sql", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];

            bool hasSqlTables = SqlTablesBootstrap.FolderExists;
            if (!hasSqlTables && !migrationsFolderExists)
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine(
                    "SqlTables and Migrations folders were not found under "
                    + AppContext.BaseDirectory
                    + "; skipping database schema bootstrap.");
                Colouring.Pop();
                return true;
            }

            if (!migrationsFolderExists)
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine("Migrations folder not found at " + migrationsRoot + "; skipping migration check.");
                Colouring.Pop();
            }

            try
            {
                string connectionString = BuildConnectionString();
                using MySqlConnection connection = OpenConnectionAllowingCreate(connectionString);

                SqlTablesBootstrap.ApplyMissingTables(connection);

                if (!migrationsFolderExists)
                    return true;

                if (migrationFiles.Length == 0)
                    return true;

                EnsureTrackingTable(connection);
                HashSet<string> applied = LoadAppliedNames(connection);

                List<string> pending = [];
                foreach (string path in migrationFiles)
                {
                    string name = Path.GetFileName(path);
                    if (!applied.Contains(name))
                        pending.Add(path);
                }

                if (pending.Count == 0)
                {
                    Console.WriteLine("Database migrations are up to date.");
                    return true;
                }

                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unapplied database migrations ({0}):",
                        pending.Count));
                foreach (string path in pending)
                    Console.WriteLine("  " + Path.GetFileName(path));
                Colouring.Pop();

                Colouring.Push(ConsoleColor.Red);
                Console.Write("Apply these migrations now? (Y/N) ");
                Colouring.Pop();

                string? answer = Console.ReadLine();
                if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                {
                    Colouring.Push(ConsoleColor.Yellow);
                    Console.WriteLine("Skipped migrations. ZoneEngine_New will continue without applying them.");
                    Colouring.Pop();
                    return true;
                }

                foreach (string path in pending)
                {
                    string name = Path.GetFileName(path);
                    Colouring.Push(ConsoleColor.Green);
                    Console.Write("Applying " + name + " ... ");
                    Colouring.Pop();

                    // MySQL DDL auto-commits; do not wrap migration scripts in a transaction.
                    string sql = File.ReadAllText(path);
                    using (MySqlCommand command = new MySqlCommand(sql, connection))
                    {
                        command.CommandTimeout = 120;
                        command.ExecuteNonQuery();
                    }

                    using (MySqlCommand mark = new MySqlCommand(
                               "INSERT INTO `schema_migrations` (`MigrationName`, `AppliedAtUtc`) "
                               + "VALUES (@Name, UTC_TIMESTAMP(6))",
                               connection))
                    {
                        mark.Parameters.AddWithValue("@Name", name);
                        mark.ExecuteNonQuery();
                    }

                    Colouring.Push(ConsoleColor.Green);
                    Console.WriteLine("OK");
                    Colouring.Pop();
                }

                return true;
            }
            catch (Exception exception)
            {
                Colouring.Push(ConsoleColor.Red);
                Console.WriteLine("Database migration failed: " + exception.Message);
                Colouring.Pop();
                return false;
            }
        }

        private static MySqlConnection OpenConnectionAllowingCreate(string connectionString)
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch (MySqlException exception) when (
                exception.Number == UnknownDatabaseErrorNumber
                || exception.Number == AccessDeniedDatabaseErrorNumber)
            {
                connection.Dispose();
                return CreateMissingDatabaseAndOpen(connectionString, exception);
            }
        }

        private static MySqlConnection CreateMissingDatabaseAndOpen(
            string connectionString,
            MySqlException openFailure)
        {
            var appBuilder = new MySqlConnectionStringBuilder(connectionString);
            string databaseName = RequireDatabaseName(connectionString);
            string appUser = string.IsNullOrWhiteSpace(appBuilder.UserID) ? "<unknown>" : appBuilder.UserID;

            Colouring.Push(ConsoleColor.Yellow);
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Database '{0}' is missing or not accessible to '{1}' ({2}).",
                    databaseName,
                    appUser,
                    openFailure.Message));
            Colouring.Pop();

            Colouring.Push(ConsoleColor.Red);
            Console.Write("Create the database and grant access now? (Y/N) ");
            Colouring.Pop();

            string? answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                PrintManualDatabaseSetupSql(databaseName, appUser);
                throw new InvalidOperationException(
                    "Database '" + databaseName + "' does not exist and creation was declined.");
            }

            try
            {
                // Same account may already have CREATE privilege.
                CreateDatabaseAndGrant(connectionString, databaseName, appUser);
            }
            catch (MySqlException createFailure) when (
                createFailure.Number == AccessDeniedDatabaseErrorNumber
                || createFailure.Number == AccessDeniedErrorNumber
                || createFailure.Number == 1227)
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine(
                    "'" + appUser + "' cannot create databases. A privileged MySQL login is required once.");
                Colouring.Pop();

                string privilegedConnectionString = PromptPrivilegedConnectionString(connectionString);
                CreateDatabaseAndGrant(privilegedConnectionString, databaseName, appUser);
            }

            Colouring.Push(ConsoleColor.Green);
            Console.WriteLine("Created database '" + databaseName + "' and granted access to '" + appUser + "'.");
            Colouring.Pop();

            MySqlConnection created = new MySqlConnection(connectionString);
            created.Open();
            return created;
        }

        private static string PromptPrivilegedConnectionString(string appConnectionString)
        {
            var appBuilder = new MySqlConnectionStringBuilder(appConnectionString);

            Console.Write("Privileged MySQL user [root]: ");
            string? privilegedUser = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(privilegedUser))
                privilegedUser = "root";

            Console.Write("Password for '" + privilegedUser + "': ");
            string privilegedPassword = ReadHiddenConsoleLine();

            var privilegedBuilder = new MySqlConnectionStringBuilder(appConnectionString)
            {
                UserID = privilegedUser.Trim(),
                Password = privilegedPassword,
                Database = string.Empty,
                AllowUserVariables = true,
                AllowLoadLocalInfile = false,
                DefaultCommandTimeout = 600
            };

            // Keep the same server/port as the app connection.
            if (!string.IsNullOrWhiteSpace(appBuilder.Server))
                privilegedBuilder.Server = appBuilder.Server;
            privilegedBuilder.Port = appBuilder.Port;

            return privilegedBuilder.ConnectionString;
        }

        private static void CreateDatabaseAndGrant(
            string adminConnectionString,
            string databaseName,
            string appUser)
        {
            if (!IsSafeDatabaseName(databaseName))
                throw new InvalidOperationException(
                    "Refusing to create database with an unsafe name: '" + databaseName + "'.");

            if (appUser == "<unknown>" || !IsSafeMysqlAccountName(appUser))
                throw new InvalidOperationException(
                    "Refusing to GRANT to an unsafe MySQL user name: '" + appUser + "'.");

            var adminBuilder = new MySqlConnectionStringBuilder(adminConnectionString)
            {
                Database = string.Empty,
                AllowUserVariables = true,
                AllowLoadLocalInfile = false,
                DefaultCommandTimeout = 600
            };

            using MySqlConnection adminConnection = new MySqlConnection(adminBuilder.ConnectionString);
            adminConnection.Open();

            string createSql =
                "CREATE DATABASE IF NOT EXISTS `" + EscapeIdentifier(databaseName) + "` "
                + "CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
            using (MySqlCommand createCommand = new MySqlCommand(createSql, adminConnection))
                createCommand.ExecuteNonQuery();

            // Cover both common account hosts used by local MySQL installs.
            foreach (string host in new[] { "%", "localhost", "127.0.0.1" })
            {
                string grantSql =
                    "GRANT ALL PRIVILEGES ON `" + EscapeIdentifier(databaseName) + "`.* TO '"
                    + EscapeMysqlStringLiteral(appUser) + "'@'" + host + "'";
                try
                {
                    using MySqlCommand grantCommand = new MySqlCommand(grantSql, adminConnection);
                    grantCommand.ExecuteNonQuery();
                }
                catch (MySqlException)
                {
                    // Account host may not exist; try the next host variant.
                }
            }

            using (MySqlCommand flushCommand = new MySqlCommand("FLUSH PRIVILEGES", adminConnection))
                flushCommand.ExecuteNonQuery();
        }

        private static void PrintManualDatabaseSetupSql(string databaseName, string appUser)
        {
            Colouring.Push(ConsoleColor.Yellow);
            Console.WriteLine("Run this once as a MySQL admin (e.g. root), then restart ZoneEngine_New:");
            Console.WriteLine(
                "  CREATE DATABASE `" + databaseName + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            Console.WriteLine(
                "  GRANT ALL PRIVILEGES ON `" + databaseName + "`.* TO '" + appUser + "'@'%';");
            Console.WriteLine(
                "  GRANT ALL PRIVILEGES ON `" + databaseName + "`.* TO '" + appUser + "'@'localhost';");
            Console.WriteLine("  FLUSH PRIVILEGES;");
            Colouring.Pop();
        }

        private static string ReadHiddenConsoleLine()
        {
            var buffer = new System.Text.StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return buffer.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length == 0)
                        continue;

                    buffer.Length--;
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                    buffer.Append(key.KeyChar);
            }
        }

        private static bool IsSafeMysqlAccountName(string name)
        {
            return IsSafeDatabaseName(name);
        }

        private static string EscapeMysqlStringLiteral(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal)
                .Replace("\\", "\\\\", StringComparison.Ordinal);
        }

        private static string RequireDatabaseName(string connectionString)
        {
            string? databaseName = new MySqlConnectionStringBuilder(connectionString).Database;
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("MysqlConnection does not specify a Database.");

            return databaseName;
        }

        private static bool IsSafeDatabaseName(string name)
        {
            if (name.Length == 0)
                return false;

            foreach (char c in name)
            {
                if (char.IsAsciiLetterOrDigit(c) || c == '_')
                    continue;

                return false;
            }

            return true;
        }

        private static string EscapeIdentifier(string name)
        {
            return name.Replace("`", "``", StringComparison.Ordinal);
        }

        private static string BuildConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder(MySqlConnectionSettings.GetRequiredConnectionString())
            {
                AllowUserVariables = true,
                AllowLoadLocalInfile = false,
                DefaultCommandTimeout = 600
            };

            return builder.ConnectionString;
        }

        private static void EnsureTrackingTable(MySqlConnection connection)
        {
            using MySqlCommand command = new MySqlCommand(EnsureTrackingTableSql, connection);
            command.ExecuteNonQuery();
        }

        private static HashSet<string> LoadAppliedNames(MySqlConnection connection)
        {
            HashSet<string> applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using MySqlCommand command = new MySqlCommand(
                "SELECT `MigrationName` FROM `schema_migrations`",
                connection);
            using MySqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
                applied.Add(reader.GetString(0));

            return applied;
        }
    }
}
