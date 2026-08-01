namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionOfferStoreTests
    {
        [TestMethod]
        public void ExactOfferCanBeClaimedOnlyOnce()
        {
            int owner = 0x710001;
            MissionOfferRecord ignored;
            string failure;
            QuestAlternativeMessage response = Store(owner, DateTime.UtcNow);
            Identity offer = response.QuestInfos[0].QuestIdentity;

            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    owner,
                    offer,
                    DateTime.UtcNow,
                    out ignored,
                    out failure),
                failure);
            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    owner,
                    offer,
                    DateTime.UtcNow,
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "already in progress");

            MissionOfferStore.DiscardRoll(owner);
        }

        [TestMethod]
        public void ExpiredOfferFailsBeforeItCanBeClaimed()
        {
            int owner = 0x710002;
            QuestAlternativeMessage response =
                Store(
                    owner,
                    DateTime.UtcNow.AddSeconds(
                        -MissionOfferStore.OfferLifetimeSeconds - 1));
            MissionOfferRecord ignored;
            string failure;

            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    owner,
                    response.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "expired");

            MissionOfferStore.DiscardRoll(owner);
        }

        [TestMethod]
        public void NewRollInvalidatesThePreviouslyOfferedIdentity()
        {
            int owner = 0x710003;
            QuestAlternativeMessage first = Store(owner, DateTime.UtcNow);
            Identity stale = first.QuestInfos[0].QuestIdentity;
            QuestAlternativeMessage second = MissionRollService.DecodeCapturedRoll(1);
            OffsetOfferIdentities(second, 0x01000000);
            Store(owner, second, DateTime.UtcNow);
            MissionOfferRecord ignored;
            string failure;

            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    owner,
                    stale,
                    DateTime.UtcNow,
                    out ignored,
                    out failure));
            StringAssert.Contains(failure, "replaced");

            MissionOfferStore.DiscardRoll(owner);
        }

        [TestMethod]
        public void SameMissionTypeForTwoOwnersRemainsIsolated()
        {
            int firstOwner = 0x710004;
            int secondOwner = 0x710005;
            QuestAlternativeMessage first = Store(firstOwner, DateTime.UtcNow);
            QuestAlternativeMessage second = Store(secondOwner, DateTime.UtcNow);
            MissionOfferRecord claimed;
            string failure;

            Assert.IsFalse(
                MissionOfferStore.TryClaimForAcceptance(
                    secondOwner,
                    first.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out claimed,
                    out failure));
            Assert.IsTrue(
                MissionOfferStore.TryClaimForAcceptance(
                    secondOwner,
                    second.QuestInfos[0].QuestIdentity,
                    DateTime.UtcNow,
                    out claimed,
                    out failure),
                failure);
            Assert.AreEqual(secondOwner, claimed.OwnerInstance);

            MissionOfferStore.DiscardRoll(firstOwner);
            MissionOfferStore.DiscardRoll(secondOwner);
        }

        private static QuestAlternativeMessage Store(int owner, DateTime issuedUtc)
        {
            QuestAlternativeMessage response = MissionRollService.DecodeCapturedRoll(0);
            OffsetOfferIdentities(response, owner & 0x00FFFFFF);
            Store(owner, response, issuedUtc);
            return response;
        }

        private static void Store(
            int owner,
            QuestAlternativeMessage response,
            DateTime issuedUtc)
        {
            QuestAlternativeMessage request = Request(response.MissionTerminalIdentity);
            string failure;
            Assert.IsTrue(
                MissionOfferStore.TryStoreRoll(
                    owner,
                    response,
                    request,
                    issuedUtc,
                    MissionRollService.SerializeBody(response),
                    out failure),
                failure);
        }

        private static QuestAlternativeMessage Request(Identity terminal)
        {
            return new QuestAlternativeMessage
                   {
                       VersionId = 4,
                       LevelSlider = 50,
                       GoodBadSlider = 50,
                       OrderChaosSlider = 50,
                       OpenHiddenSlider = 50,
                       PhysicalMysticalSlider = 50,
                       HeadOnStealthSlider = 50,
                       MoneyExperienceSlider = 50,
                       MissionTerminalIdentity = terminal,
                       QuestInfos = new QuestInfo[0]
                   };
        }

        private static void OffsetOfferIdentities(
            QuestAlternativeMessage response,
            int offset)
        {
            for (int i = 0; i < response.QuestInfos.Length; i++)
            {
                Identity identity = response.QuestInfos[i].QuestIdentity;
                identity.Instance = 0x30000000 + offset + i;
                response.QuestInfos[i].QuestIdentity = identity;
            }
        }
    }
}
