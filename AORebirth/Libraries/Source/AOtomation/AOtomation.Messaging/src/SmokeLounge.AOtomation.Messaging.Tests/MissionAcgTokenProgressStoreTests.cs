namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgTokenProgressStoreTests
    {
        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "AORebirth-MissionAcgTokenProgressTests-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.temporaryRoot))
            {
                Directory.Delete(this.temporaryRoot, true);
            }
        }

        [TestMethod]
        public void FrozenPercentageFormulaHandlesZeroAndNonzeroTotals()
        {
            MissionAcgInstanceBinding zeroBinding = this.CreateBinding(1, 700001);
            MissionAcgTokenProgressState zero =
                MissionAcgTokenProgressState.Create(
                    zeroBinding,
                    this.CreateObjective(zeroBinding, 1),
                    0,
                    MissionAcgLifecycleState.Active,
                    this.Utc(1));
            Assert.AreEqual(100, zero.InitialPercent);
            Assert.AreEqual(100, zero.Percent);
            AssertInvalidOperation(
                delegate
                {
                    zero.AddValidatedDeath(
                        new MissionAcgIdentityRecord(50000, 810001),
                        zeroBinding.OwnerIdentity,
                        3,
                        1,
                        this.Utc(2));
                });

            MissionAcgInstanceBinding binding = this.CreateBinding(2, 700002);
            MissionAcgTokenProgressState state =
                MissionAcgTokenProgressState.Create(
                    binding,
                    this.CreateObjective(binding, 2),
                    3,
                    MissionAcgLifecycleState.Active,
                    this.Utc(1));
            MissionAcgTokenProgressState validated =
                state.AddValidatedDeath(
                    new MissionAcgIdentityRecord(50000, 810002),
                    binding.OwnerIdentity,
                    4,
                    1,
                    this.Utc(2));
            MissionAcgTokenProgressDeathEvent progressEvent =
                validated.DeathEvents[0];
            Assert.AreEqual(0, validated.AppliedCount);
            Assert.AreEqual(33, progressEvent.PercentAfter);
            MissionAcgTokenProgressState applied =
                validated.AdvanceDeath(
                    progressEvent.EventId,
                    MissionAcgTokenProgressEventPhase.DurablyApplied,
                    this.Utc(3),
                    string.Empty);
            Assert.AreEqual(1, applied.AppliedCount);
            Assert.AreEqual(33, applied.Percent);
            Assert.AreEqual(
                66,
                MissionAcgTokenProgressState.CalculatePercent(2, 3));
            Assert.AreEqual(
                100,
                MissionAcgTokenProgressState.CalculatePercent(3, 3));
        }

        [TestMethod]
        public void DeterministicEventIdentityIncludesMissionSlotAndGeneration()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(3, 700003);
            var runtime = new MissionAcgIdentityRecord(50000, 820003);
            string first =
                MissionAcgTokenProgressState.BuildEventId(
                    binding,
                    runtime,
                    8,
                    1);
            Assert.AreEqual(
                first,
                MissionAcgTokenProgressState.BuildEventId(
                    binding,
                    runtime,
                    8,
                    1));
            Assert.AreNotEqual(
                first,
                MissionAcgTokenProgressState.BuildEventId(
                    binding,
                    runtime,
                    8,
                    2));
            Assert.AreNotEqual(
                first,
                MissionAcgTokenProgressState.BuildEventId(
                    binding,
                    runtime,
                    9,
                    1));
            Assert.AreNotEqual(
                first,
                MissionAcgTokenProgressState.BuildEventId(
                    this.CreateBinding(4, 700004),
                    runtime,
                    8,
                    1));
        }

        [TestMethod]
        public void EventPhasesAndAggregateAdvanceExactlyOnce()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(5, 700005);
            var runtime = new MissionAcgIdentityRecord(50000, 830005);
            MissionAcgTokenProgressState state =
                MissionAcgTokenProgressState.Create(
                    binding,
                    this.CreateObjective(binding, 5),
                    2,
                    MissionAcgLifecycleState.Active,
                    this.Utc(1));
            Assert.AreEqual(
                MissionAcgTokenProgressEventPhase.NotObserved,
                state.PhaseFor(runtime, 12, 1));

            MissionAcgTokenProgressState validated =
                state.AddValidatedDeath(
                    runtime,
                    binding.OwnerIdentity,
                    12,
                    1,
                    this.Utc(2));
            Assert.AreEqual(0, validated.AppliedCount);
            Assert.AreEqual(
                MissionAcgTokenProgressEventPhase.Validated,
                validated.PhaseFor(runtime, 12, 1));
            AssertInvalidOperation(
                delegate
                {
                    validated.AddValidatedDeath(
                        runtime,
                        binding.OwnerIdentity,
                        12,
                        1,
                        this.Utc(3));
                });

            string eventId = validated.DeathEvents[0].EventId;
            MissionAcgTokenProgressState applied =
                validated.AdvanceDeath(
                    eventId,
                    MissionAcgTokenProgressEventPhase.DurablyApplied,
                    this.Utc(3),
                    string.Empty);
            Assert.AreEqual(1, applied.AppliedCount);
            Assert.AreEqual(50, applied.Percent);
            AssertInvalidOperation(
                delegate
                {
                    applied.AdvanceDeath(
                        eventId,
                        MissionAcgTokenProgressEventPhase.DurablyApplied,
                        this.Utc(4),
                        string.Empty);
                });

            MissionAcgTokenProgressState pending =
                applied.AdvanceDeath(
                    eventId,
                    MissionAcgTokenProgressEventPhase.ClientUpdatePending,
                    this.Utc(4),
                    string.Empty);
            MissionAcgTokenProgressState sent =
                pending.AdvanceDeath(
                    eventId,
                    MissionAcgTokenProgressEventPhase.ClientUpdateSent,
                    this.Utc(5),
                    string.Empty);
            Assert.IsTrue(sent.DeathEvents[0].IsTerminal);
            Assert.AreEqual(1, sent.AppliedCount);
        }

        [TestMethod]
        public void TerminalFailureRetainsPreOrPostApplicationTruth()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(6, 700006);
            MissionAcgTokenProgressState state =
                MissionAcgTokenProgressState.Create(
                    binding,
                    this.CreateObjective(binding, 6),
                    2,
                    MissionAcgLifecycleState.Active,
                    this.Utc(1));
            MissionAcgTokenProgressState validated =
                state.AddValidatedDeath(
                    new MissionAcgIdentityRecord(50000, 840006),
                    binding.OwnerIdentity,
                    15,
                    1,
                    this.Utc(2));
            MissionAcgTokenProgressState failedBefore =
                validated.AdvanceDeath(
                    validated.DeathEvents[0].EventId,
                    MissionAcgTokenProgressEventPhase.TerminalFailure,
                    this.Utc(3),
                    "validation owner disappeared");
            Assert.AreEqual(0, failedBefore.AppliedCount);
            Assert.IsFalse(failedBefore.DeathEvents[0].WasDurablyApplied);

            MissionAcgTokenProgressState second =
                failedBefore.AddValidatedDeath(
                    new MissionAcgIdentityRecord(50000, 840007),
                    binding.OwnerIdentity,
                    16,
                    1,
                    this.Utc(4));
            string secondId = second.DeathEvents[1].EventId;
            MissionAcgTokenProgressState applied =
                second.AdvanceDeath(
                    secondId,
                    MissionAcgTokenProgressEventPhase.DurablyApplied,
                    this.Utc(5),
                    string.Empty);
            MissionAcgTokenProgressState failedAfter =
                applied.AdvanceDeath(
                    secondId,
                    MissionAcgTokenProgressEventPhase.TerminalFailure,
                    this.Utc(6),
                    "client send unavailable");
            Assert.AreEqual(1, failedAfter.AppliedCount);
            Assert.IsTrue(failedAfter.DeathEvents[1].WasDurablyApplied);
            Assert.AreEqual(
                "client send unavailable",
                failedAfter.DeathEvents[1].LastFailure);
        }

        [TestMethod]
        public void LifecycleMirrorsMissionTransitionsAndRetainsTerminalReason()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(7, 700007);
            MissionAcgTokenProgressState accepted =
                MissionAcgTokenProgressState.Create(
                    binding,
                    this.CreateObjective(binding, 7),
                    1,
                    MissionAcgLifecycleState.Accepted,
                    this.Utc(1));
            MissionAcgTokenProgressState completionStarted =
                accepted.WithLifecycle(
                    MissionAcgLifecycleState.CompletionStarted,
                    this.Utc(2),
                    string.Empty);
            MissionAcgTokenProgressState completed =
                completionStarted.WithLifecycle(
                    MissionAcgLifecycleState.Completed,
                    this.Utc(3),
                    string.Empty);
            MissionAcgTokenProgressState cleaned =
                completed.WithLifecycle(
                    MissionAcgLifecycleState.Cleaned,
                    this.Utc(4),
                    string.Empty);
            Assert.AreEqual(
                MissionAcgLifecycleState.Completed,
                cleaned.TerminalReason);
            Assert.IsFalse(cleaned.CanAcceptDeaths);
            Assert.IsFalse(
                MissionAcgTokenProgressState.CanTransition(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgLifecycleState.Invalid));

            MissionAcgTokenProgressState rollback =
                MissionAcgTokenProgressState.Create(
                    this.CreateBinding(8, 700008),
                    this.CreateObjective(this.CreateBinding(8, 700008), 8),
                    0,
                    MissionAcgLifecycleState.Reserved,
                    this.Utc(1))
                    .WithLifecycle(
                        MissionAcgLifecycleState.Cleaned,
                        this.Utc(2),
                        string.Empty);
            Assert.AreEqual(0, (int)rollback.TerminalReason);
        }

        [TestMethod]
        public void InvalidMigrationAuditRequiresReasonWithoutFabricatedEvent()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(9, 700009);
            MissionAcgTokenProgressState invalid =
                MissionAcgTokenProgressState.CreateInvalid(
                    binding,
                    this.CreateObjective(binding, 9),
                    4,
                    "legacy ambient deaths exist without durable event history",
                    this.Utc(1));
            Assert.AreEqual(MissionAcgLifecycleState.Invalid, invalid.Lifecycle);
            Assert.AreEqual(
                MissionAcgLifecycleState.Invalid,
                invalid.TerminalReason);
            Assert.AreEqual(0, invalid.DeathEvents.Count);
            StringAssert.Contains(
                invalid.LifecycleDiagnostic,
                "without durable event history");
        }

        [TestMethod]
        public void AtomicRoundTripPreservesOwnershipProgressAndPendingSend()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(10, 700010);
            MissionAcgTokenProgressState state =
                this.AppliedPendingState(binding, 10);
            MissionAcgTokenProgressStore store = this.CreateStore();
            MissionAcgTokenProgressRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(state, out persisted, out failure),
                failure);

            File.WriteAllText(
                Path.Combine(
                    store.DirectoryPath,
                    "ignored.token-progress.tmp"),
                "partial");
            MissionAcgTokenProgressLoadResult loaded = store.LoadAll();
            Assert.IsTrue(
                loaded.IsValid,
                string.Join("|", loaded.Diagnostics));
            Assert.AreEqual(1, loaded.Records.Count);
            MissionAcgTokenProgressState roundTrip =
                loaded.Records[0].State;
            Assert.AreEqual(state.AppliedCount, roundTrip.AppliedCount);
            Assert.AreEqual(state.Percent, roundTrip.Percent);
            Assert.AreEqual(
                state.DeathEvents[0].EventId,
                roundTrip.DeathEvents[0].EventId);
            Assert.AreEqual(
                MissionAcgTokenProgressEventPhase.ClientUpdatePending,
                roundTrip.DeathEvents[0].Phase);
            Assert.AreEqual(10, roundTrip.DeathEvents[0].CapturedSlot);
            Assert.AreEqual(2, roundTrip.DeathEvents[0].SpawnGeneration);
            Assert.IsTrue(
                roundTrip.Matches(
                    binding,
                    this.CreateObjective(binding, 10),
                    out failure),
                failure);
        }

        [TestMethod]
        public void DuplicateAcceptedQuestAndReplacementRegressionFailClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(11, 700011);
            MissionAcgTokenProgressState state =
                this.AppliedPendingState(binding, 11);
            MissionAcgTokenProgressStore store = this.CreateStore();
            MissionAcgTokenProgressRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(state, out persisted, out failure),
                failure);
            Assert.IsFalse(
                store.TryCreate(state, out persisted, out failure));
            StringAssert.Contains(failure, "Duplicate");

            MissionAcgTokenProgressRecord loaded;
            Assert.IsTrue(
                store.TryLoad(
                    binding.AcceptedQuestIdentity,
                    out loaded,
                    out failure),
                failure);
            MissionAcgTokenProgressState sent =
                loaded.State.AdvanceDeath(
                    loaded.State.DeathEvents[0].EventId,
                    MissionAcgTokenProgressEventPhase.ClientUpdateSent,
                    this.Utc(6),
                    string.Empty);
            MissionAcgTokenProgressRecord replaced;
            Assert.IsTrue(
                store.TryReplace(
                    loaded.WithState(sent),
                    out replaced,
                    out failure),
                failure);
            Assert.IsFalse(
                store.TryReplace(loaded, out persisted, out failure));
            StringAssert.Contains(failure, "regress");
        }

        [TestMethod]
        public void DuplicateSidecarUnknownVersionAndTruncationFailClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(12, 700012);
            MissionAcgTokenProgressRecord persisted =
                this.Persist(this.AppliedPendingState(binding, 12));
            File.Copy(
                persisted.RecordPath,
                Path.Combine(
                    this.CreateStore().DirectoryPath,
                    "duplicate.token-progress"));
            MissionAcgTokenProgressLoadResult duplicate =
                this.CreateStore().LoadAll();
            Assert.IsFalse(duplicate.IsValid);
            StringAssert.Contains(
                string.Join("|", duplicate.Diagnostics),
                "duplicate token-progress accepted quest id");

            Directory.Delete(this.CreateStore().DirectoryPath, true);
            persisted = this.Persist(this.AppliedPendingState(binding, 12));
            RewriteField(
                persisted.RecordPath,
                "FormatVersion",
                "999",
                true);
            this.AssertLoadFailure("Unknown token-progress format version");

            Directory.Delete(this.CreateStore().DirectoryPath, true);
            Directory.CreateDirectory(this.CreateStore().DirectoryPath);
            File.WriteAllText(
                Path.Combine(
                    this.CreateStore().DirectoryPath,
                    "truncated.token-progress"),
                "AORebirth-MissionAcgTokenProgress\r\nFormatVersion=1\r\n",
                new UTF8Encoding(false));
            this.AssertLoadFailure("truncated");
        }

        [TestMethod]
        public void RehashedFormulaAndEventIdentityTamperingFailClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(13, 700013);
            MissionAcgTokenProgressRecord persisted =
                this.Persist(this.AppliedPendingState(binding, 13));
            RewriteField(
                persisted.RecordPath,
                "Event00000001.PercentAfter",
                "99",
                true);
            this.AssertLoadFailure("percentage formula");

            Directory.Delete(this.CreateStore().DirectoryPath, true);
            persisted = this.Persist(this.AppliedPendingState(binding, 13));
            RewriteField(
                persisted.RecordPath,
                "Event00000001.EventIdBase64",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes("fabricated-event")),
                true);
            this.AssertLoadFailure("event id is not deterministic");

            Directory.Delete(this.CreateStore().DirectoryPath, true);
            persisted = this.Persist(this.AppliedPendingState(binding, 13));
            RewriteField(
                persisted.RecordPath,
                "Percent",
                "99",
                false);
            this.AssertLoadFailure("SHA-256 mismatch");
        }

        [TestMethod]
        public void RehashedSoloActorTamperingFailsClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(15, 700015);
            MissionAcgTokenProgressRecord persisted =
                this.Persist(this.AppliedPendingState(binding, 15));
            RewriteField(
                persisted.RecordPath,
                "Event00000001.ActorInstance",
                "999999",
                true);
            this.AssertLoadFailure(
                "actor does not match the explicit solo mission owner");
        }

        [TestMethod]
        public void BindingObjectiveAndTeamOwnershipRemainExact()
        {
            MissionAcgInstanceBinding binding =
                this.CreateBinding(14, 700014, true);
            MissionAcgObjectiveBinding objective =
                this.CreateObjective(binding, 14);
            MissionAcgTokenProgressState state =
                MissionAcgTokenProgressState.Create(
                    binding,
                    objective,
                    1,
                    MissionAcgLifecycleState.Active,
                    this.Utc(1));
            Assert.IsFalse(state.CanAcceptDeaths);
            AssertInvalidOperation(
                delegate
                {
                    state.AddValidatedDeath(
                        new MissionAcgIdentityRecord(50000, 840014),
                        binding.OwnerIdentity,
                        14,
                        1,
                        this.Utc(2));
                });
            MissionAcgTokenProgressRecord persisted = this.Persist(state);
            MissionAcgTokenProgressRecord loaded;
            string failure;
            Assert.IsTrue(
                this.CreateStore().TryLoad(
                    binding.AcceptedQuestIdentity,
                    out loaded,
                    out failure),
                failure);
            Assert.IsFalse(loaded.State.Binding.ExplicitNoTeam);
            Assert.AreEqual(
                binding.TeamIdentity,
                loaded.State.Binding.TeamIdentity);
            Assert.IsTrue(
                loaded.State.Matches(binding, objective, out failure),
                failure);

            MissionAcgInstanceBinding wrong =
                this.CreateBinding(14, 700099, true);
            Assert.IsFalse(
                loaded.State.Matches(
                    wrong,
                    this.CreateObjective(wrong, 14),
                    out failure));
            StringAssert.Contains(failure, "mission binding");
        }

        private MissionAcgTokenProgressState AppliedPendingState(
            MissionAcgInstanceBinding binding,
            int salt)
        {
            MissionAcgTokenProgressState state =
                MissionAcgTokenProgressState.Create(
                    binding,
                    this.CreateObjective(binding, salt),
                    4,
                    MissionAcgLifecycleState.Active,
                    this.Utc(1));
            MissionAcgTokenProgressState validated =
                state.AddValidatedDeath(
                    new MissionAcgIdentityRecord(50000, 900000 + salt),
                    binding.OwnerIdentity,
                    salt,
                    2,
                    this.Utc(2));
            string eventId = validated.DeathEvents[0].EventId;
            return validated.AdvanceDeath(
                    eventId,
                    MissionAcgTokenProgressEventPhase.DurablyApplied,
                    this.Utc(3),
                    string.Empty)
                .AdvanceDeath(
                    eventId,
                    MissionAcgTokenProgressEventPhase.ClientUpdatePending,
                    this.Utc(4),
                    string.Empty);
        }

        private MissionAcgInstanceBinding CreateBinding(
            int salt,
            int livePlayfield)
        {
            return this.CreateBinding(salt, livePlayfield, false);
        }

        private MissionAcgInstanceBinding CreateBinding(
            int salt,
            int livePlayfield,
            bool team)
        {
            DateTime accepted = this.Utc(0).AddMinutes(salt);
            MissionAcgIdentityRecord owner =
                new MissionAcgIdentityRecord(50000, 2000 + salt);
            MissionAcgIdentityRecord teamIdentity =
                team
                    ? new MissionAcgIdentityRecord(50001, 3000 + salt)
                    : null;
            return new MissionAcgInstanceBinding(
                MissionAcgInstanceBinding.CurrentFormatVersion,
                new MissionAcgIdentityRecord(0xD6, 0x51000000 + salt),
                new MissionAcgIdentityRecord(0xDAC3, 0x11000000 + salt),
                owner,
                teamIdentity,
                MissionRollType.FindPerson,
                42,
                4000 + salt,
                new MissionAcgIdentityRecord(0x61, 0x61000000 + salt),
                new MissionAcgIdentityRecord(0x9C50, 710),
                43308,
                27595,
                229.605F,
                6.504F,
                452.042F,
                new MissionAcgIdentityRecord(0xDAC1, 0x2000 + salt),
                "token-progress-test-bundle",
                new string('a', 64),
                new MissionAcgIdentityRecord(0xC79F, 1441805),
                livePlayfield,
                accepted,
                accepted.AddHours(48),
                !team);
        }

        private MissionAcgObjectiveBinding CreateObjective(
            MissionAcgInstanceBinding binding,
            int salt)
        {
            return new MissionAcgObjectiveBinding(
                MissionAcgObjectiveBinding.CurrentFormatVersion,
                binding.AcceptedQuestIdentity,
                binding.OwnerIdentity,
                binding.TeamIdentity,
                binding.ExplicitNoTeam,
                binding.MissionType,
                binding.AllocatedLivePlayfield2,
                binding.SelectedBundleId,
                binding.SelectedBundlePayloadSha256,
                binding.AcgBuildingIdentity,
                salt,
                new MissionAcgIdentityRecord(50000, 600000 + salt),
                new MissionAcgIdentityRecord(50000, 700000 + salt),
                100001 + salt,
                "Objective " + salt.ToString(CultureInfo.InvariantCulture),
                MissionAcgObjectiveInteraction.InfoRequest,
                binding.IssuingTerminalIdentity,
                0,
                0);
        }

        private MissionAcgTokenProgressStore CreateStore()
        {
            return new MissionAcgTokenProgressStore(
                Path.Combine(this.temporaryRoot, "mission-state"));
        }

        private MissionAcgTokenProgressRecord Persist(
            MissionAcgTokenProgressState state)
        {
            MissionAcgTokenProgressRecord persisted;
            string failure;
            Assert.IsTrue(
                this.CreateStore().TryCreate(
                    state,
                    out persisted,
                    out failure),
                failure);
            return persisted;
        }

        private DateTime Utc(int seconds)
        {
            return new DateTime(
                    2026,
                    7,
                    31,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc)
                .AddSeconds(seconds);
        }

        private void AssertLoadFailure(string expected)
        {
            MissionAcgTokenProgressLoadResult loaded =
                this.CreateStore().LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(
                string.Join("|", loaded.Diagnostics),
                expected);
        }

        private static void RewriteField(
            string path,
            string key,
            string value,
            bool recomputeHash)
        {
            string[] lines = File.ReadAllLines(path);
            var fields =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string field = lines[i].Substring(0, separator);
                if (field == "RecordSha256")
                {
                    continue;
                }

                fields[field] =
                    field == key
                        ? value
                        : lines[i].Substring(separator + 1);
            }

            var canonical = new StringBuilder();
            foreach (KeyValuePair<string, string> field in fields)
            {
                canonical.Append(field.Key);
                canonical.Append('=');
                canonical.Append(field.Value);
                canonical.Append("\r\n");
            }

            string oldHash = string.Empty;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(
                        "RecordSha256=",
                        StringComparison.Ordinal))
                {
                    oldHash =
                        lines[i].Substring("RecordSha256=".Length);
                    break;
                }
            }

            string hash =
                recomputeHash
                    ? ComputeSha256(canonical.ToString())
                    : oldHash;
            File.WriteAllText(
                path,
                "AORebirth-MissionAcgTokenProgress\r\n"
                + canonical
                + "RecordSha256="
                + hash
                + "\r\n",
                new UTF8Encoding(false));
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash =
                    sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(
                        hash[i].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static void AssertInvalidOperation(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
