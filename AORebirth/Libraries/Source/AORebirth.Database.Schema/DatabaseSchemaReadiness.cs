namespace AORebirth.Database.Schema;

using System.Globalization;
using MySqlConnector;

/// <summary>Read-only database boundary. Every command is SELECT; this assembly contains no schema writer.</summary>
public static class DatabaseSchemaReadiness
{
    public static SchemaCheckResult Check(string connectionString, string? expectedDatabase = null)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                AllowLoadLocalInfile = false,
                ConnectionTimeout = 5,
                DefaultCommandTimeout = 15
            };
            if (string.IsNullOrWhiteSpace(builder.Database))
                return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Database selection is missing; configure AO_REBIRTH_MYSQL_CONNECTION.");
            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            if (expectedDatabase != null)
            {
                using var selected = new MySqlCommand("SELECT DATABASE()", connection);
                if (!string.Equals(Convert.ToString(selected.ExecuteScalar(), CultureInfo.InvariantCulture), expectedDatabase, StringComparison.Ordinal))
                    return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Connected database does not match AO_REBIRTH_EXPECTED_DATABASE; no schema operation was attempted.");
            }
            return Evaluate(ReadSnapshot(connection));
        }
        catch (MySqlException exception) when (exception.Number == 1049)
        {
            return Failure(SchemaState.SCHEMA_MIGRATION_REQUIRED, "Selected database is absent; provision the governed baseline separately, then run DatabaseMigrationTool plan.");
        }
        catch (MySqlException exception) when (exception.Number is 1142 or 1143)
        {
            return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Schema metadata or required table reads are denied; review the runtime account SELECT grants.");
        }
        catch (MySqlException)
        {
            return Failure(SchemaState.DATABASE_UNREACHABLE, "Database connection or schema reads failed; verify endpoint, credentials and availability. No schema operation was attempted.");
        }
        catch (ArgumentException)
        {
            return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Database configuration is invalid; review AO_REBIRTH_MYSQL_CONNECTION without logging its value.");
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Schema metadata or item-instance values do not satisfy the required types; reconcile the schema before startup.");
        }
    }

    public static SchemaSnapshot ReadSnapshot(MySqlConnection connection)
    {
        var columns = new List<SchemaColumn>();
        using (var command = new MySqlCommand("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME,ORDINAL_POSITION", connection))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) columns.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

        var indexes = new List<SchemaIndex>();
        using (var command = new MySqlCommand("SELECT TABLE_NAME,INDEX_NAME,NON_UNIQUE,GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() GROUP BY TABLE_NAME,INDEX_NAME,NON_UNIQUE ORDER BY TABLE_NAME,INDEX_NAME", connection))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) indexes.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2) == 0, reader.GetString(3)));

        var engines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var command = new MySqlCommand("SELECT TABLE_NAME,COALESCE(ENGINE,'VIEW') FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME", connection))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) engines.Add(reader.GetString(0), reader.GetString(1));

        bool Has(string table, string column) => columns.Any(c => Equal(c.Table, table) && Equal(c.Column, column));
        var applied = new List<string>();
        if (Has("schema_migrations", "MigrationName"))
        {
            using var command = new MySqlCommand("SELECT MigrationName FROM schema_migrations ORDER BY MigrationName", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read()) applied.Add(reader.GetString(0));
        }
        long? next = null;
        if (Has("item_instance_id_sequence", "Id") && Has("item_instance_id_sequence", "NextInstanceId"))
        {
            using var command = new MySqlCommand("SELECT NextInstanceId FROM item_instance_id_sequence WHERE Id=1", connection);
            object? value = command.ExecuteScalar();
            if (value is not null && value is not DBNull) next = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        long maximum = 0;
        if (Has("item_instances", "InstanceId"))
        {
            using var command = new MySqlCommand("SELECT COALESCE(MAX(InstanceId),0) FROM item_instances", connection);
            maximum = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        return new(columns, indexes, applied, next, maximum, engines);
    }

    public static SchemaCheckResult Evaluate(SchemaSnapshot snapshot)
    {
        var pending = SchemaContract.MigrationNames.Where(name => !snapshot.AppliedMigrations.Contains(name, StringComparer.Ordinal)).ToArray();
        var unknown = snapshot.AppliedMigrations.Except(SchemaContract.MigrationNames.Concat(SchemaContract.OtherKnownMigrations), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length != 0)
            return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Unrecognized schema migration identity; use the matching application release or review the ledger.", pending);
        bool missingSeen = false;
        foreach (string name in SchemaContract.MigrationNames)
        {
            if (pending.Contains(name)) missingSeen = true;
            else if (missingSeen) return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Migration ledger is out of order; operator reconciliation is required.", pending);
        }
        var missing = new List<string>();
        foreach (var required in SchemaContract.Columns)
        {
            var actual = snapshot.Columns.FirstOrDefault(c => Equal(c.Table, required.Table) && Equal(c.Column, required.Column));
            if (actual is null) { missing.Add(required.Table + "." + required.Column); continue; }
            if (!Equal(actual.DataType, required.DataType) || (required.Unsigned.HasValue && actual.ColumnType.Contains("unsigned", StringComparison.OrdinalIgnoreCase) != required.Unsigned.Value))
                return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Incompatible column: " + required.Table + "." + required.Column + "; review the governed schema before migration.", pending);
        }
        foreach (string table in new[] { "characters", "stats", "charactersuploadednanos", "charactersactivenanos", "missionrewardledger" }.Concat(SchemaContract.MigrationOwnedTables))
            if (snapshot.TableEngines.TryGetValue(table, out string? engine) && !Equal(engine, "InnoDB"))
                return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Transactional table must use InnoDB: " + table + ".", pending);

        if (pending.Length > 0 || missing.Count > 0)
            return Failure(SchemaState.SCHEMA_MIGRATION_REQUIRED, "Run DatabaseMigrationTool plan, review the backup and shutdown prerequisites, then explicitly migrate. Missing columns: " + string.Join(", ", missing) + ". Pending migrations: " + string.Join(", ", pending) + ".", pending);

        bool Unique(string table, string fields) => snapshot.Indexes.Any(i => Equal(i.Table, table) && i.Unique && Equal(i.Columns, fields));
        foreach (var required in SchemaContract.UniqueKeys)
            if (!Unique(required.Item1, required.Item2))
                return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Required unique key is absent: " + required.Item1 + "(" + required.Item2 + ").");
        if (snapshot.NextInstanceId is null or <= 0 || snapshot.NextInstanceId <= snapshot.MaximumInstanceId || snapshot.NextInstanceId > int.MaxValue - 10000)
            return Failure(SchemaState.SCHEMA_INCOMPATIBLE, "Item instance sequence is missing, behind stored items, or exhausted; an operator must reconcile it while engines are stopped.");
        return new(SchemaState.SCHEMA_CURRENT, "Required schema, migration ledger and item-instance allocation state are current; no schema writes were performed.", Array.Empty<string>());
    }

    private static bool Equal(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static SchemaCheckResult Failure(SchemaState state, string message, IReadOnlyList<string>? pending = null) => new(state, message, pending ?? Array.Empty<string>());
}
