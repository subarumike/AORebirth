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
    /// Applies pending SQL files from the runtime Migrations folder, tracked in schema_migrations.
    /// </summary>
    internal static class DatabaseMigrationRunner
    {
        private const string MigrationsFolderName = "Migrations";

        private const string EnsureTrackingTableSql =
            "CREATE TABLE IF NOT EXISTS `schema_migrations` ("
            + "`MigrationName` VARCHAR(255) NOT NULL,"
            + "`AppliedAtUtc` DATETIME(6) NOT NULL,"
            + "PRIMARY KEY (`MigrationName`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci";

        public static bool TryApplyPendingMigrations()
        {
            string migrationsRoot = Path.Combine(AppContext.BaseDirectory, MigrationsFolderName);
            if (!Directory.Exists(migrationsRoot))
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine("Migrations folder not found at " + migrationsRoot + "; skipping migration check.");
                Colouring.Pop();
                return true;
            }

            string[] migrationFiles = Directory.GetFiles(migrationsRoot, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (migrationFiles.Length == 0)
                return true;

            try
            {
                string connectionString = BuildConnectionString();
                using MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();

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

        private static string BuildConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder(MySqlConnectionSettings.GetRequiredConnectionString())
            {
                AllowUserVariables = true,
                AllowLoadLocalInfile = false
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
