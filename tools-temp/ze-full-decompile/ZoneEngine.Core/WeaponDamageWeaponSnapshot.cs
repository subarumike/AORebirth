using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageWeaponSnapshot
{
	public string TemplateIdentity { get; set; }

	public string TemplateSource { get; set; }

	public int QualityLevel { get; set; }

	public bool HasMinimumDamage { get; set; }

	public int MinimumDamage { get; set; }

	public bool HasMaximumDamage { get; set; }

	public int MaximumDamage { get; set; }

	public bool HasCriticalBonus { get; set; }

	public int CriticalBonus { get; set; }

	public bool HasDamageType { get; set; }

	public DamageType DamageType { get; set; }

	public int RawDamageTypeStat { get; set; }

	public bool HasAmsCap { get; set; }

	public int AmsCap { get; set; }

	public bool HasAttackTime { get; set; }

	public int AttackTime { get; set; }

	public bool HasRechargeTime { get; set; }

	public int RechargeTime { get; set; }

	public int WeaponCategory { get; set; }

	public int WeaponSlot { get; set; }

	public IList<AttackSkillContribution> AttackSkillContributions { get; private set; }

	public WeaponDamageWeaponSnapshot()
	{
		TemplateIdentity = string.Empty;
		TemplateSource = string.Empty;
		AttackSkillContributions = new List<AttackSkillContribution>();
	}
}
