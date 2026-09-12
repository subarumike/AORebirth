namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Security.Cryptography;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;

    internal static partial class Program
    {
        private static void ValidateCutoverReconciliation(string connectionString)
        {
            var dao = new MySqlMissionDao(() => new MySqlConnection(connectionString));
            ValidateGeneratedConnectionAndRollback(dao, connectionString);
            ValidateAuthoredInventoryReconciliation(dao, connectionString);
            ValidateUnknownCommitOutcome(connectionString);
            Console.WriteLine("MISSION_DAO_CUTOVER_RECONCILIATION=PASS");
        }

        private static void ValidateGeneratedConnectionAndRollback(MySqlMissionDao dao, string connectionString)
        {
            Expect<InvalidOperationException>(() => new MySqlMissionDao(() => null).ReadOffers(101), "generated-null-read-connection-rejected");
            Expect<InvalidOperationException>(() => new MySqlMissionDao(() => null).ReserveIdentities("offer", 1), "generated-null-write-connection-rejected");
            int offerId = dao.ReserveIdentities("offer", 1);
            Require(offerId > 0, "generated-closed-connection-reserves-identity");
            var batch = ReconciledBatch(offerId, "reconcile.generated");
            Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.Applied, "generated-closed-connection-publishes");
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=101 AND Type=50000 AND StatId=61") == 115,
                "generated-fee-and-offer-committed-together");
            var restored = dao.ReadOffers(101).Single();
            var expected = batch.Offers.Single();
            expected.IssuingTerminalType = batch.TerminalType;
            expected.IssuingTerminalInstance = batch.TerminalInstance;
            expected.IssuingTerminalPlayfield = batch.TerminalPlayfield;
            foreach (var property in typeof(GeneratedMissionOffer).GetProperties())
            {
                object before = property.GetValue(expected);
                object after = property.GetValue(restored);
                bool equal = before is byte[] bytes ? bytes.SequenceEqual((byte[])after) : Equals(before, after);
                Require(equal, "generated-frozen-field-preserved-" + property.Name);
            }
            Require(dao.PublishOffers(batch).Status == GeneratedMissionResultStatus.AlreadyApplied, "generated-batch-replay-preserved");

            // The parent batch and replacement update execute before the duplicate offer insert fails.
            var conflicting = ReconciledBatch(offerId, "reconcile.generated.conflict");
            Expect<MySqlException>(() => dao.PublishOffers(conflicting), "generated-provider-failure-after-writes");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM generatedmissionbatches WHERE BatchIdentity='reconcile.generated.conflict'") == 0,
                "generated-failed-batch-rolled-back");
            Require(dao.ReadOffers(101).Single().State == GeneratedMissionState.Offered, "generated-replacement-update-rolled-back");
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=101 AND Type=50000 AND StatId=61") == 115,
                "generated-failed-batch-preserves-cash");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionrewardledger WHERE QuestId='generated-offer:reconcile.generated.conflict'") == 0,
                "generated-failed-batch-has-no-fee-claim");
            Require(dao.ReadAccepted(101).Count == 0 && dao.ReadAccepted(101, 0xDAC3, 1) == null,
                "generated-closed-connection-binding-reads");
            Require(dao.ReadObjects(101, 0xDAC3, 1).Count == 0 && dao.ReadArtifacts(101, 0xDAC3, 1).Count == 0,
                "generated-closed-connection-object-artifact-reads");
            ValidateGeneratedFailureDiagnostics(dao, connectionString);
        }

        private static void ValidateGeneratedFailureDiagnostics(MySqlMissionDao dao, string connectionString)
        {
            var failedBatch = ReconciledBatch(dao.ReserveIdentities("offer", 1), "reconcile.generated.rollback");
            var failedConnection = new FaultConnection(connectionString) { FailurePoint = "fee-ledger", FailRollback = true };
            try
            {
                new MySqlMissionDao(() => failedConnection).PublishOffers(failedBatch);
                Require(false, "generated-ledger-failure-required");
            }
            catch (InjectedPersistenceException exception)
            {
                Require(ReferenceEquals(exception, failedConnection.OperationFailure), "generated-original-failure-preserved");
                Require(ReferenceEquals(exception.Data["MissionDao.RollbackFailure"], failedConnection.RollbackFailure),
                    "generated-secondary-rollback-failure-inspectable");
            }
            Require(failedConnection.Disposed && failedConnection.LastTransaction.Disposed, "generated-failed-rollback-disposes-resources");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM generatedmissionbatches WHERE BatchIdentity='reconcile.generated.rollback'") == 0
                && Scalar(connectionString, "SELECT COUNT(*) FROM generatedmissionoffers WHERE BatchIdentity='reconcile.generated.rollback'") == 0,
                "generated-ledger-failure-rolls-back-batch-and-offer");
            Require(Scalar(connectionString, "SELECT StatValue FROM stats WHERE Instance=101 AND Type=50000 AND StatId=61") == 115,
                "generated-ledger-failure-rolls-back-fee");

            foreach (bool afterCommit in new[] { false, true })
            {
                string identity = afterCommit ? "reconcile.generated.commit-after" : "reconcile.generated.commit-before";
                var batch = ReconciledBatch(dao.ReserveIdentities("offer", 1), identity);
                var connection = new FaultConnection(connectionString) { FailurePoint = afterCommit ? "commit-after" : "commit-before" };
                try
                {
                    new MySqlMissionDao(() => connection).PublishOffers(batch);
                    Require(false, "generated-commit-failure-required");
                }
                catch (MissionCommitOutcomeUnknownException exception)
                {
                    Require(ReferenceEquals(exception.InnerException, connection.OperationFailure), "generated-commit-uncertainty-preserves-provider-failure");
                }
                Require(connection.Disposed && connection.LastTransaction.Disposed, "generated-commit-failure-disposes-resources");
                // Exact synthetic identities above, never external SQL input.
                long rows = Scalar(connectionString, "SELECT COUNT(*) FROM generatedmissionbatches WHERE BatchIdentity='" + identity + "'");
                Require(rows == (afterCommit ? 1 : 0), afterCommit ? "generated-lost-ack-has-durable-batch" : "generated-precommit-failure-rolls-back");
            }
        }

        private static void ValidateAuthoredInventoryReconciliation(MySqlMissionDao dao, string connectionString)
        {
            IMissionInventoryMutationTransaction escaped = null;
            dao.Execute(101, tx =>
            {
                escaped = (IMissionInventoryMutationTransaction)tx;
                escaped.ApplyInventoryMutation(new[] { ReconciledItem(910001, 101) }, Array.Empty<MissionItemInstanceData>());
                return true;
            });
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=910001 AND ContainerType=104 AND ContainerInstance=101 AND ContainerPlacement=101 AND StackCount=2") == 1,
                "authored-inventory-extension-commits-exact-item");
            Expect<InvalidOperationException>(() => escaped.ApplyInventoryMutation(Array.Empty<MissionItemInstanceData>(), Array.Empty<MissionItemInstanceData>()),
                "authored-inventory-escaped-scope-rejected");

            var pending = NewMission("reconcile.inventory-failure");
            bool providerFailed = false;
            Expect<InvalidOperationException>(() => dao.Execute(101, tx =>
            {
                tx.SaveMission(Key(pending.QuestId), pending);
                try
                {
                    ((IMissionInventoryMutationTransaction)tx).ApplyInventoryMutation(
                        new[] { ReconciledItem(910002, 102), ReconciledItem(910003, 101) },
                        Array.Empty<MissionItemInstanceData>());
                }
                catch (MySqlException) { providerFailed = true; }
                return true;
            }), "authored-inventory-caught-failure-poisons-scope");
            Require(providerFailed, "authored-inventory-real-unique-location-failure-reached");
            Require(pending.Version == 0 && dao.GetMission(Key(pending.QuestId)) == null, "authored-inventory-failure-restores-mission-version-and-row");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId IN (910002,910003)") == 0,
                "authored-inventory-partial-grant-rolled-back");
            Require(Scalar(connectionString, "SELECT StackCount FROM item_instances WHERE InstanceId=910001") == 2,
                "authored-inventory-existing-item-preserved");
        }

        private static void ValidateUnknownCommitOutcome(string connectionString)
        {
            MySqlConnection connection = null;
            IMissionDaoTransaction escaped = null;
            var pending = NewMission("reconcile.commit-transport");
            try
            {
                new MySqlMissionDao(() => connection = new MySqlConnection(connectionString)).Execute(101, tx =>
                {
                    escaped = tx;
                    tx.SaveMission(Key(pending.QuestId), pending);
                    connection.Close();
                    return true;
                });
                Require(false, "commit-transport-failure-missing");
            }
            catch (MissionCommitOutcomeUnknownException exception)
            {
                Require(exception.InnerException != null, "commit-uncertainty-preserves-provider-failure");
                Require(!exception.Data.Contains("MissionDao.RollbackFailure"), "commit-uncertainty-is-not-reported-as-rollback");
            }
            Expect<InvalidOperationException>(() => escaped.GetMission(Key(pending.QuestId)), "commit-uncertainty-invalidates-scope");
            Require(pending.Version == 1, "commit-uncertainty-does-not-assume-rollback-version");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM missionstates WHERE QuestId='reconcile.commit-transport'") == 0,
                "known-test-transport-close-rolls-back-before-commit");
        }

        private static GeneratedMissionOfferBatch ReconciledBatch(int offerId, string identity)
        {
            byte[] wire = { 1, 2, 3, 4 }; // Synthetic persistence fixture only.
            return new GeneratedMissionOfferBatch
            {
                OwnerId = 101, OwnerType = 50000, BatchIdentity = identity, RollSeed = 77, ResponseNonce = 88,
                Fee = 5, CurrentCash = 120, TerminalType = 0xDAC1, TerminalInstance = 55, TerminalPlayfield = 500,
                LevelSlider = 10, GoodBadSlider = 20, OrderChaosSlider = 30, OpenHiddenSlider = 40,
                PhysicalMysticalSlider = 50, HeadOnStealthSlider = 60, MoneyExperienceSlider = 70,
                OfferedAtUtcTicks = 1000, ExpiresAtUtcTicks = 2000,
                Offers = new[] { new GeneratedMissionOffer
                {
                    OwnerId = 101, BatchIdentity = identity, OfferIndex = 0, OfferType = 0xDAC3, OfferInstance = offerId,
                    MissionType = 0, Quality = 25, DestinationType = 0xC9C6, DestinationInstance = 6011, DestinationPlayfield = 127,
                    DestinationX = 123.125f, DestinationY = 7.5f, DestinationZ = -92.25f,
                    EntranceType = 0xC9C6, EntranceInstance = 6011, EntranceLow = 111, EntranceHigh = 112,
                    CashReward = 50, ExperienceReward = 100, RewardLowId = 99, RewardHighId = 100, RewardQuality = 25, RewardCount = 1,
                    Title = "Disposable normalized mission", Description = "Synthetic DAO reconciliation fixture.",
                    FrozenWireBody = wire, FrozenWireSha256 = Convert.ToHexString(SHA256.HashData(wire)).ToLowerInvariant(),
                    State = GeneratedMissionState.Offered, OfferedAtUtcTicks = 1000, ExpiresAtUtcTicks = 2000, Version = 1
                } }
            };
        }

        private static MissionItemInstanceData ReconciledItem(int instance, int slot)
        {
            return new MissionItemInstanceData
            {
                InstanceId = instance, ContainerType = 104, ContainerInstance = 101, ContainerPlacement = slot,
                ItemType = 0xC76D, LowId = 10, HighId = 10, Quality = 1, StackCount = 2, Source = 0
            };
        }
    }
}
