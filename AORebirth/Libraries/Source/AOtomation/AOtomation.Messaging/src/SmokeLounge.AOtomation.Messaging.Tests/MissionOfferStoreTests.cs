namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionOfferStoreTests
    {
        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-mission-offer-tests-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
            MissionOfferStore.ResetForTests();
            MissionOfferStore.Initialize(this.temporaryRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            MissionOfferStore.ResetForTests();
            if (Directory.Exists(this.temporaryRoot))
            {
                Directory.Delete(this.temporaryRoot, true);
            }
        }

        [TestMethod]
        public void CompleteOfferProjectionRoundTripsExactlyAcrossRestart()
        {
            DateTime issued = new DateTime(638896512000000000L, DateTimeKind.Utc);
            StoredRoll stored = this.Store(0x710001, 0, 0x010000, issued, 7001, 9001);
            List<MissionOfferRecord> before = MissionOfferStore.Snapshot();
            Assert.AreEqual(stored.Response.QuestInfos.Length, before.Count);

            this.Restart();

            List<MissionOfferRecord> after = MissionOfferStore.Snapshot();
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                AssertRecordEqual(before[i], after[i]);
            }

            MissionOfferRecord restored;
            Assert.IsTrue(
                MissionOfferStore.TryGetOffer(
                    stored.Owner,
                    stored.Response.QuestInfos[0].QuestIdentity,
                    issued.AddMinutes(1),
                    out restored));
            AssertRecordEqual(before[0], restored);
            Assert.AreEqual(MissionOfferLifecycleState.Pending, restored.LifecycleState);
            StringAssert.Contains(
                File.ReadAllText(MissionOfferStore.GetLedgerPathForTests(stored.Owner)),
                "FormatVersion=1");
        }

        [TestMethod]
        public void ConcurrentOwnersPublishIndependentDurableOfferBatches()
        {
            DateTime issued = new DateTime(638896512000000000L, DateTimeKind.Utc);
            var start = new ManualResetEventSlim(false);
            Task<StoredRoll> first = Task.Run(
                delegate
                {
                    start.Wait();
                    return this.Store(0x710016, 0, 0x170000, issued, 7019, 9019);
                });
            Task<StoredRoll> second = Task.Run(
                delegate
                {
                    start.Wait();
                    return this.Store(0x710017, 0, 0x180000, issued, 7020, 9020);
                });

            start.Set();
            Task.WaitAll(first, second);

            Assert.AreEqual(10, MissionOfferStore.Snapshot().Count);
            Assert.AreEqual(5, CountOwned(first.Result.Owner));
            Assert.AreEqual(5, CountOwned(second.Result.Owner));
            this.Restart();
            Assert.AreEqual(5, CountOwned(first.Result.Owner));
            Assert.AreEqual(5, CountOwned(second.Result.Owner));
        }

        [TestMethod]
        public void UnpaidPreparedBatchCannotBeAcceptedOrReplaceThePaidRoll()
        {
            DateTime issued = new DateTime(638896512000000000L, DateTimeKind.Utc);
            StoredRoll paid = this.Store(0x710018, 0, 0x190000, issued, 7021, 9021);
            StoredRoll prepared = this.PrepareCore(
                paid.Owner,
                1,
                0x1A0000,
                issued.AddSeconds(1),
                7022,
                9022);

            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(paid.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Prepared,
                Find(prepared.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);

            MissionOfferRecord claimed;
            string failure;
            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    prepared.Owner,
                    prepared.Response.QuestInfos[0].QuestIdentity,
                    issued.AddMinutes(1),
                    out claimed,
                    out failure));
            Assert.IsTrue(
                MissionOfferStore.TryBeginFeeCharge(
                    prepared.Batch,
                    4,
                    issued.AddSeconds(2),
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.FeeChargePending,
                Find(prepared.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    prepared.Owner,
                    prepared.Response.QuestInfos[0].QuestIdentity,
                    issued.AddMinutes(1),
                    out claimed,
                    out failure));
            Assert.IsTrue(
                MissionOfferStore.TryDiscardBatch(
                    prepared.Batch,
                    issued.AddMinutes(1),
                    "RollFeeRejected",
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(paid.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Discarded,
                Find(prepared.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
        }

        [TestMethod]
        public void RestartDiscardsUnpaidPreparationWithoutRegeneratingIt()
        {
            DateTime issued = new DateTime(638896512000000000L, DateTimeKind.Utc);
            StoredRoll paid = this.Store(0x710019, 0, 0x1B0000, issued, 7023, 9023);
            StoredRoll prepared = this.PrepareCore(
                paid.Owner,
                1,
                0x1C0000,
                issued.AddSeconds(1),
                7024,
                9024);

            this.Restart();
            MissionOfferStore.DiscardPreparedOnRestoration(issued.AddMinutes(1));

            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(paid.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Discarded,
                Find(prepared.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            MissionOfferRecord ignored;
            Assert.IsFalse(
                MissionOfferStore.TryGetOffer(
                    prepared.Owner,
                    prepared.Response.QuestInfos[0].QuestIdentity,
                    issued.AddMinutes(1),
                    out ignored));
        }

        [TestMethod]
        public void DurableFeeClaimSurvivesRestartAndPublishesTheExactBatchOnce()
        {
            DateTime issued = new DateTime(638896512000000000L, DateTimeKind.Utc);
            StoredRoll prepared = this.PrepareCore(
                Owner(0x71001A),
                0,
                0x1D0000,
                issued,
                7025,
                9025);
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryBeginFeeCharge(
                    prepared.Batch,
                    4,
                    issued.AddMilliseconds(1),
                    out failure),
                failure);

            this.Restart();
            MissionOfferStore.DiscardPreparedOnRestoration(issued.AddMinutes(1));
            bool found;
            MissionOfferBatchHandle restoredBatch;
            int rollFee;
            QuestAlternativeMessage restoredResponse;
            Assert.IsTrue(
                MissionOfferStore.TryGetFeeChargePending(
                    prepared.Owner,
                    issued.AddMinutes(1),
                    out found,
                    out restoredBatch,
                    out rollFee,
                    out restoredResponse,
                    out failure),
                failure);
            Assert.IsTrue(found);
            Assert.AreEqual(4, rollFee);
            CollectionAssert.AreEqual(
                MissionRollService.SerializeBody(prepared.Response),
                MissionRollService.SerializeBody(restoredResponse));
            Assert.IsTrue(
                MissionOfferStore.TryPublishBatch(
                    restoredBatch,
                    issued.AddMinutes(1),
                    out failure),
                failure);
            Assert.IsTrue(
                MissionOfferStore.TryPublishBatch(
                    restoredBatch,
                    issued.AddMinutes(1),
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(prepared.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            this.Restart();
            bool pendingFound;
            MissionOfferBatchHandle pendingBatch;
            QuestAlternativeMessage pendingResponse;
            Assert.IsTrue(
                MissionOfferStore.TryGetPendingRollForLogin(
                    prepared.Owner,
                    issued.AddMinutes(2),
                    out pendingFound,
                    out pendingBatch,
                    out pendingResponse,
                    out failure),
                failure);
            Assert.IsTrue(pendingFound);
            Assert.AreEqual(restoredBatch.BatchIdentity, pendingBatch.BatchIdentity);
            CollectionAssert.AreEqual(
                MissionRollService.SerializeBody(prepared.Response),
                MissionRollService.SerializeBody(pendingResponse));
        }

        [TestMethod]
        public void DuplicateClaimResumesOneDurableClaimAndAcceptedAuditCannotReplay()
        {
            StoredRoll stored = this.Store(0x710002, 0, 0x020000, DateTime.UtcNow, 7002, 9002);
            Identity offer = stored.Response.QuestInfos[0].QuestIdentity;
            MissionOfferRecord first;
            MissionOfferRecord duplicate;
            string failure;

            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    stored.Owner,
                    offer,
                    DateTime.UtcNow,
                    out first,
                    out failure),
                failure);
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    stored.Owner,
                    offer,
                    DateTime.UtcNow,
                    out duplicate,
                    out failure),
                failure);
            Assert.AreEqual(first.Revision, duplicate.Revision);
            Assert.AreEqual(
                MissionOfferLifecycleState.AcceptanceClaimed,
                duplicate.LifecycleState);

            var acceptedQuest = new MissionAcgIdentityRecord(50000, 0x6A0001);
            Assert.IsTrue(
                MissionOfferStore.TryMarkAccepted(
                    first,
                    acceptedQuest,
                    DateTime.UtcNow,
                    out failure),
                failure);
            Assert.IsTrue(
                MissionOfferStore.TryMarkAccepted(
                    duplicate,
                    acceptedQuest,
                    DateTime.UtcNow,
                    out failure),
                failure);
            Assert.IsFalse(
                MissionOfferStore.TryMarkAccepted(
                    duplicate,
                    new MissionAcgIdentityRecord(50000, 0x6A0002),
                    DateTime.UtcNow,
                    out failure));

            this.Restart();
            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    stored.Owner,
                    offer,
                    DateTime.UtcNow,
                    out duplicate,
                    out failure));
            StringAssert.Contains(failure, "already been accepted");
            MissionOfferRecord audit = Find(offer.Instance);
            Assert.AreEqual(MissionOfferLifecycleState.Accepted, audit.LifecycleState);
            Assert.AreEqual(acceptedQuest.Instance, audit.AcceptedQuestIdentity.Instance);
        }

        [TestMethod]
        public void StaleClaimCannotOverwriteAcceptedLifecycle()
        {
            StoredRoll stored = this.Store(0x710003, 0, 0x030000, DateTime.UtcNow, 7003, 9003);
            MissionOfferRecord claim;
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    stored.Owner,
                    stored.Response.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out claim,
                    out failure),
                failure);
            Assert.IsTrue(
                MissionOfferStore.TryMarkAccepted(
                    claim,
                    new MissionAcgIdentityRecord(50000, 0x6A0003),
                    DateTime.UtcNow,
                    out failure),
                failure);

            Assert.IsFalse(
                MissionOfferStore.TryReleaseClaim(
                    claim,
                    DateTime.UtcNow,
                    out failure));
            StringAssert.Contains(failure, "compare-and-swap conflict");
            Assert.AreEqual(
                MissionOfferLifecycleState.Accepted,
                Find(claim.Offer.QuestIdentity.Instance).LifecycleState);
        }

        [TestMethod]
        public void RestartRecoveryReopensOrExpiresAnUnprojectedClaimExactlyOnce()
        {
            DateTime issued = DateTime.UtcNow;
            StoredRoll active = this.Store(0x710013, 0, 0x130000, issued, 7015, 9015);
            MissionOfferRecord activeClaim;
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    active.Owner,
                    active.Response.QuestInfos[0].QuestIdentity,
                    issued.AddSeconds(1),
                    out activeClaim,
                    out failure),
                failure);
            this.Restart();
            Assert.IsTrue(
                MissionOfferStore.TryRestoreUnprojectedClaim(
                    activeClaim,
                    issued.AddSeconds(2),
                    out failure),
                failure);
            Assert.IsTrue(
                MissionOfferStore.TryRestoreUnprojectedClaim(
                    activeClaim,
                    issued.AddSeconds(2),
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(activeClaim.Offer.QuestIdentity.Instance).LifecycleState);

            DateTime oldIssued =
                issued.AddSeconds(-MissionOfferStore.OfferLifetimeSeconds - 5);
            StoredRoll expired = this.Store(0x710014, 0, 0x140000, oldIssued, 7016, 9016);
            MissionOfferRecord expiredClaim;
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    expired.Owner,
                    expired.Response.QuestInfos[0].QuestIdentity,
                    oldIssued.AddSeconds(1),
                    out expiredClaim,
                    out failure),
                failure);
            this.Restart();
            Assert.IsTrue(
                MissionOfferStore.TryRestoreUnprojectedClaim(
                    expiredClaim,
                    issued,
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.Expired,
                Find(expiredClaim.Offer.QuestIdentity.Instance).LifecycleState);
        }

        [TestMethod]
        public void ReplacementAndDiscardAreExactOwnerBatchTransitions()
        {
            DateTime issued = DateTime.UtcNow;
            StoredRoll first = this.Store(0x710004, 0, 0x040000, issued, 7004, 9004);
            StoredRoll otherOwner = this.Store(0x710005, 0, 0x050000, issued, 7005, 9005);
            StoredRoll replacement = this.Store(0x710004, 1, 0x060000, issued.AddSeconds(1), 7006, 9006);
            string failure;

            Assert.AreEqual(
                MissionOfferLifecycleState.Replaced,
                Find(first.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(otherOwner.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.IsTrue(
                MissionOfferStore.TryDiscardBatch(
                    replacement.Batch,
                    issued.AddSeconds(2),
                    "TestDiscard",
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.Discarded,
                Find(replacement.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(otherOwner.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);

            this.Restart();
            MissionOfferRecord ignored;
            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    first.Owner,
                    first.Response.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "replaced");
            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    replacement.Owner,
                    replacement.Response.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "discarded");

            StoredRoll claimedRoll = this.Store(0x710015, 0, 0x150000, issued, 7017, 9017);
            MissionOfferRecord claim;
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    claimedRoll.Owner,
                    claimedRoll.Response.QuestInfos[0].QuestIdentity,
                    issued.AddMilliseconds(1),
                    out claim,
                    out failure),
                failure);
            this.Store(0x710015, 1, 0x160000, issued.AddSeconds(1), 7018, 9018);
            Assert.IsTrue(
                MissionOfferStore.TryReleaseClaim(
                    claim,
                    issued.AddSeconds(2),
                    out failure),
                failure);
            Assert.AreEqual(
                MissionOfferLifecycleState.Replaced,
                Find(claim.Offer.QuestIdentity.Instance).LifecycleState);
        }

        [TestMethod]
        public void ExpiryTransitionsOnlyTheExactOwnerAndNeverRegenerates()
        {
            DateTime now = DateTime.UtcNow;
            StoredRoll expired =
                this.Store(
                    0x710006,
                    0,
                    0x070000,
                    now.AddSeconds(-MissionOfferStore.OfferLifetimeSeconds - 1),
                    7007,
                    9007);
            StoredRoll active = this.Store(0x710007, 0, 0x080000, now, 7008, 9008);
            DateTime feeIssued = now.AddSeconds(-MissionOfferStore.OfferLifetimeSeconds - 2);
            StoredRoll feePending = this.PrepareCore(
                Owner(0x71001B),
                0,
                0x1E0000,
                feeIssued,
                7026,
                9026);
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryBeginFeeCharge(
                    feePending.Batch,
                    4,
                    feeIssued.AddSeconds(1),
                    out failure),
                failure);
            MissionOfferStore.ExpirePending(now);

            Assert.AreEqual(
                MissionOfferLifecycleState.Expired,
                Find(expired.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(active.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                MissionOfferLifecycleState.Expired,
                Find(feePending.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            this.Restart();
            Assert.AreEqual(
                MissionOfferLifecycleState.Expired,
                Find(expired.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
        }

        [TestMethod]
        public void WrongOwnerCannotClaimAnotherOwnersSameTypeOffer()
        {
            StoredRoll first = this.Store(0x710008, 0, 0x090000, DateTime.UtcNow, 7009, 9009);
            StoredRoll second = this.Store(0x710009, 0, 0x0A0000, DateTime.UtcNow, 7010, 9010);
            MissionOfferRecord ignored;
            string failure;

            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    second.Owner,
                    first.Response.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "another owner");
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    second.Owner,
                    second.Response.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out ignored,
                    out failure),
                failure);
        }

        [TestMethod]
        public void ConcurrentDuplicateCallbacksPublishOneClaimRevision()
        {
            StoredRoll stored = this.Store(0x71000A, 0, 0x0B0000, DateTime.UtcNow, 7011, 9011);
            var start = new ManualResetEventSlim(false);
            Task<ClaimResult> first = Task.Factory.StartNew(
                delegate
                {
                    start.Wait();
                    return Claim(stored.Owner, stored.Response.QuestInfos[0].QuestIdentity);
                });
            Task<ClaimResult> second = Task.Factory.StartNew(
                delegate
                {
                    start.Wait();
                    return Claim(stored.Owner, stored.Response.QuestInfos[0].QuestIdentity);
                });
            start.Set();
            Task.WaitAll(first, second);

            Assert.IsTrue(first.Result.Succeeded, first.Result.Failure);
            Assert.IsTrue(second.Result.Succeeded, second.Result.Failure);
            Assert.AreEqual(first.Result.Record.Revision, second.Result.Record.Revision);
            Assert.AreEqual(
                1,
                MissionOfferStore.Snapshot().FindAll(
                    delegate(MissionOfferRecord record)
                    {
                        return record.Offer.QuestIdentity.Instance
                               == stored.Response.QuestInfos[0].QuestIdentity.Instance;
                    }).Count);
        }

        [TestMethod]
        public void RestoredTerminalAuditStillBlocksIdentityCollision()
        {
            StoredRoll stored = this.Store(0x71000B, 0, 0x0C0000, DateTime.UtcNow, 7012, 9012);
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryDiscardBatch(
                    stored.Batch,
                    DateTime.UtcNow,
                    "CollisionAudit",
                    out failure),
                failure);
            int identity = stored.Response.QuestInfos[0].QuestIdentity.Instance;

            this.Restart();
            Assert.IsTrue(MissionOfferStore.IsIdentityInUse(identity));
            QuestAlternativeMessage request;
            QuestAlternativeMessage colliding =
                GenerateRoll(
                    Owner(0x71000C),
                    7013,
                    9013,
                    identity,
                    out request);
            MissionOfferBatchHandle ignored;
            Assert.IsFalse(
                MissionOfferStore.TryStoreRoll(
                    Owner(0x71000C),
                    colliding,
                    request,
                    DateTime.UtcNow,
                    7013,
                    9013,
                    MissionRollService.SerializeBody(colliding),
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "collides with durable offer history");
        }

        [TestMethod]
        public void TamperedUnknownTruncatedAndDuplicateLedgersFailClosed()
        {
            string tamperedRoot = this.CreateCaseRoot("tampered");
            StoredRoll tampered = this.StoreIn(tamperedRoot, 0x71000D, 0x0E0000);
            string tamperedPath = MissionOfferStore.GetLedgerPathForTests(tampered.Owner);
            string tamperedText = File.ReadAllText(tamperedPath);
            File.WriteAllText(
                tamperedPath,
                tamperedText.Replace("MissionQuality=", "MissionQuality=9"),
                new UTF8Encoding(false));
            AssertInitializeFails(tamperedRoot, "SHA-256 mismatch");

            string versionRoot = this.CreateCaseRoot("version");
            StoredRoll version = this.StoreIn(versionRoot, 0x71000E, 0x0F0000);
            string versionPath = MissionOfferStore.GetLedgerPathForTests(version.Owner);
            RewriteAndRehash(versionPath, "FormatVersion=1", "FormatVersion=999");
            AssertInitializeFails(versionRoot, "Unknown mission offer ledger version");

            string truncatedRoot = this.CreateCaseRoot("truncated");
            StoredRoll truncated = this.StoreIn(truncatedRoot, 0x71000F, 0x100000);
            string truncatedPath = MissionOfferStore.GetLedgerPathForTests(truncated.Owner);
            string[] truncatedLines = File.ReadAllLines(truncatedPath);
            File.WriteAllLines(
                truncatedPath,
                new List<string>(truncatedLines).GetRange(0, truncatedLines.Length - 1).ToArray(),
                new UTF8Encoding(false));
            AssertInitializeFails(truncatedRoot, "lacks its SHA-256");

            string duplicateRoot = this.CreateCaseRoot("duplicate");
            this.StoreIn(duplicateRoot, 0x710010, 0x110000);
            string otherRoot = this.CreateCaseRoot("duplicate-other-owner");
            StoredRoll duplicate = this.StoreIn(otherRoot, 0x710011, 0x110000);
            string source = MissionOfferStore.GetLedgerPathForTests(duplicate.Owner);
            string duplicatePath =
                Path.Combine(
                    duplicateRoot,
                    "generated-offers",
                    "owner-50000-" + 0x710011 + ".offers");
            File.Copy(source, duplicatePath);
            AssertInitializeFails(duplicateRoot, "duplicate offer identity");
        }

        [TestMethod]
        public void TemporaryFilesAreIgnoredAndAtomicTransitionsLeaveNoPartialFiles()
        {
            StoredRoll stored = this.Store(0x710012, 0, 0x120000, DateTime.UtcNow, 7014, 9014);
            File.WriteAllText(
                Path.Combine(MissionOfferStore.DirectoryPath, "interrupted.offers.tmp"),
                "partial",
                new UTF8Encoding(false));
            this.Restart();
            Assert.AreEqual(
                MissionOfferLifecycleState.Pending,
                Find(stored.Response.QuestInfos[0].QuestIdentity.Instance).LifecycleState);
            Assert.AreEqual(
                0,
                Directory.GetFiles(MissionOfferStore.DirectoryPath, "*.bak").Length);
        }

        private StoredRoll Store(
            int ownerInstance,
            int captureIndex,
            int identityOffset,
            DateTime issuedUtc,
            int rollSeed,
            int responseNonce)
        {
            return this.StoreCore(
                Owner(ownerInstance),
                captureIndex,
                identityOffset,
                issuedUtc,
                rollSeed,
                responseNonce);
        }

        private StoredRoll StoreCore(
            Identity owner,
            int captureIndex,
            int identityOffset,
            DateTime issuedUtc,
            int rollSeed,
            int responseNonce)
        {
            StoredRoll prepared = this.PrepareCore(
                owner,
                captureIndex,
                identityOffset,
                issuedUtc,
                rollSeed,
                responseNonce);
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryBeginFeeCharge(
                    prepared.Batch,
                    60,
                    issuedUtc.AddTicks(1),
                    out failure),
                failure);
            Assert.IsTrue(
                MissionOfferStore.TryPublishBatch(
                    prepared.Batch,
                    issuedUtc.AddMilliseconds(1),
                    out failure),
                failure);
            return prepared;
        }

        private StoredRoll PrepareCore(
            Identity owner,
            int captureIndex,
            int identityOffset,
            DateTime issuedUtc,
            int rollSeed,
            int responseNonce)
        {
            QuestAlternativeMessage request;
            QuestAlternativeMessage response =
                GenerateRoll(
                    owner,
                    rollSeed,
                    responseNonce + captureIndex,
                    0x30000000 + identityOffset,
                    out request);
            MissionOfferBatchHandle batch;
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryStoreRoll(
                    owner,
                    response,
                    request,
                    issuedUtc,
                    rollSeed,
                    responseNonce + captureIndex,
                    MissionRollService.SerializeBody(response),
                    out batch,
                    out failure),
                failure);
            return new StoredRoll
                   {
                       Owner = owner,
                       Response = response,
                       Batch = batch
                   };
        }

        private StoredRoll StoreIn(
            string root,
            int ownerInstance,
            int identityOffset)
        {
            MissionOfferStore.ResetForTests();
            MissionOfferStore.Initialize(root);
            return this.StoreCore(
                Owner(ownerInstance),
                0,
                identityOffset,
                DateTime.UtcNow,
                identityOffset,
                identityOffset + 1);
        }

        private string CreateCaseRoot(string name)
        {
            string root = Path.Combine(this.temporaryRoot, name);
            Directory.CreateDirectory(root);
            return root;
        }

        private void Restart()
        {
            MissionOfferStore.ResetForTests();
            MissionOfferStore.Initialize(this.temporaryRoot);
        }

        private static int CountOwned(Identity owner)
        {
            int count = 0;
            List<MissionOfferRecord> records = MissionOfferStore.Snapshot();
            for (int i = 0; i < records.Count; i++)
            {
                if ((int)records[i].OwnerIdentity.Type == (int)owner.Type
                    && records[i].OwnerIdentity.Instance == owner.Instance)
                {
                    count++;
                }
            }

            return count;
        }

        private static MissionOfferRecord Find(int offerInstance)
        {
            List<MissionOfferRecord> records = MissionOfferStore.Snapshot();
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Offer.QuestIdentity.Instance == offerInstance)
                {
                    return records[i];
                }
            }

            Assert.Fail("Offer record was not found: " + offerInstance);
            return null;
        }

        private static ClaimResult Claim(Identity owner, Identity offer)
        {
            MissionOfferRecord record;
            string failure;
            bool succeeded =
                MissionOfferStore.TryClaimForAcceptance(
                    owner,
                    offer,
                    DateTime.UtcNow,
                    out record,
                    out failure);
            return new ClaimResult
                   {
                       Succeeded = succeeded,
                       Record = record,
                       Failure = failure
                   };
        }

        private static Identity Owner(int instance)
        {
            return new Identity
                   {
                       Type = (IdentityType)50000,
                       Instance = instance
                   };
        }

        private static QuestAlternativeMessage GenerateRoll(
            Identity owner,
            int rollSeed,
            int responseNonce,
            int firstOfferIdentity,
            out QuestAlternativeMessage request)
        {
            request =
                new QuestAlternativeMessage
                {
                    Identity = owner,
                    MissionTerminalIdentity =
                        new Identity
                        {
                            Type = (IdentityType)56001,
                            Instance = unchecked((int)0xC000028F)
                        },
                    VersionId = 4,
                    LevelSlider = 1,
                    GoodBadSlider = 0,
                    OrderChaosSlider = 0,
                    OpenHiddenSlider = 0,
                    PhysicalMysticalSlider = 0,
                    HeadOnStealthSlider = 0,
                    MoneyExperienceSlider = 0,
                    QuestInfos = new QuestInfo[0]
                };
            return MissionRollService.BuildRollResponseDeterministic(
                request,
                owner,
                4,
                100,
                0f,
                0f,
                MissionLocationSide.Omni,
                rollSeed,
                responseNonce,
                firstOfferIdentity,
                1201445827);
        }

        private static void AssertRecordEqual(
            MissionOfferRecord expected,
            MissionOfferRecord actual)
        {
            Assert.AreEqual(expected.OwnerIdentity, actual.OwnerIdentity);
            Assert.AreEqual(expected.Offer.QuestIdentity, actual.Offer.QuestIdentity);
            CollectionAssert.AreEqual(expected.SerializedRollPayload, actual.SerializedRollPayload);
            Assert.AreEqual(expected.SerializedRollPayloadSha256, actual.SerializedRollPayloadSha256);
            Assert.AreEqual(expected.OfferIndex, actual.OfferIndex);
            Assert.AreEqual(expected.BatchIdentity, actual.BatchIdentity);
            Assert.AreEqual(expected.RollSeed, actual.RollSeed);
            Assert.AreEqual(expected.ResponseNonce, actual.ResponseNonce);
            Assert.AreEqual(expected.IssuedUtc, actual.IssuedUtc);
            Assert.AreEqual(expected.ExpiresUtc, actual.ExpiresUtc);
            Assert.AreEqual(expected.LevelSlider, actual.LevelSlider);
            Assert.AreEqual(expected.GoodBadSlider, actual.GoodBadSlider);
            Assert.AreEqual(expected.OrderChaosSlider, actual.OrderChaosSlider);
            Assert.AreEqual(expected.OpenHiddenSlider, actual.OpenHiddenSlider);
            Assert.AreEqual(expected.PhysicalMysticalSlider, actual.PhysicalMysticalSlider);
            Assert.AreEqual(expected.HeadOnStealthSlider, actual.HeadOnStealthSlider);
            Assert.AreEqual(expected.MoneyExperienceSlider, actual.MoneyExperienceSlider);
            Assert.AreEqual(expected.SliderEvidenceProfile, actual.SliderEvidenceProfile);
            Assert.AreEqual(expected.IssuingTerminalIdentity, actual.IssuingTerminalIdentity);
            Assert.AreEqual(expected.MissionType, actual.MissionType);
            Assert.AreEqual(expected.MissionIconId, actual.MissionIconId);
            Assert.AreEqual(expected.MissionQuality, actual.MissionQuality);
            Assert.AreEqual(expected.Title, actual.Title);
            Assert.AreEqual(expected.Description, actual.Description);
            Assert.AreEqual(expected.FrozenCashReward, actual.FrozenCashReward);
            Assert.AreEqual(expected.FrozenExperienceReward, actual.FrozenExperienceReward);
            Assert.AreEqual(expected.FrozenItemLowId, actual.FrozenItemLowId);
            Assert.AreEqual(expected.FrozenItemHighId, actual.FrozenItemHighId);
            Assert.AreEqual(expected.FrozenItemQuality, actual.FrozenItemQuality);
            Assert.AreEqual(expected.FrozenItemCount, actual.FrozenItemCount);
            Assert.AreEqual(expected.ExteriorEntranceIdentity, actual.ExteriorEntranceIdentity);
            Assert.AreEqual(expected.ExteriorBuildingLow, actual.ExteriorBuildingLow);
            Assert.AreEqual(expected.ExteriorBuildingHigh, actual.ExteriorBuildingHigh);
            Assert.AreEqual(expected.ExteriorX, actual.ExteriorX);
            Assert.AreEqual(expected.ExteriorY, actual.ExteriorY);
            Assert.AreEqual(expected.ExteriorZ, actual.ExteriorZ);
            Assert.AreEqual(expected.LifecycleState, actual.LifecycleState);
            Assert.AreEqual(expected.Revision, actual.Revision);
        }

        private static void RewriteAndRehash(
            string path,
            string oldValue,
            string newValue)
        {
            string text = File.ReadAllText(path);
            File.WriteAllText(
                path,
                Rehash(text.Replace(oldValue, newValue)),
                new UTF8Encoding(false));
        }

        private static string Rehash(string text)
        {
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var body = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("LedgerSha256=", StringComparison.Ordinal))
                {
                    body.Add(lines[i]);
                }
            }

            body.Sort(StringComparer.Ordinal);
            string canonical = string.Join("\r\n", body.ToArray()) + "\r\n";
            string hash =
                MissionAcgHash.ComputeSha256(
                    new UTF8Encoding(false).GetBytes(canonical));
            return lines[0]
                   + "\r\n"
                   + canonical
                   + "LedgerSha256="
                   + hash
                   + "\r\n";
        }

        private static void AssertInitializeFails(
            string root,
            string expectedDiagnostic)
        {
            MissionOfferStore.ResetForTests();
            try
            {
                MissionOfferStore.Initialize(root);
                Assert.Fail("Malformed mission offer ledger unexpectedly loaded.");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.Message, expectedDiagnostic);
            }
        }

        private sealed class StoredRoll
        {
            internal Identity Owner;

            internal QuestAlternativeMessage Response;

            internal MissionOfferBatchHandle Batch;
        }

        private sealed class ClaimResult
        {
            internal bool Succeeded;

            internal MissionOfferRecord Record;

            internal string Failure;
        }
    }
}
