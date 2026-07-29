namespace ZoneEngine.Core.Navigation;

internal sealed class UnsupportedPlayfieldChaseNavigationProvider : IPlayfieldChaseNavigationProvider
{
	private readonly int playfieldResource;

	public int PlayfieldResource => playfieldResource;

	public ChaseNavigationCapability Capability => ChaseNavigationCapability.Unsupported;

	public string GeometryVersion => string.Empty;

	internal UnsupportedPlayfieldChaseNavigationProvider(int playfieldResource)
	{
		this.playfieldResource = playfieldResource;
	}

	public bool TryProjectToSurface(ChaseNavigationPoint reference, double x, double z, out ChaseNavigationPoint projected)
	{
		projected = default(ChaseNavigationPoint);
		return false;
	}

	public bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
	{
		return false;
	}

	public bool IsAttackLineTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
	{
		return false;
	}

	public ChaseRoutePlan RequestRoute(ChaseNavigationPoint start, ChaseNavigationPoint goal, ChaseRouteSearchLimits limits)
	{
		return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.Unsupported, string.Empty, 0, 0);
	}

	public bool IsRouteCurrent(ChaseRoutePlan route)
	{
		return false;
	}
}
