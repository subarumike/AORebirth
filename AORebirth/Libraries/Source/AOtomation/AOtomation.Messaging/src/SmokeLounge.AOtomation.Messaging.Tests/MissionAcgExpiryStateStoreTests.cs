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
    public class MissionAcgExpiryStateStoreTests
    {
        private string temporaryRoot;

        private MissionAcgLayoutCatalog catalog;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-acg-expiry-tests-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
            this.catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
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
        public void CreateFreezesExactBindingIdentityAndDetectsAtBoundary()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(1, 700001);
            MissionAcgExpiryState state =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc);
            string failure;
            Assert.IsTrue(state.MatchesBinding(binding, out failure), failure);
            Assert.AreEqual(
                MissionAcgExpiryCheckpoint.ExpiryDetected,
                state.Checkpoints);
            Assert.AreEqual(MissionAcgExpiryStatus.InProgress, state.Status);
            Assert.AreEqual(binding.ExpiryUtc, state.FirstDetectedUtc);
        }

        [TestMethod]
        public void CheckpointsAndStatusAdvanceMonotonically()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(2, 700002);
            MissionAcgExpiryState state =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc)
                    .Advance(
                        MissionAcgExpiryCheckpoint.CleanupStarted
                        | MissionAcgExpiryCheckpoint.InteractionsBlocked,
                        MissionAcgExpiryStatus.RetryPending,
                        binding.ExpiryUtc.AddSeconds(1),
                        "inventory owner offline");
            Assert.AreEqual(1, state.RetryCount);
            Assert.IsTrue(
                state.HasCheckpoint(MissionAcgExpiryCheckpoint.InteractionsBlocked));
            AssertInvalidOperation(
                delegate
                {
                    state.Advance(
                        MissionAcgExpiryCheckpoint.None,
                        MissionAcgExpiryStatus.InProgress,
                        binding.ExpiryUtc.AddSeconds(2),
                        string.Empty);
                });
        }

        [TestMethod]
        public void MissingCheckpointPredecessorFailsClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(3, 700003);
            MissionAcgExpiryState state =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc);
            AssertArgument(
                delegate
                {
                    state.Advance(
                        MissionAcgExpiryCheckpoint.NpcsRemoved,
                        MissionAcgExpiryStatus.InProgress,
                        binding.ExpiryUtc.AddSeconds(1),
                        string.Empty);
                });
        }

        [TestMethod]
        public void CompleteRequiresEveryVerifiedReleaseCheckpoint()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(4, 700004);
            MissionAcgExpiryState state =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc);
            AssertArgument(
                delegate
                {
                    state.Advance(
                        MissionAcgExpiryCheckpoint.CleanupComplete,
                        MissionAcgExpiryStatus.Complete,
                        binding.ExpiryUtc.AddSeconds(1),
                        string.Empty);
                });

            MissionAcgExpiryState complete =
                state.Advance(
                    AllCleanupCheckpoints(),
                    MissionAcgExpiryStatus.Complete,
                    binding.ExpiryUtc.AddSeconds(2),
                    string.Empty);
            Assert.IsTrue(complete.IsComplete);
            Assert.IsTrue(
                complete.HasCheckpoint(
                    MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed));
        }

        [TestMethod]
        public void ExpiryBoundaryAndCompletionRacePolicyAreDeterministic()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(5, 700005);
            DateTime before = binding.ExpiryUtc.AddTicks(-1);
            DateTime equal = binding.ExpiryUtc;
            Assert.IsFalse(MissionAcgExpiryPolicy.IsDue(before, binding.ExpiryUtc));
            Assert.IsTrue(MissionAcgExpiryPolicy.IsDue(equal, binding.ExpiryUtc));
            Assert.IsTrue(
                MissionAcgExpiryPolicy.CanBeginCompletion(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCompletionPhase.RewardCalculationFrozen,
                    null,
                    before,
                    binding.ExpiryUtc));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanBeginCompletion(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCompletionPhase.RewardCalculationFrozen,
                    null,
                    equal,
                    binding.ExpiryUtc));
            Assert.IsTrue(
                MissionAcgExpiryPolicy.CanBeginExpiry(
                    MissionAcgLifecycleState.CompletionStarted,
                    MissionAcgCompletionPhase.RewardCalculationFrozen,
                    false,
                    false,
                    equal,
                    binding.ExpiryUtc));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanBeginExpiry(
                    MissionAcgLifecycleState.CompletionStarted,
                    MissionAcgCompletionPhase.RewardClaimStarted,
                    true,
                    false,
                    equal,
                    binding.ExpiryUtc));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanBeginExpiry(
                    MissionAcgLifecycleState.CompletionStarted,
                    MissionAcgCompletionPhase.RewardCalculationFrozen,
                    true,
                    false,
                    equal,
                    binding.ExpiryUtc),
                "A durable completion claim must defeat a stale pre-claim objective snapshot.");
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanBeginExpiry(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCompletionPhase.None,
                    false,
                    true,
                    equal,
                    binding.ExpiryUtc),
                "A claimed abandonment must defeat a stale expiry snapshot.");
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanBeginAbandonment(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCompletionPhase.None,
                    true,
                    false,
                    false,
                    before,
                    binding.ExpiryUtc),
                "An expiry claim must defeat abandonment before either path mutates cleanup.");
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanBeginAbandonment(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCompletionPhase.ObjectiveVerified,
                    false,
                    true,
                    false,
                    before,
                    binding.ExpiryUtc),
                "A completion-transition lease must defeat abandonment before either path mutates cleanup.");
            Assert.IsTrue(
                MissionAcgExpiryPolicy.CanBeginAbandonment(
                    MissionAcgLifecycleState.Abandoned,
                    MissionAcgCompletionPhase.None,
                    false,
                    false,
                    true,
                    equal,
                    binding.ExpiryUtc),
                "Persisted abandonment ownership must remain resumable after the deadline.");
            Assert.IsTrue(
                MissionAcgExpiryPolicy.CanContinueCompletion(
                    MissionAcgCompletionPhase.RewardClaimStarted,
                    true,
                    null,
                    equal.AddHours(1),
                    binding.ExpiryUtc));
        }

        [TestMethod]
        public void InvalidTimestampFailsClosed()
        {
            AssertArgument(
                delegate
                {
                    MissionAcgExpiryPolicy.IsDue(DateTime.UtcNow, DateTime.MinValue);
                });
        }

        [TestMethod]
        public void StoreRoundTripPreservesHashProtectedState()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(6, 700006);
            MissionAcgExpiryState original =
                MissionAcgExpiryState.Create(
                    binding,
                    binding.ExpiryUtc,
                    true)
                    .Advance(
                        MissionAcgExpiryCheckpoint.CleanupStarted
                        | MissionAcgExpiryCheckpoint.InteractionsBlocked,
                        MissionAcgExpiryStatus.RetryPending,
                        binding.ExpiryUtc.AddSeconds(1),
                        "offline owner = retry");
            MissionAcgExpiryStateStore store = this.CreateStore();
            MissionAcgExpiryRecord persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(original, out persisted, out failure), failure);
            MissionAcgExpiryLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid, string.Join("|", loaded.Diagnostics));
            Assert.AreEqual(1, loaded.Records.Count);
            MissionAcgExpiryState reloaded = loaded.Records[0].State;
            Assert.AreEqual(original.Checkpoints, reloaded.Checkpoints);
            Assert.AreEqual(original.Status, reloaded.Status);
            Assert.AreEqual(original.RetryCount, reloaded.RetryCount);
            Assert.AreEqual(original.LastFailure, reloaded.LastFailure);
            Assert.IsTrue(reloaded.RequiresOwnerReconciliation);
            Assert.IsTrue(reloaded.MatchesBinding(binding, out failure), failure);
        }

        [TestMethod]
        public void AtomicReplacementPreservesMonotonicProgress()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(7, 700007);
            MissionAcgExpiryStateStore store = this.CreateStore();
            MissionAcgExpiryRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(
                    MissionAcgExpiryState.Create(binding, binding.ExpiryUtc),
                    out persisted,
                    out failure),
                failure);
            MissionAcgExpiryState advanced =
                persisted.State.Advance(
                    MissionAcgExpiryCheckpoint.CleanupStarted
                    | MissionAcgExpiryCheckpoint.InteractionsBlocked,
                    MissionAcgExpiryStatus.InProgress,
                    binding.ExpiryUtc.AddSeconds(1),
                    string.Empty);
            MissionAcgExpiryRecord replaced;
            Assert.IsTrue(
                store.TryReplace(
                    persisted.WithState(advanced),
                    out replaced,
                    out failure),
                failure);
            File.WriteAllText(
                Path.Combine(store.DirectoryPath, "partial.expiry.tmp"),
                "partial");
            MissionAcgExpiryLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid, string.Join("|", loaded.Diagnostics));
            Assert.AreEqual(1, loaded.Records.Count);
            Assert.AreEqual(advanced.Checkpoints, loaded.Records[0].State.Checkpoints);
        }

        [TestMethod]
        public void ReplacementRejectsCheckpointRegression()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(8, 700008);
            MissionAcgExpiryStateStore store = this.CreateStore();
            MissionAcgExpiryState initial =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc);
            MissionAcgExpiryRecord persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(initial, out persisted, out failure), failure);
            MissionAcgExpiryState advanced =
                initial.Advance(
                    MissionAcgExpiryCheckpoint.CleanupStarted
                    | MissionAcgExpiryCheckpoint.InteractionsBlocked,
                    MissionAcgExpiryStatus.InProgress,
                    binding.ExpiryUtc.AddSeconds(1),
                    string.Empty);
            MissionAcgExpiryRecord replaced;
            Assert.IsTrue(
                store.TryReplace(
                    persisted.WithState(advanced),
                    out replaced,
                    out failure),
                failure);
            Assert.IsFalse(
                store.TryReplace(
                    replaced.WithState(initial),
                    out persisted,
                    out failure));
            StringAssert.Contains(failure, "cannot regress");
        }

        [TestMethod]
        public void DuplicateAcceptedQuestFailsClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(9, 700009);
            MissionAcgExpiryStateStore store = this.CreateStore();
            MissionAcgExpiryRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(
                    MissionAcgExpiryState.Create(binding, binding.ExpiryUtc),
                    out persisted,
                    out failure),
                failure);
            File.Copy(
                persisted.RecordPath,
                Path.Combine(store.DirectoryPath, "duplicate.expiry"));
            MissionAcgExpiryLoadResult loaded = store.LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(
                string.Join("|", loaded.Diagnostics),
                "duplicate accepted quest id");
        }

        [TestMethod]
        public void UnknownVersionTruncationAndIntegrityMismatchFailClosed()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(10, 700010);
            string path = this.CreatePersistedPath(binding);
            RewriteField(path, "FormatVersion", "999");
            AssertLoadFailure("Unknown expiry format version");

            Directory.Delete(this.CreateStore().DirectoryPath, true);
            path = this.CreatePersistedPath(binding);
            string text = File.ReadAllText(path);
            File.WriteAllText(
                path,
                text.Replace("Status=1", "Status=2"),
                new UTF8Encoding(false));
            AssertLoadFailure("SHA-256 mismatch");

            Directory.Delete(this.CreateStore().DirectoryPath, true);
            Directory.CreateDirectory(this.CreateStore().DirectoryPath);
            File.WriteAllText(
                Path.Combine(this.CreateStore().DirectoryPath, "truncated.expiry"),
                "AORebirth-MissionAcgExpiryState\r\nFormatVersion=1\r\n");
            AssertLoadFailure("truncated");
        }

        [TestMethod]
        public void OrphanAndBindingIdentityMismatchFailClosed()
        {
            MissionAcgInstanceBinding original = this.CreateBinding(11, 700011);
            this.CreatePersistedPath(original);
            MissionAcgExpiryStateStore store = this.CreateStore();
            MissionAcgExpiryLoadResult orphan =
                store.LoadAll(new MissionAcgBindingRecord[0]);
            Assert.IsFalse(orphan.IsValid);
            StringAssert.Contains(string.Join("|", orphan.Diagnostics), "orphan");

            MissionAcgInstanceBinding mismatch = this.CreateBinding(11, 700099);
            var mismatchRecord = new MissionAcgBindingRecord(
                mismatch,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Expired,
                    MissionAcgCleanupState.KeyRemovalPending,
                    mismatch.ExpiryUtc,
                    mismatch.ExpiryUtc),
                string.Empty);
            MissionAcgExpiryLoadResult mismatched =
                store.LoadAll(new[] { mismatchRecord });
            Assert.IsFalse(mismatched.IsValid);
            StringAssert.Contains(
                string.Join("|", mismatched.Diagnostics),
                "does not match");
        }

        [TestMethod]
        public void ReleaseRequiresJournalAndIndependentRuntimeProof()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(12, 700012);
            MissionAcgExpiryState releaseReady =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc)
                    .Advance(
                        AllCleanupCheckpoints()
                        & ~MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted
                        & ~MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed
                        & ~MissionAcgExpiryCheckpoint.CleanupComplete,
                        MissionAcgExpiryStatus.InProgress,
                        binding.ExpiryUtc.AddSeconds(1),
                        string.Empty);
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanReleasePlayfield(
                    releaseReady,
                    false,
                    true,
                    true));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanReleasePlayfield(
                    releaseReady,
                    true,
                    false,
                    true));
            Assert.IsTrue(
                MissionAcgExpiryPolicy.CanReleasePlayfield(
                    releaseReady,
                    true,
                    true,
                    true));
        }

        [TestMethod]
        public void AttemptedReleaseCanRecoverOnlyFromVerifiedTotalAbsence()
        {
            MissionAcgInstanceBinding binding = this.CreateBinding(13, 700013);
            MissionAcgExpiryState attempted =
                MissionAcgExpiryState.Create(binding, binding.ExpiryUtc)
                    .Advance(
                        AllCleanupCheckpoints()
                        & ~MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed
                        & ~MissionAcgExpiryCheckpoint.CleanupComplete,
                        MissionAcgExpiryStatus.InProgress,
                        binding.ExpiryUtc.AddSeconds(1),
                        string.Empty);

            Assert.IsTrue(
                MissionAcgExpiryPolicy.CanConfirmPreviouslyReleasedPlayfield(
                    attempted,
                    true,
                    true,
                    false,
                    false));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanConfirmPreviouslyReleasedPlayfield(
                    attempted,
                    true,
                    true,
                    true,
                    false));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanConfirmPreviouslyReleasedPlayfield(
                    attempted,
                    true,
                    true,
                    false,
                    true));
            Assert.IsFalse(
                MissionAcgExpiryPolicy.CanConfirmPreviouslyReleasedPlayfield(
                    attempted,
                    false,
                    true,
                    false,
                    false));
        }

        private MissionAcgExpiryStateStore CreateStore()
        {
            return new MissionAcgExpiryStateStore(
                Path.Combine(this.temporaryRoot, "mission-state"));
        }

        private string CreatePersistedPath(MissionAcgInstanceBinding binding)
        {
            MissionAcgExpiryRecord persisted;
            string failure;
            Assert.IsTrue(
                this.CreateStore().TryCreate(
                    MissionAcgExpiryState.Create(binding, binding.ExpiryUtc),
                    out persisted,
                    out failure),
                failure);
            return persisted.RecordPath;
        }

        private MissionAcgInstanceBinding CreateBinding(int salt, int livePlayfield)
        {
            var owner = new MissionAcgIdentityRecord(0xC350, 2000 + salt);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(
                        3000 + salt,
                        MissionRollType.FindItem,
                        42,
                        owner));
            DateTime acceptedUtc =
                new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc)
                    .AddMinutes(salt);
            return MissionAcgInstanceBinding.CreateDurable(
                new MissionAcgIdentityRecord(
                    MissionAcgAllocationService.AcceptedQuestIdentityType,
                    0x51000000 + salt),
                new MissionAcgIdentityRecord(0xDAC3, 0x11000000 + salt),
                owner,
                null,
                MissionRollType.FindItem,
                42,
                3000 + salt,
                new MissionAcgIdentityRecord(
                    MissionAcgAllocationService.MissionKeyIdentityType,
                    0x61000000 + salt),
                new MissionAcgIdentityRecord(0x9C50, 710),
                43308,
                27595,
                229.605F,
                6.504F,
                452.042F,
                new MissionAcgIdentityRecord(0xDAC1, 0x2000 + salt),
                bundle,
                livePlayfield,
                acceptedUtc,
                acceptedUtc.AddHours(48));
        }

        private void AssertLoadFailure(string expected)
        {
            MissionAcgExpiryLoadResult loaded = this.CreateStore().LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(string.Join("|", loaded.Diagnostics), expected);
        }

        private static MissionAcgExpiryCheckpoint AllCleanupCheckpoints()
        {
            return MissionAcgExpiryCheckpoint.CleanupStarted
                   | MissionAcgExpiryCheckpoint.InteractionsBlocked
                   | MissionAcgExpiryCheckpoint.OccupantsEvacuated
                   | MissionAcgExpiryCheckpoint.NpcsRemoved
                   | MissionAcgExpiryCheckpoint.ObjectivesRemoved
                   | MissionAcgExpiryCheckpoint.ContainersRemoved
                   | MissionAcgExpiryCheckpoint.CorpsesRemoved
                   | MissionAcgExpiryCheckpoint.InventoryArtifactsRemoved
                   | MissionAcgExpiryCheckpoint.RuntimeRegistrationsRemoved
                   | MissionAcgExpiryCheckpoint.OperationalStateFinalized
                   | MissionAcgExpiryCheckpoint.ClientMissionRemoved
                   | MissionAcgExpiryCheckpoint.ObjectiveCorpseCleanupVerified
                   | MissionAcgExpiryCheckpoint.BindingReleaseReady
                   | MissionAcgExpiryCheckpoint.Pf2ReleaseAttempted
                   | MissionAcgExpiryCheckpoint.Pf2ReleaseConfirmed
                   | MissionAcgExpiryCheckpoint.CleanupComplete;
        }

        private static void RewriteField(string path, string key, string value)
        {
            string[] lines = File.ReadAllLines(path);
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
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
                    field == key ? value : lines[i].Substring(separator + 1);
            }

            var canonical = new StringBuilder();
            foreach (KeyValuePair<string, string> field in fields)
            {
                canonical.Append(field.Key);
                canonical.Append('=');
                canonical.Append(field.Value);
                canonical.Append("\r\n");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = ToHex(sha.ComputeHash(bytes)).ToLowerInvariant();
            }

            File.WriteAllText(
                path,
                "AORebirth-MissionAcgExpiryState\r\n"
                + canonical
                + "RecordSha256="
                + hash
                + "\r\n",
                new UTF8Encoding(false));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void AssertArgument(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected argument validation failure.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static void AssertInvalidOperation(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected invalid operation.");
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
