// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SubwayLootPoolRulesTests
    {
        [TestMethod]
        public void RegularEnemyUsesDungeonAndEnemyTypePools()
        {
            SubwayLootRollContext context = Context(
                "Disobedient Bot",
                "disobedient_bot",
                17649,
                9,
                7,
                false,
                false);

            SubwayLootPoolSelectionPlan plan = SubwayLootPoolRules.BuildSelectionPlan(context);

            Assert.AreSame(context, plan.Context);
            Assert.AreEqual(2, plan.Pools.Length);
            Assert.AreEqual(SubwayLootPoolKind.Dungeon, plan.Pools[0].Kind);
            Assert.AreEqual("subway.127.dungeon", plan.Pools[0].Key);
            Assert.AreEqual(SubwayLootPoolKind.EnemyType, plan.Pools[1].Kind);
            Assert.AreEqual("subway.127.enemy.disobedient_bot", plan.Pools[1].Key);
        }

        [TestMethod]
        public void NamedAndBossEnemiesUseDedicatedPoolsOnly()
        {
            SubwayLootPoolSelectionPlan named = SubwayLootPoolRules.BuildSelectionPlan(
                Context("Named Enemy", "named_enemy", 30001, 18, 12, true, false));
            SubwayLootPoolSelectionPlan boss = SubwayLootPoolRules.BuildSelectionPlan(
                Context("Boss Enemy", "boss_enemy", 30002, 25, 16, true, true));

            Assert.AreEqual(1, named.Pools.Length);
            Assert.AreEqual(SubwayLootPoolKind.Named, named.Pools[0].Kind);
            Assert.AreEqual("subway.127.named.named_enemy", named.Pools[0].Key);
            Assert.AreEqual(1, boss.Pools.Length);
            Assert.AreEqual(SubwayLootPoolKind.Boss, boss.Pools[0].Kind);
            Assert.AreEqual("subway.127.boss.boss_enemy", boss.Pools[0].Key);
        }

        [TestMethod]
        public void WeightedPoolSelectsOneCandidateAtDeterministicBoundaries()
        {
            SubwayLootPoolCandidate first = Observed("first", 1001, 6, 10);
            SubwayLootPoolCandidate second = Observed("second", 1002, 2, 10);
            SubwayLootPoolCandidate third = Observed("third", 1003, 2, 10);
            var pool = new SubwayLootPoolDefinition(
                "subway.127.enemy.disobedient_bot",
                SubwayLootPoolKind.EnemyType,
                0,
                new[] { first, second, third });

            int requestedMaximum = 0;
            SubwayLootPoolRollResult firstLow = SubwayLootPoolRules.Roll(
                pool,
                max =>
                    {
                        requestedMaximum = max;
                        return 0;
                    });
            SubwayLootPoolRollResult firstHigh = SubwayLootPoolRules.Roll(pool, max => 5);
            SubwayLootPoolRollResult secondLow = SubwayLootPoolRules.Roll(pool, max => 6);
            SubwayLootPoolRollResult secondHigh = SubwayLootPoolRules.Roll(pool, max => 7);
            SubwayLootPoolRollResult thirdLow = SubwayLootPoolRules.Roll(pool, max => 8);
            SubwayLootPoolRollResult thirdHigh = SubwayLootPoolRules.Roll(pool, max => 9);

            Assert.AreEqual(10, requestedMaximum);
            Assert.AreSame(first, firstLow.WeightedCandidate);
            Assert.AreSame(first, firstHigh.WeightedCandidate);
            Assert.AreSame(second, secondLow.WeightedCandidate);
            Assert.AreSame(second, secondHigh.WeightedCandidate);
            Assert.AreSame(third, thirdLow.WeightedCandidate);
            Assert.AreSame(third, thirdHigh.WeightedCandidate);
            Assert.AreEqual(0, firstLow.GuaranteedCandidates.Length);
        }

        [TestMethod]
        public void TenOfTenObservedSampleRemainsWeightedAndNeverGuaranteed()
        {
            SubwayLootPoolCandidate candidate = SubwayLootPoolCandidate.FromObservedSample(
                "observed-ten-of-ten",
                104683,
                104684,
                10,
                10,
                10,
                10,
                37,
                "capture-20260713-033511");

            Assert.AreEqual(10, candidate.ObservedCount);
            Assert.AreEqual(10, candidate.ObservedKills);
            Assert.AreEqual(37, candidate.Weight);
            Assert.IsFalse(candidate.ExplicitlyGuaranteed);
        }

        [TestMethod]
        public void WeightedPoolCanProduceAnEmptyOutcome()
        {
            SubwayLootPoolCandidate candidate = Observed("candidate", 2001, 7, 10);
            var pool = new SubwayLootPoolDefinition(
                "subway.127.dungeon",
                SubwayLootPoolKind.Dungeon,
                3,
                new[] { candidate });

            SubwayLootPoolRollResult emptyLow = SubwayLootPoolRules.Roll(pool, max => 0);
            SubwayLootPoolRollResult emptyHigh = SubwayLootPoolRules.Roll(pool, max => 2);
            SubwayLootPoolRollResult firstItemBucket = SubwayLootPoolRules.Roll(pool, max => 3);

            Assert.IsNull(emptyLow.WeightedCandidate);
            Assert.IsNull(emptyHigh.WeightedCandidate);
            Assert.AreSame(candidate, firstItemBucket.WeightedCandidate);
        }

        [TestMethod]
        public void RollContextCarriesPlayfieldEnemyAndPlayerLevels()
        {
            SubwayLootRollContext context = Context(
                "Disobedient Bot",
                "disobedient_bot",
                17649,
                9,
                14,
                false,
                false);
            SubwayLootPoolSelectionPlan plan = SubwayLootPoolRules.BuildSelectionPlan(context);

            Assert.AreEqual(127, plan.Context.PlayfieldId);
            Assert.AreEqual("Disobedient Bot", plan.Context.EnemyName);
            Assert.AreEqual("disobedient_bot", plan.Context.EnemyTypeKey);
            Assert.AreEqual(17649, plan.Context.MonsterData);
            Assert.AreEqual(9, plan.Context.EnemyLevel);
            Assert.AreEqual(14, plan.Context.PlayerLevel);
            Assert.IsFalse(plan.Context.IsNamed);
            Assert.IsFalse(plan.Context.IsBoss);
            AssertInvalidEnemyTypeKey(string.Empty);
            AssertInvalidEnemyTypeKey("Disobedient Bot");
        }

        private static SubwayLootRollContext Context(
            string name,
            string enemyTypeKey,
            int monsterData,
            int enemyLevel,
            int playerLevel,
            bool isNamed,
            bool isBoss)
        {
            return new SubwayLootRollContext(
                SubwayLootPoolRules.SubwayPlayfieldId,
                name,
                enemyTypeKey,
                monsterData,
                enemyLevel,
                playerLevel,
                isNamed,
                isBoss);
        }

        private static SubwayLootPoolCandidate Observed(
            string key,
            int itemId,
            int observedCount,
            int observedKills)
        {
            return SubwayLootPoolCandidate.FromObservedSample(
                key,
                itemId,
                itemId,
                1,
                1,
                observedCount,
                observedKills,
                observedCount,
                "test-capture");
        }

        private static void AssertInvalidEnemyTypeKey(string enemyTypeKey)
        {
            try
            {
                Context("Invalid Enemy", enemyTypeKey, 30003, 1, 1, false, false);
            }
            catch (System.ArgumentException)
            {
                return;
            }

            Assert.Fail("Unsafe or empty enemy type keys must fail closed.");
        }
    }
}
