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
            Assert.AreEqual(DamageCalculationStrategyKind.LegacyFallback, maximumRoll.Strategy);
            Assert.AreEqual(2, maximumRoll.LegacyDamageBonusContribution);
            Assert.AreEqual(DamageEvidenceClassification.ProvenRepositoryBehavior, maximumRoll.EvidenceClassification);
            AssertStage(maximumRoll, "SelectDamageStrategy", DamageCalculationStageStatus.Applied);
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
            Assert.AreEqual(DamageCalculationStrategyKind.FixedCapturedDamage, result.Strategy);
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
        public void DamageTypeStatMappingsExposeOnlyRepositoryProvenContracts()
        {
            AssertArmorStat(DamageType.Projectile, 90);
            AssertArmorStat(DamageType.Melee, 91);
            AssertArmorStat(DamageType.Energy, 92);
            AssertArmorStat(DamageType.Chemical, 93);
            AssertArmorStat(DamageType.Radiation, 94);
            AssertArmorStat(DamageType.Cold, 95);
            AssertArmorStat(DamageType.Poison, 96);
            AssertArmorStat(DamageType.Fire, 97);
            AssertArmorStat(DamageType.Nano, 168);

            AssertAddDamageStat(DamageType.Projectile, 278);
            AssertAddDamageStat(DamageType.Melee, 279);
            AssertAddDamageStat(DamageType.Energy, 280);
            AssertAddDamageStat(DamageType.Chemical, 281);
            AssertAddDamageStat(DamageType.Radiation, 282);
            AssertAddDamageStat(DamageType.Cold, 311);
            AssertAddDamageStat(DamageType.Nano, 315);
            AssertAddDamageStat(DamageType.Fire, 316);
            AssertAddDamageStat(DamageType.Poison, 317);

            int statId;
            Assert.IsFalse(DamageCalculator.TryGetArmorStatForDamageType(DamageType.Disease, out statId));
            Assert.IsFalse(DamageCalculator.TryGetAddDamageStatForDamageType(DamageType.Disease, out statId));
        }

        [TestMethod]
        public void FormulaPolicyRemainsEvidenceBlockedUntilAllRequiredInputsAreKnown()
        {
            DamageCalculationResult missingArmor = DamageCalculator.Calculate(
                new DamageCalculationRequest
                {
                    Source = new DamageSourceSnapshot
                    {
                        Category = DamageSourceCategory.Player,
                        AttackRating = 100,
                        AddAllOff = 5
                    },
                    Definition = new DamageDefinition
                    {
                        BaseMinimum = 2,
                        BaseMaximum = 4,
                        DamageType = DamageType.Projectile,
                        HasCriticalState = true,
                        IsCritical = false
                    },
                    Policy = DamageCalculationPolicy.EvidenceBackedWeaponFormula("candidate-ordinary-weapon")
                },
                new QueuedDamageRandomSource(3));

            Assert.AreEqual(DamageCalculationStrategyKind.EvidenceBlocked, missingArmor.Strategy);
            StringAssert.Contains(missingArmor.StrategyReason, "matching AC");
            AssertStage(missingArmor, "SelectDamageStrategy", DamageCalculationStageStatus.EvidenceBlocked);
            Assert.AreEqual(15, missingArmor.FinalTargetDamage);
        }

        [TestMethod]
        public void WeightedAttackSkillInputsAreRepresentedButDoNotActivateScaling()
        {
            DamageSourceSnapshot source = new DamageSourceSnapshot
            {
                Category = DamageSourceCategory.Player,
                AddAllOff = 10
            };
            source.AttackSkillContributions.Add(new AttackSkillContribution { StatId = 105, Percentage = 67, Value = 123 });
            source.AttackSkillContributions.Add(new AttackSkillContribution { StatId = 106, Percentage = 33, Value = 456 });

            DamageCalculationResult result = DamageCalculator.Calculate(
                new DamageCalculationRequest
                {
                    Source = source,
                    Definition = new DamageDefinition
                    {
                        BaseMinimum = 1,
                        BaseMaximum = 1
                    },
                    Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(true)
                },
                new QueuedDamageRandomSource());

            Assert.AreEqual(242, result.EffectiveAttackRating);
            Assert.AreEqual(15, result.FinalTargetDamage);
            AssertStage(result, "ApplyPre1000AttackRatingScaling", DamageCalculationStageStatus.Skipped);
            AssertStage(result, "ApplyPost1000AttackRatingScaling", DamageCalculationStageStatus.Skipped);
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

        [TestMethod]
        public void WeaponDamageRequestBuilderClassifiesCompleteSingleSkillProvenanceWithoutActivatingFormula()
        {
            WeaponDamageRequestBuildResult result = WeaponDamageRequestBuilder.Build(BuildCompleteProjectileInput());

            Assert.AreEqual(WeaponDamageRequestBuildClassification.FormulaInputComplete, result.Classification);
            Assert.AreEqual(DamageCalculationStrategyKind.LegacyFallback, result.ExpectedActiveStrategy);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(1, result.Request.Definition.BaseMinimum);
            Assert.AreEqual(10, result.Request.Definition.BaseMaximum);
            Assert.AreEqual(DamageType.Projectile, result.Request.Definition.DamageType);
            Assert.AreEqual(25, result.Request.Source.AddAllOff);
            Assert.AreEqual(125, result.Request.Source.AttackSkillContributions[0].Value);
            Assert.AreEqual(100, result.Request.Source.AttackSkillContributions[0].Percentage);
            Assert.AreEqual(1, CombatDamageRules.Calculate(1, 1, 0, 1, false));
        }

        [TestMethod]
        public void WeaponDamageRequestBuilderRepresentsMultipleWeightedSkillsAndRejectsInvalidTotals()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.Weapon.AttackSkillContributions.Clear();
            input.Weapon.AttackSkillContributions.Add(new AttackSkillContribution { StatId = 103, Percentage = 67 });
            input.Weapon.AttackSkillContributions.Add(new AttackSkillContribution { StatId = 106, Percentage = 33 });
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 103, Value = 90 });
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 106, Value = 30 });

            WeaponDamageRequestBuildResult complete = WeaponDamageRequestBuilder.Build(input);

            Assert.AreEqual(WeaponDamageRequestBuildClassification.FormulaInputComplete, complete.Classification);
            Assert.AreEqual(2, complete.Request.Source.AttackSkillContributions.Count);

            input.Weapon.AttackSkillContributions[1].Percentage = 20;
            WeaponDamageRequestBuildResult invalid = WeaponDamageRequestBuilder.Build(input);

            Assert.AreEqual(WeaponDamageRequestBuildClassification.FormulaInputIncomplete, invalid.Classification);
            AssertIssue(invalid, WeaponDamageInputIssueKind.InvalidSkillWeight);
        }

        [TestMethod]
        public void WeaponDamageRequestBuilderReportsMissingAndUnknownWeaponTemplateInputs()
        {
            WeaponDamageRequestBuildResult missingSkill = WeaponDamageRequestBuilder.Build(BuildInputWithoutAttackSkills());
            WeaponDamageRequestBuildResult missingMinimum = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.HasMinimumDamage = false));
            WeaponDamageRequestBuildResult missingMaximum = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.HasMaximumDamage = false));
            WeaponDamageRequestBuildResult invertedRange = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.MinimumDamage = 30));
            WeaponDamageRequestBuildResult missingCriticalBonus = WeaponDamageRequestBuilder.Build(BuildInputWithCriticalHitWithoutBonus());
            WeaponDamageRequestBuildResult missingDamageType = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.HasDamageType = false));
            WeaponDamageRequestBuildResult unknownDamageType = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.DamageType = DamageType.Unknown));

            AssertIssue(missingSkill, WeaponDamageInputIssueKind.MissingAttackSkill);
            AssertIssue(missingMinimum, WeaponDamageInputIssueKind.MissingMinimum);
            AssertIssue(missingMaximum, WeaponDamageInputIssueKind.MissingMaximum);
            Assert.AreEqual(WeaponDamageRequestBuildClassification.MalformedData, invertedRange.Classification);
            AssertIssue(invertedRange, WeaponDamageInputIssueKind.MinimumGreaterThanMaximum);
            AssertIssue(missingCriticalBonus, WeaponDamageInputIssueKind.MissingCriticalBonus);
            AssertIssue(missingDamageType, WeaponDamageInputIssueKind.MissingDamageType);
            AssertIssue(unknownDamageType, WeaponDamageInputIssueKind.UnknownDamageType);
        }

        [TestMethod]
        public void WeaponDamageRequestBuilderReportsAmsCapAndStatCardinalityIssues()
        {
            WeaponDamageRequestBuildResult zeroCap = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.AmsCap = 0));
            WeaponDamageRequestBuildResult negativeCap = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.AmsCap = -1));
            WeaponDamageRequestBuildResult missingAttackerStat = WeaponDamageRequestBuilder.Build(BuildInputWithoutAttackSkillStat());
            WeaponDamageRequestBuildResult duplicateAttackerStat = WeaponDamageRequestBuilder.Build(BuildInputWithDuplicateAttackSkillStat());

            AssertIssue(zeroCap, WeaponDamageInputIssueKind.MissingAmsCapSemantics);
            Assert.AreEqual(WeaponDamageRequestBuildClassification.MalformedData, negativeCap.Classification);
            AssertIssue(negativeCap, WeaponDamageInputIssueKind.NegativeAmsCap);
            AssertIssue(missingAttackerStat, WeaponDamageInputIssueKind.MissingAttackerStat);
            Assert.AreEqual(WeaponDamageRequestBuildClassification.MalformedData, duplicateAttackerStat.Classification);
            AssertIssue(duplicateAttackerStat, WeaponDamageInputIssueKind.DuplicateAttackerStat);
        }

        [TestMethod]
        public void WeaponDamageRequestBuilderReportsArmorAddDamageAndCriticalStateGaps()
        {
            WeaponDamageRequestBuildResult missingArmor = WeaponDamageRequestBuilder.Build(BuildInputWithoutTargetArmor());
            WeaponDamageRequestBuildResult unknownArmor = WeaponDamageRequestBuilder.Build(BuildInputWithWeaponChange(x => x.DamageType = DamageType.Disease));
            WeaponDamageRequestBuildResult missingTypeAdd = WeaponDamageRequestBuilder.Build(BuildInputWithoutTypeAddDamage());
            WeaponDamageRequestBuildResult missingCriticalState = WeaponDamageRequestBuilder.Build(BuildInputWithMissingCriticalState());

            AssertIssue(missingArmor, WeaponDamageInputIssueKind.MissingArmorStat);
            AssertIssue(unknownArmor, WeaponDamageInputIssueKind.UnknownArmorMapping);
            AssertIssue(missingTypeAdd, WeaponDamageInputIssueKind.MissingAttackerStat);
            AssertIssue(missingTypeAdd, WeaponDamageInputIssueKind.MissingAddDamageSource);
            AssertIssue(missingCriticalState, WeaponDamageInputIssueKind.MissingCriticalState);
        }

        [TestMethod]
        public void WeaponDamageRequestBuilderClassifiesFixedCapturedAndLeavesSubwayThiefDamageUnchanged()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.IsFixedCapturedDamage = true;
            input.FixedCapturedDamage = 9;
            input.Attacker.Category = DamageSourceCategory.Npc;
            input.Weapon.TemplateIdentity = "121567";
            input.Weapon.MinimumDamage = 9;
            input.Weapon.MaximumDamage = 9;

            WeaponDamageRequestBuildResult build = WeaponDamageRequestBuilder.Build(input);
            DamageCalculationResult damage = DamageCalculator.Calculate(build.Request, new QueuedDamageRandomSource());

            Assert.AreEqual(WeaponDamageRequestBuildClassification.FixedCaptured, build.Classification);
            Assert.AreEqual(DamageCalculationStrategyKind.FixedCapturedDamage, build.ExpectedActiveStrategy);
            Assert.AreEqual(9, damage.FinalTargetDamage);
            Assert.AreEqual(DamageCalculationStrategyKind.FixedCapturedDamage, damage.Strategy);
        }

        private static void AssertArmorStat(DamageType damageType, int expectedStatId)
        {
            int statId;
            Assert.IsTrue(DamageCalculator.TryGetArmorStatForDamageType(damageType, out statId), damageType.ToString());
            Assert.AreEqual(expectedStatId, statId, damageType.ToString());
        }

        private static void AssertAddDamageStat(DamageType damageType, int expectedStatId)
        {
            int statId;
            Assert.IsTrue(DamageCalculator.TryGetAddDamageStatForDamageType(damageType, out statId), damageType.ToString());
            Assert.AreEqual(expectedStatId, statId, damageType.ToString());
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

        private static WeaponDamageRequestBuildInput BuildCompleteProjectileInput()
        {
            WeaponDamageRequestBuildInput input = new WeaponDamageRequestBuildInput
            {
                CallerName = "test-player-projectile-weapon",
                HasCriticalState = true,
                IsCritical = false,
                HasUniversalAddDamageSource = true,
                UniversalAddDamage = 0
            };

            input.Weapon.TemplateIdentity = "test-template";
            input.Weapon.TemplateSource = "items.dat";
            input.Weapon.QualityLevel = 1;
            input.Weapon.HasMinimumDamage = true;
            input.Weapon.MinimumDamage = 1;
            input.Weapon.HasMaximumDamage = true;
            input.Weapon.MaximumDamage = 10;
            input.Weapon.HasCriticalBonus = true;
            input.Weapon.CriticalBonus = 5;
            input.Weapon.HasDamageType = true;
            input.Weapon.DamageType = DamageType.Projectile;
            input.Weapon.RawDamageTypeStat = 90;
            input.Weapon.HasAmsCap = true;
            input.Weapon.AmsCap = 500;
            input.Weapon.HasAttackTime = true;
            input.Weapon.AttackTime = 100;
            input.Weapon.HasRechargeTime = true;
            input.Weapon.RechargeTime = 150;
            input.Weapon.WeaponCategory = 3;
            input.Weapon.WeaponSlot = 6;
            input.Weapon.AttackSkillContributions.Add(new AttackSkillContribution { StatId = 112, Percentage = 100 });

            input.Attacker.Category = DamageSourceCategory.Player;
            input.Attacker.Identity = "player";
            input.Attacker.Readiness = WeaponDamageAttackerReadiness.CompleteStatProvenance;
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 112, Value = 125, Source = "Stat.Value" });
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 276, Value = 25, Source = "Stat.Value" });
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 278, Value = 3, Source = "Stat.Value" });

            input.Target.Category = DamageSourceCategory.Npc;
            input.Target.Identity = "target";
            input.Target.Stats.Add(new WeaponDamageStatSnapshot { StatId = 90, Value = 40, Source = "Stat.Value" });
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithWeaponChange(System.Action<WeaponDamageWeaponSnapshot> change)
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            change(input.Weapon);
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithoutAttackSkills()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.Weapon.AttackSkillContributions.Clear();
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithCriticalHitWithoutBonus()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.IsCritical = true;
            input.Weapon.HasCriticalBonus = false;
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithoutAttackSkillStat()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.Attacker.Stats.Clear();
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 276, Value = 25 });
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 278, Value = 3 });
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithDuplicateAttackSkillStat()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.Attacker.Stats.Add(new WeaponDamageStatSnapshot { StatId = 112, Value = 130 });
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithoutTargetArmor()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.Target.Stats.Clear();
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithoutTypeAddDamage()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.Attacker.Stats.Remove(input.Attacker.Stats.Single(x => x.StatId == 278));
            input.HasUniversalAddDamageSource = false;
            return input;
        }

        private static WeaponDamageRequestBuildInput BuildInputWithMissingCriticalState()
        {
            WeaponDamageRequestBuildInput input = BuildCompleteProjectileInput();
            input.HasCriticalState = false;
            return input;
        }

        private static void AssertIssue(WeaponDamageRequestBuildResult result, WeaponDamageInputIssueKind expected)
        {
            Assert.IsTrue(result.Issues.Any(x => x.Kind == expected), expected.ToString());
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
