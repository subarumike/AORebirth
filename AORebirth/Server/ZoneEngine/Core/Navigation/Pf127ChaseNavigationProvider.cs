namespace ZoneEngine.Core.Navigation
{
    using System;

    using ZoneEngine.Core.Playfields;

    internal static class PlayfieldChaseNavigationProviderFactory
    {
        internal static IPlayfieldChaseNavigationProvider Create(int playfieldResource)
        {
            return playfieldResource == Pf127CollisionGeometryLoader.SubwayPlayfieldResource
                       ? new Pf127ChaseNavigationProvider(
                           playfieldResource,
                           Pf127CollisionGeometryLoader.Current)
                       : (IPlayfieldChaseNavigationProvider)
                         new UnsupportedPlayfieldChaseNavigationProvider(playfieldResource);
        }
    }

    internal sealed class Pf127ChaseNavigationProvider : IPlayfieldChaseNavigationProvider
    {
        internal const double AgentRadius = 0.35;

        internal const double LowerCollisionProbeHeight = 0.15;

        internal const double UpperCollisionProbeHeight = 1.20;

        internal const double AttackLineProbeHeight = 1.0;

        internal const double MaximumPlanElevationDifference = 1.50;

        private const double MinimumSegmentLength = 0.01;

        private readonly int playfieldResource;

        private readonly PlayfieldCollisionGeometryLoadResult geometryLoadResult;

        private readonly BoundedGridChaseRoutePlanner planner = new BoundedGridChaseRoutePlanner();

        internal Pf127ChaseNavigationProvider(
            int playfieldResource,
            PlayfieldCollisionGeometryLoadResult geometryLoadResult)
        {
            this.playfieldResource = playfieldResource;
            this.geometryLoadResult = geometryLoadResult
                                      ?? PlayfieldCollisionGeometryLoadResult.Failed(
                                          "PF127 chase navigation geometry load result is missing.");
        }

        public int PlayfieldResource
        {
            get { return this.playfieldResource; }
        }

        public ChaseNavigationCapability Capability
        {
            get
            {
                return this.playfieldResource == Pf127CollisionGeometryLoader.SubwayPlayfieldResource
                       && this.geometryLoadResult.IsLoaded
                           ? ChaseNavigationCapability.Supported
                           : ChaseNavigationCapability.Unavailable;
            }
        }

        public string GeometryVersion
        {
            get
            {
                return this.geometryLoadResult.IsLoaded
                           ? this.geometryLoadResult.Geometry.SourceSha256
                           : string.Empty;
            }
        }

        internal string GeometryError
        {
            get { return this.geometryLoadResult.Error; }
        }

        public bool TryProjectToSurface(
            ChaseNavigationPoint reference,
            double x,
            double z,
            out ChaseNavigationPoint projected)
        {
            projected = default(ChaseNavigationPoint);
            if (this.Capability != ChaseNavigationCapability.Supported
                || !reference.IsFinite
                || !IsFinite(x)
                || !IsFinite(z))
            {
                return false;
            }

            // The promoted PF127 collision set proves blocking surfaces but does not
            // contain a reliable walkable-floor projection. Keep each bounded route
            // on the caller's authoritative live elevation; cross-elevation routing
            // remains unavailable until a captured floor/navigation representation exists.
            projected = new ChaseNavigationPoint(x, reference.Y, z);
            return projected.IsFinite;
        }

        public bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
        {
            if (this.Capability != ChaseNavigationCapability.Supported
                || !start.IsFinite
                || !end.IsFinite
                || Math.Abs(start.Y - end.Y) > MaximumPlanElevationDifference)
            {
                return false;
            }

            double distance = start.Distance2D(end);
            if (distance < MinimumSegmentLength)
            {
                return true;
            }

            double directionX = (end.X - start.X) / distance;
            double directionZ = (end.Z - start.Z) / distance;
            double offsetX = -directionZ * AgentRadius;
            double offsetZ = directionX * AgentRadius;

            return this.IsProbeSegmentClear(start, end, 0.0, 0.0, LowerCollisionProbeHeight)
                   && this.IsProbeSegmentClear(start, end, offsetX, offsetZ, LowerCollisionProbeHeight)
                   && this.IsProbeSegmentClear(start, end, -offsetX, -offsetZ, LowerCollisionProbeHeight)
                   && this.IsProbeSegmentClear(start, end, 0.0, 0.0, UpperCollisionProbeHeight)
                   && this.IsProbeSegmentClear(start, end, offsetX, offsetZ, UpperCollisionProbeHeight)
                   && this.IsProbeSegmentClear(start, end, -offsetX, -offsetZ, UpperCollisionProbeHeight);
        }

        public bool IsAttackLineTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
        {
            if (this.Capability != ChaseNavigationCapability.Supported
                || !start.IsFinite
                || !end.IsFinite)
            {
                return false;
            }

            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            double deltaZ = end.Z - start.Z;
            return (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ)
                       < MinimumSegmentLength * MinimumSegmentLength
                   || this.IsProbeSegmentClear(start, end, 0.0, 0.0, AttackLineProbeHeight);
        }

        public ChaseRoutePlan RequestRoute(
            ChaseNavigationPoint start,
            ChaseNavigationPoint goal,
            ChaseRouteSearchLimits limits)
        {
            if (this.Capability != ChaseNavigationCapability.Supported)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.Unavailable,
                    this.GeometryVersion,
                    0,
                    0);
            }

            if (Math.Abs(start.Y - goal.Y) > MaximumPlanElevationDifference)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.Unreachable,
                    this.GeometryVersion,
                    0,
                    0);
            }

            return this.planner.Plan(this, start, goal, limits ?? ChaseRouteSearchLimits.Default);
        }

        public bool IsRouteCurrent(ChaseRoutePlan route)
        {
            return route != null
                   && route.IsSuccess
                   && this.Capability == ChaseNavigationCapability.Supported
                   && string.Equals(
                       route.GeometryVersion,
                       this.GeometryVersion,
                       StringComparison.Ordinal);
        }

        private bool IsProbeSegmentClear(
            ChaseNavigationPoint start,
            ChaseNavigationPoint end,
            double offsetX,
            double offsetZ,
            double probeHeight)
        {
            var adjustedStart = new CollisionPoint3(
                start.X + offsetX,
                start.Y + probeHeight,
                start.Z + offsetZ);
            var adjustedEnd = new CollisionPoint3(
                end.X + offsetX,
                end.Y + probeHeight,
                end.Z + offsetZ);
            SegmentTriangleHit hit;
            try
            {
                return !this.geometryLoadResult.Geometry.TryFindFirstBlockingHit(
                    adjustedStart,
                    adjustedEnd,
                    out hit);
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
}
