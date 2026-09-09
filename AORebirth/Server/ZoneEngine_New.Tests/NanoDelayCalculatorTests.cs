namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Helpers;

    [TestClass]
    public sealed class NanoDelayCalculatorTests
    {
        [TestMethod]
        public void NeutralAggDefAndNoInitiativeLeaveTheTemplateDelay()
        {
            int attack = NanoDelayCalculator.AttackTimeCentiseconds(
                attackDelayCentiseconds: 300,
                attackDelayCapCentiseconds: 100,
                aggDef: NanoDelayCalculator.AggDefNeutral,
                nanoInitiative: 0);

            Assert.AreEqual(300, attack);
        }

        [TestMethod]
        public void AggressiveStanceAndInitiativeBothCutTheDelay()
        {
            // 300 - (100 - 25) - (200 / 2) = 125, above the 100 cap.
            int attack = NanoDelayCalculator.AttackTimeCentiseconds(300, 100, aggDef: 100, nanoInitiative: 200);

            Assert.AreEqual(125, attack);
        }

        [TestMethod]
        public void ReductionNeverGoesBelowTheTemplateCap()
        {
            int attack = NanoDelayCalculator.AttackTimeCentiseconds(300, 100, aggDef: 100, nanoInitiative: 1000);

            Assert.AreEqual(100, attack);
        }

        [TestMethod]
        public void InitiativeAboveTheSoftCapContributesAtAThird()
        {
            // 1200 + (1500 - 1200) / 3 = 1300 initiative, so 650 comes off a 1000 delay.
            int recharge = NanoDelayCalculator.RechargeTimeCentiseconds(
                rechargeDelayCentiseconds: 1000,
                rechargeDelayCapCentiseconds: 100,
                aggDef: NanoDelayCalculator.AggDefNeutral,
                nanoInitiative: 1500);

            Assert.AreEqual(350, recharge);
        }

        [TestMethod]
        public void DefensiveStanceNeverPushesPastTheTemplateDelay()
        {
            int recharge = NanoDelayCalculator.RechargeTimeCentiseconds(500, 100, aggDef: 0, nanoInitiative: 0);

            Assert.AreEqual(500, recharge);
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
