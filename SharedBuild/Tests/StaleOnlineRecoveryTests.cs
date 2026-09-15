using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ZoneEngine;
using AORebirth.Interfaces.Persistence.Characters;

namespace AORebirth.SharedBuild.Stage8OfflineSmokeTests
{
    internal static class StaleOnlineRecoveryTests
    {
        public static void Run(string repositoryRoot)
        {
            HealthyZeroRowsContinuesWithoutUpdate();
            LoginEngineGuardBlocksPendingHandoffWithoutOpeningDatabase();
            OnlineOwnerProcessNamesAreRecognized();
            StaleRowsAreLoggedAndClearedExactly();
            ProcessGuardBlocksWithoutMutation();
            ListenerGuardBlocksWithoutMutation();
            UpdateFailureBlocksAndRollsBack();
            PostUpdateFailureBlocksAndRollsBack();
            QueryFailureBlocksWithoutMutation();
            UnrelatedCharacterFieldsRemainUnchanged();
            SharedActiveNanoSchemaContractRemains(repositoryRoot);
            Console.WriteLine("PASS: guarded ZoneEngine stale Online recovery tests");
        }

        private static void HealthyZeroRowsContinuesWithoutUpdate()
        {
            var store = new FakeStore(new FakeCharacter[0]);
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result == 0, "healthy zero-row recovery did not continue");
            Require(store.ClearCalls == 0, "healthy zero-row recovery performed an update");
            Require(store.CountCalls == 0, "healthy zero-row recovery entered mutation verification");
            Require(!store.Committed, "healthy zero-row recovery committed a mutation transaction");
            Require(runtime.AuditContains("staleRows=0"), "healthy zero-row count was not logged");
            Require(runtime.AuditContains("cleanupRequired=NO"), "healthy zero-row cleanup state was not logged");
            Require(runtime.AuditContains("RECOVERY_ALLOWED=NOT_REQUIRED"), "healthy zero-row recovery was treated as required");
            Require(runtime.AuditContains("DATABASE_VALIDATION_ALLOWED=YES"), "healthy startup was not allowed");
        }

        private static void LoginEngineGuardBlocksPendingHandoffWithoutOpeningDatabase()
        {
            foreach (bool pendingHandoff in new[] { false, true })
            {
                var store = new FakeStore(pendingHandoff
                    ? new[] { new FakeCharacter(42, "PendingHandoff", 1) }
                    : new FakeCharacter[0]);
                var runtime = new FakeRuntime(store) { LoginEngineActive = true };

                int result = StaleOnlineRecovery.Execute(runtime, 7501);

                Require(result != 0, "LoginEngine-active recovery did not fail closed");
                Require(runtime.OpenStoreCalls == 0, "LoginEngine-active recovery opened the database");
                Require(store.ClearCalls == 0, "LoginEngine-active recovery performed an update");
                Require(!pendingHandoff || store.Characters[0].Online == 1, "pending login handoff lost online ownership");
                Require(runtime.AuditContains("processDetected=YES"), "LoginEngine ownership guard was not logged");
                Require(runtime.AuditContains("DATABASE_VALIDATION_ALLOWED=NO"), "LoginEngine-active recovery allowed validation");
            }
        }

