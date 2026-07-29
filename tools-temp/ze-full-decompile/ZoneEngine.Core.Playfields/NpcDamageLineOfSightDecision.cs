namespace ZoneEngine.Core.Playfields;

internal enum NpcDamageLineOfSightDecision
{
	AllowedNotRequired,
	AllowedClear,
	DeniedBlocked,
	DeniedGeometryUnavailable,
	DeniedInvalidSegment
}
