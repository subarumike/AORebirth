namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Data;
    using System.Linq;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;

    internal static partial class Program
    {
        private static void ValidateParallelContracts(IMissionDao dao, string connectionString)
        {
            ValidateReadCardinalityAndParameters(dao);
            ValidateConnectionFailures(connectionString);
            ValidateCommitFailures(dao, connectionString);
            ValidateFeeFailure(dao, connectionString);
#if MISSION_DAO_ISOLATED_TESTS
            ValidateConfiguredFactoryAndCompatibility(connectionString);
#endif
        }

        private static void ValidateReadCardinalityAndParameters(IMissionDao dao)
        {
            const int characterId = 10001;
            Require(dao.GetMission(new MissionKeyData(characterId, "missing")) == null, "missing-single-is-null");
            var empty = dao.ReadCharacter(characterId);
            Require(empty.CharacterId == characterId && empty.Missions.Count == 0 && empty.Objectives.Count == 0
                && empty.Flags.Count == 0 && empty.Rewards.Count == 0, "empty-snapshot-collections");
            var first = NewMission("parallel.z");
            first.CharacterId = characterId;
            dao.Execute(characterId, tx => { tx.SaveMission(new MissionKeyData(characterId, first.QuestId), first); return true; });
            Require(dao.GetMissions(characterId).Single().QuestId == first.QuestId, "exactly-one-read");
            var second = NewMission("parallel.a'\\;--");
            second.CharacterId = characterId;
            second.State = MissionLifecycleState.Completed;
            string value = "literal'; UPDATE missionstates SET State=5; --\\";
            dao.Execute(characterId, tx =>
            {
                var key = new MissionKeyData(characterId, second.QuestId);
                tx.SaveMission(key, second);
                foreach (string child in new[] { "z", "a" })
                {
                    tx.SaveObjective(new MissionObjectiveKeyData(key, child),
                        new MissionObjectiveProgressData { CharacterId = characterId, QuestId = key.QuestId,
                            ObjectiveId = child, RequiredCount = 2, LastObservationKey = null });
                    tx.SaveFlag(key, new MissionFlagData { CharacterId = characterId, QuestId = key.QuestId,
                        FlagKey = child, Value = child == "a" ? null : value });
                    Require(tx.TryClaimReward(new MissionRewardKeyData(key, child), "item", "claim-" + child, 700, 800).Status
                        == MissionRewardClaimStatus.Claimed, "ordering-reward-fixture-claimed");
                }
                return true;
            });
            var snapshot = dao.ReadCharacter(characterId);
            Require(snapshot.Missions.Select(x => x.QuestId).SequenceEqual(new[] { second.QuestId, first.QuestId }),
                "multiple-missions-key-order");
            Require(snapshot.Objectives.Select(x => x.ObjectiveId).SequenceEqual(new[] { "a", "z" }),
                "objective-key-order");
            Require(snapshot.Flags.Select(x => x.FlagKey).SequenceEqual(new[] { "a", "z" }), "flag-key-order");
            Require(snapshot.Rewards.Select(x => x.RewardKey).SequenceEqual(new[] { "a", "z" }), "reward-key-order");
            Require(snapshot.Flags[0].Value == null && snapshot.Flags[1].Value == value
                && snapshot.Objectives.All(x => x.LastObservationKey == null), "nullable-and-quoted-values-roundtrip");
            Require(snapshot.Missions[0].State == MissionLifecycleState.Completed
                && snapshot.Missions[1].State == MissionLifecycleState.Active, "quoted-input-is-data-not-sql");
            Require(dao.GetMission(new MissionKeyData(101, second.QuestId)) == null, "read-character-isolation");
            // SQL persistence has no physical delete or expiry API. Completion/abandonment
            // are caller-supplied state updates, exercised by ValidateLifecycleIsolation.
        }

        private static void ValidateConnectionFailures(string connectionString)
        {
            var failure = new InvalidOperationException("injected factory");
            try { new MySqlMissionDao(() => { throw failure; }).GetMissions(101); Require(false, "factory-error-required"); }
            catch (InvalidOperationException actual) { Require(ReferenceEquals(actual, failure), "factory-error-propagates"); }
            Expect<ArgumentNullException>(() => new MySqlMissionDao(null), "null-factory-rejected");
            foreach (string point in new[] { "open", "begin", "create", "execute" })
            {
                var connection = new FaultConnection(connectionString) { FailurePoint = point };
                var target = new MySqlMissionDao(() => connection);
                Expect<InjectedPersistenceException>(() => target.Execute(101, tx => tx.GetMissions(101)),
                    point + "-failure-propagates");
                Require(connection.Disposed, point + "-failure-disposes-connection");
                if (connection.LastTransaction != null)
                    Require(connection.LastTransaction.Disposed, point + "-failure-disposes-transaction");
            }
        }

        private static void ValidateCommitFailures(IMissionDao dao, string connectionString)
        {
            foreach (bool afterCommit in new[] { false, true })
            {
                var connection = new FaultConnection(connectionString)
                    { FailurePoint = afterCommit ? "commit-after" : "commit-before" };
                var target = new MySqlMissionDao(() => connection);
                var mission = NewMission("parallel." + connection.FailurePoint);
                IMissionDaoTransaction escaped = null;
                Expect<InjectedPersistenceException>(() => target.Execute(101, tx =>
                {
                    escaped = tx;
                    tx.SaveMission(Key(mission.QuestId), mission);
                    return true;
                }), connection.FailurePoint + "-propagates");
                Require(mission.Version == 0, connection.FailurePoint + "-restores-dto-version");
                Expect<InvalidOperationException>(() => escaped.GetMission(Key(mission.QuestId)), "failed-commit-scope-closed");
                Require(connection.Disposed && connection.LastTransaction.Disposed, "failed-commit-resources-disposed");
                var durable = dao.GetMission(Key(mission.QuestId));
                Require(afterCommit ? durable != null && durable.Version == 1 : durable == null,
                    afterCommit ? "lost-commit-ack-requires-reload" : "pre-commit-failure-rolls-back");
            }
        }

        private static void ValidateFeeFailure(IMissionDao dao, string connectionString)
        {
            long cashBefore = Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=102 AND Type=50000 AND StatId=61");
            var connection = new FaultConnection(connectionString) { FailurePoint = "fee-ledger", FailRollback = true };
            var request = new MissionRollFeeRequest { CharacterType = 50000, CharacterId = 102,
                BatchIdentity = "parallel-fee-failure", Fee = 1, AppliedAtUtcTicks = 700 };
            try
            {
                new MySqlMissionDao(() => connection).TryChargeRollFee(request);
                Require(false, "fee-error-required");
            }
            catch (InjectedPersistenceException actual)
            {
                Require(ReferenceEquals(actual, connection.OperationFailure), "fee-preserves-original-command-error");
                Require(ReferenceEquals(actual.Data["MissionDao.RollbackFailure"], connection.RollbackFailure),
                    "fee-retains-secondary-rollback-error");
            }
            Require(connection.Disposed && connection.LastTransaction.Disposed, "fee-error-disposes-resources");
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=102 AND Type=50000 AND StatId=61")
                == cashBefore, "fee-partial-debit-rolled-back");
            Require(dao.TryChargeRollFee(request).Status == MissionRollFeeStatus.Applied, "failed-fee-ledger-retry-applies");
            Require(dao.TryChargeRollFee(request).Status == MissionRollFeeStatus.AlreadyApplied, "fee-retry-exactly-once");
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=102 AND Type=50000 AND StatId=61")
                == cashBefore - 1, "fee-retry-debits-once");
        }

