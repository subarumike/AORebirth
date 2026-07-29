namespace ZoneEngine.Core;

public enum EnemyBehaviorSignal
{
	AddThreat,
	TargetFollow,
	TargetOutOfRange,
	CoordinateFollowTarget,
	TargetInRange,
	AttackInfo,
	MissedAttackInfo,
	HealthDamage,
	StopFightFromPlayer,
	StopFightFromNpc,
	DeathAction,
	WipeHatelist,
	TargetInvalidOrZoned,
	ScriptedReset,
	ResetArrived,
	HardCorrection
}
