namespace ZoneEngine.Core;

public enum WeaponDamageCandidateCriticalOrdering
{
	None,
	MaximumPlusCriticalBonus,
	RollPlusCriticalBonus,
	CriticalBonusArScaled,
	CriticalBonusUnscaled,
	CriticalBonusAcReduced,
	CriticalMinimumFloor
}
