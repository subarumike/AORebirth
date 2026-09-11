using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AORebirth.Database.Migrations;
using AORebirth.Database.Schema;
using MySqlConnector;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Logging;

bool connected = args.Length == 5 && args[0] == "--run-connected" && args[3] == "--login-engine" && File.Exists(args[4]);
if ((!connected && (args.Length != 3 || args[0] != "--run-disposable")) || args[1] != "--engine" || !File.Exists(args[2]))
{
    Console.Error.WriteLine("USAGE: ZoneEngineSchemaValidation --run-disposable --engine <absolute ZoneEngine_New.dll> OR --run-connected --engine <absolute ZoneEngine_New.dll> --login-engine <absolute LoginEngine.exe-or-dll>. Creates its own loopback-only, labeled disposable MySQL. Never reads production connection settings.");
    return 64;
}
try
{
    using var fixture = new DisposableSchemaDatabase();
    fixture.Start();
    using var connection = fixture.Open();
    FixtureSql.CreateBaseline(connection);
    AuthoredMissionSmoke.CreateBaseline(connection);
    if (connected)
    {
        Require(MigrationSmoke.Run(fixture, MigrateArgs(), Console.Out) == 0, "connected-explicit-migration");
        EngineSmoke.Validate(args[2], fixture, SchemaState.SCHEMA_CURRENT);
        ConnectedAcceptanceSmoke.Validate(args[2], args[4], fixture, connection);
        return ConnectedAcceptanceSmoke.HandoffRejected ? 0 : 2;
    }
    FixtureSql.Execute(connection, "INSERT INTO instanceditems (Id,ContainerType,ContainerInstance,ContainerPlacement,Itemtype,LowId,HighId,Quality,MultipleCount) VALUES (42,1001,104,1,0,10,10,1,2); INSERT INTO items (ContainerType,ContainerInstance,ContainerPlacement,LowId,HighId,Quality,MultipleCount) VALUES (1001,104,2,11,11,1,1)");
    string before = FixtureSql.Fingerprint(connection);
    Require(DatabaseSchemaReadiness.Check(fixture.ConnectionString).State == SchemaState.SCHEMA_MIGRATION_REQUIRED, "negative-readiness");
    Require(MigrationSmoke.Run(fixture, new[] { "status" }, TextWriter.Null) == 2, "status-negative");
    Require(MigrationSmoke.Run(fixture, new[] { "validate" }, TextWriter.Null) == 2, "validate-negative");
    Require(MigrationSmoke.Run(fixture, new[] { "plan" }, TextWriter.Null) == 0, "plan-negative");
    Require(before == FixtureSql.Fingerprint(connection), "status-plan-read-only");
    EngineSmoke.Validate(args[2], fixture, SchemaState.SCHEMA_MIGRATION_REQUIRED);
    Require(before == FixtureSql.Fingerprint(connection), "negative-startup-database-unchanged");
    Console.WriteLine("SCHEMA_NEGATIVE_TEST=PASS DATABASE_NEGATIVE_FINGERPRINT_UNCHANGED=PASS");
    EngineSmoke.NegativeLifecycle(args[2], fixture);
    Require(before == FixtureSql.Fingerprint(connection), "negative-normal-startup-database-unchanged");
    Console.WriteLine("SCHEMA_NEGATIVE_NORMAL_STARTUP=PASS DATABASE_NEGATIVE_NORMAL_FINGERPRINT_UNCHANGED=PASS");
    Require(MigrationSmoke.Run(fixture, MigrateArgs(), Console.Out) == 0, "explicit-migration");
    Require(DatabaseSchemaReadiness.Check(fixture.ConnectionString).IsCurrent, "current-readiness");
    Require(FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM item_instances") == 2, "both-item-tables-imported");
    Require(FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=42 AND ContainerType=104 AND ContainerInstance=1001 AND StackCount=2 AND Source=1") == 1, "identity-location-stack-source");
    Require(FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM schema_migrations") == SchemaContract.MigrationNames.Count, "ordered-ledger-recorded");
    string current = FixtureSql.Fingerprint(connection);
    EngineSmoke.Validate(args[2], fixture, SchemaState.SCHEMA_CURRENT);
    Require(current == FixtureSql.Fingerprint(connection), "current-startup-database-unchanged");
    Require(DatabaseSchemaReadiness.Check(fixture.ConnectionString, DisposableSchemaDatabase.DatabaseName + "_mismatch").State == SchemaState.SCHEMA_INCOMPATIBLE, "expected-database-library-mismatch-refused");
    EngineSmoke.ExpectedDatabaseMismatch(args[2], fixture);
    Require(current == FixtureSql.Fingerprint(connection), "expected-database-mismatch-database-unchanged");
    Console.WriteLine("EXPECTED_DATABASE_MISMATCH_REFUSED=PASS DATABASE_TARGET_MISMATCH_FINGERPRINT_UNCHANGED=PASS");
    Require(MigrationSmoke.Run(fixture, new[] { "status" }, TextWriter.Null) == 0, "status-current");
    Require(MigrationSmoke.Run(fixture, new[] { "validate" }, TextWriter.Null) == 0, "validate-current");
    Require(MigrationSmoke.Run(fixture, new[] { "plan" }, TextWriter.Null) == 0, "plan-current");
    Require(MigrationSmoke.Run(fixture, MigrateArgs(), TextWriter.Null) == 0, "migration-rerun");
    Require(current == FixtureSql.Fingerprint(connection), "migration-rerun-no-writes");
    Console.WriteLine("SCHEMA_CURRENT_TEST=PASS EXPLICIT_MIGRATION_TEST=PASS MIGRATION_RERUN=PASS DATABASE_CURRENT_FINGERPRINT_UNCHANGED=PASS");

    FixtureSql.Execute(connection, "ALTER TABLE characters MODIFY Online INT");
    string incompatible = FixtureSql.Fingerprint(connection);
    EngineSmoke.Validate(args[2], fixture, SchemaState.SCHEMA_INCOMPATIBLE);
    Require(MigrationSmoke.Run(fixture, MigrateArgs(), TextWriter.Null) == 2, "incompatible-migration-refused");
    Require(incompatible == FixtureSql.Fingerprint(connection), "incompatible-database-unchanged");
    FixtureSql.Execute(connection, "ALTER TABLE characters MODIFY Online SMALLINT");
    Require(current == FixtureSql.Fingerprint(connection), "incompatible-fixture-restored");
    Console.WriteLine("SCHEMA_INCOMPATIBLE_TEST=PASS INCOMPATIBLE_MIGRATION_REFUSED=PASS DATABASE_INCOMPATIBLE_FINGERPRINT_UNCHANGED=PASS");

    // Drop only our fixture-created target tables inside the newly created disposable database.
    FixtureSql.Execute(connection, "DROP TABLE item_instance_id_sequence; DROP TABLE item_instances; DROP TABLE schema_migrations; UPDATE items SET ContainerPlacement=1");
    string legacy = FixtureSql.TableFingerprint(connection, "items") + FixtureSql.TableFingerprint(connection, "instanceditems");
    Require(MigrationSmoke.Run(fixture, MigrateArgs(), TextWriter.Null) != 0, "conflicting-import-fails");
    Require(FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM item_instances") == 0, "failed-second-copy-rolled-back");
    Require(FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM schema_migrations") == 0, "failed-copy-not-marked");
    Require(legacy == FixtureSql.TableFingerprint(connection, "items") + FixtureSql.TableFingerprint(connection, "instanceditems"), "legacy-data-preserved-on-failure");
    FixtureSql.Execute(connection, "UPDATE items SET ContainerPlacement=2");
    Require(MigrationSmoke.Run(fixture, MigrateArgs(), TextWriter.Null) == 0, "retry-after-operator-conflict-repair");
    Console.WriteLine("COPY_FAILURE_ROLLBACK=PASS LEGACY_SOURCE_PRESERVED=PASS MIGRATION_RETRY_AFTER_REPAIR=PASS");
    SnapshotSmoke.Validate(fixture, connection);
    Console.WriteLine("CHARACTER_SNAPSHOT_ATOMIC_COMMIT=PASS CHARACTER_SNAPSHOT_FAILURE_ROLLBACK=PASS");
    TradeSmoke.Validate(fixture, connection);
    Console.WriteLine("TWO_PARTY_TRADE_ATOMIC_COMMIT=PASS TWO_PARTY_TRADE_LATE_FAILURE_ROLLBACK=PASS");
    GeneratedMissionSmoke.Validate(fixture, connection);
    InventoryMutationSmoke.Validate(fixture, connection);
    CharacterPersistenceFaultSmoke.Validate(fixture, connection);
    ActiveNanoSmoke.Validate(fixture, connection);
    AuthoredMissionSmoke.Validate(fixture, connection);
    CutoverDurableReloadSmoke.Validate(args[2], fixture, connection);

    // A missing real world package is a lifecycle failure, not a reason to lose the
    // independent schema and transactional-import proofs completed above.
    current = FixtureSql.Fingerprint(connection);
    EngineSmoke.Lifecycle(args[2], fixture);
    Require(current == FixtureSql.Fingerprint(connection), "runtime-start-stop-database-unchanged");
    Console.WriteLine("RUNTIME_START_STOP=PASS DATABASE_RUNTIME_FINGERPRINT_UNCHANGED=PASS");
    EngineSmoke.Lifecycle(args[2], fixture);
    Require(current == FixtureSql.Fingerprint(connection), "runtime-restart-database-unchanged");
    Console.WriteLine("SCHEMA_NEGATIVE_TEST=PASS SCHEMA_CURRENT_TEST=PASS EXPLICIT_MIGRATION_TEST=PASS COPY_FAILURE_ROLLBACK=PASS RUNTIME_RESTART=PASS PRODUCTION_CONTACT=NO");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("SCHEMA_VALIDATION=FAIL " + (exception is FixtureFailure ? exception.Message : exception.GetType().Name));
    if (connected) Console.Error.WriteLine(exception.StackTrace); // Frames only; never connection/credential-bearing exception messages.
    if (exception is MySqlException sqlException)
        Console.Error.WriteLine("DISPOSABLE_SQL_ERROR_NUMBER=" + sqlException.Number);
    return 1;
}

static string[] MigrateArgs() => new[] { "migrate", "--expected-database", DisposableSchemaDatabase.DatabaseName, "--acknowledge-backup", "--acknowledge-engines-stopped" };
static void Require(bool condition, string code) { if (!condition) throw new FixtureFailure(code); }

sealed class FixtureFailure(string code) : Exception(code);

static class FixtureSql
{
    public static void Execute(MySqlConnection connection, string sql, int commandTimeout = 30) { using var command = new MySqlCommand(sql, connection) { CommandTimeout = commandTimeout }; command.ExecuteNonQuery(); }
    public static long Scalar(MySqlConnection connection, string sql) { using var command = new MySqlCommand(sql, connection); return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture); }
    public static void CreateBaseline(MySqlConnection connection)
    {
        // Minimal exact DAO contract fixture; no unrelated SqlTables bootstrap or seed data.
        // The canonical bootstrap includes its full teleport seed corpus as individual inserts.
        Execute(connection, File.ReadAllText(Path.Combine(ConnectedAcceptanceSmoke.RepositoryRoot(),
            "AORebirth/Libraries/Source/AORebirth.Database/SqlTables/teleports.sql")), commandTimeout: 180);
        Execute(connection, "CREATE TABLE charactersactivenanos (Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,CharacterId INT NOT NULL,NanoId INT UNSIGNED NOT NULL,Strain INT UNSIGNED NOT NULL,NanoInstance INT NOT NULL DEFAULT 0,DurationCentiseconds INT NOT NULL DEFAULT 0,ExpiresAtUtcTicks BIGINT NOT NULL DEFAULT 0) ENGINE=InnoDB");
        Execute(connection, "CREATE TABLE missionrewardledger (Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,CharacterId INT NOT NULL,QuestId VARCHAR(128) NOT NULL,RewardKey VARCHAR(191) NOT NULL,RewardType VARCHAR(64) NOT NULL,Status INT NOT NULL,Attempts INT NOT NULL DEFAULT 0,EffectReference VARCHAR(255) NULL,LastError VARCHAR(1024) NULL,ClaimToken VARCHAR(64) NULL,ClaimedAtUtcTicks BIGINT NOT NULL DEFAULT 0,ClaimExpiresAtUtcTicks BIGINT NOT NULL DEFAULT 0,AppliedAtUtcTicks BIGINT NOT NULL DEFAULT 0,CreatedAtUtcTicks BIGINT NOT NULL,UpdatedAtUtcTicks BIGINT NOT NULL,Version BIGINT NOT NULL DEFAULT 1,UNIQUE KEY character_quest_reward(CharacterId,QuestId,RewardKey),KEY character_quest(CharacterId,QuestId)) ENGINE=InnoDB");
        Execute(connection, "CREATE TABLE characters (Id INT PRIMARY KEY,Name VARCHAR(32),FirstName VARCHAR(32),LastName VARCHAR(32),Playfield INT,X FLOAT,Y FLOAT,Z FLOAT,HeadingW FLOAT,HeadingX FLOAT,HeadingY FLOAT,HeadingZ FLOAT,Online SMALLINT) ENGINE=InnoDB; CREATE TABLE stats (Type INT,Instance INT,StatId INT,StatValue INT,UNIQUE KEY main(Type,Instance,StatId)) ENGINE=InnoDB; CREATE TABLE charactersuploadednanos (Id INT AUTO_INCREMENT PRIMARY KEY,CharacterId INT,NanoId INT,INDEX Nanos(CharacterId,NanoId)) ENGINE=InnoDB; CREATE TABLE itemnames (Id INT PRIMARY KEY,Name VARCHAR(250)) ENGINE=InnoDB; CREATE TABLE instanceditems (Id INT PRIMARY KEY,ContainerType INT,ContainerInstance INT,ContainerPlacement INT,Itemtype INT,LowId INT,HighId INT,Quality INT,MultipleCount INT) ENGINE=InnoDB; CREATE TABLE items (Id INT AUTO_INCREMENT PRIMARY KEY,ContainerType INT,ContainerInstance INT,ContainerPlacement INT,LowId INT,HighId INT,Quality INT,MultipleCount INT) ENGINE=InnoDB");
    }
    public static string Fingerprint(MySqlConnection connection, bool includeAutoIncrementCounters = true)
    {
        var tables = new List<string>();
        using (var command = new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME", connection))
        using (var reader = command.ExecuteReader()) while (reader.Read()) tables.Add(reader.GetString(0));
        var result = new StringBuilder();
        foreach (string table in tables)
        {
            using var command = new MySqlCommand("SHOW CREATE TABLE `" + table + "`", connection);
            using (var reader = command.ExecuteReader())
            {
                reader.Read();
                string definition = reader.GetString(1);
                // MySQL deliberately does not roll back AUTO_INCREMENT reservations.
                // Only rollback tests opt out of these safe gaps; read-only/startup
                // proofs continue to compare the complete exact table definition.
                if (!includeAutoIncrementCounters)
                    definition = Regex.Replace(definition, @"\sAUTO_INCREMENT=\d+", string.Empty, RegexOptions.CultureInvariant);
                result.Append(definition);
            }
            result.Append(TableFingerprint(connection, table));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.ToString())));
    }
    public static string TableFingerprint(MySqlConnection connection, string table)
    {
        var rows = new List<string>();
        using var command = new MySqlCommand("SELECT * FROM `" + table + "`", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read()) rows.Add(string.Concat(Enumerable.Range(0, reader.FieldCount).Select(i => Cell(reader.GetValue(i)))));
        return string.Join("\n", rows.Order(StringComparer.Ordinal));
    }

    private static string Cell(object value)
    {
        if (value is DBNull) return "null;";
        string text = value switch
        {
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToHexString(bytes),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        return value.GetType().Name + ":" + text.Length.ToString(CultureInfo.InvariantCulture) + ":" + text + ";";
    }
}

sealed class SilentLogger : IZoneLogger
{
    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
    public void Error(Exception exception, string message) { }
    public IZoneLogger CreateForPlayfield(int playfieldId) => this;
}

static class TradeSmoke
{
    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            int inventoryType = (int)SmokeLounge.AOtomation.Messaging.GameData.IdentityType.Inventory;
            int characterType = (int)SmokeLounge.AOtomation.Messaging.GameData.IdentityType.CanbeAffected;
            int cashStat = (int)SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.Cash;
            FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Name,Online) VALUES (9101,'DisposableTradeA',0),(9102,'DisposableTradeB',0); INSERT INTO stats (Type,Instance,StatId,StatValue) VALUES ({characterType},9101,{cashStat},100),({characterType},9102,{cashStat},200); ALTER TABLE stats ADD CONSTRAINT fixture_second_cash_limit CHECK (Instance <> 9102 OR StatValue <= 200)");
            var inventory = new MySqlInventoryRepository(new SilentLogger());
            var nanos = new MySqlUploadedNanoRepository(new SilentLogger());
            var persistence = new MySqlTradePersistence(inventory, nanos);
            int first = inventory.LeaseInstanceIdBlock(3);
            ItemInstanceRecord Item(int id, int owner, int placement) => new()
            {
                InstanceId = id, ContainerType = inventoryType, ContainerInstance = owner,
                ContainerPlacement = placement, LowId = 20, HighId = 20, Quality = 1, StackCount = 1
            };
            inventory.Insert(Item(first, 9101, 1));
            inventory.Insert(Item(first + 1, 9102, 1));
            var batch = new TradePersistenceBatch(
                new[] { Item(first + 2, 9101, 2) },
                new[] { new ItemLocationUpdate(first, inventoryType, 9102, 1), new ItemLocationUpdate(first + 1, inventoryType, 9101, 1) },
                new[] { new TradeCharacterWrite(9101, 90, new[] { 37 }), new TradeCharacterWrite(9102, 210, new[] { 38 }) });
            string before = FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false);
            bool failedAsIntended = false;
            try { persistence.Persist(batch); }
            catch (MySqlException exception) when (exception.Number == 3819) { failedAsIntended = true; }
            if (!failedAsIntended || before != FixtureSql.Fingerprint(connection, includeAutoIncrementCounters: false))
                throw new FixtureFailure("trade-second-cash-failure-did-not-roll-back-items-nanos-and-both-cash-balances");
            FixtureSql.Execute(connection, "ALTER TABLE stats DROP CHECK fixture_second_cash_limit");
            persistence.Persist(batch);
            if (FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM item_instances WHERE (InstanceId={first} AND ContainerInstance=9102 AND ContainerPlacement=1) OR (InstanceId={first + 1} AND ContainerInstance=9101 AND ContainerPlacement=1) OR (InstanceId={first + 2} AND ContainerInstance=9101 AND ContainerPlacement=2)") != 3
                || FixtureSql.Scalar(connection, $"SELECT COUNT(*) FROM stats WHERE Type={characterType} AND StatId={cashStat} AND ((Instance=9101 AND StatValue=90) OR (Instance=9102 AND StatValue=210))") != 2
                || FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM charactersuploadednanos WHERE (CharacterId=9101 AND NanoId=37) OR (CharacterId=9102 AND NanoId=38)") != 2)
                throw new FixtureFailure("trade-success-did-not-commit-items-nanos-and-both-cash-balances");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }
}

static class SnapshotSmoke
{
    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", fixture.ConnectionString);
            FixtureSql.Execute(connection, "INSERT INTO characters (Id,Name,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES (9001,'DisposableSnapshot',4582,1,2,3,1,0,0,0,1); ALTER TABLE stats ADD CONSTRAINT fixture_nonnegative_stat CHECK (StatValue >= 0)");
            var repository = new MySqlCharacterRepository(new SilentLogger());
            var character = new CharacterRecord { Id = 9001, Playfield = 127, X = 11, Y = 22, Z = 33, HeadingW = 1 };
            string before = FixtureSql.Fingerprint(connection);
            bool failedAsIntended = false;
            try
            {
                repository.SaveSnapshot(character, 0, new[]
                {
                    new StatRecord { StatId = 17, StatValue = 120 },
                    new StatRecord { StatId = 18, StatValue = -1 }
                });
            }
            catch (MySqlException exception) when (exception.Number == 3819) { failedAsIntended = true; }
            if (!failedAsIntended || before != FixtureSql.Fingerprint(connection))
                throw new FixtureFailure("character-snapshot-failed-stat-did-not-roll-back-location-online-and-stats");
            repository.SaveSnapshot(character, 0, new[] { new StatRecord { StatId = 17, StatValue = 120 } });
            if (FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM characters WHERE Id=9001 AND Playfield=127 AND X=11 AND Y=22 AND Z=33 AND Online=0") != 1
                || FixtureSql.Scalar(connection, "SELECT COUNT(*) FROM stats WHERE Instance=9001 AND StatId=17 AND StatValue=120") != 1)
                throw new FixtureFailure("character-snapshot-success-did-not-commit-location-online-and-stats");
            FixtureSql.Execute(connection, "ALTER TABLE stats DROP CHECK fixture_nonnegative_stat");
        }
        finally { Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", previous); }
    }

}

static class MigrationSmoke
{
    public static int Run(DisposableSchemaDatabase fixture, string[] arguments, TextWriter output)
    {
        // Exercise the separately packaged operator entrypoint, not an in-process
        // substitute. Its connection is supplied only through its own environment key.
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        FixtureEnvironment.ClearInheritedRuntimeSettings(start);
        start.ArgumentList.Add(typeof(MigrationCommand).Assembly.Location);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["AO_REBIRTH_MIGRATION_CONNECTION"] = fixture.ConnectionString;
        using var process = Process.Start(start) ?? throw new FixtureFailure("migration-tool-start-failed");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(true); throw new FixtureFailure("migration-tool-timeout"); }
        string text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        string password = new MySqlConnectionStringBuilder(fixture.ConnectionString).Password;
        output.Write(text.Replace(fixture.ConnectionString, "<redacted>", StringComparison.Ordinal).Replace(password, "<redacted>", StringComparison.Ordinal));
        return process.ExitCode;
    }
}