        private static void OnlineOwnerProcessNamesAreRecognized()
        {
            var runtimeType = typeof(StaleOnlineRecovery).Assembly.GetType("ZoneEngine.SystemStaleOnlineRecoveryRuntime", true);
            var predicate = runtimeType.GetMethod("IsOnlineOwnerProcessName",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Require(predicate != null, "production process-name ownership predicate is missing");
            foreach (string processName in new[] { "ZoneEngine", "LoginEngine", "loginengine", "LOGINENGINE" })
            {
                Require((bool)predicate.Invoke(null, new object[] { processName }), "online owner process was not guarded: " + processName);
            }
            Require(!(bool)predicate.Invoke(null, new object[] { "UnrelatedProcess" }), "unrelated process was treated as an online owner");
        }

        private static void StaleRowsAreLoggedAndClearedExactly()
        {
            var store = new FakeStore(
                new[]
                {
                    new FakeCharacter(38, "Nemmoburger", 1),
                    new FakeCharacter(39, "Nanotechnica", 1),
                    new FakeCharacter(40, "Offline", 0)
                });
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result == 0, "stale-row recovery did not continue");
            Require(store.Characters.Single(character => character.Id == 38).Online == 0, "character 38 stayed online");
            Require(store.Characters.Single(character => character.Id == 39).Online == 0, "character 39 stayed online");
            Require(store.Characters.Single(character => character.Id == 40).Online == 0, "offline row changed");
            Require(runtime.AuditContains("characterIds=38,39"), "affected IDs were not logged");
            Require(runtime.AuditContains("oldOnlineValues=38:1,39:1"), "old Online values were not logged");
            Require(runtime.AuditContains("cleanupRequired=YES"), "required cleanup state was not logged");
            Require(runtime.AuditContains("rowsUpdated=2"), "updated row count was not logged");
            Require(runtime.AuditContains("postUpdateNonzero=0"), "post-update count was not logged");
        }

