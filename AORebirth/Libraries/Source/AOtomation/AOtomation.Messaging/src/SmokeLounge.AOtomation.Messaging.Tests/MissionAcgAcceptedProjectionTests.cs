namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgAcceptedProjectionTests
    {
        private const string Header = "AORebirth-MissionAcgAcceptedProjection";

        private static readonly MissionRollType[] RequiredTypes =
        {
            MissionRollType.KillPerson,
            MissionRollType.FindPerson,
            MissionRollType.FindItem,
            MissionRollType.FindItemReturn,
            MissionRollType.RepairMachine
        };

        private string temporaryRoot;

        private MissionAcgLayoutCatalog catalog;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-acg-accepted-projection-tests-"
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
        public void AllFiveAcceptedProjectionsRoundTripExactlyAcrossRestart()
        {
            IDictionary<MissionRollType, SourceOffer> sources = FindAllMissionTypes();
            MissionAcgAcceptedProjectionStore store = this.CreateStore("all-five");
            var expected = new Dictionary<int, MissionAcgAcceptedProjection>();

            for (int i = 0; i < RequiredTypes.Length; i++)
            {
                MissionRollType type = RequiredTypes[i];
                MissionAcgAcceptedProjection projection =
                    this.CreateProjection(sources[type], i + 1, 1000 + i);
                MissionAcgAcceptedProjection persisted;
                string failure;
                Assert.IsTrue(
                    store.TryCreate(projection, out persisted, out failure),
                    failure);
                expected.Add(
                    projection.Binding.AcceptedQuestIdentity.Instance,
                    projection);
            }

            MissionAcgAcceptedProjectionLoadResult restarted =
                this.CreateStore("all-five").LoadAll();
            Assert.IsTrue(
                restarted.IsValid,
                string.Join("|", restarted.Diagnostics));
            Assert.AreEqual(RequiredTypes.Length, restarted.Projections.Count);

            for (int i = 0; i < restarted.Projections.Count; i++)
            {
                MissionAcgAcceptedProjection actual = restarted.Projections[i];
                MissionAcgAcceptedProjection original;
                Assert.IsTrue(
                    expected.TryGetValue(
                        actual.Binding.AcceptedQuestIdentity.Instance,
                        out original));
                AssertProjectionExact(original, actual);
            }
        }

        [TestMethod]
        public void FrozenTextRewardsItemsActionsAndQfuContractsSurviveRestart()
        {
            IDictionary<MissionRollType, SourceOffer> sources = FindAllMissionTypes();
            MissionAcgAcceptedProjectionStore store = this.CreateStore("semantics");

            for (int i = 0; i < RequiredTypes.Length; i++)
            {
                MissionRollType type = RequiredTypes[i];
                SourceOffer source = sources[type];
                MissionAcgAcceptedProjection original =
                    this.CreateProjection(source, 20 + i, 2000 + i);
                MissionAcgAcceptedProjection persisted;
                string failure;
                Assert.IsTrue(
                    store.TryCreate(original, out persisted, out failure),
                    failure);
            }

            MissionAcgAcceptedProjectionLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid, string.Join("|", loaded.Diagnostics));
            Assert.AreEqual(RequiredTypes.Length, loaded.Projections.Count);

            var seen = new HashSet<MissionRollType>();
            for (int i = 0; i < loaded.Projections.Count; i++)
            {
                MissionAcgAcceptedProjection projection = loaded.Projections[i];
                QuestInfo restored = projection.ReconstructOffer();
                QuestActionList restoredAction = restored.QuestActions[0];
                SourceOffer source = sources[projection.Binding.MissionType];
                QuestActionList sourceAction = source.Offer.QuestActions[0];

                Assert.AreEqual(source.Offer.ShortInfo, projection.Title);
                Assert.AreEqual(source.Offer.Info, projection.Description);
                Assert.AreEqual(source.Offer.ShortInfo, restored.ShortInfo);
                Assert.AreEqual(source.Offer.Info, restored.Info);
                Assert.AreEqual(source.Offer.CashReward, projection.FrozenCashReward);
                Assert.AreEqual(
                    source.Offer.ExperienceReward,
                    projection.FrozenExperienceReward);
                Assert.AreEqual(sourceAction.Playfield, restoredAction.Playfield);
                Assert.AreEqual(sourceAction.Unknown18, restoredAction.Unknown18);
                Assert.AreEqual(sourceAction.Unknown19, restoredAction.Unknown19);
                Assert.AreEqual(sourceAction.X, restoredAction.X);
                Assert.AreEqual(sourceAction.Y, restoredAction.Y);
                Assert.AreEqual(sourceAction.Z, restoredAction.Z);
                Assert.AreEqual(sourceAction.UnknownHash15, restoredAction.UnknownHash15);
                Assert.AreEqual(
                    ExpectedQfuVersion(projection.Binding.MissionType),
                    projection.QfuVersion);
                Assert.AreEqual(
                    projection.Binding.MissionType == MissionRollType.FindPerson ? 64 : 0,
                    projection.QfuQuestIdentityFlag);
                Assert.AreEqual(
                    MissionAcgAcceptancePhase.QfuSent,
                    projection.AcceptancePhase);
                Assert.IsNotNull(projection.RuntimeObjectiveIdentity);
                Assert.AreNotEqual(
                    projection.Binding.OriginalOfferIdentity,
                    projection.Binding.AcceptedQuestIdentity);

                AssertRewardExact(source.Offer, projection, restored);
                seen.Add(projection.Binding.MissionType);
            }

            CollectionAssert.AreEquivalent(
                RequiredTypes,
                new List<MissionRollType>(seen));
        }

        [TestMethod]
        public void TamperedTruncatedPartialAndUnknownVersionRecordsFailClosed()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.FindItem];

            MissionAcgAcceptedProjectionStore tamperedStore =
                this.CreateStore("tampered");
            string tamperedPath =
                PersistOne(
                    tamperedStore,
                    this.CreateProjection(source, 40, 3040));
            MutateFieldWithoutHash(tamperedPath, "MissionTitle");
            AssertInvalid(tamperedStore, "SHA-256 mismatch");

            MissionAcgAcceptedProjectionStore truncatedStore =
                this.CreateStore("truncated");
            string truncatedPath =
                PersistOne(
                    truncatedStore,
                    this.CreateProjection(source, 41, 3041));
            File.WriteAllText(
                truncatedPath,
                Header + "\r\nFormatVersion=1\r\n",
                new UTF8Encoding(false));
            AssertInvalid(truncatedStore, "RecordSha256");

            MissionAcgAcceptedProjectionStore partialStore =
                this.CreateStore("partial");
            string partialPath =
                PersistOne(
                    partialStore,
                    this.CreateProjection(source, 42, 3042));
            RewriteRecord(partialPath, null, null, "MissionDescription");
            AssertInvalid(partialStore, "field set is incomplete");

            MissionAcgAcceptedProjectionStore versionStore =
                this.CreateStore("version");
            string versionPath =
                PersistOne(
                    versionStore,
                    this.CreateProjection(source, 43, 3043));
            RewriteRecord(versionPath, "FormatVersion", "999", null);
            AssertInvalid(versionStore, "Unknown accepted-projection format version");
        }

        [TestMethod]
        public void DuplicateAcceptedIdentityAndOwnerOfferAreRejected()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.FindPerson];
            MissionAcgAcceptedProjectionStore store = this.CreateStore("duplicates");
            MissionAcgAcceptedProjection first =
                this.CreateProjection(source, 50, 4050);
            MissionAcgAcceptedProjection persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(first, out persisted, out failure), failure);

            Assert.IsFalse(store.TryCreate(first, out persisted, out failure));
            StringAssert.Contains(failure, "Duplicate accepted quest");

            MissionAcgAcceptedProjection sameOwnerOffer =
                this.CreateProjection(source, 51, 4050);
            Assert.IsFalse(
                store.TryCreate(sameOwnerOffer, out persisted, out failure));
            StringAssert.Contains(failure, "already claimed by owner");

            string duplicatePath =
                Path.Combine(store.DirectoryPath, "duplicate.accepted");
            File.Copy(OnlyRecordPath(store), duplicatePath);
            AssertInvalid(store, "duplicate accepted quest");
        }

        [TestMethod]
        public void AtomicTemporaryContentIsNotPublishedAsAcceptedState()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.RepairMachine];
            MissionAcgAcceptedProjectionStore store = this.CreateStore("atomic");
            MissionAcgAcceptedProjection original =
                this.CreateProjection(source, 60, 5060);
            PersistOne(store, original);
            File.WriteAllText(
                Path.Combine(store.DirectoryPath, "partial.accepted.tmp"),
                "not a complete record",
                new UTF8Encoding(false));

            MissionAcgAcceptedProjectionLoadResult loaded = store.LoadAll();
            Assert.IsTrue(loaded.IsValid, string.Join("|", loaded.Diagnostics));
            Assert.AreEqual(1, loaded.Projections.Count);
            AssertProjectionExact(original, loaded.Projections[0]);
        }

        [TestMethod]
        public void ConcurrentDuplicateOwnerOfferCreationPublishesExactlyOneProjection()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.KillPerson];
            MissionAcgAcceptedProjectionStore store = this.CreateStore("concurrent-owner-offer");
            MissionAcgAcceptedProjection first =
                this.CreateProjection(source, 70, 6070);
            MissionAcgAcceptedProjection second =
                this.CreateProjection(source, 71, 6070);
            var start = new ManualResetEvent(false);
            bool firstSucceeded = false;
            bool secondSucceeded = false;
            string firstFailure = string.Empty;
            string secondFailure = string.Empty;

            var firstThread = new Thread(
                new ThreadStart(
                delegate
                {
                    MissionAcgAcceptedProjection ignored;
                    start.WaitOne();
                    firstSucceeded = store.TryCreate(
                        first,
                        out ignored,
                        out firstFailure);
                }));
            var secondThread = new Thread(
                new ThreadStart(
                delegate
                {
                    MissionAcgAcceptedProjection ignored;
                    start.WaitOne();
                    secondSucceeded = store.TryCreate(
                        second,
                        out ignored,
                        out secondFailure);
                }));

            firstThread.Start();
            secondThread.Start();
            start.Set();
            Assert.IsTrue(firstThread.Join(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(secondThread.Join(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(1, (firstSucceeded ? 1 : 0) + (secondSucceeded ? 1 : 0));
            StringAssert.Contains(
                firstSucceeded ? secondFailure : firstFailure,
                "already claimed by owner");

            MissionAcgAcceptedProjectionLoadResult restarted = store.LoadAll();
            Assert.IsTrue(
                restarted.IsValid,
                string.Join("|", restarted.Diagnostics));
            Assert.AreEqual(1, restarted.Projections.Count);
            Assert.AreEqual(6070, restarted.Projections[0].Binding.OwnerIdentity.Instance);
            Assert.AreEqual(
                source.Offer.QuestIdentity.Instance,
                restarted.Projections[0].Binding.OriginalOfferIdentity.Instance);
        }

        [TestMethod]
        public void EveryAcceptancePhaseRoundTripsWithoutChangingFrozenOrBoundFields()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.RepairMachine];
            MissionAcgAcceptedProjectionStore store = this.CreateStore("every-phase");
            var expected = new Dictionary<int, MissionAcgAcceptedProjection>();

            foreach (MissionAcgAcceptancePhase phase
                in Enum.GetValues(typeof(MissionAcgAcceptancePhase)))
            {
                int salt = 100 + (int)phase;
                MissionAcgAcceptedProjection projection =
                    this.CreateProjection(source, salt, 7000 + (int)phase, phase);
                MissionAcgAcceptedProjection persisted;
                string failure;
                Assert.IsTrue(
                    store.TryCreate(projection, out persisted, out failure),
                    phase + ": " + failure);
                expected.Add(
                    projection.Binding.AcceptedQuestIdentity.Instance,
                    projection);
            }

            MissionAcgAcceptedProjectionLoadResult restarted =
                this.CreateStore("every-phase").LoadAll();
            Assert.IsTrue(
                restarted.IsValid,
                string.Join("|", restarted.Diagnostics));
            Assert.AreEqual(
                Enum.GetValues(typeof(MissionAcgAcceptancePhase)).Length,
                restarted.Projections.Count);
            for (int i = 0; i < restarted.Projections.Count; i++)
            {
                MissionAcgAcceptedProjection actual = restarted.Projections[i];
                MissionAcgAcceptedProjection original =
                    expected[actual.Binding.AcceptedQuestIdentity.Instance];
                AssertProjectionExact(original, actual);
                Assert.IsNotNull(actual.MissionArtifactIdentity);
                Assert.AreEqual(original.FrozenCashReward, actual.FrozenCashReward);
                Assert.AreEqual(
                    original.FrozenExperienceReward,
                    actual.FrozenExperienceReward);
            }
        }

        [TestMethod]
        public void StalePhaseReplacementCannotOverwriteTerminalLifecycle()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.FindItem];
            MissionAcgAcceptedProjectionStore store = this.CreateStore("stale-replacement");
            MissionAcgAcceptedProjection original =
                this.CreateProjection(
                    source,
                    125,
                    8125,
                    MissionAcgAcceptancePhase.OfferClaimed);
            PersistOne(store, original);

            MissionAcgAcceptedProjection terminal =
                original.WithLifecycle(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    original.UpdatedUtc.AddSeconds(1));
            MissionAcgAcceptedProjection persisted;
            string failure;
            Assert.IsTrue(
                store.TryReplace(original, terminal, out persisted, out failure),
                failure);

            MissionAcgAcceptedProjection staleAdvance =
                original.WithPhase(
                    MissionAcgAcceptancePhase.BindingPersisted,
                    original.UpdatedUtc.AddSeconds(2));
            Assert.IsFalse(
                store.TryReplace(original, staleAdvance, out persisted, out failure));
            StringAssert.Contains(failure, "changed after the expected record was read");

            MissionAcgAcceptedProjectionLoadResult restarted = store.LoadAll();
            Assert.IsTrue(restarted.IsValid, string.Join("|", restarted.Diagnostics));
            Assert.AreEqual(1, restarted.Projections.Count);
            Assert.AreEqual(
                MissionAcgAcceptancePhase.OfferClaimed,
                restarted.Projections[0].AcceptancePhase);
            Assert.AreEqual(
                MissionAcgLifecycleState.Cleaned,
                restarted.Projections[0].LifecycleState);
            Assert.AreEqual(
                MissionAcgCleanupState.Completed,
                restarted.Projections[0].CleanupState);
        }

        [TestMethod]
        public void ExpiredOfferClaimCleanupPersistsAndReleasesItsAllocatorReservation()
        {
            SourceOffer source = FindAllMissionTypes()[MissionRollType.FindItem];
            MissionAcgAcceptedProjectionStore store = this.CreateStore("expired-offer-claim");
            MissionAcgAcceptedProjection pending =
                this.CreateProjection(
                    source,
                    130,
                    8130,
                    MissionAcgAcceptancePhase.OfferClaimed);
            PersistOne(store, pending);

            var allocator = new MissionAcgAllocationService(this.catalog);
            var pendingRecord = new MissionAcgBindingRecord(
                pending.Binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Reserved,
                    MissionAcgCleanupState.None,
                    pending.UpdatedUtc,
                    null),
                string.Empty);
            string failure;
            Assert.IsTrue(allocator.TryRestore(new[] { pendingRecord }, out failure), failure);
            Assert.IsTrue(
                allocator.IsReservedBy(
                    pending.Binding.AllocatedLivePlayfield2,
                    pending.Binding.AcceptedQuestIdentity));

            MissionAcgAcceptedProjection cleaned =
                pending.WithLifecycle(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    pending.Binding.ExpiryUtc.AddSeconds(1));
            MissionAcgAcceptedProjection persisted;
            Assert.IsTrue(
                store.TryReplace(pending, cleaned, out persisted, out failure),
                failure);
            allocator.RollbackUnpersisted(
                pending.Binding.AcceptedQuestIdentity,
                pending.Binding.MissionKeyIdentity,
                pending.Binding.AllocatedLivePlayfield2);
            Assert.IsFalse(allocator.IsReserved(pending.Binding.AllocatedLivePlayfield2));

            MissionAcgAcceptedProjectionLoadResult restarted = store.LoadAll();
            Assert.IsTrue(
                restarted.IsValid,
                string.Join("|", restarted.Diagnostics));
            Assert.AreEqual(1, restarted.Projections.Count);
            Assert.AreEqual(
                MissionAcgAcceptancePhase.OfferClaimed,
                restarted.Projections[0].AcceptancePhase);
            Assert.AreEqual(
                MissionAcgLifecycleState.Cleaned,
                restarted.Projections[0].LifecycleState);
            Assert.AreEqual(
                MissionAcgCleanupState.Completed,
                restarted.Projections[0].CleanupState);

            var restoredAllocator = new MissionAcgAllocationService(this.catalog);
            var cleanedRecord = new MissionAcgBindingRecord(
                restarted.Projections[0].Binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    restarted.Projections[0].UpdatedUtc,
                    null),
                string.Empty);
            Assert.IsTrue(
                restoredAllocator.TryRestore(new[] { cleanedRecord }, out failure),
                failure);
            Assert.IsFalse(
                restoredAllocator.IsReserved(
                    cleanedRecord.Binding.AllocatedLivePlayfield2));

            MissionAcgIdentityRecord newAccepted;
            int newlyAllocatedPlayfield;
            Assert.IsTrue(
                restoredAllocator.TryReserveAcceptedQuestIdentity(out newAccepted));
            Assert.IsTrue(
                restoredAllocator.TryReservePlayfield(
                    newAccepted,
                    out newlyAllocatedPlayfield));
            Assert.AreNotEqual(
                MissionAcgAllocationService.LegacySharedPlayfield2,
                newlyAllocatedPlayfield);
            Assert.IsTrue(restoredAllocator.IsReserved(newlyAllocatedPlayfield));
        }

        private MissionAcgAcceptedProjectionStore CreateStore(string suffix)
        {
            return new MissionAcgAcceptedProjectionStore(
                Path.Combine(this.temporaryRoot, suffix, "mission-state"),
                this.catalog);
        }

        private MissionAcgAcceptedProjection CreateProjection(
            SourceOffer source,
            int salt,
            int ownerInstance)
        {
            return this.CreateProjection(
                source,
                salt,
                ownerInstance,
                MissionAcgAcceptancePhase.QfuSent);
        }

        private MissionAcgAcceptedProjection CreateProjection(
            SourceOffer source,
            int salt,
            int ownerInstance,
            MissionAcgAcceptancePhase phase)
        {
            MissionRollType missionType =
                MissionTypeCatalog.TypeFromIcon(source.Offer.MissionIconId);
            var owner = new MissionAcgIdentityRecord(0xC350, ownerInstance);
            int livePlayfield = FirstLivePlayfield(this.catalog, salt);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(
                        0x200000 + salt,
                        missionType,
                        source.Offer.Quality,
                        owner));
            QuestActionList action = source.Offer.QuestActions[0];
            DateTime offeredUtc =
                new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(salt);
            DateTime acceptedUtc = offeredUtc.AddMinutes(1);
            DateTime expiryUtc = offeredUtc.AddHours(48);
            MissionAcgInstanceBinding binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x50000000 + salt),
                    IdentityRecord(source.Offer.QuestIdentity),
                    owner,
                    null,
                    missionType,
                    source.Offer.Quality,
                    0x200000 + salt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x60000000 + salt),
                    IdentityRecord(action.Playfield),
                    action.Unknown18,
                    action.Unknown19,
                    action.X,
                    action.Y,
                    action.Z,
                    IdentityRecord(source.Roll.MissionTerminalIdentity),
                    bundle,
                    livePlayfield,
                    acceptedUtc,
                    expiryUtc);
            int runtimeInstance =
                unchecked((int)0x60000000)
                | ((livePlayfield
                    - MissionAcgAllocationService.MinimumLivePlayfield2) << 8)
                | 1;
            MissionAcgIdentityRecord objective =
                (int)phase >= (int)MissionAcgAcceptancePhase.ObjectivePersisted
                    ? new MissionAcgIdentityRecord(0xC350, runtimeInstance)
                    : null;
            MissionAcgIdentityRecord artifact =
                missionType == MissionRollType.RepairMachine
                || missionType == MissionRollType.FindItemReturn
                    ? new MissionAcgIdentityRecord(0xC73D, 0x53000000 + salt)
                    : null;
            int qfuVersion = ExpectedQfuVersion(missionType);
            int qfuFlag = missionType == MissionRollType.FindPerson ? 64 : 0;

            MissionAcgLifecycleState lifecycle =
                (int)phase >= (int)MissionAcgAcceptancePhase.AcceptanceCommitted
                    ? MissionAcgLifecycleState.Accepted
                    : MissionAcgLifecycleState.Reserved;
            return MissionAcgAcceptedProjection.Create(
                binding,
                source.Body,
                source.OfferIndex,
                source.Roll.LevelSlider,
                DecodeSlider(source.Roll.GoodBadSlider),
                DecodeSlider(source.Roll.OrderChaosSlider),
                DecodeSlider(source.Roll.OpenHiddenSlider),
                DecodeSlider(source.Roll.PhysicalMysticalSlider),
                DecodeSlider(source.Roll.HeadOnStealthSlider),
                DecodeSlider(source.Roll.MoneyExperienceSlider),
                offeredUtc,
                expiryUtc,
                qfuVersion,
                qfuFlag,
                phase,
                objective,
                artifact,
                MissionTypeCatalog.TypeFromIcon(source.Offer.MissionIconId)
                    == MissionRollType.RepairMachine ? 100348 : 0,
                MissionTypeCatalog.TypeFromIcon(source.Offer.MissionIconId)
                    == MissionRollType.RepairMachine ? 100348 : 0,
                lifecycle,
                MissionAcgCleanupState.None,
                acceptedUtc);
        }

        private static IDictionary<MissionRollType, SourceOffer> FindAllMissionTypes()
        {
            var sources = new Dictionary<MissionRollType, SourceOffer>();
            for (int rollIndex = 0;
                 rollIndex < MissionRollService.CapturedRollCount;
                 rollIndex++)
            {
                QuestAlternativeMessage roll =
                    MissionRollService.DecodeCapturedRoll(rollIndex);
                byte[] body = MissionRollService.SerializeBody(roll);
                for (int offerIndex = 0;
                     offerIndex < roll.QuestInfos.Length;
                     offerIndex++)
                {
                    QuestInfo offer = roll.QuestInfos[offerIndex];
                    MissionRollType type =
                        MissionTypeCatalog.TypeFromIcon(offer.MissionIconId);
                    if (IsRequired(type) && !sources.ContainsKey(type))
                    {
                        sources.Add(
                            type,
                            new SourceOffer(roll, body, offerIndex, offer));
                    }
                }
            }

            Assert.AreEqual(
                RequiredTypes.Length,
                sources.Count,
                "The finalized mission-roll corpus must retain all five accepted mission contracts.");
            return sources;
        }

        private static bool IsRequired(MissionRollType type)
        {
            for (int i = 0; i < RequiredTypes.Length; i++)
            {
                if (RequiredTypes[i] == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ExpectedQfuVersion(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                case MissionRollType.FindPerson:
                case MissionRollType.RepairMachine:
                    return 16;
                case MissionRollType.FindItem:
                    return 15;
                case MissionRollType.FindItemReturn:
                    return 8;
                default:
                    Assert.Fail("Unexpected generated mission type " + type + ".");
                    return 0;
            }
        }

        private static int DecodeSlider(byte value)
        {
            int decoded;
            Assert.IsTrue(
                MissionSliderProfile.TryDecodeSignedPercent(value, out decoded));
            return decoded;
        }

        private static MissionAcgIdentityRecord IdentityRecord(Identity identity)
        {
            Assert.IsNotNull(identity);
            return new MissionAcgIdentityRecord((int)identity.Type, identity.Instance);
        }

        private static int FirstLivePlayfield(
            MissionAcgLayoutCatalog catalog,
            int salt)
        {
            int value = MissionAcgAllocationService.MinimumLivePlayfield2 + salt;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            Assert.IsTrue(value <= MissionAcgAllocationService.MaximumLivePlayfield2);
            return value;
        }

        private static void AssertProjectionExact(
            MissionAcgAcceptedProjection expected,
            MissionAcgAcceptedProjection actual)
        {
            Assert.AreEqual(expected.FormatVersion, actual.FormatVersion);
            Assert.AreEqual(expected.Binding.AcceptedQuestIdentity, actual.Binding.AcceptedQuestIdentity);
            Assert.AreEqual(expected.Binding.OriginalOfferIdentity, actual.Binding.OriginalOfferIdentity);
            Assert.AreEqual(expected.Binding.OwnerIdentity, actual.Binding.OwnerIdentity);
            Assert.AreEqual(expected.Binding.TeamIdentity, actual.Binding.TeamIdentity);
            Assert.AreEqual(expected.Binding.MissionType, actual.Binding.MissionType);
            Assert.AreEqual(expected.Binding.MissionQuality, actual.Binding.MissionQuality);
            Assert.AreEqual(expected.Binding.DeterministicSeed, actual.Binding.DeterministicSeed);
            Assert.AreEqual(expected.Binding.MissionKeyIdentity, actual.Binding.MissionKeyIdentity);
            Assert.AreEqual(expected.Binding.ExteriorEntranceIdentity, actual.Binding.ExteriorEntranceIdentity);
            Assert.AreEqual(expected.Binding.ExteriorEntranceLow, actual.Binding.ExteriorEntranceLow);
            Assert.AreEqual(expected.Binding.ExteriorEntranceHigh, actual.Binding.ExteriorEntranceHigh);
            Assert.AreEqual(expected.Binding.ExteriorX, actual.Binding.ExteriorX);
            Assert.AreEqual(expected.Binding.ExteriorY, actual.Binding.ExteriorY);
            Assert.AreEqual(expected.Binding.ExteriorZ, actual.Binding.ExteriorZ);
            Assert.AreEqual(expected.Binding.IssuingTerminalIdentity, actual.Binding.IssuingTerminalIdentity);
            Assert.AreEqual(expected.Binding.SelectedBundleId, actual.Binding.SelectedBundleId);
            Assert.AreEqual(expected.Binding.SelectedBundlePayloadSha256, actual.Binding.SelectedBundlePayloadSha256);
            Assert.AreEqual(expected.Binding.AcgBuildingIdentity, actual.Binding.AcgBuildingIdentity);
            Assert.AreEqual(expected.Binding.AllocatedLivePlayfield2, actual.Binding.AllocatedLivePlayfield2);
            Assert.AreEqual(expected.Binding.AcceptedUtc, actual.Binding.AcceptedUtc);
            Assert.AreEqual(expected.Binding.ExpiryUtc, actual.Binding.ExpiryUtc);
            CollectionAssert.AreEqual(expected.SelectedRollBody, actual.SelectedRollBody);
            Assert.AreEqual(expected.SelectedRollBodySha256, actual.SelectedRollBodySha256);
            Assert.AreEqual(expected.SelectedOfferIndex, actual.SelectedOfferIndex);
            Assert.AreEqual(expected.RawLevelSlider, actual.RawLevelSlider);
            Assert.AreEqual(expected.GoodBadSlider, actual.GoodBadSlider);
            Assert.AreEqual(expected.OrderChaosSlider, actual.OrderChaosSlider);
            Assert.AreEqual(expected.OpenHiddenSlider, actual.OpenHiddenSlider);
            Assert.AreEqual(expected.PhysicalMysticalSlider, actual.PhysicalMysticalSlider);
            Assert.AreEqual(expected.HeadOnStealthSlider, actual.HeadOnStealthSlider);
            Assert.AreEqual(expected.MoneyExperienceSlider, actual.MoneyExperienceSlider);
            Assert.AreEqual(expected.OfferedUtc, actual.OfferedUtc);
            Assert.AreEqual(expected.OfferExpiryUtc, actual.OfferExpiryUtc);
            Assert.AreEqual(expected.MissionIconId, actual.MissionIconId);
            Assert.AreEqual(expected.Title, actual.Title);
            Assert.AreEqual(expected.Description, actual.Description);
            Assert.AreEqual(expected.FrozenCashReward, actual.FrozenCashReward);
            Assert.AreEqual(expected.FrozenExperienceReward, actual.FrozenExperienceReward);
            Assert.AreEqual(expected.FrozenItemLowId, actual.FrozenItemLowId);
            Assert.AreEqual(expected.FrozenItemHighId, actual.FrozenItemHighId);
            Assert.AreEqual(expected.FrozenItemQuality, actual.FrozenItemQuality);
            Assert.AreEqual(expected.FrozenItemCount, actual.FrozenItemCount);
            Assert.AreEqual(expected.QfuVersion, actual.QfuVersion);
            Assert.AreEqual(expected.QfuQuestIdentityFlag, actual.QfuQuestIdentityFlag);
            Assert.AreEqual(expected.AcceptancePhase, actual.AcceptancePhase);
            Assert.AreEqual(expected.RuntimeObjectiveIdentity, actual.RuntimeObjectiveIdentity);
            Assert.AreEqual(expected.MissionArtifactIdentity, actual.MissionArtifactIdentity);
            Assert.AreEqual(expected.RepairArtifactLowId, actual.RepairArtifactLowId);
            Assert.AreEqual(expected.RepairArtifactHighId, actual.RepairArtifactHighId);
            Assert.AreEqual(expected.LifecycleState, actual.LifecycleState);
            Assert.AreEqual(expected.CleanupState, actual.CleanupState);
            Assert.AreEqual(expected.UpdatedUtc, actual.UpdatedUtc);

            QuestInfo expectedOffer = expected.ReconstructOffer();
            QuestInfo actualOffer = actual.ReconstructOffer();
            Assert.AreEqual(expectedOffer.QuestIdentity, actualOffer.QuestIdentity);
            Assert.AreEqual(expectedOffer.ShortInfo, actualOffer.ShortInfo);
            Assert.AreEqual(expectedOffer.Info, actualOffer.Info);
            Assert.AreEqual(expectedOffer.CashReward, actualOffer.CashReward);
            Assert.AreEqual(expectedOffer.ExperienceReward, actualOffer.ExperienceReward);
        }

        private static void AssertRewardExact(
            QuestInfo expected,
            MissionAcgAcceptedProjection projection,
            QuestInfo restored)
        {
            int expectedCount = expected.ItemRewards == null ? 0 : expected.ItemRewards.Length;
            int restoredCount = restored.ItemRewards == null ? 0 : restored.ItemRewards.Length;
            Assert.AreEqual(expectedCount, restoredCount);
            Assert.AreEqual(expectedCount == 0 ? 0 : 1, projection.FrozenItemCount);
            if (expectedCount == 0)
            {
                Assert.AreEqual(0, projection.FrozenItemLowId);
                Assert.AreEqual(0, projection.FrozenItemHighId);
                Assert.AreEqual(0, projection.FrozenItemQuality);
                return;
            }

            Assert.AreEqual(expected.ItemRewards[0].LowId, restored.ItemRewards[0].LowId);
            Assert.AreEqual(expected.ItemRewards[0].HighId, restored.ItemRewards[0].HighId);
            Assert.AreEqual(expected.ItemRewards[0].Quality, restored.ItemRewards[0].Quality);
            Assert.AreEqual(expected.ItemRewards[0].LowId, projection.FrozenItemLowId);
            Assert.AreEqual(expected.ItemRewards[0].HighId, projection.FrozenItemHighId);
            Assert.AreEqual(expected.ItemRewards[0].Quality, projection.FrozenItemQuality);
        }

        private static string PersistOne(
            MissionAcgAcceptedProjectionStore store,
            MissionAcgAcceptedProjection projection)
        {
            MissionAcgAcceptedProjection persisted;
            string failure;
            Assert.IsTrue(store.TryCreate(projection, out persisted, out failure), failure);
            return OnlyRecordPath(store);
        }

        private static string OnlyRecordPath(MissionAcgAcceptedProjectionStore store)
        {
            string[] paths =
                Directory.GetFiles(
                    store.DirectoryPath,
                    "*" + MissionAcgAcceptedProjectionStore.FileExtension,
                    SearchOption.TopDirectoryOnly);
            Assert.AreEqual(1, paths.Length);
            return paths[0];
        }

        private static void AssertInvalid(
            MissionAcgAcceptedProjectionStore store,
            string expectedDiagnostic)
        {
            MissionAcgAcceptedProjectionLoadResult loaded = store.LoadAll();
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains(
                string.Join("|", loaded.Diagnostics),
                expectedDiagnostic);
        }

        private static void MutateFieldWithoutHash(string path, string key)
        {
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            string prefix = key + "=";
            bool changed = false;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    lines[i] = lines[i] + "A";
                    changed = true;
                    break;
                }
            }

            Assert.IsTrue(changed);
            File.WriteAllText(
                path,
                string.Join("\r\n", lines) + "\r\n",
                new UTF8Encoding(false));
        }

        private static void RewriteRecord(
            string path,
            string replaceKey,
            string replaceValue,
            string removeKey)
        {
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = lines[i].Substring(0, separator);
                if (key == "RecordSha256" || key == removeKey)
                {
                    continue;
                }

                values[key] =
                    key == replaceKey
                        ? replaceValue
                        : lines[i].Substring(separator + 1);
            }

            var canonical = new StringBuilder();
            foreach (KeyValuePair<string, string> field in values)
            {
                canonical.Append(field.Key);
                canonical.Append('=');
                canonical.Append(field.Value);
                canonical.Append("\r\n");
            }

            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
                hash = ToHex(sha.ComputeHash(bytes)).ToLowerInvariant();
            }

            File.WriteAllText(
                path,
                Header
                + "\r\n"
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

        private sealed class SourceOffer
        {
            internal SourceOffer(
                QuestAlternativeMessage roll,
                byte[] body,
                int offerIndex,
                QuestInfo offer)
            {
                this.Roll = roll;
                this.Body = body;
                this.OfferIndex = offerIndex;
                this.Offer = offer;
            }

            internal QuestAlternativeMessage Roll { get; private set; }

            internal byte[] Body { get; private set; }

            internal int OfferIndex { get; private set; }

            internal QuestInfo Offer { get; private set; }
        }
    }
}
