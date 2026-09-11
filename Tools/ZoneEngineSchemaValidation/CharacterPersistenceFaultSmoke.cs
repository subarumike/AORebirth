using System.Data;
using AORebirth.Database.Domain.Characters;
using AORebirth.Interfaces.Persistence.Characters;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;

// All statements and COMMITs below reach the fixture's real MySQL. The decorator only
// injects transport failures at deterministic boundaries; it never emulates storage.
static class CharacterPersistenceFaultSmoke
{
    public static void Validate(DisposableSchemaDatabase fixture, MySqlConnection connection)
    {
        const int owner = 9821;
        FixtureSql.Execute(connection, $"INSERT INTO characters (Id,Username,Name,Playfield,X,Y,Z,HeadingW,HeadingX,HeadingY,HeadingZ,Online) VALUES ({owner},'dao_fault','DaoFault',4582,1,2,3,1,0,0,0,0)");
        var dao = new MySqlCharacterPersistenceDao(() => fixture.Open());
        int id = dao.LeaseItemInstanceIds(2);
        dao.InsertItem(new PersistedItemData { InstanceId = id, ContainerType = (int)IdentityType.Inventory, ContainerInstance = owner,
            ContainerPlacement = 64, LowId = 43384, HighId = 43384, Quality = 1, StackCount = 5, Source = 1 });
        dao.SaveStats(owner, new[] { new CharacterStatData { StatId = (int)CharacterStat.Cash, StatValue = 100 } });
        var batch = new CharacterInventoryMutationData
        {
            CharacterId = owner,
            Inserts = new[] { new PersistedItemData { InstanceId = id + 1, ContainerType = (int)IdentityType.Inventory, ContainerInstance = owner,
                ContainerPlacement = 64, LowId = 42423, HighId = 42423, Quality = 4, StackCount = 1, Source = 1 } },
            Locations = new[] { new ItemLocationData { InstanceId = id, ContainerType = (int)IdentityType.Inventory, ContainerInstance = owner, ContainerPlacement = 65 } },
            Stacks = new[] { new ItemStackData { InstanceId = id, ExpectedCount = 5, FinalCount = 3 } },
            UploadedNanoIds = new[] { 37 }, FinalStats = new[] { new CharacterStatData { StatId = (int)CharacterStat.Cash, StatValue = 150 } }
        };
        string old = Fingerprint();
        // Parking, insertion, final location, stack CAS, uploaded nano, final base stat.
        foreach (int after in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            var fault = new PersistenceFault { FailAfterWrite = after };
            bool failed = false;
            try { new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), fault)).CommitInventoryMutation(batch); }
            catch (IOException) { failed = true; }
            Require(failed && fault.CommitCalls == 0 && fault.RollbackCalls == 1 && Fingerprint() == old, "partial-write-rollback-" + after);
        }
        var beforeCommit = new PersistenceFault { CommitFault = CommitFault.Before };
        RequireUnknown(() => new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), beforeCommit)).CommitInventoryMutation(batch));
        Require(beforeCommit.CommitCalls == 1 && beforeCommit.RollbackCalls == 0 && Fingerprint() == old, "commit-before-send-unknown-old-state");

        var afterCommit = new PersistenceFault { CommitFault = CommitFault.After };
        RequireUnknown(() => new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), afterCommit)).CommitInventoryMutation(batch));
        Require(afterCommit.CommitCalls == 1 && afterCommit.RollbackCalls == 0, "lost-ack-no-rollback-or-replay");
        var rows = dao.LoadCarriedItems(owner);
        Require(rows.Count == 2 && rows.Single(r => r.InstanceId == id).StackCount == 3
            && rows.Single(r => r.InstanceId == id).ContainerPlacement == 65
            && rows.Single(r => r.InstanceId == id + 1).ContainerPlacement == 64
            && dao.LoadStats(owner).Single().StatValue == 150 && dao.LoadUploadedNanos(owner).SequenceEqual(new[] { 37 }), "lost-ack-full-new-state");
        string committed = Fingerprint();
        bool duplicateRejected = false;
        try { dao.CommitInventoryMutation(batch); } catch (MySqlException) { duplicateRejected = true; }
        Require(duplicateRejected && Fingerprint() == committed, "same-identity-replay-does-not-duplicate-or-park");

        CharacterStateData character = dao.LoadCharacter(owner);
        character.X = 88;
        var stats = new[] { new CharacterStatData { StatId = (int)CharacterStat.Cash, StatValue = 160 } };
        var snapshotFault = new PersistenceFault { FailAfterWrite = 1 };
        try { new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), snapshotFault)).SaveSnapshot(character, 1, stats); }
        catch (IOException) { }
        Require(Fingerprint() == committed && snapshotFault.RollbackCalls == 1, "snapshot-location-stat-rollback");
        var snapshotUnknown = new PersistenceFault { CommitFault = CommitFault.After };
        RequireUnknown(() => new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), snapshotUnknown)).SaveSnapshot(character, 1, stats));
        Require(dao.LoadCharacter(owner).X == 88 && dao.LoadStats(owner).Single().StatValue == 160, "snapshot-unknown-full-new-state");

        // A failed cleanup must not hide the original unknown outcome or invite retry.
        var disposal = new PersistenceFault { CommitFault = CommitFault.After, FailDispose = true };
        RequireUnknown(() => new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), disposal)).SaveStats(owner, stats));
        Require(disposal.CommitCalls == 1 && disposal.RollbackCalls == 0, "unknown-preserved-through-disposal");
        bool readCleanup = false;
        try { new MySqlCharacterPersistenceDao(() => new FaultConnection(fixture.Open(), new PersistenceFault { FailDispose = true })).LoadCharacter(owner); }
        catch (IOException) { readCleanup = true; }
        Require(readCleanup, "read-only-cleanup-misclassified-as-commit");
        Console.WriteLine("CHARACTER_DAO_REAL_MYSQL_FAULTS=PASS PARTIAL_WRITE_CASES=7 SNAPSHOT_ROLLBACK=PASS COMMIT_BEFORE_SEND=UNKNOWN_OLD_STATE COMMIT_LOST_ACK=UNKNOWN_NEW_STATE DUPLICATE_REPLAY=REJECTED DISPOSAL_PRIMARY_FAILURE=PASS");

        string Fingerprint()
        {
            // Fresh physical connection, never a cached DTO or an in-flight transaction.
            using var reopened = fixture.Open();
            return FixtureSql.Fingerprint(reopened, includeAutoIncrementCounters: false);
        }
    }

    static void RequireUnknown(Action operation)
    {
        bool unknown = false;
        try { operation(); } catch (CharacterPersistenceCommitOutcomeUnknownException) { unknown = true; }
        Require(unknown, "commit-failure-not-classified-as-unknown");
    }
    static void Require(bool condition, string message) { if (!condition) throw new FixtureFailure(message); }
}

