namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Linq;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;

    internal static partial class Program
    {
        private static void ValidateGeneratedEntranceIdentity(MySqlMissionDao dao, string connectionString)
        {
            // Captured entrance identity bits; all other values use the existing synthetic DAO fixture.
            const uint capturedInstance = 0xC00001F9u;
            int signedInstance = unchecked((int)capturedInstance);
            Require(signedInstance < 0, "entrance-captured-instance-has-sign-bit");
            int offerId = dao.ReserveIdentities("offer", 2);
            var zero = EntranceIdentityBatch(offerId, "identity.zero", 0);
            Expect<ArgumentException>(() => dao.PublishOffers(zero), "entrance-zero-rejected");
            Require(dao.ReadOffers(101).Count == 0, "entrance-zero-not-persisted");

            var positive = EntranceIdentityBatch(offerId, "identity.positive", 6011);
            Require(dao.PublishOffers(positive).Status == GeneratedMissionResultStatus.Applied, "entrance-positive-published");
            var positiveReload = new MySqlMissionDao(() => new MySqlConnection(connectionString)).ReadOffers(101).Single();
            Require(positiveReload.EntranceType == 0xDAC6 && positiveReload.EntranceInstance == 6011, "entrance-positive-readback");

            var captured = EntranceIdentityBatch(offerId + 1, "identity.captured", signedInstance);
            Require(dao.PublishOffers(captured).Status == GeneratedMissionResultStatus.Applied, "entrance-captured-published");
            var readBack = new MySqlMissionDao(() => new MySqlConnection(connectionString)).ReadOffers(101)
                .Single(value => value.OfferInstance == offerId + 1);
            Require(readBack.EntranceType == 0xDAC6 && unchecked((uint)readBack.EntranceInstance) == capturedInstance,
                "entrance-captured-readback-exact-uint-bits");
            Require(Scalar(connectionString, "SELECT EntranceInstance FROM generatedmissionoffers WHERE BatchIdentity='identity.captured'") == signedInstance,
                "entrance-signed-int-sql-storage");
            var expected = captured.Offers.Single();
            expected.IssuingTerminalType = captured.TerminalType;
            expected.IssuingTerminalInstance = captured.TerminalInstance;
            expected.IssuingTerminalPlayfield = captured.TerminalPlayfield;
            foreach (var property in typeof(GeneratedMissionOffer).GetProperties())
            {
                object before = property.GetValue(expected);
                object after = property.GetValue(readBack);
                Require(before is byte[] bytes ? bytes.SequenceEqual((byte[])after) : Equals(before, after),
                    "entrance-frozen-field-unchanged-" + property.Name);
            }
            Require(dao.PublishOffers(captured).Status == GeneratedMissionResultStatus.AlreadyApplied,
                "entrance-captured-idempotent-publication");

            foreach (int invalid in new[] { 0, -1, signedInstance })
            {
                var owner = EntranceIdentityBatch(offerId + 1, "identity.invalid-owner", signedInstance);
                owner.OwnerId = invalid;
                owner.Offers[0].OwnerId = invalid;
                Expect<ArgumentException>(() => dao.PublishOffers(owner), "owner-positive-only-batch");
                var offerOwner = EntranceIdentityBatch(offerId + 1, "identity.invalid-offer-owner", signedInstance);
                offerOwner.Offers[0].OwnerId = invalid;
                Expect<ArgumentException>(() => dao.PublishOffers(offerOwner), "owner-positive-only-offer");
                var offer = EntranceIdentityBatch(invalid, "identity.invalid-offer", signedInstance);
                Expect<ArgumentException>(() => dao.PublishOffers(offer), "offer-positive-only-publication");

                foreach (Action<GeneratedMissionAcceptance> invalidate in new Action<GeneratedMissionAcceptance>[]
                {
                    value => value.OwnerId = invalid,
                    value => value.OfferInstance = invalid,
                    value => value.QuestInstance = invalid,
                    value => value.KeyInstance = invalid
                })
                {
                    var acceptance = EntranceIdentityAcceptance(offerId + 1);
                    invalidate(acceptance);
                    Expect<ArgumentException>(() => dao.Accept(acceptance), "owner-offer-quest-key-positive-only-acceptance");
                }

                var invalidItem = EntranceIdentityAcceptance(offerId + 1);
                invalidItem.Artifacts = new[] { ReconciledItem(910001, 64), ReconciledItem(invalid, 65) };
                Expect<ArgumentException>(() => dao.Accept(invalidItem), "item-instance-positive-only-grant");
                Require(dao.ReadAccepted(101).Count == 0 && dao.ReadObjects(101, 0xDAC3, 110001).Count == 0
                    && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances") == 0,
                    "invalid-item-rolls-back-binding-object-and-positive-key");
            }

            Require(dao.ReadOffers(101).Count == 2, "invalid-identities-publish-no-offers");
            Require(dao.Accept(EntranceIdentityAcceptance(offerId + 1)).Status == GeneratedMissionResultStatus.Applied,
                "positive-owner-offer-quest-item-still-accepted");
            var acceptedReload = new MySqlMissionDao(() => new MySqlConnection(connectionString)).ReadAccepted(101, 0xDAC3, 110001);
            Require(acceptedReload != null && acceptedReload.KeyInstance == 910001
                && acceptedReload.Offer.EntranceType == 0xDAC6
                && unchecked((uint)acceptedReload.Offer.EntranceInstance) == capturedInstance,
                "accepted-entrance-reload-exact-uint-bits");
            Require(Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=910001 AND ContainerInstance=101") == 1,
                "positive-item-still-persisted");
            Console.WriteLine("ENTRANCE_ZERO_REJECTED=PASS ENTRANCE_POSITIVE_ACCEPTED=PASS CAPTURED_0xC00001F9_ROUNDTRIP=PASS");
            Console.WriteLine("OWNER_OFFER_QUEST_ITEM_POSITIVE_ONLY=PASS SCHEMA_CHANGED=NO PRODUCTION_CONTACT=NO");
        }

        private static GeneratedMissionOfferBatch EntranceIdentityBatch(int offerId, string identity, int entranceInstance)
        {
            var batch = ReconciledBatch(offerId, identity);
            batch.Offers[0].EntranceType = 0xDAC6;
            batch.Offers[0].EntranceInstance = entranceInstance;
            return batch;
        }

        private static GeneratedMissionAcceptance EntranceIdentityAcceptance(int offerId)
        {
            return new GeneratedMissionAcceptance
            {
                OwnerId = 101, OfferType = 0xDAC3, OfferInstance = offerId, QuestType = 0xDAC3, QuestInstance = 110001,
                KeyInstance = 910001, BundleId = "disposable-identity", BundleSha256 = new string('a', 64),
                BuildingType = 0xC9C6, BuildingInstance = 6001, LivePlayfield = 120001,
                ObjectiveType = 50000, ObjectiveInstance = 7001, ObjectiveTemplateId = 9001, ObjectiveInteraction = 1, RequiredCount = 1,
                AcceptedAtUtcTicks = 1001, ExpiresAtUtcTicks = 1001 + TimeSpan.TicksPerHour * 48,
                Artifacts = new[] { ReconciledItem(910001, 64) },
                Objects = new[] { new GeneratedMissionObject
                {
                    OwnerId = 101, QuestType = 0xDAC3, QuestInstance = 110001, RuntimeType = 50000, RuntimeInstance = 7001,
                    CapturedType = 50000, CapturedInstance = 7011, Kind = 7, TemplateId = 9001, HeadingW = 1,
                    Level = 25, CurrentHealth = 100, MaxHealth = 100, Version = 1, UpdatedAtUtcTicks = 1001
                } }
            };
        }
    }
}
