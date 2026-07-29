using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageObservationInput
{
	public string AttackerIdentity { get; set; }

	public DamageSourceCategory AttackerCategory { get; set; }

	public string TargetIdentity { get; set; }

	public string WeaponTemplateIdentity { get; set; }

	public string WeaponInstanceIdentity { get; set; }

	public int? WeaponQualityLevel { get; set; }

	public int? WeaponMinimum { get; set; }

	public int? WeaponMaximum { get; set; }

	public int? BaseRoll { get; set; }

	public int? LegacyDamageBonus { get; set; }

	public int? CriticalBonus { get; set; }

	public int? RawDamageType { get; set; }

	public DamageType MappedDamageType { get; set; }

	public int? AttackRating { get; set; }

	public int? AddAllOff { get; set; }

	public int? TemporaryOffensiveModifiers { get; set; }

	public int? AmsCap { get; set; }

	public bool? AmsCapPresent { get; set; }

	public int? TargetArmor { get; set; }

	public int? TypeSpecificAddDamage { get; set; }

	public int? UniversalAddDamage { get; set; }

	public bool? MultipleDamageSourcesPossible { get; set; }

	public bool? ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible { get; set; }

	public bool? PacketOrderComplete { get; set; }

	public bool? CriticalStateEvidencePresent { get; set; }

	public IList<AttackSkillContribution> AttackSkillDefinitions { get; private set; }

	public IList<string> KnownUncertainties { get; private set; }

	public WeaponDamageObservationInput()
	{
		AttackerIdentity = string.Empty;
		TargetIdentity = string.Empty;
		WeaponTemplateIdentity = string.Empty;
		WeaponInstanceIdentity = string.Empty;
		AttackSkillDefinitions = new List<AttackSkillContribution>();
		KnownUncertainties = new List<string>();
	}
}
