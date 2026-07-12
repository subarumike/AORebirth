// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core;

    [TestClass]
    public class DamageCalculationTests
    {
        [TestMethod]
        public void RepositoryLegacyNormalHitPreservesExistingDamageRules()
        {
            QueuedDamageRandomSource randomSource = new QueuedDamageRandomSource(2, 3);

            DamageCalculationResult minimumRoll = CombatDamageRules.CalculateDetailed(
                1,
                3,
                0,
                1,
                false,
                randomSource);

            DamageCalculationResult maximumRoll = CombatDamageRules.CalculateDetailed(
                1,
                3,
                2,
                1,
                false,
                randomSource);

            DamageCalculationResult playerFallback = CombatDamageRules.CalculateDetailed(
                0,
                0,
                -8,
                1,
                true,
                randomSource);

            DamageCalculationResult npcFallback = CombatDamageRules.CalculateDetailed(
                -5,
                -2,
                0,
                0,
                false,
                randomSource);

            Assert.AreEqual(2, minimumRoll.FinalTargetDamage);
            Assert.AreEqual(5, maximumRoll.FinalTargetDamage);
            Assert.AreEqual(CombatDamageRules.PlayerFallbackDamage, playerFallback.FinalTargetDamage);
            Assert.AreEqual(CombatDamageRules.NpcFallbackDamage, npcFallback.FinalTargetDamage);
            Assert.AreEqual(DamageEvidenceClassification.ProvenRepositoryBehavior, maximumRoll.EvidenceClassification);
            AssertStage(maximumRoll, "RollOrSelectBaseDamage", DamageCalculationStageStatus.Preserved);
            AssertStage(maximumRoll, "ApplyFlatDamageModifiers", DamageCalculationStageStatus.Preserved);
            AssertStage(maximumRoll, "ReturnTrace", DamageCalculationStageStatus.Applied);
        }

        [TestMethod]
        public void FixedCapturedDamageBypassesUnprovenFormulaStages()
        {
            DamageCalculationResult result = DamageCalculator.Calculate(
                new DamageCalculationRequest
                {
                    Context = new DamageCalculationContext
                    {
                        Mode = DamageCalculationMode.PvM,
                        AttackCategory = DamageAttackCategory.FixedDamage,
                        CompatibilityPolicy = "captured-subway-thief-fixed-attack-info",
                        EvidenceSource = "PlayfieldLifecycleTraceTests.Thief contract"
                    },
                    Source = new DamageSourceSnapshot
                    {
                        Category = DamageSourceCategory.Npc,
                        Level = 1,
                        AttackRating = 5000
                    },
                    Target = new DamageTargetSnapshot
                    {
                        Category = DamageTargetCategory.Player,
                        CurrentHealth = 100,
                        MaximumHealth = 100
                    },
                    Definition = new DamageDefinition
                    {
                        FixedDamage = 9,
                        BaseMinimum = 9,
                        BaseMaximum = 9,
                        DamageType = DamageType.Projectile,
                        EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior
                    },
                    Mitigation = new DamageMitigationSet
                    {
                        MatchingArmor = 999999
                    },
                    Policy = DamageCalculationPolicy.CapturedFixedDamage("captured-subway-thief-fixed-9"),
                    EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior
                },
                new QueuedDamageRandomSource());

            Assert.AreEqual(9, result.FinalTargetDamage);
            Assert.AreEqual(DamageType.Projectile, result.SelectedDamageType);
            AssertStage(result, "RollOrSelectBaseDamage", DamageCalculationStageStatus.Applied);
            AssertStage(result, "ApplyArmorMitigation", DamageCalculationStageStatus.EvidenceBlocked);
        }

        [TestMethod]
        public void AttackRatingCapTraceCoversBoundaryInputsWithoutActivatingUnknownScaling()
        {
            Assert.AreEqual(999, CalculateWithAttackRatingCap(999, 1000).AttackRatingCapResult);
            Assert.AreEqual(1000, CalculateWithAttackRatingCap(1000, 1000).AttackRatingCapResult);
            Assert.AreEqual(1000, CalculateWithAttackRatingCap(1200, 1000).AttackRatingCapResult);
            Assert.AreEqual(800, CalculateWithAttackRatingCap(1200, 800).AttackRatingCapResult);
            Assert.AreEqual(1100, CalculateWithAttackRatingCap(1200, 1100).AttackRatingCapResult);

            DamageCalculationResult missingCap = CalculateWithoutAttackRatingCap(1200);
            DamageCalculationResult zeroCap = CalculateWithAttackRatingCap(1200, 0);
            DamageCalculationResult invalidCap = CalculateWithAttackRatingCap(1200, -1);

            Assert.AreEqual(1200, missingCap.AttackRatingCapResult);
            Assert.AreEqual(1200, zeroCap.AttackRatingCapResult);
            Assert.AreEqual(1200, invalidCap.AttackRatingCapResult);
            AssertStage(missingCap, "ApplyAttackRatingCap", DamageCalculationStageStatus.Skipped);
            AssertStage(zeroCap, "ApplyAttackRatingCap", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(invalidCap, "ApplyAttackRatingCap", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(missingCap, "ApplyPre1000AttackRatingScaling", DamageCalculationStageStatus.Skipped);
            AssertStage(missingCap, "ApplyPost1000AttackRatingScaling", DamageCalculationStageStatus.Skipped);
        }

        [TestMethod]
        public void UnprovenMechanicsRemainEvidenceBlockedAndSideEffectFree()
        {
            DamageCalculationResult result = DamageCalculator.Calculate(
                new DamageCalculationRequest
                {
                    Context = new DamageCalculationContext
                    {
                        Mode = DamageCalculationMode.PvP,
                        AttackCategory = DamageAttackCategory.SpecialAttack,
                        SpecialAttackCategory = SpecialAttackCategory.FullAuto
                    },
                    Source = new DamageSourceSnapshot
                    {
                        Category = DamageSourceCategory.Player,
                        Level = 20
                    },
                    Target = new DamageTargetSnapshot
                    {
                        Category = DamageTargetCategory.Player,
                        CurrentHealth = 1000,
                        MaximumHealth = 1000
                    },
                    Definition = new DamageDefinition
                    {
                        BaseMinimum = 10,
                        BaseMaximum = 10,
                        BulletCount = 5,
                        AttackSpecificCap = 10000,
                        DamageType = DamageType.Projectile
                    },
                    Modifiers = new DamageModifierSet
                    {
                        FlatAddDamage = 2
                    },
                    Mitigation = new DamageMitigationSet
                    {
                        MatchingArmor = 100,
                        ReflectPercentage = 30,
                        ReflectCap = 40,
                        TypedAbsorbPool = 50,
                        UniversalAbsorbPool = 60,
                        DamageShield = 7
                    },
                    Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(true)
                },
                new QueuedDamageRandomSource());

            Assert.AreEqual(15, result.FinalTargetDamage);
            Assert.AreEqual(0, result.FinalAttackerDamage);
            AssertStage(result, "ApplyArmorMitigation", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ResolveSpecialSubHits", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "AggregateSubHits", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ApplySpecialCompression", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ApplyAttackSpecificCap", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ApplyPvPConversion", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ApplyPvPMaximumHealthCap", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ApplyReflect", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ConsumeTypedAbsorbs", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ConsumeUniversalAbsorbs", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ResolveReflectedReturnDamage", DamageCalculationStageStatus.EvidenceBlocked);
            AssertStage(result, "ResolveDamageShieldReturnDamage", DamageCalculationStageStatus.EvidenceBlocked);
        }

        [TestMethod]
        public void DeterministicRandomSourceReplaysBaseDamageRolls()
        {
            DamageCalculationResult first = CombatDamageRules.CalculateDetailed(
                1,
                10,
                0,
                1,
                false,
                new QueuedDamageRandomSource(4));

            DamageCalculationResult second = CombatDamageRules.CalculateDetailed(
                1,
                10,
                0,
                1,
                false,
                new QueuedDamageRandomSource(4));

            Assert.AreEqual(first.FinalTargetDamage, second.FinalTargetDamage);
            Assert.AreEqual(4, first.BaseRoll);
            Assert.AreEqual(4, second.BaseRoll);
        }

        private static DamageCalculationResult CalculateWithAttackRatingCap(int attackRating, int cap)
        {
            DamageCalculationRequest request = BuildCapRequest(attackRating);
            request.Definition.HasAttackRatingCap = true;
            request.Definition.AttackRatingCap = cap;
            return DamageCalculator.Calculate(request, new QueuedDamageRandomSource());
        }

        private static DamageCalculationResult CalculateWithoutAttackRatingCap(int attackRating)
        {
            return DamageCalculator.Calculate(BuildCapRequest(attackRating), new QueuedDamageRandomSource());
        }

        private static DamageCalculationRequest BuildCapRequest(int attackRating)
        {
            return new DamageCalculationRequest
            {
                Source = new DamageSourceSnapshot
                {
                    Category = DamageSourceCategory.Player,
                    Level = 1,
                    AttackRating = attackRating
                },
                Definition = new DamageDefinition
                {
                    BaseMinimum = 1,
                    BaseMaximum = 1,
                    EvidenceClassification = DamageEvidenceClassification.ControlledTestConfirmed
                },
                Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(true),
                EvidenceClassification = DamageEvidenceClassification.ControlledTestConfirmed
            };
        }

        private static void AssertStage(
            DamageCalculationResult result,
            string stage,
            DamageCalculationStageStatus status)
        {
            DamageCalculationStageResult stageResult = result.Trace.Stages.Single(x => x.Stage == stage);
            Assert.AreEqual(status, stageResult.Status, stage);
        }

        private sealed class QueuedDamageRandomSource : IDamageRandomSource
        {
            private readonly Queue<int> values;

            public QueuedDamageRandomSource(params int[] values)
            {
                this.values = new Queue<int>(values ?? new int[0]);
            }

            public int NextInclusive(int minimumInclusive, int maximumInclusive)
            {
                if (this.values.Count == 0)
                {
                    return minimumInclusive;
                }

                int value = this.values.Dequeue();
                Assert.IsTrue(value >= minimumInclusive && value <= maximumInclusive);
                return value;
            }

            public bool NextChance(int chanceBasisPoints)
            {
                if (this.values.Count == 0)
                {
                    return false;
                }

                return this.values.Dequeue() < chanceBasisPoints;
            }
        }
    }
}
