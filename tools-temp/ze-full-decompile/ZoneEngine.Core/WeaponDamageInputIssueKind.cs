namespace ZoneEngine.Core;

public enum WeaponDamageInputIssueKind
{
	MissingWeaponTemplate,
	MissingMinimum,
	MissingMaximum,
	MinimumGreaterThanMaximum,
	MissingDamageType,
	UnknownDamageType,
	MissingAttackSkill,
	UnknownAttackStat,
	InvalidSkillWeight,
	MissingAttackerStat,
	DuplicateAttackerStat,
	MissingArmorStat,
	UnknownArmorMapping,
	MissingAmsCapSemantics,
	NegativeAmsCap,
	MissingAddDamageSource,
	MissingCriticalState,
	MissingCriticalBonus
}
