namespace ZoneEngine.Core.Navigation;

internal enum ChaseRoutePlanStatus
{
	Success,
	Unsupported,
	Unavailable,
	InvalidRequest,
	Unreachable,
	SearchLimitReached
}
