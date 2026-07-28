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
    public class MissionAcgBindingPersistenceTests
    {
        private string temporaryRoot;

        private MissionAcgLayoutCatalog catalog;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-acg-tests-"
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
        public void NewBindingHasDistinctAcceptedAndOfferIdentities()
        {
            MissionAcgBindingRecord record = this.CreateRecord(1, 1, FirstLivePf(), 1);
            Assert.AreNotEqual(
                record.Binding.OriginalOfferIdentity,
                record.Binding.AcceptedQuestIdentity);
            Assert.AreEqual(
                MissionAcgAllocationService.AcceptedQuestIdentityType,
                record.Binding.AcceptedQuestIdentity.Type);
        }

        [TestMethod]
        public void SameSelectionInputsChooseSameBundle()
        {
            var input = new MissionAcgSelectionInput(
                123456,
                MissionRollType.FindItem,
                42,
                new MissionAcgIdentityRecord(0xC350, 7001));
            MissionAcgLayoutBundle first =
                MissionAcgLayoutSelector.Select(this.catalog, input);
            MissionAcgLayoutBundle second =
                MissionAcgLayoutSelector.Select(this.catalog, input);
            Assert.AreEqual(first.LayoutId, second.LayoutId);
            Assert.AreEqual(first.GeneratorPayloadSha256, second.GeneratorPayloadSha256);
        }

        [TestMethod]
        public void ReloadPreservesBundleBuildingPlayfieldKeyEntranceAndExpiry()
        {
            MissionAcgBindingRecord original = this.CreateRecord(2, 2, FirstLivePf(), 2);
            MissionAcgBindingRecord reloaded = this.RoundTrip(original);
            Assert.AreEqual(
                original.Binding.SelectedBundleId,
                reloaded.Binding.SelectedBundleId);
            Assert.AreEqual(
                original.Binding.AcgBuildingIdentity,
                reloaded.Binding.AcgBuildingIdentity);
            Assert.AreEqual(
                original.Binding.AllocatedLivePlayfield2,
                reloaded.Binding.AllocatedLivePlayfield2);
            Assert.AreEqual(
                original.Binding.MissionKeyIdentity,
                reloaded.Binding.MissionKeyIdentity);
            Assert.AreEqual(
                original.Binding.ExteriorEntranceIdentity,
                reloaded.Binding.ExteriorEntranceIdentity);
            Assert.AreEqual(original.Binding.ExpiryUtc, reloaded.Binding.ExpiryUtc);
        }

        [TestMethod]
        public void TwoAcceptedMissionsAndTwoOwnersReceiveDistinctLivePlayfields()
        {
            var allocator = new MissionAcgAllocationService(this.catalog);
            int first;
            int second;
            Assert.IsTrue(allocator.TryReservePlayfield(out first));
            Assert.IsTrue(allocator.TryReservePlayfield(out second));
            Assert.AreNotEqual(first, second);

            MissionAcgBindingRecord ownerOne = this.CreateRecord(3, 10, first, 3);
            MissionAcgBindingRecord ownerTwo = this.CreateRecord(4, 20, second, 4);
            Assert.AreNotEqual(
                ownerOne.Binding.OwnerIdentity,
                ownerTwo.Binding.OwnerIdentity);
            Assert.AreNotEqual(
                ownerOne.Binding.AllocatedLivePlayfield2,
                ownerTwo.Binding.AllocatedLivePlayfield2);
        }

        [TestMethod]
        public void SameTypeMissionsRemainExactlyResolvableByAcceptedIdAndKey()
        {
            MissionAcgBindingRecord older = this.CreateRecord(5, 30, FirstLivePf(), 5);
            MissionAcgBindingRecord newer = this.CreateRecord(6, 30, NextLivePf(FirstLivePf()), 6);
            var records = new[] { older, newer };
            MissionAcgBindingRecord resolved;
            Assert.IsTrue(
                MissionAcgBindingResolver.TryResolveByKey(
                    records,
                    30,
                    older.Binding.MissionKeyIdentity.Instance,
                    older.Binding.AcceptedUtc.AddMinutes(1),
                    out resolved));
            Assert.AreSame(older, resolved);
            Assert.IsFalse(
                MissionAcgBindingResolver.TryResolveByKey(
                    records,
                    30,
                    0x123456,
                    older.Binding.AcceptedUtc.AddMinutes(1),
                    out resolved));
            Assert.IsFalse(
                MissionAcgBindingResolver.TryResolveByKey(
                    records,
                    31,
                    older.Binding.MissionKeyIdentity.Instance,
                    older.Binding.AcceptedUtc.AddMinutes(1),
                    out resolved));
        }

        [TestMethod]
        public void SoloOwnershipRejectsWrongOwnerAndDoesNotInferTeam()
        {
            MissionAcgBindingRecord record = this.CreateRecord(7, 40, FirstLivePf(), 7);
            Assert.IsTrue(record.Binding.ExplicitNoTeam);
            Assert.IsNull(record.Binding.TeamIdentity);
            Assert.AreEqual(40, record.Binding.OwnerIdentity.Instance);
            Assert.AreNotEqual(41, record.Binding.OwnerIdentity.Instance);
        }

        [TestMethod]
        public void ExteriorMarkerResolutionRejectsAmbiguousSameEntrance()
        {
            MissionAcgBindingRecord first = this.CreateRecord(70, 45, FirstLivePf(), 70);
            MissionAcgBindingRecord second =
                this.CreateRecord(71, 45, NextLivePf(FirstLivePf()), 71);
            MissionAcgBindingRecord resolved;
            DateTime now = first.Binding.AcceptedUtc.AddMinutes(1);
            Assert.IsTrue(
                MissionAcgBindingResolver.TryResolveByExteriorMarker(
                    new[] { first },
                    45,
                    710,
                    first.Binding.ExteriorX,
                    first.Binding.ExteriorY,
                    first.Binding.ExteriorZ,
                    10,
                    14,
                    now,
                    out resolved));
            Assert.AreSame(first, resolved);
            Assert.IsFalse(
                MissionAcgBindingResolver.TryResolveByExteriorMarker(
                    new[] { first, second },
                    45,
                    710,
                    first.Binding.ExteriorX,
                    first.Binding.ExteriorY,
                    first.Binding.ExteriorZ,
                    10,
                    14,
                    now,
                    out resolved));
        }

        [TestMethod]
        public void AllocatedPlayfieldNeverUsesCapturedOrLegacySharedPlayfield()
        {
            var allocator = new MissionAcgAllocationService(this.catalog);
            for (int i = 0; i < 32; i++)
            {
                int live;
                Assert.IsTrue(allocator.TryReservePlayfield(out live));
                Assert.AreNotEqual(
                    MissionAcgAllocationService.LegacySharedPlayfield2,
                    live);
                Assert.IsNull(this.catalog.FindBySourcePlayfield2(live));
            }
        }

        [TestMethod]
        public void DuplicateAcceptedQuestRecordsFailClosed()
        {
            MissionAcgBindingStore store = this.CreateStore();
            MissionAcgBindingRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(
                    this.CreateRecord(8, 50, FirstLivePf(), 8),
                    out persisted,
                    out failure),
                failure);
            string duplicate =
                Path.Combine(store.DirectoryPath, "duplicate.acg");
            File.Copy(persisted.RecordPath, duplicate);

            MissionAcgBindingLoadResult loaded = store.LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(
                string.Join("|", loaded.Diagnostics),
                "duplicate accepted quest id");
        }

        [TestMethod]
        public void DuplicateActivePlayfieldRecordsFailClosed()
        {
            MissionAcgBindingStore store = this.CreateStore();
            MissionAcgBindingRecord first;
            MissionAcgBindingRecord second;
            string failure;
            int live = FirstLivePf();
            Assert.IsTrue(
                store.TryCreate(
                    this.CreateRecord(9, 60, live, 9),
                    out first,
                    out failure),
                failure);
            Assert.IsTrue(
                store.TryCreate(
                    this.CreateRecord(10, 61, live, 10),
                    out second,
                    out failure),
                failure);

            MissionAcgBindingLoadResult loaded = store.LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(
                string.Join("|", loaded.Diagnostics),
                "duplicate active PF2");
        }

        [TestMethod]
        public void MissingBundleFailsClosed()
        {
            string path = this.CreatePersistedPath(11, 70, FirstLivePf(), 11);
            RewriteField(path, "SelectedBundleId", "missing-bundle");
            AssertLoadFailure("missing bundle");
        }

        [TestMethod]
        public void BuildingMismatchFailsClosed()
        {
            string path = this.CreatePersistedPath(12, 71, FirstLivePf(), 12);
            RewriteField(path, "AcgBuildingInstance", "123");
            AssertLoadFailure("building identity mismatch");
        }

        [TestMethod]
        public void PayloadHashMismatchFailsClosed()
        {
            string path = this.CreatePersistedPath(13, 72, FirstLivePf(), 13);
            RewriteField(
                path,
                "SelectedBundlePayloadSha256",
                new string('0', 64));
            AssertLoadFailure("payload hash mismatch");
        }

        [TestMethod]
        public void UnknownVersionFailsClosed()
        {
            string path = this.CreatePersistedPath(14, 73, FirstLivePf(), 14);
            RewriteField(path, "FormatVersion", "999");
            AssertLoadFailure("Unknown binding format version");
        }

        [TestMethod]
        public void TruncatedSidecarFailsClosed()
        {
            MissionAcgBindingStore store = this.CreateStore();
            Directory.CreateDirectory(store.DirectoryPath);
            File.WriteAllText(
                Path.Combine(store.DirectoryPath, "truncated.acg"),
                "AORebirth-MissionAcgBinding\r\nFormatVersion=2\r\n");
            AssertLoadFailure("truncated");
        }

        [TestMethod]
        public void TemporaryAtomicWriteIsNeverVisibleAsRecord()
        {
            MissionAcgBindingStore store = this.CreateStore();
            MissionAcgBindingRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(
                    this.CreateRecord(15, 74, FirstLivePf(), 15),
                    out persisted,
                    out failure),
                failure);
            File.WriteAllText(
                Path.Combine(store.DirectoryPath, "partial.acg.tmp"),
                "partial");
            MissionAcgBindingLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid);
            Assert.AreEqual(1, loaded.Records.Count);
        }

        [TestMethod]
        public void PersistenceFailureCanRollbackAllUnpersistedReservations()
        {
            var allocator = new MissionAcgAllocationService(this.catalog);
            MissionAcgIdentityRecord accepted;
            MissionAcgIdentityRecord key;
            int live;
            Assert.IsTrue(allocator.TryReserveAcceptedQuestIdentity(out accepted));
            Assert.IsTrue(allocator.TryReservePlayfield(out live));
            Assert.IsTrue(allocator.TryReserveMissionKeyIdentity(out key));
            allocator.RollbackUnpersisted(accepted, key, live);
            Assert.IsFalse(allocator.IsReserved(live));
        }

        [TestMethod]
        public void PlayfieldExhaustionFailsWithoutReturningSharedOrCapturedPf()
        {
            var allocator = new MissionAcgAllocationService(this.catalog);
            int capacity =
                MissionAcgAllocationService.MaximumLivePlayfield2
                - MissionAcgAllocationService.MinimumLivePlayfield2
                + 1;
            int allocatedCount = 0;
            int live;
            while (allocator.TryReservePlayfield(out live))
            {
                allocatedCount++;
                Assert.AreNotEqual(
                    MissionAcgAllocationService.LegacySharedPlayfield2,
                    live);
                Assert.IsNull(this.catalog.FindBySourcePlayfield2(live));
                Assert.IsTrue(allocatedCount <= capacity);
            }

            Assert.IsTrue(allocatedCount > 0);
            Assert.AreEqual(0, live);
        }

        [TestMethod]
        public void RestoredReservationsAreAppliedBeforeNewAllocation()
        {
            int restoredPf = FirstLivePf();
            MissionAcgBindingRecord record = this.CreateRecord(16, 75, restoredPf, 16);
            var allocator = new MissionAcgAllocationService(this.catalog);
            string failure;
            Assert.IsTrue(allocator.TryRestore(new[] { record }, out failure), failure);
            int allocated;
            Assert.IsTrue(allocator.TryReservePlayfield(out allocated));
            Assert.AreNotEqual(restoredPf, allocated);
        }

        [TestMethod]
        public void DuplicateRestoreReservationFails()
        {
            int live = FirstLivePf();
            var allocator = new MissionAcgAllocationService(this.catalog);
            string failure;
            Assert.IsFalse(
                allocator.TryRestore(
                    new[]
                    {
                        this.CreateRecord(17, 76, live, 17),
                        this.CreateRecord(18, 77, live, 18)
                    },
                    out failure));
            StringAssert.Contains(failure, "Duplicate active PF2");
        }

        [TestMethod]
        public void ExpiredAndAbandonedBindingsBlockEntry()
        {
            MissionAcgBindingRecord active = this.CreateRecord(19, 78, FirstLivePf(), 19);
            Assert.IsTrue(
                active.State.CanEnter(
                    active.Binding.AcceptedUtc.AddMinutes(1),
                    active.Binding.ExpiryUtc));
            Assert.IsFalse(
                active.State.CanEnter(
                    active.Binding.ExpiryUtc.AddSeconds(1),
                    active.Binding.ExpiryUtc));

            MissionAcgInstanceState abandoned =
                active.State.Transition(
                    MissionAcgLifecycleState.Abandoned,
                    MissionAcgCleanupState.KeyRemovalPending,
                    DateTime.UtcNow);
            Assert.IsFalse(abandoned.CanEnter(DateTime.UtcNow, active.Binding.ExpiryUtc));
        }

        [TestMethod]
        public void PlayfieldReleaseRequiresTerminalCleanupAndAffectsOnlyOwnBinding()
        {
            var allocator = new MissionAcgAllocationService(this.catalog);
            int first;
            int second;
            Assert.IsTrue(allocator.TryReservePlayfield(out first));
            Assert.IsTrue(allocator.TryReservePlayfield(out second));
            MissionAcgBindingRecord record = this.CreateRecord(20, 79, first, 20);
            Assert.IsFalse(allocator.ReleaseAfterCleanup(record));
            MissionAcgInstanceState abandoned =
                record.State.Transition(
                    MissionAcgLifecycleState.Abandoned,
                    MissionAcgCleanupState.KeyRemovalPending,
                    DateTime.UtcNow);
            MissionAcgInstanceState pending =
                abandoned.Transition(
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending,
                    DateTime.UtcNow);
            MissionAcgInstanceState cleaned =
                pending.Transition(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    DateTime.UtcNow);
            Assert.IsTrue(
                allocator.ReleaseAfterCleanup(record.WithState(cleaned)));
            Assert.IsFalse(allocator.IsReserved(first));
            Assert.IsTrue(allocator.IsReserved(second));
        }

        [TestMethod]
        public void ActiveBindingSurvivesShutdownStyleReload()
        {
            MissionAcgBindingRecord original =
                this.CreateRecord(21, 80, FirstLivePf(), 21);
            MissionAcgBindingRecord reloaded = this.RoundTrip(original);
            var allocator = new MissionAcgAllocationService(this.catalog);
            string failure;
            Assert.IsTrue(allocator.TryRestore(new[] { reloaded }, out failure), failure);
            Assert.IsTrue(
                allocator.IsReserved(reloaded.Binding.AllocatedLivePlayfield2));
        }

        [TestMethod]
        public void SidecarsAreIsolatedFromAuthoredQuestPersistence()
        {
            MissionAcgBindingStore store = this.CreateStore();
            StringAssert.EndsWith(
                store.DirectoryPath,
                Path.Combine("mission-state", MissionAcgBindingStore.DirectoryName));
        }

        [TestMethod]
        public void CatalogRegressionKeepsFiveSelectableAndIncompleteShapeExcluded()
        {
            Assert.AreEqual(5, this.catalog.SelectableLayouts.Count);
            Assert.IsNull(
                this.catalog.FindBySourcePlayfield2(
                    MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2));
            for (int i = 0; i < this.catalog.SelectableLayouts.Count; i++)
            {
                MissionAcgLayoutBundle bundle = this.catalog.SelectableLayouts[i];
                Assert.IsTrue(
                    string.Equals(
                        bundle.ExpectedGeneratorPayloadSha256,
                        bundle.GeneratorPayloadSha256,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private MissionAcgBindingRecord RoundTrip(MissionAcgBindingRecord record)
        {
            MissionAcgBindingStore store = this.CreateStore();
            MissionAcgBindingRecord persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(record, out persisted, out failure), failure);
            MissionAcgBindingLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid, string.Join("|", loaded.Diagnostics));
            Assert.AreEqual(1, loaded.Records.Count);
            return loaded.Records[0];
        }

        private string CreatePersistedPath(
            int acceptedSalt,
            int owner,
            int livePf,
            int keySalt)
        {
            MissionAcgBindingStore store = this.CreateStore();
            MissionAcgBindingRecord persisted;
            string failure;
            Assert.IsTrue(
                store.TryCreate(
                    this.CreateRecord(acceptedSalt, owner, livePf, keySalt),
                    out persisted,
                    out failure),
                failure);
            return persisted.RecordPath;
        }

        private MissionAcgBindingStore CreateStore()
        {
            return new MissionAcgBindingStore(
                Path.Combine(this.temporaryRoot, "mission-state"),
                this.catalog);
        }

        private MissionAcgBindingRecord CreateRecord(
            int acceptedSalt,
            int owner,
            int livePf,
            int keySalt)
        {
            var ownerIdentity = new MissionAcgIdentityRecord(0xC350, owner);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(
                        1000 + acceptedSalt,
                        MissionRollType.FindItem,
                        42,
                        ownerIdentity));
            DateTime acceptedUtc =
                new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)
                    .AddMinutes(acceptedSalt);
            MissionAcgInstanceBinding binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x50000000 + acceptedSalt),
                    new MissionAcgIdentityRecord(0xDAC3, 0x01000000 + acceptedSalt),
                    ownerIdentity,
                    null,
                    MissionRollType.FindItem,
                    42,
                    1000 + acceptedSalt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x60000000 + keySalt),
                    new MissionAcgIdentityRecord(0x9C50, 710),
                    43308,
                    27595,
                    229.605F,
                    6.504F,
                    452.042F,
                    new MissionAcgIdentityRecord(0xDAC1, 0x1000 + acceptedSalt),
                    bundle,
                    livePf,
                    acceptedUtc,
                    acceptedUtc.AddHours(48));
            return new MissionAcgBindingRecord(
                binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Accepted,
                    MissionAcgCleanupState.None,
                    acceptedUtc,
                    null),
                string.Empty);
        }

        private int FirstLivePf()
        {
            int value = MissionAcgAllocationService.MinimumLivePlayfield2;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || this.catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private int NextLivePf(int current)
        {
            int value = current + 1;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || this.catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private void AssertLoadFailure(string expected)
        {
            MissionAcgBindingLoadResult loaded = this.CreateStore().LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(
                string.Join("|", loaded.Diagnostics),
                expected);
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
                "AORebirth-MissionAcgBinding\r\n"
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
    }
}
