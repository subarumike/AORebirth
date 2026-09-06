namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;

    internal static partial class Program
    {
        private static void ValidateHardening(IMissionDao dao, string connectionString)
        {
            // Continue independent cases so one failure does not hide other evidence.
            var failures = new List<string>();
            Action<string, Action> run = (name, test) =>
            {
                try { test(); }
                catch (Exception exception)
                {
                    failures.Add(name + ":" + exception.GetType().Name);
                    Console.Error.WriteLine("MISSION_DAO_FAILED_CASE=" + name + ":" + exception.Message);
                }
            };
            run("rollback-versions", () => ValidateVersionRestoration(dao));
            run("scope-lifetime", () => ValidateScopeLifetime(dao));
            run("write-failure", () => ValidateCaughtWriteFailure(dao));
            run("stat-validation", () => ValidateInvalidStatBatch(dao, connectionString));
            run("observation-errors", () => ValidateObservationErrors(dao, connectionString));
            run("closed-connection", () => ValidateClosedConnection(connectionString));
            run("rollback-error", ValidateOriginalFailure);
            run("fee-input", () => ValidateFeeInput(dao, connectionString));
            run("lifecycle-stale-isolation", () => ValidateLifecycleIsolation(dao));
            run("reward-leases", () => ValidateRewardLeases(dao));
            run("atomic-rewards", () => ValidateAtomicRewardRetry(dao, connectionString));
            run("snapshot-ordering", () => ValidateSnapshotOrdering(dao));
            run("mutation-cutpoints", () => ValidateMutationCutpoints(dao, connectionString));
            run("ledger-write-failure", () => ValidateLedgerWriteFailure(dao, connectionString));
            run("acceptance-failures", () => ValidateAcceptanceFailures(dao, connectionString));
            run("parallel-contracts", () => ValidateParallelContracts(dao, connectionString));
            if (failures.Count != 0)
            {
                throw new InvalidOperationException(string.Join(", ", failures));
            }
        }

        private static MissionStateData NewMission(string quest, MissionLifecycleState state = MissionLifecycleState.Active)
        {
            return new MissionStateData
            {
                CharacterId = 101, QuestId = quest, State = state,
                CreatedAtUtcTicks = 100, UpdatedAtUtcTicks = 100
            };
        }

        private static MissionKeyData Key(string quest) { return new MissionKeyData(101, quest); }

        private static void Save(IMissionDao dao, MissionStateData mission)
        {
            dao.Execute(mission.CharacterId, tx =>
            {
                tx.SaveMission(new MissionKeyData(mission.CharacterId, mission.QuestId), mission);
                return true;
            });
        }

        private static void Expect<T>(Action action, string code) where T : Exception
        {
            bool caught = false;
            try { action(); }
            catch (T) { caught = true; }
            Require(caught, code);
        }

        private static void ValidateVersionRestoration(IMissionDao dao)
        {
            const string quest = "hardening.rollback";
            MissionStateData mission = NewMission(quest);
            var objective = new MissionObjectiveProgressData
            {
                CharacterId = 101, QuestId = quest, ObjectiveId = "one", RequiredCount = 2
            };
            var flag = new MissionFlagData { CharacterId = 101, QuestId = quest, FlagKey = "flag" };
            var account = new MissionAccountFlagData { AccountKey = "mission_account", FlagKey = quest };
            Action<IMissionDaoTransaction> write = tx =>
            {
                tx.SaveMission(Key(quest), mission);
                tx.SaveObjective(new MissionObjectiveKeyData(Key(quest), "one"), objective);
                tx.SaveFlag(Key(quest), flag);
                tx.SaveAccountFlag("mission_account", account);
                tx.SaveMission(Key(quest), mission); // Same object saved twice, unwind in reverse.
            };
            Expect<ExpectedRollbackException>(() => dao.Execute<int>(101, "mission_account", tx =>
            {
                write(tx);
                throw new ExpectedRollbackException();
            }), "rollback-injected");
            Require(mission.Version == 0 && objective.Version == 0 && flag.Version == 0 && account.Version == 0,
                "rollback-restores-all-insert-versions");
            Require(dao.GetMission(Key(quest)) == null && dao.GetAccountFlag("mission_account", quest) == null,
                "rollback-removes-inserts");
            dao.Execute(101, "mission_account", tx => { write(tx); return true; });
            Require(mission.Version == 2 && objective.Version == 1 && flag.Version == 1 && account.Version == 1,
                "same-dtos-retry-after-rollback");
            Expect<ExpectedRollbackException>(() => dao.Execute<int>(101, "mission_account", tx =>
            {
                write(tx);
                throw new ExpectedRollbackException();
            }), "rollback-update-injected");
            Require(mission.Version == 2 && objective.Version == 1 && flag.Version == 1 && account.Version == 1,
                "rollback-restores-all-update-versions");
            Require(dao.GetMission(Key(quest)).Version == 2, "rollback-keeps-persisted-version");
        }

        private static void ValidateScopeLifetime(IMissionDao dao)
        {
            IMissionDaoTransaction escaped = dao.Execute(101, tx => tx);
            Expect<InvalidOperationException>(() => escaped.GetMissions(101), "committed-scope-closed");
            IMissionDaoTransaction rolledBack = null;
            Expect<ExpectedRollbackException>(() => dao.Execute<int>(101, tx =>
            {
                rolledBack = tx;
                throw new ExpectedRollbackException();
            }), "scope-rollback-injected");
            Expect<InvalidOperationException>(() => rolledBack.GetMission(Key("dao.lifecycle")), "rolled-back-scope-closed");
            Expect<InvalidOperationException>(() => dao.Execute(101, tx => tx.GetMissions(102)), "cross-character-read");
            Expect<InvalidOperationException>(() => dao.Execute(101, tx => tx.GetAccountFlag("mission_account", "x")),
                "account-scope-required");
            Expect<InvalidOperationException>(() => dao.Execute(101, "mission_account", tx => tx.GetAccountFlag("other", "x")),
                "cross-account-read");
        }

        private static void ValidateCaughtWriteFailure(IMissionDao dao)
        {
            MissionStateData pending = NewMission("hardening.caught-error");
            Expect<InvalidOperationException>(() => dao.Execute(101, tx =>
            {
                tx.SaveMission(Key(pending.QuestId), pending);
                try { tx.SaveMission(Key("dao.lifecycle"), NewMission("dao.lifecycle")); }
                catch (MySqlException) { } // A caller cannot turn a failed write into a partial commit.
                return true;
            }), "caught-sql-failure-prevents-commit");
            Require(dao.GetMission(Key(pending.QuestId)) == null && pending.Version == 0, "caught-sql-failure-rolls-back");
        }

        private static MissionStatMutationData Stat(int id, long delta)
        {
            return new MissionStatMutationData
            {
                StatIdentityType = 50000, StatId = id, Kind = MissionStatMutationKind.AddClamped,
                Value = delta, MinimumValue = 0, MaximumValue = int.MaxValue
            };
        }

        private static long Scalar(string connectionString, string sql)
        {
            using (MySqlConnection connection = Open(connectionString))
            using (var command = new MySqlCommand(sql, connection))
            {
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static void ValidateInvalidStatBatch(IMissionDao dao, string connectionString)
        {
            Save(dao, NewMission("hardening.invalid-stat", MissionLifecycleState.Completed));
            var key = new MissionRewardKeyData(Key("hardening.invalid-stat"), "invalid");
            Expect<InvalidOperationException>(() => dao.Execute(101, tx =>
            {
                try { tx.TryApplyCharacterStatReward(key, "stats", new[] { Stat(901, 7), Stat(901, 8) }, null, 200); }
                catch (InvalidOperationException) { }
                return true;
            }), "duplicate-stat-cannot-commit");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM stats WHERE Instance=101 AND StatId=901") == 0,
                "invalid-batch-no-stat-write");
            Require(dao.Execute(101, tx => tx.GetReward(key)) == null, "invalid-batch-no-ledger");
            Expect<ArgumentNullException>(() => dao.Execute(101, tx => tx.TryApplyCharacterStatReward(
                key, "stats", new[] { Stat(901, 7), null }, null, 200)), "null-stat-rejected-before-writes");
        }

        private static void ValidateObservationErrors(IMissionDao dao, string connectionString)
        {
            // latin1 schema cannot represent this value. INSERT IGNORE used to turn
            // the conversion failure into a warning and persist a damaged identity.
            Expect<MySqlException>(() => dao.Execute(101, tx => tx.TryAddObservation(new MissionObjectiveObservationData
            {
                CharacterId = 101, QuestId = "dao.lifecycle", ObjectiveId = "objective.one",
                ObservationKey = "unrepresentable-\ud83d\ude00", EventType = "Kill", ObservedAtUtcTicks = 200
            })), "observation-conversion-failure-surfaces");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionobjectiveobservations WHERE ObservationKey LIKE 'unrepresentable-%'") == 0,
                "no-damaged-observation-insert");
        }

        private static void ValidateClosedConnection(string connectionString)
        {
            MySqlConnection supplied = null;
            var closedDao = new MySqlMissionDao(() => supplied = new MySqlConnection(connectionString));
            Require(closedDao.Execute(101, tx => tx.GetMissions(101)).Count > 0, "closed-factory-execute");
            Require(supplied.State == ConnectionState.Closed, "connection-disposed-after-execute");
            Require(closedDao.ReadCharacter(101).Missions.Count > 0, "closed-factory-snapshot");
            Require(supplied.State == ConnectionState.Closed, "connection-disposed-after-read");
            Expect<InvalidOperationException>(() => new MySqlMissionDao(() => null).GetMissions(101), "null-connection-rejected");
        }

        private static void ValidateOriginalFailure()
        {
            var connection = new FailingRollbackConnection();
            var original = new ExpectedRollbackException();
            try
            {
                new MySqlMissionDao(() => connection).Execute<int>(101, tx => { throw original; });
                Require(false, "original-exception-missing");
            }
            catch (ExpectedRollbackException actual)
            {
                Require(ReferenceEquals(original, actual), "original-failure-not-masked-by-rollback");
                Require(ReferenceEquals(connection.Transaction.Failure, actual.Data["MissionDao.RollbackFailure"]),
                    "rollback-failure-remains-inspectable");
            }
            Require(connection.Disposed && connection.Transaction.Disposed, "failed-rollback-disposes-resources");
        }

        private static void ValidateFeeInput(IMissionDao dao, string connectionString)
        {
            foreach (string batch in new[] { "bad;batch", "   ", new string('x', 97) })
            {
                Require(dao.TryChargeRollFee(new MissionRollFeeRequest
                {
                    CharacterType = 50000, CharacterId = 102, BatchIdentity = batch, Fee = 5, AppliedAtUtcTicks = 201
                }).Status == MissionRollFeeStatus.Conflict, "invalid-fee-key-rejected");
            }
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=102 AND Type=50000 AND StatId=61") == 95,
                "invalid-fee-key-does-not-debit");
            Require(dao.TryChargeRollFee(new MissionRollFeeRequest
            {
                CharacterType = 50000, CharacterId = 102, BatchIdentity = "batch-double-submit", Fee = 6, AppliedAtUtcTicks = 202
            }).Status == MissionRollFeeStatus.Conflict, "fee-key-different-amount-conflicts");
            Require(dao.TryChargeRollFee(new MissionRollFeeRequest
            {
                CharacterType = 50000, CharacterId = 102, BatchIdentity = "too-expensive", Fee = 999, AppliedAtUtcTicks = 203
            }).Status == MissionRollFeeStatus.InsufficientCredits, "insufficient-fee-no-debit");
        }

        private static void ValidateLifecycleIsolation(IMissionDao dao)
        {
            MissionStateData record = NewMission("hardening.lifecycle", MissionLifecycleState.Offered);
            record.OfferedAtUtcTicks = DateTime.MaxValue.Ticks;
            Save(dao, record);
            MissionStateData stale = record.Clone();
            foreach (MissionLifecycleState state in new[] { MissionLifecycleState.Active, MissionLifecycleState.Completed,
                MissionLifecycleState.Failed, MissionLifecycleState.Abandoned })
            {
                record.State = state;
                record.AcceptedAtUtcTicks = 302;
                record.CompletedAtUtcTicks = 303;
                record.FailedAtUtcTicks = 304;
                record.AbandonedAtUtcTicks = 305;
                Save(dao, record);
                MissionStateData loaded = dao.GetMission(Key(record.QuestId));
                Require(loaded.State == state && loaded.OfferedAtUtcTicks == DateTime.MaxValue.Ticks
                    && loaded.AbandonedAtUtcTicks == 305 && loaded.CurrentStepId == null, "lifecycle-ticks-null-roundtrip");
            }
            Expect<InvalidOperationException>(() => Save(dao, stale), "stale-version-rejected");
            Require(dao.GetMission(Key(record.QuestId)).State == MissionLifecycleState.Abandoned, "stale-write-preserves-state");
            Expect<ArgumentOutOfRangeException>(() => Save(dao, NewMission("hardening.invalid-state", (MissionLifecycleState)99)),
                "undefined-lifecycle-rejected");
            Require(dao.GetMission(new MissionKeyData(102, record.QuestId)) == null, "same-quest-other-character-absent");
            var other = record.Clone(); other.CharacterId = 102; other.Version = 0;
            Save(dao, other);
            Require(dao.GetMission(Key(record.QuestId)).Version == record.Version, "other-character-write-isolated");
        }

        private static void ValidateRewardLeases(IMissionDao dao)
        {
            Save(dao, NewMission("hardening.leases", MissionLifecycleState.Completed));
            var key = new MissionRewardKeyData(Key("hardening.leases"), "item");
            using (var start = new ManualResetEventSlim(false))
            {
                Func<string, Task<MissionRewardClaimResultData>> claim = token => Task.Run(() =>
                {
                    start.Wait();
                    return dao.Execute(101, tx => tx.TryClaimReward(key, "item", token, 400, 410));
                });
                Task<MissionRewardClaimResultData> first = claim("one"), second = claim("two");
                start.Set(); Task.WaitAll(first, second);
                Require(new[] { first.Result, second.Result }.Count(x => x.Status == MissionRewardClaimStatus.Claimed) == 1,
                    "concurrent-claim-one-winner");
                Require(new[] { first.Result, second.Result }.Count(x => x.Status == MissionRewardClaimStatus.Busy) == 1,
                    "concurrent-claim-other-busy");
                MissionRewardStageData old = first.Result.Stage;
                MissionRewardClaimResultData reclaim = dao.Execute(101, tx => tx.TryClaimReward(key, "item", "renewed", 410, 420));
                Require(reclaim.Status == MissionRewardClaimStatus.Claimed && reclaim.Stage.Attempts == 2, "expired-claim-retry");
                Require(!dao.Execute(101, tx =>
                {
                    MissionRewardStageData stage;
                    return tx.TryMarkRewardApplied(key, old.ClaimToken, old.Version, "old", 411, out stage);
                }), "stale-claim-token-rejected");
                Require(dao.Execute(101, tx =>
                {
                    MissionRewardStageData stage;
                    return tx.TryMarkRewardFailed(key, "renewed", reclaim.Stage.Version, "retryable", 412, out stage);
                }), "reward-failure-durable");
                MissionRewardClaimResultData retry = dao.Execute(101, tx => tx.TryClaimReward(key, "item", "third", 413, 423));
                Require(retry.Status == MissionRewardClaimStatus.Claimed && retry.Stage.Attempts == 3, "failed-claim-retry");
                Require(dao.Execute(101, tx =>
                {
                    MissionRewardStageData stage;
                    return tx.TryMarkRewardApplied(key, "third", retry.Stage.Version, null, 414, out stage);
                }), "reward-retry-applied");
                Require(dao.Execute(101, tx => tx.TryClaimReward(key, "item", "fourth", 415, 425)).Status
                    == MissionRewardClaimStatus.AlreadyApplied, "applied-reward-never-reclaimed");
            }
        }

        private static void ValidateAtomicRewardRetry(IMissionDao dao, string connectionString)
        {
            Save(dao, NewMission("hardening.atomic", MissionLifecycleState.Completed));
            var key = new MissionRewardKeyData(Key("hardening.atomic"), "stats");
            var mutations = new[] { Stat(902, 7), Stat(903, long.MaxValue) };
            Expect<ExpectedRollbackException>(() => dao.Execute<int>(101, tx =>
            {
                tx.TryApplyCharacterStatReward(key, "stats", mutations, null, 500);
                throw new ExpectedRollbackException();
            }), "stat-ledger-rollback-injected");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM stats WHERE Instance=101 AND StatId IN (902,903)") == 0,
                "all-stats-rolled-back");
            Require(dao.Execute(101, tx => tx.GetReward(key)) == null, "atomic-ledger-rolled-back");
            using (var start = new ManualResetEventSlim(false))
            {
                Func<MissionAtomicStatRewardResultData> apply = () =>
                {
                    start.Wait();
                    return dao.Execute(101, tx => tx.TryApplyCharacterStatReward(key, "stats", mutations, null, 501));
                };
                var first = Task.Run(apply); var second = Task.Run(apply);
                start.Set(); Task.WaitAll(first, second);
                Require(new[] { first.Result, second.Result }.Count(x => x.Status == MissionAtomicRewardStatus.Applied) == 1,
                    "concurrent-stat-reward-once");
                Require(new[] { first.Result, second.Result }.Count(x => x.Status == MissionAtomicRewardStatus.AlreadyApplied) == 1,
                    "concurrent-stat-reward-replay");
            }
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=101 AND StatId=902") == 7,
                "stat-reward-not-doubled");
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=101 AND StatId=903") == int.MaxValue,
                "stat-overflow-clamped");
        }

        private static void ValidateSnapshotOrdering(IMissionDao dao)
        {
            MissionCharacterSnapshotData snapshot = dao.ReadCharacter(101);
            string[] ids = snapshot.Missions.Select(x => x.QuestId).ToArray();
            Require(ids.SequenceEqual(ids.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), "snapshot-deterministic-order");
            snapshot.Missions[0].CurrentStepId = "client-mutated";
            Require(dao.GetMission(Key(snapshot.Missions[0].QuestId)).CurrentStepId != "client-mutated", "snapshot-detached");
            Require(dao.GetAccountFlags("mission_account").Count >= 2, "account-flags-buffered-read");
            Require(dao.GetMissions(9999).Count == 0, "empty-is-not-failure");
        }

        private static void ValidateMutationCutpoints(IMissionDao dao, string connectionString)
        {
            for (int cutoff = 1; cutoff <= 7; cutoff++)
            {
                string quest = "hardening.cut." + cutoff;
                var missionKey = Key(quest);
                Action<IMissionDaoTransaction>[] writes =
                {
                    tx => tx.SaveMission(missionKey, NewMission(quest, MissionLifecycleState.Completed)),
                    tx => tx.SaveObjective(new MissionObjectiveKeyData(missionKey, "one"),
                        new MissionObjectiveProgressData { CharacterId = 101, QuestId = quest, ObjectiveId = "one", RequiredCount = 1 }),
                    tx => tx.TryAddObservation(new MissionObjectiveObservationData
                        { CharacterId = 101, QuestId = quest, ObjectiveId = "one", ObservationKey = "seen", EventType = "Kill", ObservedAtUtcTicks = 600 }),
                    tx => tx.SaveFlag(missionKey, new MissionFlagData { CharacterId = 101, QuestId = quest, FlagKey = "flag" }),
                    tx => tx.SaveAccountFlag("mission_account", new MissionAccountFlagData { AccountKey = "mission_account", FlagKey = quest }),
                    tx => tx.TryClaimReward(new MissionRewardKeyData(missionKey, "item"), "item", "claim", 600, 610),
                    tx => tx.TryApplyCharacterStatReward(new MissionRewardKeyData(missionKey, "stat"), "stats", new[] { Stat(904, 7) }, null, 600)
                };
                Expect<ExpectedRollbackException>(() => dao.Execute<int>(101, "mission_account", tx =>
                {
                    for (int index = 0; index < cutoff; index++) writes[index](tx);
                    throw new ExpectedRollbackException();
                }), "mutation-cutpoint-injected-" + cutoff);
                MissionCharacterSnapshotData snapshot = dao.ReadCharacter(101);
                Require(!snapshot.Missions.Any(x => x.QuestId == quest)
                    && !snapshot.Objectives.Any(x => x.QuestId == quest)
                    && !snapshot.Flags.Any(x => x.QuestId == quest)
                    && !snapshot.Rewards.Any(x => x.QuestId == quest), "mutation-cutpoint-snapshot-rolled-back-" + cutoff);
                Require(dao.GetAccountFlag("mission_account", quest) == null, "mutation-cutpoint-account-rolled-back-" + cutoff);
                Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionobjectiveobservations WHERE QuestId LIKE 'hardening.cut.%'") == 0,
                    "mutation-cutpoint-observation-rolled-back-" + cutoff);
                Require(Scalar(connectionString, "SELECT COUNT(*) FROM stats WHERE Instance=101 AND StatId=904") == 0,
                    "mutation-cutpoint-stat-rolled-back-" + cutoff);
            }
        }

        private static void ValidateLedgerWriteFailure(IMissionDao dao, string connectionString)
        {
            Save(dao, NewMission("hardening.bad-ledger", MissionLifecycleState.Completed));
            var key = new MissionRewardKeyData(Key("hardening.bad-ledger"), "stats");
            bool ledgerFailed = false;
            Expect<InvalidOperationException>(() => dao.Execute(101, tx =>
            {
                try
                {
                    // Stats are written first. The ledger effect then fails latin1
                    // conversion, exercising a real SQL failure after the stat write.
                    tx.TryApplyCharacterStatReward(key, "stats", new[] { Stat(905, 9) }, "\ud83d\ude00", 700);
                }
                catch (MySqlException) { ledgerFailed = true; }
                return true;
            }), "caught-ledger-failure-prevents-commit");
            Require(ledgerFailed, "ledger-sql-failure-reached");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM stats WHERE Instance=101 AND StatId=905") == 0,
                "ledger-failure-rolls-back-stat-write");
            Require(dao.Execute(101, tx => tx.GetReward(key)) == null, "failed-ledger-absent");
        }

        private static void ValidateAcceptanceFailures(IMissionDao dao, string connectionString)
        {
            var fresh = new MySqlMissionDao(() => new MySqlConnection(connectionString));
            MissionStateData parent = NewMission("acceptance.child-failure");
            var child = new MissionObjectiveProgressData
            {
                CharacterId = 101, QuestId = parent.QuestId, ObjectiveId = "bad-\ud83d\ude00", RequiredCount = 2
            };
            bool childFailed = false;
            Expect<InvalidOperationException>(() => dao.Execute(101, tx =>
            {
                tx.SaveMission(Key(parent.QuestId), parent);
                try { tx.SaveObjective(new MissionObjectiveKeyData(Key(parent.QuestId), child.ObjectiveId), child); }
                catch (MySqlException) { childFailed = true; }
                return true;
            }), "caught-child-failure-prevents-success");
            Require(childFailed, "child-provider-write-failure-reached");
            Require(parent.Version == 0 && child.Version == 0, "child-failure-restores-versions");
            Require(fresh.GetMission(Key(parent.QuestId)) == null, "child-failure-parent-not-committed");
            Require(Scalar(connectionString,
                "SELECT COUNT(*) FROM missionobjectiveprogress WHERE CharacterId=101 AND QuestId='acceptance.child-failure'") == 0,
                "child-failure-child-not-committed");

            MissionStateData cancelled = NewMission("acceptance.cancelled");
            var objective = new MissionObjectiveProgressData
            {
                CharacterId = 101, QuestId = cancelled.QuestId, ObjectiveId = "one", RequiredCount = 2
            };
            using (var cancellation = new CancellationTokenSource())
            {
                Expect<OperationCanceledException>(() => dao.Execute(101, tx =>
                {
                    tx.SaveMission(Key(cancelled.QuestId), cancelled);
                    tx.SaveObjective(new MissionObjectiveKeyData(Key(cancelled.QuestId), "one"), objective);
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                    return true;
                }), "callback-cancellation-propagates");
            }
            Require(cancelled.Version == 0 && objective.Version == 0, "cancellation-restores-versions");
            Require(fresh.GetMission(Key(cancelled.QuestId)) == null, "cancellation-parent-absent-fresh-dao");
            Require(Scalar(connectionString,
                "SELECT COUNT(*) FROM missionobjectiveprogress WHERE CharacterId=101 AND QuestId='acceptance.cancelled'") == 0,
                "cancellation-child-absent-fresh-connection");

            // A real provider read error without altering any schema or fixture table.
            var missingTable = new MySqlMissionDao(() =>
            {
                MySqlConnection connection = Open(connectionString);
                try { connection.ChangeDatabase("information_schema"); return connection; }
                catch { connection.Dispose(); throw; }
            });
            Expect<MySqlException>(() => missingTable.GetMissions(101), "read-error-not-empty-success");
            Expect<MySqlException>(() => missingTable.ReadCharacter(101), "snapshot-read-error-not-empty-success");

            MissionStateData pending = NewMission("acceptance.caught-stale");
            MissionStateData stale = fresh.GetMission(Key("dao.lifecycle"));
            stale.Version += 100;
            bool mismatch = false;
            Expect<InvalidOperationException>(() => dao.Execute(101, tx =>
            {
                tx.SaveMission(Key(pending.QuestId), pending);
                try { tx.SaveMission(Key(stale.QuestId), stale); }
                catch (InvalidOperationException) { mismatch = true; }
                return true;
            }), "caught-row-mismatch-prevents-success");
            Require(mismatch, "affected-row-mismatch-reached");
            Require(pending.Version == 0 && fresh.GetMission(Key(pending.QuestId)) == null,
                "affected-row-mismatch-rolls-back-prior-write");
            Require(fresh.GetMission(Key(stale.QuestId)).Version != stale.Version, "affected-row-mismatch-no-stale-overwrite");
        }

        private sealed class FailingRollbackConnection : IDbConnection
        {
            internal bool Disposed;
            internal readonly FailingRollbackTransaction Transaction = new FailingRollbackTransaction();
            public string ConnectionString { get; set; }
            public int ConnectionTimeout { get { return 0; } }
            public string Database { get { return "fake"; } }
            public ConnectionState State { get { return ConnectionState.Open; } }
            public IDbTransaction BeginTransaction() { return this.Transaction; }
            public IDbTransaction BeginTransaction(IsolationLevel level) { return this.Transaction; }
            public void ChangeDatabase(string name) { throw new NotSupportedException(); }
            public void Close() { }
            public IDbCommand CreateCommand() { throw new NotSupportedException(); }
            public void Open() { }
            public void Dispose() { this.Disposed = true; }
        }

        private sealed class FailingRollbackTransaction : IDbTransaction
        {
            internal bool Disposed;
            internal readonly Exception Failure = new InvalidOperationException("injected-rollback-failure");
            public IDbConnection Connection { get { return null; } }
            public IsolationLevel IsolationLevel { get { return IsolationLevel.RepeatableRead; } }
            public void Commit() { }
            public void Rollback() { throw this.Failure; }
            public void Dispose() { this.Disposed = true; }
        }
    }
}