enum CommitFault { None, Before, After }
sealed class PersistenceFault
{
    public int FailAfterWrite = -1;
    public CommitFault CommitFault;
    public bool FailDispose;
    public int Writes, CommitCalls, RollbackCalls;
}

sealed class FaultConnection(IDbConnection inner, PersistenceFault fault) : IDbConnection
{
    public string ConnectionString { get => inner.ConnectionString; set => inner.ConnectionString = value; }
    public int ConnectionTimeout => inner.ConnectionTimeout;
    public string Database => inner.Database;
    public ConnectionState State => inner.State;
    public void Open() => inner.Open();
    public void Close() => inner.Close();
    public void ChangeDatabase(string databaseName) => inner.ChangeDatabase(databaseName);
    public IDbTransaction BeginTransaction() => new FaultTransaction(this, inner.BeginTransaction(), fault);
    public IDbTransaction BeginTransaction(IsolationLevel il) => new FaultTransaction(this, inner.BeginTransaction(il), fault);
    public IDbCommand CreateCommand() => new FaultCommand(inner.CreateCommand(), fault);
    public void Dispose() { inner.Dispose(); if (fault.FailDispose) throw new IOException("Injected connection disposal failure."); }
}

sealed class FaultTransaction(IDbConnection connection, IDbTransaction inner, PersistenceFault fault) : IDbTransaction
{
    internal IDbTransaction Inner => inner;
    public IDbConnection Connection => connection;
    public IsolationLevel IsolationLevel => inner.IsolationLevel;
    public void Commit()
    {
        fault.CommitCalls++;
        if (fault.CommitFault == CommitFault.Before) throw new IOException("Injected loss before sending COMMIT.");
        inner.Commit();
        if (fault.CommitFault == CommitFault.After) throw new IOException("Injected loss after durable COMMIT, before acknowledgement.");
    }
    public void Rollback() { fault.RollbackCalls++; inner.Rollback(); }
    public void Dispose() { inner.Dispose(); if (fault.FailDispose) throw new IOException("Injected transaction disposal failure."); }
}

sealed class FaultCommand(IDbCommand inner, PersistenceFault fault) : IDbCommand
{
    public string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
    public int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public IDbConnection? Connection { get => inner.Connection; set => inner.Connection = value; }
    public IDataParameterCollection Parameters => inner.Parameters;
    public IDbTransaction? Transaction { get => inner.Transaction; set => inner.Transaction = value is FaultTransaction wrapped ? wrapped.Inner : value; }
    public UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
    public void Cancel() => inner.Cancel();
    public IDbDataParameter CreateParameter() => inner.CreateParameter();
    public int ExecuteNonQuery()
    {
        if (fault.FailAfterWrite == 0) throw new IOException("Injected failure before first write.");
        int result = inner.ExecuteNonQuery();
        if (++fault.Writes == fault.FailAfterWrite) throw new IOException("Injected failure after durable transaction statement.");
        return result;
    }
    public IDataReader ExecuteReader() => inner.ExecuteReader();
    public IDataReader ExecuteReader(CommandBehavior behavior) => inner.ExecuteReader(behavior);
    public object? ExecuteScalar() => inner.ExecuteScalar();
    public void Prepare() => inner.Prepare();
    public void Dispose() => inner.Dispose();
}
