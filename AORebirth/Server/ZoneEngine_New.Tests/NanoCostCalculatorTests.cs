namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Helpers;

    [TestClass]
    public sealed class NanoCostCalculatorTests
    {
        [TestMethod]
        public void NoModifierLeavesTheTemplateCost()
        {
            Assert.AreEqual(250, NanoCostCalculator.Compute(250, npCostModifierPercent: 0));
        }

        [TestMethod]
        public void ModifierRemovesItsPercentage()
        {
            Assert.AreEqual(200, NanoCostCalculator.Compute(250, npCostModifierPercent: 20));
        }

        [TestMethod]
        public void StackedModifiersCannotBeatTheReductionCap()
        {
            Assert.AreEqual(
                125,
                NanoCostCalculator.Compute(250, npCostModifierPercent: NanoCostCalculator.MaxReductionPercent + 40));
        }

        [TestMethod]
        public void NegativeModifiersNeverRaiseTheCost()
        {
            Assert.AreEqual(250, NanoCostCalculator.Compute(250, npCostModifierPercent: -30));
        }

        [TestMethod]
        public void ACostBearingNanoNeverBecomesFree()
        {
            Assert.AreEqual(1, NanoCostCalculator.Compute(1, npCostModifierPercent: 50));
        }

        [TestMethod]
        public void FreeNanosStayFree()
        {
            Assert.AreEqual(0, NanoCostCalculator.Compute(0, npCostModifierPercent: 50));
        }
    }
}
