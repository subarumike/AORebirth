namespace ZoneEngine.Core.Navigation
{
    using System;

    internal enum NpcChaseRouteStateKind
    {
        Direct = 0,
        Route = 1,
        Failed = 2
    }

    internal sealed class NpcChaseRouteState
    {
        internal int NpcInstance { get; set; }

        internal int TargetInstance { get; set; }

        internal NpcChaseRouteStateKind Kind { get; set; }

        internal string GeometryVersion { get; set; }

        internal ChaseNavigationPoint StartSample { get; set; }

        internal ChaseNavigationPoint TargetSample { get; set; }

        internal ChaseNavigationPoint DirectDestination { get; set; }

        internal ChaseRoutePlan Route { get; set; }

        internal int RouteIndex { get; set; }

        internal int LastIssuedRouteIndex { get; set; }

        internal ChaseNavigationPoint LastProgressPoint { get; set; }

        internal DateTime LastProgressUtc { get; set; }

        internal DateTime NextEvaluationUtc { get; set; }

        internal DateTime RetryAtUtc { get; set; }

        internal ChaseRoutePlanStatus LastFailureStatus { get; set; }

        internal NpcChaseInvalidationReason LastInvalidationReason { get; set; }
    }

    internal struct NpcChaseUpdateResult
    {
        private NpcChaseUpdateResult(
            NpcChaseMovementKind kind,
            bool hasDestination,
            bool shouldIssueMovement,
            ChaseNavigationPoint destination,
            bool routeRequested,
            NpcChaseInvalidationReason invalidationReason)
        {
            this.Kind = kind;
            this.HasDestination = hasDestination;
            this.ShouldIssueMovement = shouldIssueMovement;
            this.Destination = destination;
            this.RouteRequested = routeRequested;
            this.InvalidationReason = invalidationReason;
        }

        internal NpcChaseMovementKind Kind { get; private set; }

        internal bool HasDestination { get; private set; }

        internal bool ShouldIssueMovement { get; private set; }

        internal ChaseNavigationPoint Destination { get; private set; }

        internal bool RouteRequested { get; private set; }

        internal NpcChaseInvalidationReason InvalidationReason { get; private set; }

        internal static NpcChaseUpdateResult Unsupported()
        {
            return new NpcChaseUpdateResult(
                NpcChaseMovementKind.Unsupported,
                false,
                false,
                default(ChaseNavigationPoint),
                false,
                NpcChaseInvalidationReason.None);
        }

        internal static NpcChaseUpdateResult Unavailable()
        {
            return new NpcChaseUpdateResult(
                NpcChaseMovementKind.Unavailable,
                false,
                false,
                default(ChaseNavigationPoint),
                false,
                NpcChaseInvalidationReason.None);
        }

        internal static NpcChaseUpdateResult Hold(
            bool routeRequested,
            NpcChaseInvalidationReason invalidationReason)
        {
            return new NpcChaseUpdateResult(
                NpcChaseMovementKind.Hold,
                false,
                false,
                default(ChaseNavigationPoint),
                routeRequested,
                invalidationReason);
        }

        internal static NpcChaseUpdateResult Move(
            NpcChaseMovementKind kind,
            ChaseNavigationPoint destination,
            bool shouldIssueMovement,
            bool routeRequested,
            NpcChaseInvalidationReason invalidationReason)
        {
            if (kind != NpcChaseMovementKind.Direct && kind != NpcChaseMovementKind.Route)
            {
                throw new ArgumentOutOfRangeException("kind");
            }

            return new NpcChaseUpdateResult(
                kind,
                true,
                shouldIssueMovement,
                destination,
                routeRequested,
                invalidationReason);
        }
    }
}
