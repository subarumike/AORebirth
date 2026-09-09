namespace ZoneEngine_New.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Nanos;

    [TestClass]
    public sealed class NanoCastRulesTests
    {
        [TestMethod]
        public void AReadyCasterPassesTheGate()
        {
            Assert.AreEqual(NanoCastRefusal.None, NanoCastRules.Evaluate(Ready()));
        }

        [TestMethod]
        public void DeathCastingAndRechargeAllBlockACast()
        {
            Assert.AreEqual(
                NanoCastRefusal.CasterDead,
                NanoCastRules.Evaluate(Ready() with { CasterIsDead = true }));
            Assert.AreEqual(
                NanoCastRefusal.AlreadyCasting,
                NanoCastRules.Evaluate(Ready() with { CasterIsCasting = true }));
            Assert.AreEqual(
                NanoCastRefusal.Recharging,
                NanoCastRules.Evaluate(Ready() with { CasterIsRecharging = true }));
        }

        [TestMethod]
        public void MissingUploadTargetRequirementsAndNanoAreEachRefused()
        {
            Assert.AreEqual(
                NanoCastRefusal.NotUploaded,
                NanoCastRules.Evaluate(Ready() with { IsUploaded = false }));
            Assert.AreEqual(
                NanoCastRefusal.InvalidTarget,
                NanoCastRules.Evaluate(Ready() with { TargetExists = false }));
            Assert.AreEqual(
                NanoCastRefusal.TargetDead,
                NanoCastRules.Evaluate(Ready() with { TargetIsDead = true }));
            Assert.AreEqual(
                NanoCastRefusal.RequirementsNotMet,
                NanoCastRules.Evaluate(Ready() with { RequirementsMet = false }));
            Assert.AreEqual(
                NanoCastRefusal.NotEnoughNano,
                NanoCastRules.Evaluate(Ready() with { CurrentNano = 10, NanoCost = 11 }));
        }

        [TestMethod]
        public void EveryRefusalHasSomethingToTellThePlayer()
        {
            foreach (NanoCastRefusal refusal in Enum.GetValues<NanoCastRefusal>())
            {
                if (refusal == NanoCastRefusal.None)
                    continue;

                Assert.AreNotEqual(string.Empty, NanoCastRules.Describe(refusal), refusal.ToString());
            }
        }

        [TestMethod]
        public void RechargeIsADeadlineThatExpiresOnItsOwn()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Player player = TestWorld.CreatePlayer(600);

            player.StartNanoRecharge(centiseconds: 500, start);

            Assert.IsTrue(player.IsInNanoRecharge(start.AddSeconds(4)));
            Assert.IsFalse(player.IsInNanoRecharge(start.AddSeconds(5)));
        }

        [TestMethod]
        public void RechargeSurvivesAnInterruptButNotARevive()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Player player = TestWorld.CreatePlayer(601);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.StartNanoRecharge(centiseconds: 500, start);

            player.InterruptTimedActions(TimedActionInterrupt.Jump);
            Assert.IsTrue(player.IsInNanoRecharge(start.AddSeconds(1)));

            player.Revive();
            Assert.IsFalse(player.IsInNanoRecharge(start.AddSeconds(1)));
        }

        [TestMethod]
        public void ATemplateWithoutARechargeDelayNeverLocksTheCasterOut()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Player player = TestWorld.CreatePlayer(602);

            player.StartNanoRecharge(
                NanoDelayCalculator.RechargeTimeCentiseconds(0, 0, aggDef: 50, nanoInitiative: 0),
                start);

            Assert.IsFalse(player.IsInNanoRecharge(start));
        }

        [TestMethod]
        public void AnInterruptedCastIsDiscardedInsteadOfCompleting()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Player player = TestWorld.CreatePlayer(603);
            var cast = new PendingNanoCast(
                TestNanos.Create(3000, attackDelay: 300),
                player.Identity,
                nanoCost: 50,
                attackTimeCentiseconds: 300,
                start);

            player.BeginNanoCast(cast);
            Assert.IsTrue(player.IsCastingNano);
            Assert.IsFalse(cast.IsReady(start.AddSeconds(2)));
            Assert.IsTrue(cast.IsReady(start.AddSeconds(3)));

            player.InterruptTimedActions(TimedActionInterrupt.Jump);

            Assert.IsFalse(player.IsCastingNano);
            Assert.IsFalse(player.IsInNanoRecharge(start));
        }

        static NanoCastAttempt Ready()
            => new()
            {
                CasterIsDead = false,
                CasterIsCasting = false,
                CasterIsRecharging = false,
                IsUploaded = true,
                RequirementsMet = true,
                TargetExists = true,
                TargetIsDead = false,
                CurrentNano = 500,
                NanoCost = 100
            };
    }
}
