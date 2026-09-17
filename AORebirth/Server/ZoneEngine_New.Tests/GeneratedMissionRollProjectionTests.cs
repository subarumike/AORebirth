namespace ZoneEngine_New.Tests
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine.Core.Missions;
    using ZoneEngine_New.Core.Missions;

    [TestClass]
    public sealed class GeneratedMissionRollProjectionTests
    {
        [TestMethod]
        public void CapturedBundleHashesUseDaoCanonicalHexWithoutChangingDigest()
        {
            foreach (var bundle in MissionAcgCapturedLayoutCatalog.CreateBundles())
            {
                string hash = GeneratedMissionAcgService.CanonicalBundleHash(bundle);
                Assert.AreEqual(64, hash.Length);
                Assert.IsTrue(hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
                CollectionAssert.AreEqual(Convert.FromHexString(bundle.GeneratorPayloadSha256), Convert.FromHexString(hash));
            }
        }
        private static readonly Identity Owner = new() { Type = IdentityType.CanbeAffected, Instance = 990101 };
        private static QuestAlternativeMessage Request() => new()
        {
            Identity = Owner, LevelSlider = 1,
            MissionTerminalIdentity = new Identity { Type = (IdentityType)0xDAC1, Instance = 12345 },
            QuestInfos = []
        };

        [TestMethod]
        public void SuppliedDurableAllocatorOwnsEveryGeneratedIdentity()
        {
            QuestAlternativeMessage request = Request();
            int next = 0x55569000;
            var response = GeneratedMissionRollService.Generate(request, Owner, 60, 710, 500, 500,
                MissionLocationSide.Omni, 1201445827, 1234, 4567, () => next++);
            CollectionAssert.AreEqual(Enumerable.Range(0x55569000, 5).ToArray(),
                response.QuestInfos.Select(offer => offer.QuestIdentity.Instance).ToArray());
            Assert.AreEqual(0x55569005, next);
        }

        [TestMethod]
        public void NewRuntimeCannotFallBackToFileIdentityAllocation()
        {
            Assert.ThrowsException<ArgumentNullException>(() => GeneratedMissionRollService.Generate(
                Request(), Owner, 60, 710, 500, 500, MissionLocationSide.Omni, 1201445827, 1234, 4567, null!));
            Assert.IsNull(typeof(GeneratedMissionService).Assembly.GetType("ZoneEngine.Core.Missions.MissionOfferIdentityStore"));
            Assert.IsNull(typeof(GeneratedMissionService).Assembly.GetType("ZoneEngine.Core.Missions.MissionStateDirectory"));
        }

        [TestMethod]
        public void FrozenSqlProjectionPreservesEveryDestinationRewardAndWireField()
        {
            QuestAlternativeMessage request = Request();
            int next = 0x55569000;
            var response = GeneratedMissionRollService.Generate(request, Owner, 60, 710,
                500, 500, MissionLocationSide.Omni, 1201445827, 1234, 4567, () => next++);
            DateTime issued = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
            var batch = GeneratedMissionRollProjection.Create(request, response, 710, 60, 1234, 4567, issued);
            byte[] wire = GeneratedMissionWire.Write(response);
            Assert.AreEqual(5, batch.Offers.Count);
            Assert.AreEqual(issued.AddHours(48).Ticks, batch.ExpiresAtUtcTicks);
            Assert.AreEqual(1234, batch.RollSeed);
            Assert.AreEqual(4567, batch.ResponseNonce);
            for (int i = 0; i < batch.Offers.Count; i++)
            {
                var row = batch.Offers[i];
                var offer = response.QuestInfos[i];
                var target = offer.QuestActions[0];
                Assert.AreEqual(i, row.OfferIndex);
                Assert.AreEqual(Owner.Instance, row.OwnerId);
                Assert.AreEqual(offer.QuestIdentity.Instance, row.OfferInstance);
                Assert.AreEqual((int)target.Playfield.Type, row.DestinationType);
                Assert.AreEqual(target.Playfield.Instance, row.DestinationInstance);
                Assert.AreEqual(target.X, row.DestinationX);
                Assert.AreEqual(target.Y, row.DestinationY);
                Assert.AreEqual(target.Z, row.DestinationZ);
                Assert.AreEqual(target.Unknown18, row.EntranceLow);
                Assert.AreEqual(target.Unknown19, row.EntranceHigh);
                Assert.AreEqual(offer.CashReward, row.CashReward);
                Assert.AreEqual(offer.ExperienceReward, row.ExperienceReward);
                Assert.AreEqual(offer.ItemRewards[0].LowId, row.RewardLowId);
                Assert.AreEqual(offer.ItemRewards[0].HighId, row.RewardHighId);
                Assert.AreEqual(offer.ItemRewards[0].Quality, row.RewardQuality);
                CollectionAssert.AreEqual(wire, row.FrozenWireBody);
                Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(wire)), row.FrozenWireSha256);
            }
        }

        [TestMethod]
        public void SignedSliderValuesAreStoredWithoutByteWrap()
        {
            QuestAlternativeMessage request = Request();
            request.GoodBadSlider = 156;
            request.OrderChaosSlider = 156;
            request.MoneyExperienceSlider = 156;
            int next = 0x55569000;
            var response = GeneratedMissionRollService.Generate(request, Owner, 60, 710,
                500, 500, MissionLocationSide.Omni, 1201445827, 1234, 4567, () => next++);
            var batch = GeneratedMissionRollProjection.Create(request, response, 710, 60, 1234, 4567, DateTime.UtcNow);
            Assert.AreEqual(-100, batch.GoodBadSlider);
            Assert.AreEqual(-100, batch.OrderChaosSlider);
            Assert.AreEqual(-100, batch.MoneyExperienceSlider);
        }
    }
}
