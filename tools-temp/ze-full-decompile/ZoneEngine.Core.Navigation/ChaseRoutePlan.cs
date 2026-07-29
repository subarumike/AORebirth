using System;

namespace ZoneEngine.Core.Navigation;

internal sealed class ChaseRoutePlan
{
	internal ChaseRoutePlanStatus Status { get; private set; }

	internal ChaseNavigationPoint[] Points { get; private set; }

	internal string GeometryVersion { get; private set; }

	internal int ExpandedNodes { get; private set; }

	internal int SegmentChecks { get; private set; }

	internal bool IsSuccess => Status == ChaseRoutePlanStatus.Success && Points.Length != 0;

	private ChaseRoutePlan(ChaseRoutePlanStatus status, ChaseNavigationPoint[] points, string geometryVersion, int expandedNodes, int segmentChecks)
	{
		Status = status;
		Points = points ?? new ChaseNavigationPoint[0];
		GeometryVersion = geometryVersion ?? string.Empty;
		ExpandedNodes = expandedNodes;
		SegmentChecks = segmentChecks;
	}

	internal static ChaseRoutePlan Success(ChaseNavigationPoint[] points, string geometryVersion, int expandedNodes, int segmentChecks)
	{
		if (points == null || points.Length == 0)
		{
			throw new ArgumentException("A successful route requires points.", "points");
		}
		return new ChaseRoutePlan(ChaseRoutePlanStatus.Success, (ChaseNavigationPoint[])points.Clone(), geometryVersion, expandedNodes, segmentChecks);
	}

	internal static ChaseRoutePlan Failed(ChaseRoutePlanStatus status, string geometryVersion, int expandedNodes, int segmentChecks)
	{
		if (status == ChaseRoutePlanStatus.Success)
		{
			throw new ArgumentOutOfRangeException("status");
		}
		return new ChaseRoutePlan(status, new ChaseNavigationPoint[0], geometryVersion, expandedNodes, segmentChecks);
	}
}
