using System;

namespace ZoneEngine.Core.Navigation;

internal sealed class ChaseRouteSearchLimits
{
	internal static readonly ChaseRouteSearchLimits Default = new ChaseRouteSearchLimits(2.5, 32.0, 160.0, 4096, 32768, 10.0, 1.5, 256);

	internal double CellSize { get; private set; }

	internal double DetourMargin { get; private set; }

	internal double MaximumStartGoalDistance { get; private set; }

	internal int MaximumExpandedNodes { get; private set; }

	internal int MaximumSegmentChecks { get; private set; }

	internal double GoalConnectionDistance { get; private set; }

	internal double MaximumVerticalStep { get; private set; }

	internal int MaximumSmoothingChecks { get; private set; }

	internal ChaseRouteSearchLimits(double cellSize, double detourMargin, double maximumStartGoalDistance, int maximumExpandedNodes, int maximumSegmentChecks, double goalConnectionDistance, double maximumVerticalStep, int maximumSmoothingChecks)
	{
		if (cellSize <= 0.0 || detourMargin <= 0.0 || maximumStartGoalDistance <= 0.0 || maximumExpandedNodes <= 0 || maximumSegmentChecks <= 0 || goalConnectionDistance <= 0.0 || maximumVerticalStep <= 0.0 || maximumSmoothingChecks < 0)
		{
			throw new ArgumentOutOfRangeException("limits");
		}
		CellSize = cellSize;
		DetourMargin = detourMargin;
		MaximumStartGoalDistance = maximumStartGoalDistance;
		MaximumExpandedNodes = maximumExpandedNodes;
		MaximumSegmentChecks = maximumSegmentChecks;
		GoalConnectionDistance = goalConnectionDistance;
		MaximumVerticalStep = maximumVerticalStep;
		MaximumSmoothingChecks = maximumSmoothingChecks;
	}
}