static class FixtureEnvironment
{
    public static void ClearInheritedRuntimeSettings(ProcessStartInfo start)
    {
        foreach (string key in start.Environment.Keys.Where(key => key.StartsWith("AO_REBIRTH_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(key);
        start.Environment.Remove("NOTIFY_SOCKET");
    }
}

static class EngineSmoke
{
    public static void ExpectedDatabaseMismatch(string assembly, DisposableSchemaDatabase fixture)
    {
        foreach (string mode in new[] { "--validate-database", "--headless" })
        {
            ProcessStartInfo start = CreateStartInfo(assembly, fixture, mode);
            start.Environment["AO_REBIRTH_EXPECTED_DATABASE"] = DisposableSchemaDatabase.DatabaseName + "_mismatch";
            using var process = Process.Start(start) ?? throw new FixtureFailure("engine-mismatched-database-start-failed");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(true); throw new FixtureFailure("expected-database-mismatch-did-not-fail-closed"); }
            string text = output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult();
            if (process.ExitCode == 0 || !text.Contains("AO_REBIRTH_EXPECTED_DATABASE", StringComparison.Ordinal) || text.Contains("ZONEENGINE_NEW_READY", StringComparison.Ordinal))
                throw new FixtureFailure("expected-database-mismatch-result exit=" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + " output=" + SafeOutput(text, fixture));
        }
    }
    public static void Validate(string assembly, DisposableSchemaDatabase fixture, SchemaState expected)
    {
        using var process = Start(assembly, fixture, "--validate-database");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(true); throw new FixtureFailure("engine-schema-check-timeout"); }
        string text = output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult();
        bool current = expected == SchemaState.SCHEMA_CURRENT;
        if ((current && process.ExitCode != 0) || (!current && process.ExitCode == 0) || !text.Contains(expected.ToString(), StringComparison.Ordinal))
            throw new FixtureFailure("engine-schema-check-result exit=" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + " output=" + SafeOutput(text, fixture));
    }
    public static void NegativeLifecycle(string assembly, DisposableSchemaDatabase fixture)
    {
        using var process = Start(assembly, fixture, "--headless");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(true); throw new FixtureFailure("negative-normal-startup-did-not-fail-closed"); }
        string text = output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult();
        if (process.ExitCode == 0 || !text.Contains("SCHEMA_MIGRATION_REQUIRED", StringComparison.Ordinal) || text.Contains("ZONEENGINE_NEW_READY", StringComparison.Ordinal))
            throw new FixtureFailure("negative-normal-startup-result exit=" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + " output=" + SafeOutput(text, fixture));
    }
    public static void Lifecycle(string assembly, DisposableSchemaDatabase fixture)
    {
        string shutdown = Path.Combine(fixture.DirectoryPath, "shutdown-" + Guid.NewGuid().ToString("N"));
        using var process = Start(assembly, fixture, "--headless", "--shutdown-file", shutdown);
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lines = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is null) return; lock (lines) lines.AppendLine(e.Data); if (e.Data.Contains("ZONEENGINE_NEW_READY", StringComparison.Ordinal)) ready.TrySetResult(true); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.AppendLine(e.Data); };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try
        {
            Task finished = Task.WhenAny(ready.Task, process.WaitForExitAsync(), Task.Delay(TimeSpan.FromSeconds(60))).GetAwaiter().GetResult();
            if (finished != ready.Task)
            {
                if (!process.HasExited) process.Kill(true);
                process.WaitForExit();
                string text;
                lock (lines) text = lines.ToString();
                throw new FixtureFailure("engine-runtime-not-ready exit=" + process.ExitCode.ToString(CultureInfo.InvariantCulture) + " output=" + SafeOutput(text, fixture));
            }
            using (var client = new TcpClient())
            {
                if (!client.ConnectAsync(IPAddress.Loopback, fixture.ZonePort).Wait(TimeSpan.FromSeconds(5)))
                    throw new FixtureFailure("zone-port-not-bound");
            }
            File.WriteAllText(shutdown, "stop");
            if (!process.WaitForExit(30000)) { process.Kill(true); throw new FixtureFailure("engine-shutdown-timeout"); }
            if (process.ExitCode != 0) throw new FixtureFailure("engine-shutdown-exit");
        }
        finally
        {
            // A failed assertion must not leave an engine holding the disposable
            // database or loopback listener open when Docker cleanup follows.
            if (!process.HasExited) { process.Kill(true); process.WaitForExit(); }
            if (File.Exists(shutdown)) File.Delete(shutdown);
        }
    }
    private static string SafeOutput(string text, DisposableSchemaDatabase fixture)
    {
        string password = new MySqlConnectionStringBuilder(fixture.ConnectionString).Password;
        string redacted = text.Replace(fixture.ConnectionString, "<redacted>", StringComparison.Ordinal);
        return string.IsNullOrEmpty(password) ? redacted : redacted.Replace(password, "<redacted>", StringComparison.Ordinal);
    }
    private static Process Start(string assembly, DisposableSchemaDatabase fixture, params string[] arguments)
        => Process.Start(CreateStartInfo(assembly, fixture, arguments)) ?? throw new FixtureFailure("engine-start-failed");

    private static ProcessStartInfo CreateStartInfo(string assembly, DisposableSchemaDatabase fixture, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(assembly))! };
        FixtureEnvironment.ClearInheritedRuntimeSettings(start);
        start.ArgumentList.Add(Path.GetFullPath(assembly));
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["AO_REBIRTH_MYSQL_CONNECTION"] = fixture.ConnectionString;
        start.Environment["AO_REBIRTH_CONFIG_PATH"] = fixture.ConfigPath;
        start.Environment["AO_REBIRTH_BIND_MODE"] = "Loopback";
        start.Environment["AO_REBIRTH_EXPECTED_DATABASE"] = DisposableSchemaDatabase.DatabaseName;
        start.Environment["AO_REBIRTH_REQUIRED_SQL_TYPE"] = "MySql";
        return start;
    }
}

