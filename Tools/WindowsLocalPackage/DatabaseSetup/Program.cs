using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using AORebirth.Database.Migrations;
using AORebirth.Database.Schema;
using MySqlConnector;

namespace AORebirth.WindowsLocalPackage;

internal static class Program
{
    private const string Database = "cellao_codex_clean";
    private const string RuntimeUser = "aorebirth_local";
    private const string DefaultPassword = "aorebirth";

    private static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new SetupRefusedException("PLATFORM_REFUSED: this package setup requires Windows.");
            if (args.Length != 5 || (args[0] != "prepare" && args[0] != "set-passwords") || args[1] != "--root" || args[3] != "--port"
                || !uint.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out uint port)
                || port is < 1024 or > 65535)
            {
                Console.Error.WriteLine("USAGE: DatabaseSetup prepare|set-passwords --root PACKAGE_ROOT --port DEDICATED_PORT");
                return 64;
            }

            string root = Path.GetFullPath(args[2]);
            string expectedData = Path.Combine(root, "data", "mysql");
            RequireOrdinaryDirectory(expectedData);
            MySqlConnectionStringBuilder admin = ReadConnection("AO_REBIRTH_MIGRATION_CONNECTION", port, "root");
            MySqlConnectionStringBuilder runtime = ReadConnection("AO_REBIRTH_MYSQL_CONNECTION", port, RuntimeUser);
            string adminPassword = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_ADMIN_PASSWORD") ?? string.Empty;
            if ((adminPassword != DefaultPassword && adminPassword.Length < 24)
                || (runtime.Password != DefaultPassword && runtime.Password.Length < 24))
                throw new SetupRefusedException("SECRET_CONFIGURATION_REQUIRED: local database passwords are missing or invalid.");
            if (args[0] == "prepare" && admin.Password.Length != 0 && admin.Password != adminPassword)
                throw new SetupRefusedException("SECRET_CONFIGURATION_MISMATCH: administrator configuration is inconsistent.");

            var server = new MySqlConnectionStringBuilder(admin.ConnectionString) { Database = string.Empty };
            using var connection = new MySqlConnection(server.ConnectionString);
            connection.Open();
            VerifyBundledServer(connection, expectedData, port);

            if (args[0] == "set-passwords")
            {
                if (!SchemaExists(connection))
                    throw new SetupRefusedException("DATABASE_NOT_READY: start the local database setup before updating its login.");

                // Keep administrator access until both runtime accounts have been updated.
                foreach (string host in new[] { "localhost", "127.0.0.1" })
                {
                    Execute(connection, "ALTER USER 'aorebirth_local'@'" + host + "' IDENTIFIED BY @password", ("@password", runtime.Password));
                }
                Execute(connection, "ALTER USER CURRENT_USER() IDENTIFIED BY @password", ("@password", adminPassword));
                Console.WriteLine("LOCAL_SQL_LOGIN_READY: documented local credentials applied; game data preserved.");
                return 0;
            }

            if (SchemaExists(connection))
            {
                if (admin.Password.Length == 0)
                    throw new SetupRefusedException("PARTIAL_SETUP: an existing schema cannot be initialized with a passwordless administrator. Existing data was preserved.");
                return ValidateExisting(admin.ConnectionString, runtime.ConnectionString);
            }

            using var acquire = new MySqlCommand("SELECT GET_LOCK('aorebirth-windows-local-setup',0)", connection);
            if (Convert.ToInt32(acquire.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                throw new SetupRefusedException("SETUP_BUSY: another setup owns this bundled database instance.");
            try
            {
                // CREATE without IF NOT EXISTS also refuses a schema created by another writer.
                if (SchemaExists(connection))
                    throw new SetupRefusedException("SCHEMA_ALREADY_EXISTS: existing data was preserved; restart setup to validate it.");
                Execute(connection, "CREATE DATABASE `cellao_codex_clean` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci");
                RequireEmptySchema(connection);

                Execute(connection, "ALTER USER CURRENT_USER() IDENTIFIED BY @password", ("@password", adminPassword));
                admin.Password = adminPassword;
                Console.WriteLine("FRESH_DATABASE: isolated empty schema verified; administrator secured.");

                // The existing init command drops its table set. It is reachable only after
                // this invocation created the absent schema and proved it has no objects.
                if (MigrationCommand.Run(["init"], () => admin.ConnectionString, TextWriter.Null) != 0)
                    throw new SetupRefusedException("BOOTSTRAP_FAILED: partial schema preserved. No automatic reset or retry is permitted.");
                BackupFreshBaseline(root, admin, port);
                if (MigrationCommand.Run(["migrate", "--expected-database", Database, "--acknowledge-backup", "--acknowledge-engines-stopped"],
                        () => admin.ConnectionString, TextWriter.Null) != 0)
                    throw new SetupRefusedException("MIGRATION_FAILED: partial schema preserved. Review the current schema before retrying.");

                connection.ChangeDatabase(Database);
                foreach (string name in SchemaContract.OtherKnownMigrations)
                {
                    Execute(connection, ReadResource(name));
                    Execute(connection, "INSERT INTO schema_migrations (MigrationName,AppliedAtUtc) VALUES (@name,UTC_TIMESTAMP(6))", ("@name", name));
                }
                RequireAccountSchema(connection);

                // Runtime and the account broker get row access only, never DDL or user administration.
                foreach (string host in new[] { "localhost", "127.0.0.1" })
                {
                    string account = "'aorebirth_local'@'" + host + "'";
                    Execute(connection, "CREATE USER " + account + " IDENTIFIED BY @password", ("@password", runtime.Password));
                    Execute(connection, "GRANT SELECT,INSERT,UPDATE,DELETE ON `cellao_codex_clean`.* TO " + account);
                }
                int result = ValidateExisting(admin.ConnectionString, runtime.ConnectionString);
                if (result == 0)
                    Console.WriteLine("FIRST_RUN_READY: create the first game account through the packaged Account Broker registration page.");
                return result;
            }
            finally
            {
                using var release = new MySqlCommand("SELECT RELEASE_LOCK('aorebirth-windows-local-setup')", connection);
                release.ExecuteScalar();
            }
        }
        catch (SetupRefusedException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (MySqlException exception)
        {
            Console.Error.WriteLine("DATABASE_SETUP_FAILED: MySQL error " + exception.Number.ToString(CultureInfo.InvariantCulture)
                + ". Existing files and data were preserved; no reset was attempted.");
            return 3;
        }
        catch (Exception)
        {
            // Exceptions can contain connection values, passwords or SQL; never emit them.
            Console.Error.WriteLine("DATABASE_SETUP_FAILED: configuration or packaged resources are invalid. No automatic reset was attempted.");
            return 3;
        }
    }

    private static MySqlConnectionStringBuilder ReadConnection(string variable, uint port, string user)
    {
        string? raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
            throw new SetupRefusedException("DATABASE_ENVIRONMENT_REQUIRED: inherited database configuration is missing.");
        var builder = new MySqlConnectionStringBuilder(raw);
        if (builder.Server != "127.0.0.1" || builder.Port != port || builder.Database != Database || builder.UserID != user)
            throw new SetupRefusedException("DATABASE_TARGET_REFUSED: only the dedicated loopback package database is permitted.");
        builder.ConnectionProtocol = MySqlConnectionProtocol.Sockets;
        builder.AllowLoadLocalInfile = false;
        builder.AllowUserVariables = true;
        builder.Pooling = false;
        builder.ConnectionTimeout = 5;
        builder.DefaultCommandTimeout = 120;
        return builder;
    }

    private static void VerifyBundledServer(MySqlConnection connection, string expectedData, uint port)
    {
        using var command = new MySqlCommand("SELECT @@datadir,@@port", connection);
        using MySqlDataReader reader = command.ExecuteReader();
        if (!reader.Read() || Convert.ToUInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != port
            || !string.Equals(NormalizePath(reader.GetString(0)), NormalizePath(expectedData), StringComparison.OrdinalIgnoreCase))
            throw new SetupRefusedException("DATABASE_INSTANCE_REFUSED: the listener is not using this package's dedicated data directory and port.");
    }

    private static void RequireOrdinaryDirectory(string path)
    {
        for (DirectoryInfo? current = new DirectoryInfo(path); current != null; current = current.Parent)
        {
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new SetupRefusedException("DATABASE_DIRECTORY_REFUSED: the bundled data directory must exist without redirected parent directories.");
        }
    }

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void BackupFreshBaseline(string root, MySqlConnectionStringBuilder admin, uint port)
    {
        string executable = Path.Combine(root, "mysql", "bin", "mysqldump.exe");
        if (!File.Exists(executable) || (File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0)
            throw new SetupRefusedException("BACKUP_TOOL_MISSING: migrations were not attempted.");
        RequireOrdinaryDirectory(Path.GetDirectoryName(executable)!);
        string directory = Path.Combine(root, "data", "backups");
        Directory.CreateDirectory(directory);
        RequireOrdinaryDirectory(directory);
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier user = identity.User
            ?? throw new SetupRefusedException("BACKUP_ACCESS_FAILED: the current Windows user cannot be identified.");
        var permissions = new DirectorySecurity();
        permissions.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        permissions.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(permissions);

        string destination = Path.Combine(directory, "pre-migration-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture) + ".sql");
        string partial = destination + ".partial";
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = root
        };
        foreach (string argument in new[] { "--host=127.0.0.1", "--port=" + port.ToString(CultureInfo.InvariantCulture),
            "--user=root", "--protocol=TCP", "--get-server-public-key", "--single-transaction", "--skip-lock-tables", "--routines", "--events",
            "--triggers", "--set-gtid-purged=OFF", "--no-tablespaces", "--column-statistics=0", "--databases", Database })
            start.ArgumentList.Add(argument);
        start.Environment.Remove("AO_REBIRTH_MIGRATION_CONNECTION");
        start.Environment.Remove("AO_REBIRTH_MYSQL_CONNECTION");
        start.Environment.Remove("AO_REBIRTH_MYSQL_ADMIN_PASSWORD");
        start.Environment["MYSQL_PWD"] = admin.Password;

        using var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new SetupRefusedException("BACKUP_FAILED: the packaged dump tool did not start; migrations were not attempted.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            Task copy = process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
            Task discardErrors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            Task.WhenAll(copy, discardErrors, process.WaitForExitAsync(timeout.Token)).GetAwaiter().GetResult();
            if (process.ExitCode != 0 || output.Length == 0)
                throw new SetupRefusedException("BACKUP_FAILED: the dump did not complete successfully; partial output was retained and migrations were not attempted.");
            output.Flush(flushToDisk: true);
            output.Dispose();
            File.Move(partial, destination);
            Console.WriteLine("BACKUP_COMPLETE: initial database saved in the package's private data/backups directory.");
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000))
                    throw new SetupRefusedException("BACKUP_STOP_FAILED: the dump child did not exit within its shutdown timeout; migrations were not attempted.");
            }
            throw new SetupRefusedException("BACKUP_FAILED: the bounded dump did not complete; partial output was retained and migrations were not attempted.");
        }
    }

    private static bool SchemaExists(MySqlConnection connection)
    {
        using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=@name", connection);
        command.Parameters.AddWithValue("@name", Database);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0;
    }

    private static void RequireEmptySchema(MySqlConnection connection)
    {
        using var command = new MySqlCommand("SELECT (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=@name)"
            + "+(SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=@name)"
            + "+(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=@name)"
            + "+(SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_SCHEMA=@name)", connection);
        command.Parameters.AddWithValue("@name", Database);
        if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
            throw new SetupRefusedException("SCHEMA_NOT_EMPTY: initialization refused. Existing data was preserved.");
    }

    private static int ValidateExisting(string adminConnection, string runtimeConnection)
    {
        SchemaCheckResult status = DatabaseSchemaReadiness.Check(adminConnection, Database);
        if (!status.IsCurrent)
        {
            Console.Error.WriteLine(status.State + ": existing schema was inspected without changes. A separately reviewed migration or repair is required.");
            return 5;
        }
        using var runtime = new MySqlConnection(runtimeConnection);
        runtime.Open();
        RequireAccountSchema(runtime);
        SchemaCheckResult runtimeStatus = DatabaseSchemaReadiness.Check(runtimeConnection, Database);
        if (!runtimeStatus.IsCurrent)
            throw new SetupRefusedException("RUNTIME_DATABASE_ACCESS_FAILED: existing runtime configuration requires review; no grants were changed.");
        Console.WriteLine("SCHEMA_CURRENT: product schema, account schema and runtime read access verified.");
        return 0;
    }

    private static void RequireAccountSchema(MySqlConnection connection)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var query = new MySqlCommand("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()", connection))
        using (MySqlDataReader reader = query.ExecuteReader())
            while (reader.Read())
                columns.Add(reader.GetString(0) + "." + reader.GetString(1));

        // Derive the broker's required columns from the authoritative embedded baseline,
        // rather than maintaining another account schema in package tooling.
        string sql = ReadResource("AccountIdentity.sql");
        MatchCollection tables = Regex.Matches(sql, @"(?ms)^CREATE TABLE `(?<table>[a-z0-9_]+)`\s*\((?<body>.*?)\)\s*ENGINE=");
        if (tables.Count == 0)
            throw new SetupRefusedException("ACCOUNT_SCHEMA_RESOURCE_INVALID: packaged account schema cannot be inspected.");
        foreach (Match table in tables)
            foreach (Match column in Regex.Matches(table.Groups["body"].Value, @"(?m)^\s*`(?<column>[A-Za-z0-9_]+)`\s+"))
                if (!columns.Contains(table.Groups["table"].Value + "." + column.Groups["column"].Value))
                    throw new SetupRefusedException("ACCOUNT_SCHEMA_MIGRATION_REQUIRED: existing account schema was preserved; explicit migration is required.");
    }

    private static string ReadResource(string name)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new SetupRefusedException("PACKAGED_SQL_MISSING: rebuild the package from accepted source.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Execute(MySqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = new MySqlCommand(sql, connection) { CommandTimeout = 120 };
        foreach ((string name, object value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private sealed class SetupRefusedException(string message) : Exception(message);
}
