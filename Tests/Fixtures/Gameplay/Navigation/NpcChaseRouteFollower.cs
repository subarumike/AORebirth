namespace ZoneEngine.Core.Navigation
{
    using System;

    internal sealed class NpcChaseRouteFollower
    {
        internal const double WaypointArrivalDistance = 0.75;

        internal const double MaximumRouteDeviation = 4.0;

        internal const double MinimumProgressDistance = 0.35;

        internal static readonly TimeSpan StuckTimeout = TimeSpan.FromSeconds(2.5);

        internal bool TrySelectDestination(
            IPlayfieldChaseNavigationProvider provider,
            NpcChaseRouteState state,
            ChaseNavigationPoint current,
            DateTime utcNow,
            out ChaseNavigationPoint destination,
            out bool shouldIssueMovement,
            out NpcChaseInvalidationReason invalidationReason)
        {
            destination = default(ChaseNavigationPoint);
            shouldIssueMovement = false;
            invalidationReason = NpcChaseInvalidationReason.None;
            if (provider == null
                || state == null
                || state.Route == null
                || !provider.IsRouteCurrent(state.Route))
            {
                invalidationReason = NpcChaseInvalidationReason.GeometryVersionChanged;
                return false;
            }

            while (state.RouteIndex < state.Route.Points.Length
                   && current.Distance2D(state.Route.Points[state.RouteIndex]) <= WaypointArrivalDistance)
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

            ChaseNavigationPoint segmentStart = state.RouteIndex == 0
                                                     ? state.StartSample
                                                     : state.Route.Points[state.RouteIndex - 1];
            destination = state.Route.Points[state.RouteIndex];
            if (DistanceToSegment2D(current, segmentStart, destination) > MaximumRouteDeviation)
            {
                invalidationReason = NpcChaseInvalidationReason.RouteDeviation;
                return false;
            }

            if (current.Distance2D(state.LastProgressPoint) >= MinimumProgressDistance)
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

        private static double DistanceToSegment2D(
            ChaseNavigationPoint point,
            ChaseNavigationPoint start,
            ChaseNavigationPoint end)
        {
            double segmentX = end.X - start.X;
            double segmentZ = end.Z - start.Z;
            double lengthSquared = (segmentX * segmentX) + (segmentZ * segmentZ);
            if (lengthSquared <= 1.0e-12)
            {
                return point.Distance2D(start);
            }

            double factor = (((point.X - start.X) * segmentX) + ((point.Z - start.Z) * segmentZ))
                            / lengthSquared;
            factor = Math.Max(0.0, Math.Min(1.0, factor));
            var closest = new ChaseNavigationPoint(
                start.X + (segmentX * factor),
                start.Y + ((end.Y - start.Y) * factor),
                start.Z + (segmentZ * factor));
            return point.Distance2D(closest);
        }
    }
}
