namespace AORebirth.Database.Schema;

public enum SchemaState { SCHEMA_CURRENT, SCHEMA_MIGRATION_REQUIRED, SCHEMA_INCOMPATIBLE, DATABASE_UNREACHABLE }

public sealed record SchemaCheckResult(SchemaState State, string Message, IReadOnlyList<string> PendingMigrations)
{
    public bool IsCurrent => State == SchemaState.SCHEMA_CURRENT;
}

public sealed record ColumnRequirement(string Table, string Column, string DataType, bool? Unsigned = null);
public sealed record SchemaColumn(string Table, string Column, string DataType, string ColumnType);
public sealed record SchemaIndex(string Table, string Name, bool Unique, string Columns);
public sealed record SchemaSnapshot(
    IReadOnlyList<SchemaColumn> Columns,
    IReadOnlyList<SchemaIndex> Indexes,
    IReadOnlyList<string> AppliedMigrations,
    long? NextInstanceId,
    long MaximumInstanceId,
    IReadOnlyDictionary<string, string> TableEngines);

/// <summary>Versioned minimum contract actually consumed by the NewEngine repositories.</summary>
public static class SchemaContract
{
    public static readonly IReadOnlyList<string> MigrationNames = Array.AsReadOnly(new[]
    {
        "20260904_item_instances_from_legacy.sql",
        "20260905_item_instance_id_sequence.sql",
        "20260906_item_instances_source.sql"
    });

    // Other governed components may legitimately record these unrelated migrations.
    public static readonly IReadOnlyList<string> OtherKnownMigrations = Array.AsReadOnly(new[]
    {
        "20260816_account_email_verification_tokens.sql",
        "20260831_account_password_reset_tokens.sql"
    });

    public static readonly IReadOnlyList<ColumnRequirement> Columns = BuildColumns();

    private static IReadOnlyList<ColumnRequirement> BuildColumns()
    {
        var result = new List<ColumnRequirement>();
        void Add(string table, string type, string names, bool? unsigned = null)
        {
            result.AddRange(names.Split(' ').Select(name => new ColumnRequirement(table, name, type, unsigned)));
        }
        Add("characters", "int", "Id Playfield");
        Add("characters", "varchar", "Name FirstName LastName");
        Add("characters", "float", "X Y Z HeadingW HeadingX HeadingY HeadingZ");
        Add("characters", "smallint", "Online");
        Add("stats", "int", "Type Instance StatId StatValue");
        Add("charactersuploadednanos", "int", "CharacterId NanoId");
        Add("itemnames", "int", "Id");
        Add("itemnames", "varchar", "Name");
        Add("item_instances", "int", "InstanceId ContainerType ContainerInstance ContainerPlacement ItemType LowId HighId Quality StackCount", false);
        Add("item_instances", "tinyint", "Source", true);
        Add("item_instance_id_sequence", "tinyint", "Id", false);
        Add("item_instance_id_sequence", "int", "NextInstanceId", false);
        Add("schema_migrations", "varchar", "MigrationName");
        Add("schema_migrations", "datetime", "AppliedAtUtc");
        return result.AsReadOnly();
    }
}
