namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using MySqlConnector;

    using Utility;

    /// <summary>
    /// Creates missing governed tables from the runtime SqlTables folder.
    /// </summary>
    internal static class SqlTablesBootstrap
    {
        private const string SqlTablesFolderName = "SqlTables";

        private static readonly Regex AddColumnRegex = new Regex(
            @"ADD\s+COLUMN\s+`?([A-Za-z0-9_]+)`?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex CreateTableRegex = new Regex(
            @"^\s*CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?`?(?<name>[A-Za-z0-9_]+)`?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled);

        public static string FolderPath => Path.Combine(AppContext.BaseDirectory, SqlTablesFolderName);

        public static bool FolderExists => Directory.Exists(FolderPath);

        public static void ApplyMissingTables(MySqlConnection connection)
        {
            if (!FolderExists)
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine("SqlTables folder not found at " + FolderPath + "; skipping table bootstrap.");
                Colouring.Pop();
                return;
            }

            string[] sqlFiles = Directory.GetFiles(FolderPath, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (sqlFiles.Length == 0)
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine("SqlTables folder is empty; skipping table bootstrap.");
                Colouring.Pop();
                return;
            }

            List<string> pending = [];
            foreach (string sqlFile in sqlFiles)
            {
                string scriptName = Path.GetFileNameWithoutExtension(sqlFile);
                if (IsAlterScript(scriptName))
                {
                    if (!IsAlterMigrationApplied(connection, scriptName, sqlFile))
                        pending.Add(sqlFile);
                    continue;
                }

                if (!IsCreateScriptApplied(connection, sqlFile, scriptName))
                    pending.Add(sqlFile);
            }

            if (pending.Count == 0)
            {
                Console.WriteLine("SQL tables are complete.");
                return;
            }

            Colouring.Push(ConsoleColor.Yellow);
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SQL tables are not complete ({0} missing):",
                    pending.Count));
            foreach (string sqlFile in pending)
                Console.WriteLine("  " + Path.GetFileName(sqlFile));
            Colouring.Pop();

            Colouring.Push(ConsoleColor.Red);
            Console.Write("Create the missing SQL tables now? (Y/N) ");
            Colouring.Pop();

            string? answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Colouring.Push(ConsoleColor.Yellow);
                Console.WriteLine("Skipped SQL table bootstrap. ZoneEngine_New will continue without creating them.");
                Colouring.Pop();
                return;
            }

            foreach (string sqlFile in pending)
            {
                string name = Path.GetFileNameWithoutExtension(sqlFile);
                Colouring.Push(ConsoleColor.Green);
                Console.Write("Creating " + name + " ... ");
                Colouring.Pop();

                ExecuteSqlFile(connection, sqlFile);

                Colouring.Push(ConsoleColor.Green);
                Console.WriteLine("OK");
                Colouring.Pop();
            }
        }

        private static bool IsCreateScriptApplied(
            MySqlConnection connection,
            string sqlFilePath,
            string scriptName)
        {
            string[] createdTables = ParseCreateTableNames(File.ReadAllText(sqlFilePath));
            if (createdTables.Length == 0)
                return TableExists(connection, scriptName);

            foreach (string tableName in createdTables)
            {
                if (!TableExists(connection, tableName))
                    return false;
            }

            return true;
        }

        private static string[] ParseCreateTableNames(string sql)
        {
            MatchCollection matches = CreateTableRegex.Matches(sql);
            List<string> names = new List<string>(matches.Count);
            foreach (Match match in matches)
            {
                string name = match.Groups["name"].Value;
                if (name.Length == 0)
                    continue;

                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    names.Add(name);
            }

            return names.ToArray();
        }

        private static void ExecuteSqlFile(MySqlConnection connection, string sqlFilePath)
        {
            string[] lines = File.ReadAllLines(sqlFilePath);
            int counter = 0;
            while (counter < lines.Length)
            {
                string statement = ReadSqlStatement(lines, ref counter);
                if (string.IsNullOrWhiteSpace(statement))
                    continue;

                // Multi-table scripts (e.g. aorebirth_identity.sql) may already have some tables.
                if (TryGetCreateTableName(statement, out string tableName) && TableExists(connection, tableName))
                    continue;

                using MySqlCommand command = new MySqlCommand(statement, connection);
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }
        }

        private static bool TryGetCreateTableName(string statement, out string tableName)
        {
            Match match = CreateTableRegex.Match(statement);
            if (!match.Success)
            {
                tableName = string.Empty;
                return false;
            }

            tableName = match.Groups["name"].Value;
            return tableName.Length > 0;
        }

        private static string ReadSqlStatement(string[] lines, ref int counter)
        {
            var statement = new StringBuilder();
            while (counter < lines.Length)
            {
                string line = lines[counter];
                counter++;
                if (line.Trim().Length == 0)
                    continue;

                if (statement.Length > 0)
                    statement.Append('\n');

                statement.Append(line);
                if (line.TrimEnd().EndsWith(";", StringComparison.Ordinal))
                    break;
            }

            return statement.ToString();
        }

        private static bool IsAlterScript(string scriptName)
        {
            return scriptName.EndsWith("_alter", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAlterMigrationApplied(
            MySqlConnection connection,
            string alterScriptName,
            string sqlFilePath)
        {
            const string alterSuffix = "_alter";
            string baseTableName = alterScriptName.Substring(0, alterScriptName.Length - alterSuffix.Length);

            // Base CREATE scripts already include the newer columns; alter is only for upgrades.
            if (!TableExists(connection, baseTableName))
                return true;

            string[] columnsToAdd = ParseAlterAddColumnNames(File.ReadAllText(sqlFilePath));
            if (columnsToAdd.Length == 0)
                return true;

            HashSet<string> existingColumns = LoadColumnNames(connection, baseTableName);
            foreach (string columnName in columnsToAdd)
            {
                if (!existingColumns.Contains(columnName))
                    return false;
            }

            return true;
        }

        private static string[] ParseAlterAddColumnNames(string alterSql)
        {
            MatchCollection matches = AddColumnRegex.Matches(alterSql);
            string[] columnNames = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                columnNames[i] = matches[i].Groups[1].Value;

            return columnNames;
        }

        private static bool TableExists(MySqlConnection connection, string tableName)
        {
            using MySqlCommand command = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables "
                + "WHERE table_schema = DATABASE() AND table_name = @tableName",
                connection);
            command.Parameters.AddWithValue("@tableName", tableName);
            object? result = command.ExecuteScalar();
            return Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
        }

        private static HashSet<string> LoadColumnNames(MySqlConnection connection, string tableName)
        {
            HashSet<string> columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using MySqlCommand command = new MySqlCommand(
                "SELECT column_name FROM information_schema.columns "
                + "WHERE table_schema = DATABASE() AND table_name = @tableName",
                connection);
            command.Parameters.AddWithValue("@tableName", tableName);
            using MySqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(0));

            return columns;
        }
    }
}
