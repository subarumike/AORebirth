namespace ZoneEngine.Core.Navigation
{
    using System;

    internal sealed class OfficialDungeonChaseNavigationProvider : IPlayfieldChaseNavigationProvider
    {
        internal const double AgentRadius = 0.35;

        internal const double MaximumSurfaceProjectionDifference = 1.50;

        internal const double SegmentSampleSpacing = 0.50;

        internal const double AttackLineSurfaceTolerance = 3.0;

        private const double MinimumSegmentLength = 0.01;

        private readonly int playfieldResource;

        private readonly OfficialDungeonGeometryLoadResult geometryLoadResult;

        private readonly BoundedGridChaseRoutePlanner planner =
            new BoundedGridChaseRoutePlanner();

        internal OfficialDungeonChaseNavigationProvider(
            int playfieldResource,
            OfficialDungeonGeometryLoadResult geometryLoadResult)
        {
            this.playfieldResource = playfieldResource;
            this.geometryLoadResult = geometryLoadResult
                                      ?? OfficialDungeonGeometryLoadResult.Failed(
                                          "Official dungeon geometry load result is missing.");
        }

        public int PlayfieldResource
        {
            get { return this.playfieldResource; }
        }

        public ChaseNavigationCapability Capability
        {
            get
            {
                return this.geometryLoadResult.IsLoaded
                       && this.geometryLoadResult.Geometry.PlayfieldResource
                       == this.playfieldResource
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
            return this.Capability == ChaseNavigationCapability.Supported
                   && this.geometryLoadResult.Geometry.TryProjectToSurface(
                       reference,
                       x,
                       z,
                       out projected);
        }

        public bool IsSegmentTraversable(
            ChaseNavigationPoint start,
            ChaseNavigationPoint end)
        {
            if (this.Capability != ChaseNavigationCapability.Supported
                || !start.IsFinite
                || !end.IsFinite)
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
            return this.IsSampledSurfaceSegmentTraversable(start, end, 0.0, 0.0)
                   && this.IsSampledSurfaceSegmentTraversable(
                       start,
                       end,
                       offsetX,
                       offsetZ)
                   && this.IsSampledSurfaceSegmentTraversable(
                       start,
                       end,
                       -offsetX,
                       -offsetZ);
        }

        public bool IsAttackLineTraversable(
            ChaseNavigationPoint start,
            ChaseNavigationPoint end)
        {
            if (this.Capability != ChaseNavigationCapability.Supported
                || !start.IsFinite
                || !end.IsFinite)
            {
                return false;
            }

            double distance = start.Distance2D(end);
            if (distance < MinimumSegmentLength)
            {
                return true;
            }

            int samples = Math.Max(1, (int)Math.Ceiling(distance / SegmentSampleSpacing));
            for (int sample = 0; sample <= samples; sample++)
            {
                double fraction = (double)sample / samples;
                var reference = new ChaseNavigationPoint(
                    start.X + ((end.X - start.X) * fraction),
                    start.Y + ((end.Y - start.Y) * fraction),
                    start.Z + ((end.Z - start.Z) * fraction));
                ChaseNavigationPoint projected;
                if (!this.geometryLoadResult.Geometry.TryProjectToSurface(
                        reference,
                        reference.X,
                        reference.Z,
                        out projected)
                    || Math.Abs(projected.Y - reference.Y) > AttackLineSurfaceTolerance)
                {
                    return false;
                }
            }

            return true;
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

            return this.planner.Plan(
                this,
                start,
                goal,
                limits ?? ChaseRouteSearchLimits.Default);
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

        private bool IsSampledSurfaceSegmentTraversable(
            ChaseNavigationPoint start,
            ChaseNavigationPoint end,
            double offsetX,
            double offsetZ)
        {
            double distance = start.Distance2D(end);
            int samples = Math.Max(1, (int)Math.Ceiling(distance / SegmentSampleSpacing));
            ChaseNavigationPoint previous = default(ChaseNavigationPoint);
            bool hasPrevious = false;
            for (int sample = 0; sample <= samples; sample++)
            {
                double fraction = (double)sample / samples;
                var reference = new ChaseNavigationPoint(
                    start.X + ((end.X - start.X) * fraction),
                    start.Y + ((end.Y - start.Y) * fraction),
                    start.Z + ((end.Z - start.Z) * fraction));
                ChaseNavigationPoint projected;
                if (!this.geometryLoadResult.Geometry.TryProjectToSurface(
                        reference,
                        reference.X + offsetX,
                        reference.Z + offsetZ,
                        out projected)
                    || Math.Abs(projected.Y - reference.Y)
                       > MaximumSurfaceProjectionDifference
                    || (hasPrevious
                        && Math.Abs(projected.Y - previous.Y)
                        > MaximumSurfaceProjectionDifference))
                {
                    return false;
                }

                previous = projected;
                hasPrevious = true;
            }

            return true;
        }
    }
}