sealed class DisposableSchemaDatabase : IDisposable
{
    public const string DatabaseName = "aorebirth_zone_schema_disposable";
    private const string Image = "mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d";
    private const string Label = "org.aorebirth.purpose=zone-schema-disposable";
    private readonly string name = "aorebirth-zone-schema-" + Guid.NewGuid().ToString("N");
    private bool created;
    private bool networkCreated;
    private string containerId = string.Empty;
    private string networkId = string.Empty;
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "aorebirth-zone-schema-" + Guid.NewGuid().ToString("N"));
    public string ConnectionString { get; private set; } = string.Empty;
    public int ZonePort { get; private set; }
    public int LoginPort { get; private set; }
    public string FixtureId => name;
    public string ConfigPath => Path.Combine(DirectoryPath, "Config.xml");
    public void Start()
    {
        Directory.CreateDirectory(DirectoryPath);
        Docker("image", "inspect", Image);
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string environment = Path.Combine(DirectoryPath, "mysql.env");
        File.WriteAllLines(environment, new[] { "MYSQL_ROOT_PASSWORD=" + password, "MYSQL_DATABASE=" + DatabaseName, "MYSQL_USER=zone_schema_fixture", "MYSQL_PASSWORD=" + password });
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(environment, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        // A dedicated bridge keeps this fixture off every existing application network.
        // Host access is published only on loopback; Docker internal networks suppress
        // the host port mapping on supported desktop engines.
        networkId = Docker("network", "create", "--label", Label, name).Trim();
        RequireDockerId(networkId);
        networkCreated = true;
        containerId = Docker("run", "--detach", "--name", name, "--label", Label, "--restart", "no", "--network", name, "--publish", "127.0.0.1::3306", "--env-file", environment, Image).Trim();
        RequireDockerId(containerId);
        created = true;
        File.Delete(environment);
        string mapping = Docker("port", name, "3306/tcp").Trim();
        if (!mapping.StartsWith("127.0.0.1:", StringComparison.Ordinal) || !uint.TryParse(mapping[10..], out uint port)) throw new FixtureFailure("unexpected-docker-port-binding");
        ConnectionString = new MySqlConnectionStringBuilder { Server = "127.0.0.1", Port = port, Database = DatabaseName, UserID = "zone_schema_fixture", Password = password, SslMode = MySqlSslMode.Disabled, AllowPublicKeyRetrieval = true, ConnectionTimeout = 2, AllowUserVariables = true }.ConnectionString;
        bool ready = false;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            try { using var connection = Open(); ready = true; break; }
            catch (MySqlException) { Thread.Sleep(1000); }
        }
        if (!ready) throw new FixtureFailure("disposable-mysql-timeout");
        // Config is an isolated copy of nonsecret runtime defaults; endpoints are loopback-only.
        ZonePort = AvailablePort();
        LoginPort = AvailablePort();
        int communicationPort = AvailablePort();
        File.WriteAllText(ConfigPath, "<?xml version=\"1.0\"?><Config><SQLType>MySql</SQLType><MysqlConnection>REPLACE_WITH_DISPOSABLE_RUNTIME_SECRET</MysqlConnection><ListenIP>127.0.0.1</ListenIP><ChatIP>127.0.0.1</ChatIP><ISCommLocalIP>127.0.0.1</ISCommLocalIP><ZoneIP>127.0.0.1</ZoneIP><ZonePort>" + ZonePort.ToString(CultureInfo.InvariantCulture) + "</ZonePort><LoginPort>17500</LoginPort><CommPort>" + communicationPort.ToString(CultureInfo.InvariantCulture) + "</CommPort><ChatPort>17512</ChatPort><DefaultPlayfield>4582</DefaultPlayfield></Config>");
    }
    public MySqlConnection Open() { var connection = new MySqlConnection(ConnectionString); connection.Open(); return connection; }
    public void Dispose()
    {
        bool resourcesCreated = created || networkCreated;
        if (created)
        {
            string label = Docker("inspect", "--format", "{{index .Config.Labels \"org.aorebirth.purpose\"}}", containerId).Trim();
            if (label != "zone-schema-disposable") throw new FixtureFailure("disposable-label-changed-refusing-cleanup");
            Docker("rm", "--force", "--volumes", containerId);
            if (!string.IsNullOrWhiteSpace(Docker("container", "ls", "--all", "--filter", "id=" + containerId, "--format", "{{.ID}}")))
                throw new FixtureFailure("disposable-container-residue");
            created = false;
        }
        if (networkCreated)
        {
            string label = Docker("network", "inspect", "--format", "{{index .Labels \"org.aorebirth.purpose\"}}", networkId).Trim();
            if (label != "zone-schema-disposable") throw new FixtureFailure("disposable-network-label-changed-refusing-cleanup");
            Docker("network", "rm", networkId);
            if (!string.IsNullOrWhiteSpace(Docker("network", "ls", "--filter", "id=" + networkId, "--format", "{{.ID}}")))
                throw new FixtureFailure("disposable-network-residue");
            networkCreated = false;
        }
        if (Directory.Exists(DirectoryPath))
        {
            string exactDirectory = Path.GetFullPath(DirectoryPath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetDirectoryName(exactDirectory), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), comparison)
                || !Path.GetFileName(exactDirectory).StartsWith("aorebirth-zone-schema-", StringComparison.Ordinal))
                throw new FixtureFailure("disposable-directory-outside-exact-temp-parent-refusing-cleanup");
            Directory.Delete(exactDirectory, true);
        }
        if (resourcesCreated) Console.WriteLine("DISPOSABLE_CONTAINER_RESIDUE=NONE DISPOSABLE_NETWORK_RESIDUE=NONE DISPOSABLE_CLEANUP=PASS");
    }
    private static int AvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }
    private static void RequireDockerId(string id)
    {
        if (id.Length != 64 || id.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new FixtureFailure("disposable-resource-id-invalid-refusing-unscoped-operations");
    }
    private static string Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new FixtureFailure("docker-unavailable");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000)) { process.Kill(true); throw new FixtureFailure("docker-timeout"); }
        if (process.ExitCode != 0) throw new FixtureFailure("docker-" + arguments[0] + "-failed" + (arguments[0] == "port" ? ": " + error.GetAwaiter().GetResult().Trim() : string.Empty));
        return output.GetAwaiter().GetResult();
    }
}
