using System;

namespace ZoneEngine.Core.Navigation;

internal sealed class NpcChaseRouteFollower
{
	internal const double WaypointArrivalDistance = 0.75;

	internal const double MaximumRouteDeviation = 4.0;

	internal const double MinimumProgressDistance = 0.35;

	internal static readonly TimeSpan StuckTimeout = TimeSpan.FromSeconds(2.5);

	internal bool TrySelectDestination(IPlayfieldChaseNavigationProvider provider, NpcChaseRouteState state, ChaseNavigationPoint current, DateTime utcNow, out ChaseNavigationPoint destination, out bool shouldIssueMovement, out NpcChaseInvalidationReason invalidationReason)
	{
		destination = default(ChaseNavigationPoint);
		shouldIssueMovement = false;
		invalidationReason = NpcChaseInvalidationReason.None;
		if (provider == null || state == null || state.Route == null || !provider.IsRouteCurrent(state.Route))
		{
			invalidationReason = NpcChaseInvalidationReason.GeometryVersionChanged;
			return false;
		}
		while (state.RouteIndex < state.Route.Points.Length && current.Distance2D(state.Route.Points[state.RouteIndex]) <= 0.75)
		{
			state.RouteIndex++;
			state.LastIssuedRouteIndex = -1;
			state.LastProgressPoint = current;
			state.LastProgressUtc = utcNow;
		}
		if (state.RouteIndex >= state.Route.Points.Length)
		{
			invalidationReason = NpcChaseInvalidationReason.RouteCompleted;
			return false;
		}
		ChaseNavigationPoint start = ((state.RouteIndex == 0) ? state.StartSample : state.Route.Points[state.RouteIndex - 1]);
		destination = state.Route.Points[state.RouteIndex];
		if (DistanceToSegment2D(current, start, destination) > 4.0)
		{
			invalidationReason = NpcChaseInvalidationReason.RouteDeviation;
			return false;
		}
		if (current.Distance2D(state.LastProgressPoint) >= 0.35)
		{
			state.LastProgressPoint = current;
			state.LastProgressUtc = utcNow;
		}
		else if (utcNow - state.LastProgressUtc >= StuckTimeout)
		{
			invalidationReason = NpcChaseInvalidationReason.Stuck;
			return false;
		}
		if (!provider.IsSegmentTraversable(current, destination))
		{
			invalidationReason = NpcChaseInvalidationReason.RouteSegmentInvalid;
			return false;
		}
		shouldIssueMovement = state.LastIssuedRouteIndex != state.RouteIndex;
		state.LastIssuedRouteIndex = state.RouteIndex;
		return true;
	}

	private static double DistanceToSegment2D(ChaseNavigationPoint point, ChaseNavigationPoint start, ChaseNavigationPoint end)
	{
		double num = end.X - start.X;
		double num2 = end.Z - start.Z;
		double num3 = num * num + num2 * num2;
		if (num3 <= 1E-12)
		{
			return point.Distance2D(start);
		}
		double val = ((point.X - start.X) * num + (point.Z - start.Z) * num2) / num3;
		val = Math.Max(0.0, Math.Min(1.0, val));
		ChaseNavigationPoint other = new ChaseNavigationPoint(start.X + num * val, start.Y + (end.Y - start.Y) * val, start.Z + num2 * val);
		return point.Distance2D(other);
	}
}
