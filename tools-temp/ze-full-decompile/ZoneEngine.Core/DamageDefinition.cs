namespace ZoneEngine.Core;

public class DamageDefinition
{
	public int BaseMinimum { get; set; }

	public int BaseMaximum { get; set; }

	public int CriticalBonus { get; set; }

	public bool HasCriticalState { get; set; }

	public bool HasCriticalBonus { get; set; }

	public int FixedDamage { get; set; }

	public int PercentageHealthDamage { get; set; }

	public DamageType DamageType { get; set; }

	public int WeaponTemplateId { get; set; }

	public int AttackRatingCap { get; set; }

	public bool HasAttackRatingCap { get; set; }

	public bool IsCritical { get; set; }

	public int BulletCount { get; set; }

	public int AmmoLimitedCount { get; set; }

	public int AttackSpecificCap { get; set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public DamageDefinition()
	{
		DamageType = DamageType.Unknown;
		AttackSpecificCap = 0;
		EvidenceClassification = DamageEvidenceClassification.Unknown;
	}
}