#if MISSION_DAO_ISOLATED_TESTS
        private static void ValidateConfiguredFactoryAndCompatibility(string connectionString)
        {
            int opened = 0;
            Connector.TestConnectionFactory = () => { opened++; return new MySqlConnection(connectionString); };
            try
            {
                var configured = DatabaseDaoFactory.CreateMissionDao();
                Require(opened == 0 && configured is MySqlMissionDao, "factory-lazy-configured-mysql");
                Require(configured.GetMissions(101).Count > 0 && opened == 1, "factory-first-operation-connects");
                const int characterId = 10002;
                Require(NewCharacterStartAreaSelectionDao.GetState(characterId) == null, "compat-missing-is-null");
                Require(NewCharacterStartAreaSelectionDao.MarkPending(characterId), "compat-mark-pending");
                Require(NewCharacterStartAreaSelectionDao.MarkPending(characterId), "compat-pending-idempotent");
                Require(NewCharacterStartAreaSelectionDao.GetState(characterId) == MissionStartAreaSelectionStates.Pending,
                    "compat-read-pending");
                Require(!NewCharacterStartAreaSelectionDao.TryComplete(characterId, "Arete"), "compat-state-case-sensitive");
                Require(NewCharacterStartAreaSelectionDao.TryComplete(characterId, NewCharacterStartAreaSelectionDao.AreteState),
                    "compat-completes-once");
                Require(!NewCharacterStartAreaSelectionDao.TryComplete(characterId, NewCharacterStartAreaSelectionDao.IccShuttleportState)
                    && !NewCharacterStartAreaSelectionDao.MarkPending(characterId), "compat-completed-not-overwritten");
                Require(configured.GetStartAreaSelectionState(characterId) == MissionStartAreaSelectionStates.Arete,
                    "compat-and-dao-same-durable-row");
                Require(!NewCharacterStartAreaSelectionDao.MarkPending(0)
                    && NewCharacterStartAreaSelectionDao.GetState(0) == null
                    && !NewCharacterStartAreaSelectionDao.TryComplete(0, null), "compat-invalid-input-contract");

                var unsupported = new FaultConnection(connectionString);
                Connector.TestConnectionFactory = () => unsupported;
                Expect<NotSupportedException>(() => configured.GetMissions(101), "configured-non-mysql-rejected");
                Require(unsupported.Disposed && unsupported.CommandCount == 0, "unsupported-provider-no-mission-sql");
                Connector.TestConnectionFactory = () => { throw new InjectedPersistenceException(); };
                Require(NewCharacterStartAreaSelectionDao.GetState(characterId) == null
                    && !NewCharacterStartAreaSelectionDao.MarkPending(characterId)
                    && !NewCharacterStartAreaSelectionDao.TryComplete(characterId, MissionStartAreaSelectionStates.Arete),
                    "compat-provider-failure-remains-null-false");
            }
            finally { Connector.TestConnectionFactory = null; }
        }
