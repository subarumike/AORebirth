using System;

namespace ZoneEngine.Core;

public static class CombatDamageRules
{
	public const int PlayerFallbackDamage = 15;

	public const int NpcFallbackDamage = 1;

	private static readonly IDamageRandomSource DamageRandom = new SystemDamageRandomSource();

	public static int Calculate(int minDamage, int maxDamage, int damageBonus, int level, bool isPlayer)
	{
		return CalculateDetailed(minDamage, maxDamage, damageBonus, level, isPlayer, DamageRandom).FinalTargetDamage;
	}

	public static DamageCalculationResult CalculateDetailed(int minDamage, int maxDamage, int damageBonus, int level, bool isPlayer, IDamageRandomSource randomSource)
	{
		int num = Math.Max(0, minDamage);
		int baseMaximum = Math.Max(num, maxDamage);
		return DamageCalculator.Calculate(new DamageCalculationRequest
		{
			Context = new DamageCalculationContext
			{
				Mode = DamageCalculationMode.PvM,
				AttackCategory = DamageAttackCategory.RegularAttack,
				SpecialAttackCategory = SpecialAttackCategory.None,
				CompatibilityPolicy = (isPlayer ? "repository-player-legacy-normal-hit" : "repository-npc-legacy-normal-hit"),
				EvidenceSource = "CombatDamageRules.Calculate pre-centralization behavior"
			},
			Source = new DamageSourceSnapshot
			{
				Category = (isPlayer ? DamageSourceCategory.Player : DamageSourceCategory.Npc),
				Level = level
			},
			Definition = new DamageDefinition
			{
				BaseMinimum = num,
				BaseMaximum = baseMaximum,
				DamageType = DamageType.Unknown,
				EvidenceClassification = DamageEvidenceClassification.ProvenRepositoryBehavior
			},
			Modifiers = new DamageModifierSet
			{
				LegacyDamageBonus = damageBonus
			},
			Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(isPlayer),
			EvidenceClassification = DamageEvidenceClassification.ProvenRepositoryBehavior,
			HitOutcome = DamageHitOutcome.Hit
		}, randomSource ?? DamageRandom);
	}
}
