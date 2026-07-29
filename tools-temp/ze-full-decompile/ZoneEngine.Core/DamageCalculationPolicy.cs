namespace ZoneEngine.Core;

public sealed class DamageCalculationPolicy
{
	public string Name { get; set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public bool UseRepositoryLegacyNormalHit { get; set; }

	public bool PreserveLegacyFallbackFloor { get; set; }

	public bool EnableArmorMitigation { get; set; }

	public bool EnableCriticalDamage { get; set; }

	public bool EnableAttackRatingScaling { get; set; }

	public bool EnableReflect { get; set; }

	public bool EnableAbsorb { get; set; }

	public bool EnablePvP { get; set; }

	public bool EnableSpecialAggregation { get; set; }

	public bool EnablePercentageHealthDamage { get; set; }

	public bool EnableReturnedDamage { get; set; }

	public bool IsFixedCapturedDamage { get; set; }

	public bool EnableEvidenceBackedWeaponFormula { get; set; }

	public int PlayerFallbackDamage { get; set; }

	public int NpcFallbackDamage { get; set; }

	public DamageCalculationPolicy()
	{
		Name = string.Empty;
		EvidenceClassification = DamageEvidenceClassification.Unknown;
		UseRepositoryLegacyNormalHit = true;
		PreserveLegacyFallbackFloor = true;
		EnableArmorMitigation = false;
		EnableCriticalDamage = false;
		EnableAttackRatingScaling = false;
		EnableReflect = false;
		EnableAbsorb = false;
		EnablePvP = false;
		EnableSpecialAggregation = false;
		EnablePercentageHealthDamage = false;
		EnableReturnedDamage = false;
		IsFixedCapturedDamage = false;
		EnableEvidenceBackedWeaponFormula = false;
		PlayerFallbackDamage = 15;
		NpcFallbackDamage = 1;
	}

	public static DamageCalculationPolicy RepositoryLegacyNormalHit(bool isPlayer)
	{
		return new DamageCalculationPolicy
		{
			Name = (isPlayer ? "repository-player-legacy-normal-hit" : "repository-npc-legacy-normal-hit"),
			EvidenceClassification = DamageEvidenceClassification.ProvenRepositoryBehavior,
			UseRepositoryLegacyNormalHit = true,
			PreserveLegacyFallbackFloor = true
		};
	}

	public static DamageCalculationPolicy CapturedFixedDamage(string name)
	{
		return new DamageCalculationPolicy
		{
			Name = name,
			EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior,
			UseRepositoryLegacyNormalHit = true,
			PreserveLegacyFallbackFloor = true,
			IsFixedCapturedDamage = true
		};
	}

	public static DamageCalculationPolicy EvidenceBackedWeaponFormula(string name)
	{
		return new DamageCalculationPolicy
		{
			Name = name,
			EvidenceClassification = DamageEvidenceClassification.Unknown,
			UseRepositoryLegacyNormalHit = false,
			PreserveLegacyFallbackFloor = false,
			EnableEvidenceBackedWeaponFormula = true,
			EnableArmorMitigation = true,
			EnableCriticalDamage = true,
			EnableAttackRatingScaling = true
		};
	}
}
