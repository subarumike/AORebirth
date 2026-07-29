namespace ZoneEngine.Core.Navigation;

internal interface IPlayfieldChaseNavigationProvider
{
	int PlayfieldResource { get; }

	ChaseNavigationCapability Capability { get; }

	string GeometryVersion { get; }

	bool TryProjectToSurface(ChaseNavigationPoint reference, double x, double z, out ChaseNavigationPoint projected);

	bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end);

	bool IsAttackLineTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end);

	ChaseRoutePlan RequestRoute(ChaseNavigationPoint start, ChaseNavigationPoint goal, ChaseRouteSearchLimits limits);

	bool IsRouteCurrent(ChaseRoutePlan route);
}