#endif

        private sealed class InjectedPersistenceException : Exception { }

        // Test-only ADO decorator. All successful SQL and transactions still execute
        // against the disposable MySQL instance; no alternative DAO is modeled here.
        private sealed class FaultConnection : IDbConnection
        {
            private readonly MySqlConnection inner;
            internal string FailurePoint;
            internal bool FailRollback;
            internal bool Disposed;
            internal int CommandCount;
            internal FaultTransaction LastTransaction;
            internal readonly InjectedPersistenceException OperationFailure = new InjectedPersistenceException();
            internal readonly InjectedPersistenceException RollbackFailure = new InjectedPersistenceException();
            internal FaultConnection(string connectionString) { this.inner = new MySqlConnection(connectionString); }
            internal void Fail(string point) { if (this.FailurePoint == point) throw this.OperationFailure; }
            public string ConnectionString { get { return this.inner.ConnectionString; } set { this.inner.ConnectionString = value; } }
            public int ConnectionTimeout { get { return this.inner.ConnectionTimeout; } }
            public string Database { get { return this.inner.Database; } }
            public ConnectionState State { get { return this.inner.State; } }
            public void Open() { this.Fail("open"); this.inner.Open(); }
            public void Close() { this.inner.Close(); }
            public void ChangeDatabase(string databaseName) { this.inner.ChangeDatabase(databaseName); }
            public IDbTransaction BeginTransaction() { return this.BeginTransaction(IsolationLevel.Unspecified); }
            public IDbTransaction BeginTransaction(IsolationLevel isolationLevel)
            {
                this.Fail("begin");
                return this.LastTransaction = new FaultTransaction(this, this.inner.BeginTransaction(isolationLevel));
            }
            public IDbCommand CreateCommand()
            {
                this.Fail("create");
                this.CommandCount++;
                return new FaultCommand(this, this.inner.CreateCommand());
            }
            public void Dispose() { this.Disposed = true; this.inner.Dispose(); }
        }

        private sealed class FaultTransaction : IDbTransaction
        {
            private readonly FaultConnection owner;
            internal readonly IDbTransaction Inner;
            internal bool Disposed;
            internal FaultTransaction(FaultConnection owner, IDbTransaction inner) { this.owner = owner; this.Inner = inner; }
            public IDbConnection Connection { get { return this.owner; } }
            public IsolationLevel IsolationLevel { get { return this.Inner.IsolationLevel; } }
            public void Commit() { this.owner.Fail("commit-before"); this.Inner.Commit(); this.owner.Fail("commit-after"); }
            public void Rollback()
            {
                // Roll back the real transaction first so fault tests leave no locks.
                this.Inner.Rollback();
                if (this.owner.FailRollback) throw this.owner.RollbackFailure;
            }
            public void Dispose() { this.Disposed = true; this.Inner.Dispose(); }
        }

        private sealed class FaultCommand : IDbCommand
        {
            private readonly FaultConnection owner;
            private readonly IDbCommand inner;
            internal FaultCommand(FaultConnection owner, IDbCommand inner) { this.owner = owner; this.inner = inner; }
            public string CommandText { get { return this.inner.CommandText; } set { this.inner.CommandText = value; } }
            public int CommandTimeout { get { return this.inner.CommandTimeout; } set { this.inner.CommandTimeout = value; } }
            public CommandType CommandType { get { return this.inner.CommandType; } set { this.inner.CommandType = value; } }
            public IDbConnection Connection { get { return this.owner; } set { } }
            public IDataParameterCollection Parameters { get { return this.inner.Parameters; } }
            public IDbTransaction Transaction
            {
                get { return this.inner.Transaction; }
                set { this.inner.Transaction = value is FaultTransaction ? ((FaultTransaction)value).Inner : value; }
            }
            public UpdateRowSource UpdatedRowSource { get { return this.inner.UpdatedRowSource; } set { this.inner.UpdatedRowSource = value; } }
            public void Cancel() { this.inner.Cancel(); }
            public IDbDataParameter CreateParameter() { return this.inner.CreateParameter(); }
            public void Prepare() { this.inner.Prepare(); }
            public int ExecuteNonQuery()
            {
                this.owner.Fail("execute");
                if (this.owner.FailurePoint == "fee-ledger" && this.CommandText.StartsWith("INSERT INTO missionrewardledger",
                    StringComparison.Ordinal)) throw this.owner.OperationFailure;
                return this.inner.ExecuteNonQuery();
            }
            public IDataReader ExecuteReader() { return this.ExecuteReader(CommandBehavior.Default); }
            public IDataReader ExecuteReader(CommandBehavior behavior) { this.owner.Fail("execute"); return this.inner.ExecuteReader(behavior); }
            public object ExecuteScalar() { this.owner.Fail("execute"); return this.inner.ExecuteScalar(); }
            public void Dispose() { this.inner.Dispose(); }
        }
    }
}
