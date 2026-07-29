namespace ZoneEngine.Core.Navigation;

internal enum NpcChaseInvalidationReason
{
	None,
	DirectPathRestored,
	TargetMoved,
	TargetReplaced,
	GeometryVersionChanged,
	RouteSegmentInvalid,
	RouteDeviation,
	Stuck,
	RouteCompleted,
	TargetLost,
	CombatCancelled,
	Death,
	CorpseTransition,
	Despawn,
	LeashReset,
	EncounterReset,
	PlayfieldReset,
	RuntimeDisposed
}
