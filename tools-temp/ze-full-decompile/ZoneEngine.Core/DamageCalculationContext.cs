namespace ZoneEngine.Core;

public sealed class DamageCalculationContext
{
	public DamageCalculationMode Mode { get; set; }

	public DamageAttackCategory AttackCategory { get; set; }

	public SpecialAttackCategory SpecialAttackCategory { get; set; }

	public string CompatibilityPolicy { get; set; }

	public string EvidenceSource { get; set; }

	public DamageCalculationContext()
	{
		Mode = DamageCalculationMode.PvM;
		AttackCategory = DamageAttackCategory.RegularAttack;
		SpecialAttackCategory = SpecialAttackCategory.None;
		CompatibilityPolicy = string.Empty;
		EvidenceSource = string.Empty;
	}
}
