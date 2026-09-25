namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Helpers;

    [TestClass]
    public sealed class NanoDelayCalculatorTests
    {
        [TestMethod]
        public void ZeroAggDefAndNoInitiativeLeaveTheTemplateDelay()
        {
            int attack = NanoDelayCalculator.AttackTimeCentiseconds(
                attackDelayCentiseconds: 300,
                attackDelayCapCentiseconds: 100,
                aggDef: 0,
                nanoInitiative: 0);

            Assert.AreEqual(300, attack);
        }

        [TestMethod]
        public void AggressiveStanceAndInitiativeBothCutTheDelay()
        {
            // 500 - 80 - (200 / 2) = 320, above the 100 cap.
            int attack = NanoDelayCalculator.AttackTimeCentiseconds(500, 100, aggDef: 80, nanoInitiative: 200);

            Assert.AreEqual(320, attack);
        }

        [TestMethod]
        public void ReductionNeverGoesBelowTheTemplateCap()
        {
            int attack = NanoDelayCalculator.AttackTimeCentiseconds(300, 100, aggDef: 100, nanoInitiative: 1000);

            Assert.AreEqual(100, attack);
        }

        [TestMethod]
        public void InitiativeAboveTheSoftCapContributesAtASixth()
        {
            // (1500 - 1200) / 6 + 600 = 650 initiative, so 350 remains of a 1000 delay.
            int recharge = NanoDelayCalculator.RechargeTimeCentiseconds(
                rechargeDelayCentiseconds: 1000,
                rechargeDelayCapCentiseconds: 100,
                aggDef: 0,
                nanoInitiative: 1500);

            Assert.AreEqual(350, recharge);
        }

        [TestMethod]
        public void AggDefIsClampedAndDefensiveStanceLengthensTheDelay()
        {
            // 500 - (-100) = 600.
            Assert.AreEqual(600, NanoDelayCalculator.RechargeTimeCentiseconds(500, 100, aggDef: -100, nanoInitiative: 0));

            // -250 clamps to -100.
            Assert.AreEqual(600, NanoDelayCalculator.RechargeTimeCentiseconds(500, 100, aggDef: -250, nanoInitiative: 0));

            // 150 clamps to 100: 500 - 100 = 400.
            Assert.AreEqual(400, NanoDelayCalculator.RechargeTimeCentiseconds(500, 100, aggDef: 150, nanoInitiative: 0));
        }

        [TestMethod]
        public void TemplatesWithoutARechargeDelayNeverLockOut()
        {
            Assert.AreEqual(0, NanoDelayCalculator.RechargeTimeCentiseconds(0, 0, aggDef: 50, nanoInitiative: 100));
        }

        [TestMethod]
        public void MissingCapAllowsTheDelayToReachZero()
        {
            Assert.AreEqual(
                0,
                NanoDelayCalculator.AttackTimeCentiseconds(200, 0, aggDef: 100, nanoInitiative: 1000));
        }
    }
}
