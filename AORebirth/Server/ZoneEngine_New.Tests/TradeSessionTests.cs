namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Trade;

    [TestClass]
    public sealed class TradeOfferTests
    {
        [TestMethod]
        public void AddFillsTheLowestFreeSlotAndLocksTheItem()
        {
            TradeOffer offer = new();
            Item first = TestWorld.CreateItem(instanceId: 1);
            Item second = TestWorld.CreateItem(instanceId: 2);

            Assert.AreEqual(0, offer.Add(first));
            Assert.AreEqual(1, offer.Add(second));
            Assert.IsTrue(first.Locked);
            Assert.IsTrue(second.Locked);
        }

        [TestMethod]
        public void AddReportsFailureOnceTheWindowIsFull()
        {
            TradeOffer offer = new();
            for (int i = 0; i < TradeOffer.Capacity; i++)
                Assert.AreEqual(i, offer.Add(TestWorld.CreateItem(instanceId: i + 1)));

            Item overflow = TestWorld.CreateItem(instanceId: 99);
            Assert.AreEqual(-1, offer.Add(overflow));
            Assert.IsFalse(overflow.Locked);
            Assert.AreEqual(TradeOffer.Capacity, offer.Count);
        }

        [TestMethod]
        public void RemoveUnlocksTheItemAndFreesTheSlotForReuse()
        {
            TradeOffer offer = new();
            Item item = TestWorld.CreateItem();
            offer.Add(item);

            Assert.AreSame(item, offer.Remove(0));
            Assert.IsFalse(item.Locked);
            Assert.IsNull(offer.Remove(0));
            Assert.AreEqual(0, offer.Add(TestWorld.CreateItem(instanceId: 2)));
        }

        [TestMethod]
        public void TryRestorePutsAnItemBackOnTheTableAfterAFailedRemoval()
        {
            TradeOffer offer = new();
            Item item = TestWorld.CreateItem();
            offer.Add(item);
            offer.Remove(0);

            Assert.IsTrue(offer.TryRestore(0, item));
            Assert.IsTrue(item.Locked);
            Assert.AreEqual(1, offer.Count);
        }

        [TestMethod]
        public void TryRestoreRefusesOccupiedAndOutOfRangeSlots()
        {
            TradeOffer offer = new();
            offer.Add(TestWorld.CreateItem(instanceId: 1));

            Assert.IsFalse(offer.TryRestore(0, TestWorld.CreateItem(instanceId: 2)));
            Assert.IsFalse(offer.TryRestore(-1, TestWorld.CreateItem(instanceId: 3)));
            Assert.IsFalse(offer.TryRestore(TradeOffer.Capacity, TestWorld.CreateItem(instanceId: 4)));
        }

        [TestMethod]
        public void DrainAllEmptiesTheOfferAndUnlocksEveryItem()
        {
            TradeOffer offer = new();
            Item first = TestWorld.CreateItem(instanceId: 1);
            Item second = TestWorld.CreateItem(instanceId: 2);
            offer.Add(first);
            offer.Add(second);

            Item[] drained = offer.DrainAll();

            Assert.AreEqual(2, drained.Length);
            Assert.AreEqual(0, offer.Count);
            Assert.IsFalse(first.Locked);
            Assert.IsFalse(second.Locked);
            Assert.AreEqual(0, offer.DrainAll().Length);
        }
    }

    [TestClass]
    public sealed class TradeSessionTests
    {
        static readonly Identity Bag = new() { Type = IdentityType.TradeWindow, Instance = 1 };

        [TestMethod]
        public void PlayerTradeRoutesEachSideToItsOwnOffer()
        {
            Player initiator = TestWorld.CreatePlayer(1, "Alpha");
            Player partner = TestWorld.CreatePlayer(2, "Beta");
            TradeSession session = new(Bag, TradeKind.Player, initiator, partner, machine: null);

            Assert.AreSame(session.InitiatorOffer, session.OfferFor(initiator));
            Assert.AreSame(session.PartnerOffer, session.OfferFor(partner));
            Assert.AreSame(partner, session.Other(initiator));
            Assert.AreSame(initiator, session.Other(partner));
            Assert.IsTrue(session.Involves(initiator));
            Assert.IsTrue(session.Involves(partner));
            Assert.IsFalse(session.Involves(TestWorld.CreatePlayer(3)));
        }

        [TestMethod]
        public void PlayerTradeNeedsBothSidesToAcceptAndEnd()
        {
            Player initiator = TestWorld.CreatePlayer(1);
            Player partner = TestWorld.CreatePlayer(2);
            TradeSession session = new(Bag, TradeKind.Player, initiator, partner, machine: null);

            session.InitiatorOffer.Accepted = true;
            Assert.IsFalse(session.BothAccepted);

            session.PartnerOffer.Accepted = true;
            Assert.IsTrue(session.BothAccepted);

            session.InitiatorOffer.Ended = true;
            Assert.IsFalse(session.BothEnded);

            session.PartnerOffer.Ended = true;
            Assert.IsTrue(session.BothEnded);
        }

        [TestMethod]
        public void ShopTradeIgnoresThePartnerSideOfTheAgreement()
        {
            Player shopper = TestWorld.CreatePlayer(1);
            TradeSession session = new(Bag, TradeKind.Shop, shopper, partner: null, machine: null);

            session.InitiatorOffer.Accepted = true;
            session.InitiatorOffer.Ended = true;

            Assert.IsTrue(session.BothAccepted);
            Assert.IsTrue(session.BothEnded);
            Assert.IsNull(session.Other(shopper));
        }

        [TestMethod]
        public void ClearAcceptancesResetsBothSidesSoAnEditReopensTheNegotiation()
        {
            Player initiator = TestWorld.CreatePlayer(1);
            Player partner = TestWorld.CreatePlayer(2);
            TradeSession session = new(Bag, TradeKind.Player, initiator, partner, machine: null);
            session.InitiatorOffer.Accepted = true;
            session.InitiatorOffer.Ended = true;
            session.PartnerOffer.Accepted = true;
            session.PartnerOffer.Ended = true;

            session.ClearAcceptances();

            Assert.IsFalse(session.BothAccepted);
            Assert.IsFalse(session.BothEnded);
        }

        [TestMethod]
        public void ShopPicksAreCappedAtTheWindowSize()
        {
            TradeSession session = ShopSession();

            for (int i = 0; i < TradeOffer.Capacity; i++)
                Assert.AreEqual(i, session.AddShopPick(i));

            Assert.AreEqual(-1, session.AddShopPick(99));
            Assert.AreEqual(TradeOffer.Capacity, session.ShopPicks.Count);
        }

        [TestMethod]
        public void RemovingAShopPickShiftsTheLaterPaneEntriesDown()
        {
            TradeSession session = ShopSession();
            session.AddShopPick(10);
            session.AddShopPick(11);
            session.AddShopPick(12);

            Assert.IsTrue(session.RemoveShopPick(1));

            CollectionAssert.AreEqual(new[] { 10, 12 }, new System.Collections.Generic.List<int>(session.ShopPicks));
        }

        [TestMethod]
        public void RemovingAnUnknownPaneSlotIsRejected()
        {
            TradeSession session = ShopSession();
            session.AddShopPick(10);

            Assert.IsFalse(session.RemoveShopPick(-1));
            Assert.IsFalse(session.RemoveShopPick(1));
            Assert.AreEqual(1, session.ShopPicks.Count);
        }

        [TestMethod]
        public void ClearShopPicksEmptiesTheBuyPane()
        {
            TradeSession session = ShopSession();
            session.AddShopPick(10);
            session.AddShopPick(11);

            session.ClearShopPicks();

            Assert.AreEqual(0, session.ShopPicks.Count);
        }

        static TradeSession ShopSession()
            => new(Bag, TradeKind.Shop, TestWorld.CreatePlayer(1), partner: null, machine: null);
    }
}
