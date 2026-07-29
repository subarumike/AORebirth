namespace ZoneEngine.Core;

public enum WeaponDamageObservationIssueKind
{
	HealthDeltaMismatch,
	AmbiguousWeaponIdentity,
	UnknownDamageType,
	ContradictoryAttackerStats,
	AmbiguousTargetArmor,
	MultipleDamageSourcesPossible,
	ExternalDamagePossible,
	CriticalStateClaimedWithoutEvidence,
	IncompletePacketOrder,
	MissingAddAllOff,
	MissingAmsCapSemantics,
	MissingArmor,
	UnknownHitKind
}
