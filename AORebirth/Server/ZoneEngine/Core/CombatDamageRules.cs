namespace ZoneEngine.Core
{
    using System;

    public static class CombatDamageRules
    {
        public const int PlayerFallbackDamage = 15;

        public const int NpcFallbackDamage = 1;

        private static readonly IDamageRandomSource DamageRandom = new SystemDamageRandomSource();

        public static int Calculate(
            int minDamage,
            int maxDamage,
            int damageBonus,
            int level,
            bool isPlayer)
        {
            return CalculateDetailed(
                minDamage,
                maxDamage,
                damageBonus,
                level,
                isPlayer,
                DamageRandom).FinalTargetDamage;
        }

        public static DamageCalculationResult CalculateDetailed(
            int minDamage,
            int maxDamage,
            int damageBonus,
            int level,
            bool isPlayer,
            IDamageRandomSource randomSource)
        {
            int normalizedMinDamage = Math.Max(0, minDamage);
            int normalizedMaxDamage = Math.Max(normalizedMinDamage, maxDamage);

            return DamageCalculator.Calculate(
                new DamageCalculationRequest
                {
                    Context = new DamageCalculationContext
                    {
                        Mode = DamageCalculationMode.PvM,
                        AttackCategory = DamageAttackCategory.RegularAttack,
                        SpecialAttackCategory = SpecialAttackCategory.None,
                        CompatibilityPolicy = isPlayer ? "repository-player-legacy-normal-hit" : "repository-npc-legacy-normal-hit",
                        EvidenceSource = "CombatDamageRules.Calculate pre-centralization behavior"
                    },
                    Source = new DamageSourceSnapshot
                    {
                        Category = isPlayer ? DamageSourceCategory.Player : DamageSourceCategory.Npc,
                        Level = level
                    },
                    Definition = new DamageDefinition
                    {
                        BaseMinimum = normalizedMinDamage,
                        BaseMaximum = normalizedMaxDamage,
                        DamageType = DamageType.Unknown,
                        EvidenceClassification = DamageEvidenceClassification.ProvenRepositoryBehavior
                    },
                    Modifiers = new DamageModifierSet
                    {
                        FlatAddDamage = damageBonus
                    },
                    Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(isPlayer),
                    EvidenceClassification = DamageEvidenceClassification.ProvenRepositoryBehavior,
                    HitOutcome = DamageHitOutcome.Hit
                },
                randomSource ?? DamageRandom);
        }
    }
}