        private static void ProcessGuardBlocksWithoutMutation()
        {
            var store = new FakeStore(new[] { new FakeCharacter(1, "Guarded", 1) });
            var runtime = new FakeRuntime(store) { ProcessDetected = true };

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result != 0, "process guard allowed recovery");
            Require(runtime.OpenStoreCalls == 0, "process guard opened the database");
            Require(store.Characters[0].Online == 1, "process guard mutated the database");
            Require(runtime.AuditContains("processDetected=YES"), "process guard result was not logged");
        }

        private static void ListenerGuardBlocksWithoutMutation()
        {
            var store = new FakeStore(new[] { new FakeCharacter(1, "Guarded", 1) });
            var runtime = new FakeRuntime(store) { ListenerDetected = true };

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result != 0, "listener guard allowed recovery");
            Require(runtime.OpenStoreCalls == 0, "listener guard opened the database");
            Require(store.Characters[0].Online == 1, "listener guard mutated the database");
            Require(runtime.AuditContains("port7501ListenerDetected=YES"), "listener guard result was not logged");
        }

        private static void UpdateFailureBlocksAndRollsBack()
        {
            var store = new FakeStore(new[] { new FakeCharacter(1, "Rollback", 1) }) { UpdateFails = true };
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result != 0, "failed update allowed startup");
            Require(store.Characters[0].Online == 1, "failed update was not rolled back");
            Require(!store.Committed, "failed update committed");
        }

        private static void PostUpdateFailureBlocksAndRollsBack()
        {
            var store = new FakeStore(new[] { new FakeCharacter(1, "Verify", 1) })
            {
                ForcePostUpdateNonzero = true
            };
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result != 0, "failed post-update verification allowed startup");
            Require(store.Characters[0].Online == 1, "failed verification was not rolled back");
            Require(!store.Committed, "failed verification committed");
        }

        private static void QueryFailureBlocksWithoutMutation()
        {
            var store = new FakeStore(new[] { new FakeCharacter(1, "Query", 1) }) { QueryFails = true };
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result != 0, "failed query allowed startup");
            Require(store.Characters[0].Online == 1, "failed query mutated a row");
            Require(store.ClearCalls == 0, "failed query reached the update");
            Require(runtime.AuditContains("error=InvalidOperationException"), "internal exception type was not logged");
            Require(runtime.AuditContains("exceptionMessage=\"query failure\""), "internal exception message was not logged");
            Require(runtime.AuditContains("exceptionSource="), "internal exception source was not logged");
            Require(runtime.AuditContains("exceptionLine="), "internal exception line was not logged");
            Require(runtime.AuditContains("exceptionStack="), "internal exception stack was not logged");
            Require(runtime.AuditContains("DATABASE_VALIDATION_ALLOWED=NO"), "internal exception did not fail closed");
        }

        private static void UnrelatedCharacterFieldsRemainUnchanged()
        {
            var character = new FakeCharacter(1, "StableName", 1) { UnrelatedValue = 8675309 };
            var store = new FakeStore(new[] { character });
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result == 0, "unrelated-field test recovery failed");
            Require(character.Name == "StableName", "character name changed");
            Require(character.UnrelatedValue == 8675309, "unrelated character field changed");
        }

        private static void SharedActiveNanoSchemaContractRemains(string repositoryRoot)
        {
            string activeNanoSchema = File.ReadAllText(
                Stage8RepositoryRootResolver.ResolveRequiredFile(
                    repositoryRoot,
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Database",
                    "SqlTables",
                    "charactersactivenanos.sql"));
            Require(
                activeNanoSchema.Contains("`NanoInstance` int(32) NOT NULL DEFAULT 0")
                && activeNanoSchema.Contains("`DurationCentiseconds` int(32) NOT NULL DEFAULT 0")
                && activeNanoSchema.Contains("`ExpiresAtUtcTicks` bigint(20) NOT NULL DEFAULT 0"),
                "governed base schema lacks the authoritative active-nano persistence cohort");

            string activeNanoAlter = File.ReadAllText(
                Stage8RepositoryRootResolver.ResolveRequiredFile(
                    repositoryRoot,
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Database",
                    "SqlTables",
                    "charactersactivenanos_alter.sql"));
            Require(
                activeNanoAlter.Contains("`NanoInstance` int(32) NOT NULL DEFAULT 0")
                && activeNanoAlter.Contains("`DurationCentiseconds` int(32) NOT NULL DEFAULT 0")
                && activeNanoAlter.Contains("`ExpiresAtUtcTicks` bigint(20) NOT NULL DEFAULT 0"),
                "forward active-nano migration diverges from the governed base schema cohort");

            string databaseProject = File.ReadAllText(
                Stage8RepositoryRootResolver.ResolveRequiredFile(
                    repositoryRoot,
                    "AORebirth",
                    "Libraries",
                    "Source",
                    "AORebirth.Database",
                    "AORebirth.Database.csproj"));
            Require(
                databaseProject.Contains("SqlTables\\charactersactivenanos_alter.sql"),
                "authoritative active-nano forward migration is not packaged as governed database content");

        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeRuntime : IStaleOnlineRecoveryRuntime
        {
            private readonly FakeStore store;
            private readonly List<string> audit = new List<string>();

            public FakeRuntime(FakeStore store)
            {
                this.store = store;
            }

            public bool ProcessDetected { get; set; }

            public bool ListenerDetected { get; set; }

            public bool LoginEngineActive { get; set; }

            public int OpenStoreCalls { get; private set; }

            public DateTime UtcNow
            {
                get { return new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc); }
            }

            public string ExpectedDatabase
            {
                get { return "aorebirth_test"; }
            }

            public IDisposable AcquireProcessLock()
            {
                return new NoOpDisposable();
            }

            public bool IsOtherZoneEngineProcessRunning()
            {
                return this.ProcessDetected || this.LoginEngineActive;
            }

            public bool IsPortListening(int port)
            {
                return this.ListenerDetected;
            }

            public IDisposable ReservePort(int port)
            {
                return new NoOpDisposable();
            }

            public ICharacterDao CreateCharacterDao()
            {
                this.OpenStoreCalls++;
                return this.store;
            }

            public void Audit(string message)
            {
                this.audit.Add(message);
            }

            public bool AuditContains(string value)
            {
                return this.audit.Any(line => line.Contains(value));
            }
        }

        // Orchestrator fixture only. SQL atomicity and ownership are checked against the real DAO separately.
        private sealed class FakeStore : ICharacterDao
        {
            private readonly Dictionary<int, int> originalOnline;
            private bool clearAttempted;

            public FakeStore(IEnumerable<FakeCharacter> characters)
            {
                this.Characters = characters.ToList();
                this.originalOnline = this.Characters.ToDictionary(character => character.Id, character => character.Online);
            }

            public List<FakeCharacter> Characters { get; private set; }

            public bool QueryFails { get; set; }

            public bool UpdateFails { get; set; }

            public bool ForcePostUpdateNonzero { get; set; }

            public bool Committed { get; private set; }

            public int ClearCalls { get; private set; }

            public int CountCalls { get; private set; }

            public string DatabaseName
            {
                get { return "aorebirth_test"; }
            }

            public StaleOnlineRecoveryData RecoverStaleOnline(string expectedDatabase)
            {
                try
                {
                    if (expectedDatabase != this.DatabaseName) throw new InvalidDataException("database mismatch");
                    var rows = this.ReadNonzeroRows();
                    if (rows.Count == 0) return new StaleOnlineRecoveryData(this.DatabaseName, rows, 0, null);
                    int updated = this.ClearRows(rows.Select(row => row.CharacterId).ToArray());
                    long remaining = this.CountNonzeroRows();
                    if (updated != rows.Count || remaining != 0) throw new InvalidDataException("recovery verification failed");
                    this.Commit();
                    return new StaleOnlineRecoveryData(this.DatabaseName, rows, updated, remaining);
                }
                finally { this.Dispose(); }
            }

            public CharacterDirectoryData LoadById(int id) { throw new NotSupportedException(); }
            public CharacterDirectoryData LoadByName(string name) { throw new NotSupportedException(); }
            public IList<CharacterDirectoryData> ListForAccount(string account) { throw new NotSupportedException(); }
            public bool IsOwnedByAccount(string account, uint id) { throw new NotSupportedException(); }
            public int MarkOnline(int id) { throw new NotSupportedException(); }
            public int MarkOffline(int id) { throw new NotSupportedException(); }
            public IList<CharacterDirectoryData> ListLoggedIn() { throw new NotSupportedException(); }

            public IReadOnlyList<StaleOnlineCharacterData> ReadNonzeroRows()
            {
                if (this.QueryFails)
                {
                    throw new InvalidOperationException("query failure");
                }

                return this.Characters
                    .Where(character => character.Online != 0)
                    .OrderBy(character => character.Id)
                    .Select(character => new StaleOnlineCharacterData(character.Id, character.Online))
                    .ToArray();
            }

            public int ClearRows(IReadOnlyList<int> characterIds)
            {
                this.ClearCalls++;
                this.clearAttempted = true;
                if (this.UpdateFails)
                {
                    throw new InvalidOperationException("update failure");
                }

                int updated = 0;
                foreach (FakeCharacter character in this.Characters.Where(
                    character => characterIds.Contains(character.Id) && character.Online != 0))
                {
                    character.Online = 0;
                    updated++;
                }

                return updated;
            }

            public long CountNonzeroRows()
            {
                this.CountCalls++;
                if (this.ForcePostUpdateNonzero && this.clearAttempted)
                {
                    return 1;
                }

                return this.Characters.LongCount(character => character.Online != 0);
            }

            public void Commit()
            {
                this.Committed = true;
            }

            public void Dispose()
            {
                if (this.Committed)
                {
                    return;
                }

                foreach (FakeCharacter character in this.Characters)
                {
                    character.Online = this.originalOnline[character.Id];
                }
            }
        }

        private sealed class FakeCharacter
        {
            public FakeCharacter(int id, string name, int online)
            {
                this.Id = id;
                this.Name = name;
                this.Online = online;
            }

            public int Id { get; private set; }

            public string Name { get; private set; }

            public int Online { get; set; }

            public int UnrelatedValue { get; set; }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
