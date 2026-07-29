using System;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Navigation;

internal sealed class Pf127ChaseNavigationProvider : IPlayfieldChaseNavigationProvider
{
	internal const double AgentRadius = 0.35;

	internal const double LowerCollisionProbeHeight = 0.15;

	internal const double UpperCollisionProbeHeight = 1.2;

	internal const double AttackLineProbeHeight = 1.0;

	internal const double MaximumPlanElevationDifference = 1.5;

	private const double MinimumSegmentLength = 0.01;

	private readonly int playfieldResource;

	private readonly PlayfieldCollisionGeometryLoadResult geometryLoadResult;

	private readonly BoundedGridChaseRoutePlanner planner = new BoundedGridChaseRoutePlanner();

	public int PlayfieldResource => playfieldResource;

	public ChaseNavigationCapability Capability => (playfieldResource != 127 || !geometryLoadResult.IsLoaded) ? ChaseNavigationCapability.Unavailable : ChaseNavigationCapability.Supported;

	public string GeometryVersion => geometryLoadResult.IsLoaded ? geometryLoadResult.Geometry.SourceSha256 : string.Empty;

	internal string GeometryError => geometryLoadResult.Error;

	internal Pf127ChaseNavigationProvider(int playfieldResource, PlayfieldCollisionGeometryLoadResult geometryLoadResult)
	{
		this.playfieldResource = playfieldResource;
		this.geometryLoadResult = geometryLoadResult ?? PlayfieldCollisionGeometryLoadResult.Failed("PF127 chase navigation geometry load result is missing.");
	}

	public bool TryProjectToSurface(ChaseNavigationPoint reference, double x, double z, out ChaseNavigationPoint projected)
	{
		projected = default(ChaseNavigationPoint);
		if (Capability != ChaseNavigationCapability.Supported || !reference.IsFinite || !IsFinite(x) || !IsFinite(z))
		{
			return false;
		}
		projected = new ChaseNavigationPoint(x, reference.Y, z);
		return projected.IsFinite;
	}

	public bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
	{
		if (Capability != ChaseNavigationCapability.Supported || !start.IsFinite || !end.IsFinite || Math.Abs(start.Y - end.Y) > 1.5)
		{
			return false;
		}
		double num = start.Distance2D(end);
		if (num < 0.01)
		{
			return true;
		}
		double num2 = (end.X - start.X) / num;
		double num3 = (end.Z - start.Z) / num;
		double num4 = (0.0 - num3) * 0.35;
		double num5 = num2 * 0.35;
		return IsProbeSegmentClear(start, end, 0.0, 0.0, 0.15) && IsProbeSegmentClear(start, end, num4, num5, 0.15) && IsProbeSegmentClear(start, end, 0.0 - num4, 0.0 - num5, 0.15) && IsProbeSegmentClear(start, end, 0.0, 0.0, 1.2) && IsProbeSegmentClear(start, end, num4, num5, 1.2) && IsProbeSegmentClear(start, end, 0.0 - num4, 0.0 - num5, 1.2);
	}

	public bool IsAttackLineTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
	{
		if (Capability != ChaseNavigationCapability.Supported || !start.IsFinite || !end.IsFinite)
		{
			return false;
		}
		double num = end.X - start.X;
		double num2 = end.Y - start.Y;
		double num3 = end.Z - start.Z;
		return num * num + num2 * num2 + num3 * num3 < 0.0001 || IsProbeSegmentClear(start, end, 0.0, 0.0, 1.0);
	}

	public ChaseRoutePlan RequestRoute(ChaseNavigationPoint start, ChaseNavigationPoint goal, ChaseRouteSearchLimits limits)
	{
		if (Capability != ChaseNavigationCapability.Supported)
		{
			return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.Unavailable, GeometryVersion, 0, 0);
		}
		if (Math.Abs(start.Y - goal.Y) > 1.5)
		{
			return ChaseRoutePlan.Failed(ChaseRoutePlanStatus.Unreachable, GeometryVersion, 0, 0);
		}
		return planner.Plan(this, start, goal, limits ?? ChaseRouteSearchLimits.Default);
	}

	public bool IsRouteCurrent(ChaseRoutePlan route)
	{
		return route != null && route.IsSuccess && Capability == ChaseNavigationCapability.Supported && string.Equals(route.GeometryVersion, GeometryVersion, StringComparison.Ordinal);
	}

	private bool IsProbeSegmentClear(ChaseNavigationPoint start, ChaseNavigationPoint end, double offsetX, double offsetZ, double probeHeight)
	{
		CollisionPoint3 start2 = new CollisionPoint3(start.X + offsetX, start.Y + probeHeight, start.Z + offsetZ);
		CollisionPoint3 end2 = new CollisionPoint3(end.X + offsetX, end.Y + probeHeight, end.Z + offsetZ);
		try
		{
			SegmentTriangleHit hit;
			return !geometryLoadResult.Geometry.TryFindFirstBlockingHit(start2, end2, out hit);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool IsFinite(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}
}
