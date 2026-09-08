namespace AORebirth.Database.Migrations;

using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AORebirth.Database.Schema;
using MySqlConnector;

/// <summary>Operator-only writer. No server project references this assembly.</summary>
public static class MigrationCommand
{
    public static int Run(string[] args, Func<string?> connectionProvider, TextWriter output)
    {
        if (!TryParse(args, out string operation, out string? expectedDatabase))
        {
            output.WriteLine("USAGE: DatabaseMigrationTool status|validate|plan OR migrate --expected-database NAME --acknowledge-backup --acknowledge-engines-stopped. Connection comes only from AO_REBIRTH_MIGRATION_CONNECTION.");
            return 64;
        }
        try
        {
            string? raw = connectionProvider();
            if (string.IsNullOrWhiteSpace(raw)) { output.WriteLine("DATABASE_UNREACHABLE: AO_REBIRTH_MIGRATION_CONNECTION is required."); return 3; }
            var builder = new MySqlConnectionStringBuilder(raw) { AllowLoadLocalInfile = false, AllowUserVariables = true, ConnectionTimeout = 5, DefaultCommandTimeout = 120 };
            if (string.IsNullOrWhiteSpace(builder.Database) || (operation == "migrate" && builder.Database != expectedDatabase))
            { output.WriteLine("REFUSED: exact database acknowledgement does not match the selected database."); return 64; }
            var before = DatabaseSchemaReadiness.Check(builder.ConnectionString);
            WriteStatus(output, before);
            if (operation is "status" or "validate") return before.IsCurrent ? 0 : 2;
            foreach (string name in SchemaContract.MigrationNames)
                output.WriteLine("MIGRATION=" + name + " SHA256=" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReadScript(name)))).ToLowerInvariant());
            output.WriteLine("PLAN: Stop all writers, verify recoverable backup, apply in listed order. Legacy item tables retained. No automatic reverse migration; legacy inventory becomes stale after NewEngine writes.");
            if (operation == "plan") return before.State is SchemaState.DATABASE_UNREACHABLE or SchemaState.SCHEMA_INCOMPATIBLE ? 2 : 0;
            if (before.IsCurrent) return 0;
            if (before.State != SchemaState.SCHEMA_MIGRATION_REQUIRED) return 2;

            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var acquire = new MySqlCommand("SELECT GET_LOCK(CONCAT(DATABASE(),':aorebirth-schema'),0)", connection);
            if (Convert.ToInt32(acquire.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
            { output.WriteLine("REFUSED: another migration operator holds the database migration lock."); return 2; }
            try { Apply(connection, output); }
            finally
            {
                using var release = new MySqlCommand("SELECT RELEASE_LOCK(CONCAT(DATABASE(),':aorebirth-schema'))", connection);
                release.ExecuteScalar();
            }
            var after = DatabaseSchemaReadiness.Check(builder.ConnectionString);
            WriteStatus(output, after);
            return after.IsCurrent ? 0 : 2;
        }
        catch (MigrationRefusedException exception)
        {
            output.WriteLine("MIGRATION_REFUSED: " + exception.Message);
            return 2;
        }
        catch (MySqlException exception)
        {
            // Server messages can contain user/host/SQL values. Emit only the stable error number.
            output.WriteLine("MIGRATION_FAILED: MySQL error " + exception.Number.ToString(CultureInfo.InvariantCulture) + "; stop and inspect status. The migration ledger records completed steps only. No automatic retry or reverse migration was attempted.");
            return 3;
        }
        catch (Exception)
        {
            output.WriteLine("MIGRATION_FAILED: invalid configuration or governed migration asset. No credentials are logged; inspect the approved release and run status.");
            return 3;
        }
    }

    public static bool TryParse(string[] args, out string operation, out string? expectedDatabase)
    {
        operation = args.Length == 0 ? string.Empty : args[0];
        expectedDatabase = null;
        if (args.Length == 1 && operation is "status" or "validate" or "plan") return true;
        if (args.Length == 5 && operation == "migrate" && args[1] == "--expected-database" && IsIdentifier(args[2]) && args[3] == "--acknowledge-backup" && args[4] == "--acknowledge-engines-stopped")
        { expectedDatabase = args[2]; return true; }
        return false;
    }

    public static string ReadScript(string name)
    {
        if (!SchemaContract.MigrationNames.Contains(name, StringComparer.Ordinal)) throw new MigrationRefusedException("Unknown migration identity.");
        Assembly assembly = typeof(MigrationCommand).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(candidate => candidate.EndsWith("." + name, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        // Normalize checkout line endings so the plan hash is identical on Windows and Linux.
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void Apply(MySqlConnection connection, TextWriter output)
    {
        SchemaSnapshot snapshot = DatabaseSchemaReadiness.ReadSnapshot(connection);
        SchemaCheckResult status = DatabaseSchemaReadiness.Evaluate(snapshot);
        if (status.IsCurrent) return;
        if (status.State != SchemaState.SCHEMA_MIGRATION_REQUIRED) throw new MigrationRefusedException(status.Message);
        var baseline = SchemaContract.Columns.Where(c => !SchemaContract.MigrationOwnedTables.Contains(c.Table, StringComparer.Ordinal));
        if (baseline.Any(required => !snapshot.Columns.Any(actual => Equal(actual.Table, required.Table) && Equal(actual.Column, required.Column))))
            throw new MigrationRefusedException("Governed baseline tables/columns are absent. Provision or restore the baseline separately; this tool never bootstraps unrelated SQL tables.");
        if (!snapshot.Indexes.Any(index => Equal(index.Table, "stats") && index.Unique && Equal(index.Columns, "Type,Instance,StatId")))
            throw new MigrationRefusedException("Baseline stats unique key is absent; reconcile the baseline separately.");
        bool firstPending = !snapshot.AppliedMigrations.Contains(SchemaContract.MigrationNames[0], StringComparer.Ordinal);
        if (firstPending)
        {
            if (!snapshot.TableEngines.ContainsKey("items") || !snapshot.TableEngines.ContainsKey("instanceditems"))
                throw new MigrationRefusedException("Legacy items/instanceditems source tables are absent; review baseline and inventory provenance.");
            if (snapshot.TableEngines.ContainsKey("item_instances") && Scalar(connection, "SELECT COUNT(*) FROM item_instances") != 0)
                throw new MigrationRefusedException("Untracked item_instances is nonempty. Do not mark or skip it automatically: reconcile the complete legacy import and backup before proceeding.");
        }
        Execute(connection, "CREATE TABLE IF NOT EXISTS schema_migrations (MigrationName VARCHAR(255) NOT NULL, AppliedAtUtc DATETIME(6) NOT NULL, PRIMARY KEY (MigrationName)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        foreach (string name in SchemaContract.MigrationNames)
        {
            if (snapshot.AppliedMigrations.Contains(name, StringComparer.Ordinal)) continue;
            string sql = ReadScript(name);
            if (name == SchemaContract.MigrationNames[0])
            {
                const string end = "ENGINE=InnoDB;";
                int split = sql.IndexOf(end, StringComparison.Ordinal) + end.Length;
                if (split < end.Length) throw new MigrationRefusedException("The governed import DDL boundary changed; review the migration executor.");
                Execute(connection, sql[..split]);
                // MySQL DDL commits independently. Both inventory copies and their ledger row
                // share a transaction, so a failed second copy cannot leave a skipped partial import.
                using var transaction = connection.BeginTransaction();
                Execute(connection, sql[split..], transaction);
                Mark(connection, name, transaction);
                transaction.Commit();
            }
            else
            {
                Execute(connection, sql);
                Mark(connection, name);
            }
            output.WriteLine("APPLIED=" + name);
        }
    }

    private static bool Equal(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool IsIdentifier(string value) => value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    private static void WriteStatus(TextWriter output, SchemaCheckResult result) => output.WriteLine(result.State + ": " + result.Message);
    private static long Scalar(MySqlConnection connection, string sql)
    { using var command = new MySqlCommand(sql, connection); return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture); }
    private static void Execute(MySqlConnection connection, string sql, MySqlTransaction? transaction = null)
    { using var command = new MySqlCommand(sql, connection, transaction); command.CommandTimeout = 120; command.ExecuteNonQuery(); }
    private static void Mark(MySqlConnection connection, string name, MySqlTransaction? transaction = null)
    {
        using var command = new MySqlCommand("INSERT INTO schema_migrations (MigrationName,AppliedAtUtc) VALUES (@name,UTC_TIMESTAMP(6))", connection, transaction);
        command.Parameters.AddWithValue("@name", name);
        command.ExecuteNonQuery();
    }
    private sealed class MigrationRefusedException(string message) : Exception(message);
}
