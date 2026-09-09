namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Trade;

    [TestClass]
    public sealed class TradeRulesTests
    {
        const int LowId = 100;
        const int HighId = 200;

        static readonly StubCatalog Catalog = new StubCatalog()
            .Add(LowId, quality: 1, price: 100)
            .Add(HighId, quality: 200, price: 10000)
            .Add(300, quality: 50, price: 500);

        [TestMethod]
        public void ClampCashKeepsValueInsideClientSafeRange()
        {
            Assert.AreEqual(0, TradeRules.ClampCash(-1));
            Assert.AreEqual(0, TradeRules.ClampCash(0));
            Assert.AreEqual(1234, TradeRules.ClampCash(1234));
            Assert.AreEqual(TradeRules.MaxCash, TradeRules.ClampCash(TradeRules.MaxCash));
            Assert.AreEqual(TradeRules.MaxCash, TradeRules.ClampCash(long.MaxValue));
        }

        [TestMethod]
        public void ItemValueReturnsLowPriceAtBottomOfTheBandAndHighPriceAtTop()
        {
            Assert.AreEqual(100, TradeRules.ItemValue(Catalog, LowId, HighId, quality: 1));
            Assert.AreEqual(10000, TradeRules.ItemValue(Catalog, LowId, HighId, quality: 200));
        }

        [TestMethod]
        public void ItemValueScalesQuadraticallyBetweenTemplates()
        {
            // QL100 sits just under halfway up the 1-200 band, so it costs roughly a quarter of
            // the 100..10000 value spread: 100 + 99^2 * 9900 / 199^2.
            int mid = TradeRules.ItemValue(Catalog, LowId, HighId, quality: 100);
            Assert.AreEqual(2550, mid);
        }

        [TestMethod]
        public void ItemValueUsesLowPriceWhenTemplatesShareQuality()
        {
            Assert.AreEqual(500, TradeRules.ItemValue(Catalog, 300, 300, quality: 50));
        }

        [TestMethod]
        public void ItemValueIsZeroForUnknownTemplates()
        {
            Assert.AreEqual(0, TradeRules.ItemValue(Catalog, 999, 999, quality: 50));
        }

        [TestMethod]
        public void PricingSkillStepsClampsComputerLiteracyToZeroThroughThreeThousand()
        {
            Assert.AreEqual(0, TradeRules.PricingSkillSteps(-100));
            Assert.AreEqual(0, TradeRules.PricingSkillSteps(0));
            Assert.AreEqual(25, TradeRules.PricingSkillSteps(1000));
            Assert.AreEqual(75, TradeRules.PricingSkillSteps(TradeRules.MaxPricingComputerLiteracy));
            Assert.AreEqual(75, TradeRules.PricingSkillSteps(TradeRules.MaxPricingComputerLiteracy + 1));
            Assert.AreEqual(75, TradeRules.PricingSkillSteps(int.MaxValue));
        }

        [TestMethod]
        public void ShopPricesStopChangingOnceComputerLiteracyHitsTheCap()
        {
            int atCap = TradeRules.PricingSkillSteps(TradeRules.MaxPricingComputerLiteracy);
            int overCap = TradeRules.PricingSkillSteps(9000);

            Assert.AreEqual(
                TradeRules.BuyPrice(1000, sellModifier: 100, skillSteps: atCap),
                TradeRules.BuyPrice(1000, sellModifier: 100, skillSteps: overCap));
            Assert.AreEqual(
                TradeRules.SellPrice(1000, buyModifier: 10, skillSteps: atCap),
                TradeRules.SellPrice(1000, buyModifier: 10, skillSteps: overCap));
        }

        [TestMethod]
        public void BuyPriceDropsAsComputerLiteracyStepsRise()
        {
            // value 1000, sellmodifier 100 => 1000 credits with no skill discount.
            Assert.AreEqual(1000, TradeRules.BuyPrice(1000, sellModifier: 100, skillSteps: 0));
            Assert.AreEqual(900, TradeRules.BuyPrice(1000, sellModifier: 100, skillSteps: 10));
            Assert.AreEqual(0, TradeRules.BuyPrice(1000, sellModifier: 100, skillSteps: 100));
        }

        [TestMethod]
        public void BuyPriceNeverGoesNegativeWhenSkillExceedsTheDiscountRange()
        {
            Assert.AreEqual(0, TradeRules.BuyPrice(1000, sellModifier: 100, skillSteps: 250));
        }

        [TestMethod]
        public void SellPriceRisesWithComputerLiteracyStepsAndVendorBuyModifier()
        {
            Assert.AreEqual(100, TradeRules.SellPrice(1000, buyModifier: 10, skillSteps: 0));
            Assert.AreEqual(110, TradeRules.SellPrice(1000, buyModifier: 10, skillSteps: 10));
            Assert.AreEqual(0, TradeRules.SellPrice(1000, buyModifier: 0, skillSteps: 10));
        }

    }
}
