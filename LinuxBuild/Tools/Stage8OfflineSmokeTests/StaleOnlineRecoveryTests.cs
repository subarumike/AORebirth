using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ZoneEngine;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class StaleOnlineRecoveryTests
    {
        public static void Run(string repositoryRoot)
        {
            HealthyZeroRowsContinuesWithoutUpdate();
            StaleRowsAreLoggedAndClearedExactly();
            ProcessGuardBlocksWithoutMutation();
            ListenerGuardBlocksWithoutMutation();
            UpdateFailureBlocksAndRollsBack();
            PostUpdateFailureBlocksAndRollsBack();
            QueryFailureBlocksWithoutMutation();
            UnrelatedCharacterFieldsRemainUnchanged();
            StrictDatabaseValidationContractRemains();
            ServiceRunsRecoveryImmediatelyBeforeValidation(repositoryRoot);
            Console.WriteLine("PASS: guarded ZoneEngine stale Online recovery tests");
        }

        private static void HealthyZeroRowsContinuesWithoutUpdate()
        {
            var store = new FakeStore(new FakeCharacter[0]);
            var runtime = new FakeRuntime(store);

            int result = StaleOnlineRecovery.Execute(runtime, 7501);

            Require(result == 0, "healthy zero-row recovery did not continue");
            Require(store.ClearCalls == 0, "healthy zero-row recovery performed an update");
            Require(store.Committed, "healthy zero-row recovery did not complete verification");
            Require(runtime.AuditContains("staleRows=0"), "healthy zero-row count was not logged");
            Require(runtime.AuditContains("DATABASE_VALIDATION_ALLOWED=YES"), "healthy startup was not allowed");
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

        private static void StrictDatabaseValidationContractRemains()
        {
            string source = File.ReadAllText(
                Path.Combine("..", "AORebirth", "Server", "ZoneEngine", "Program.cs"));
            Require(
                source.Contains("SELECT COUNT(*) FROM characters WHERE Online IS NOT NULL AND Online <> 0"),
                "strict Online validation query changed");
            Require(
                source.Contains("ZoneEngine database readiness requires zero online characters."),
                "strict Online validation failure changed");
        }

        private static void ServiceRunsRecoveryImmediatelyBeforeValidation(string repositoryRoot)
        {
            string unitPath = Path.Combine(
                repositoryRoot,
                "LinuxBuild",
                "deployment",
                "systemd",
                "ao-rebirth-zoneengine.service");
            string[] lines = File.ReadAllLines(unitPath);
            int recoveryIndex = Array.FindIndex(
                lines,
                line => line.Contains("ZoneEngine --recover-stale-online"));
            int validationIndex = Array.FindIndex(
                lines,
                line => line.Contains("ZoneEngine --validate-database"));
            Require(recoveryIndex >= 0, "systemd recovery pre-start command is missing");
            Require(validationIndex == recoveryIndex + 1, "recovery is not immediately before database validation");
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
                return this.ProcessDetected;
            }

            public bool IsPortListening(int port)
            {
                return this.ListenerDetected;
            }

            public IDisposable ReservePort(int port)
            {
                return new NoOpDisposable();
            }

            public IStaleOnlineRecoveryStore OpenStore()
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

        private sealed class FakeStore : IStaleOnlineRecoveryStore
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

            public string DatabaseName
            {
                get { return "aorebirth_test"; }
            }

            public IReadOnlyList<StaleOnlineRecoveryRow> ReadNonzeroRows()
            {
                if (this.QueryFails)
                {
                    throw new InvalidOperationException("query failure");
                }

                return this.Characters
                    .Where(character => character.Online != 0)
                    .OrderBy(character => character.Id)
                    .Select(character => new StaleOnlineRecoveryRow(character.Id, character.Online))
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
