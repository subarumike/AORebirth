namespace ZoneEngine.Core.Navigation
{
    using System;

    internal enum ChaseNavigationCapability
    {
        Unsupported = 0,
        Unavailable = 1,
        Supported = 2
    }

    internal enum ChaseRoutePlanStatus
    {
        Success = 0,
        Unsupported = 1,
        Unavailable = 2,
        InvalidRequest = 3,
        Unreachable = 4,
        SearchLimitReached = 5
    }

    internal enum NpcChaseMovementKind
    {
        Unsupported = 0,
        Unavailable = 1,
        Direct = 2,
        Route = 3,
        Hold = 4
    }

    internal enum NpcChaseInvalidationReason
    {
        None = 0,
        DirectPathRestored = 1,
        TargetMoved = 2,
        TargetReplaced = 3,
        GeometryVersionChanged = 4,
        RouteSegmentInvalid = 5,
        RouteDeviation = 6,
        Stuck = 7,
        RouteCompleted = 8,
        TargetLost = 9,
        CombatCancelled = 10,
        Death = 11,
        CorpseTransition = 12,
        Despawn = 13,
        LeashReset = 14,
        EncounterReset = 15,
        PlayfieldReset = 16,
        RuntimeDisposed = 17
    }

    internal struct ChaseNavigationPoint
    {
        internal ChaseNavigationPoint(double x, double y, double z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal double X { get; private set; }

        internal double Y { get; private set; }

        internal double Z { get; private set; }

        internal bool IsFinite
        {
            get
            {
                return IsFiniteValue(this.X)
                       && IsFiniteValue(this.Y)
                       && IsFiniteValue(this.Z);
            }
        }

        internal double Distance2D(ChaseNavigationPoint other)
        {
            double x = this.X - other.X;
            double z = this.Z - other.Z;
            return Math.Sqrt((x * x) + (z * z));
        }

        internal double DistanceSquared2D(ChaseNavigationPoint other)
        {
            double x = this.X - other.X;
            double z = this.Z - other.Z;
            return (x * x) + (z * z);
        }

        private static bool IsFiniteValue(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class ChaseRouteSearchLimits
    {
        internal static readonly ChaseRouteSearchLimits Default =
            new ChaseRouteSearchLimits(
                2.5,
                32.0,
                160.0,
                4096,
                32768,
                10.0,
                1.5,
                256);

        internal ChaseRouteSearchLimits(
            double cellSize,
            double detourMargin,
            double maximumStartGoalDistance,
            int maximumExpandedNodes,
            int maximumSegmentChecks,
            double goalConnectionDistance,
            double maximumVerticalStep,
            int maximumSmoothingChecks)
        {
            if (cellSize <= 0.0
                || detourMargin <= 0.0
                || maximumStartGoalDistance <= 0.0
                || maximumExpandedNodes <= 0
                || maximumSegmentChecks <= 0
                || goalConnectionDistance <= 0.0
                || maximumVerticalStep <= 0.0
                || maximumSmoothingChecks < 0)
            {
                throw new ArgumentOutOfRangeException("limits");
            }

            this.CellSize = cellSize;
            this.DetourMargin = detourMargin;
            this.MaximumStartGoalDistance = maximumStartGoalDistance;
            this.MaximumExpandedNodes = maximumExpandedNodes;
            this.MaximumSegmentChecks = maximumSegmentChecks;
            this.GoalConnectionDistance = goalConnectionDistance;
            this.MaximumVerticalStep = maximumVerticalStep;
            this.MaximumSmoothingChecks = maximumSmoothingChecks;
        }

        internal double CellSize { get; private set; }

        internal double DetourMargin { get; private set; }

        internal double MaximumStartGoalDistance { get; private set; }

        internal int MaximumExpandedNodes { get; private set; }

        internal int MaximumSegmentChecks { get; private set; }

        internal double GoalConnectionDistance { get; private set; }

        internal double MaximumVerticalStep { get; private set; }

        internal int MaximumSmoothingChecks { get; private set; }
    }

    internal sealed class ChaseRoutePlan
    {
        private ChaseRoutePlan(
            ChaseRoutePlanStatus status,
            ChaseNavigationPoint[] points,
            string geometryVersion,
            int expandedNodes,
            int segmentChecks)
        {
            this.Status = status;
            this.Points = points ?? new ChaseNavigationPoint[0];
            this.GeometryVersion = geometryVersion ?? string.Empty;
            this.ExpandedNodes = expandedNodes;
            this.SegmentChecks = segmentChecks;
        }

        internal ChaseRoutePlanStatus Status { get; private set; }

        internal ChaseNavigationPoint[] Points { get; private set; }

        internal string GeometryVersion { get; private set; }

        internal int ExpandedNodes { get; private set; }

        internal int SegmentChecks { get; private set; }

        internal bool IsSuccess
        {
            get { return this.Status == ChaseRoutePlanStatus.Success && this.Points.Length > 0; }
        }

        internal static ChaseRoutePlan Success(
            ChaseNavigationPoint[] points,
            string geometryVersion,
            int expandedNodes,
            int segmentChecks)
        {
            if (points == null || points.Length == 0)
            {
                throw new ArgumentException("A successful route requires points.", "points");
            }

            return new ChaseRoutePlan(
                ChaseRoutePlanStatus.Success,
                (ChaseNavigationPoint[])points.Clone(),
                geometryVersion,
                expandedNodes,
                segmentChecks);
        }

        internal static ChaseRoutePlan Failed(
            ChaseRoutePlanStatus status,
            string geometryVersion,
            int expandedNodes,
            int segmentChecks)
        {
            if (status == ChaseRoutePlanStatus.Success)
            {
                throw new ArgumentOutOfRangeException("status");
            }

            return new ChaseRoutePlan(
                status,
                new ChaseNavigationPoint[0],
                geometryVersion,
                expandedNodes,
                segmentChecks);
        }
    }

    internal interface IPlayfieldChaseNavigationProvider
    {
        int PlayfieldResource { get; }

        ChaseNavigationCapability Capability { get; }

        string GeometryVersion { get; }

        bool TryProjectToSurface(
            ChaseNavigationPoint reference,
            double x,
            double z,
            out ChaseNavigationPoint projected);

        bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end);

        ChaseRoutePlan RequestRoute(
            ChaseNavigationPoint start,
            ChaseNavigationPoint goal,
            ChaseRouteSearchLimits limits);

        bool IsRouteCurrent(ChaseRoutePlan route);
    }

    internal sealed class UnsupportedPlayfieldChaseNavigationProvider : IPlayfieldChaseNavigationProvider
    {
        private readonly int playfieldResource;

        internal UnsupportedPlayfieldChaseNavigationProvider(int playfieldResource)
        {
            this.playfieldResource = playfieldResource;
        }

        public int PlayfieldResource
        {
            get { return this.playfieldResource; }
        }

        public ChaseNavigationCapability Capability
        {
            get { return ChaseNavigationCapability.Unsupported; }
        }

        public string GeometryVersion
        {
            get { return string.Empty; }
        }

        public bool TryProjectToSurface(
            ChaseNavigationPoint reference,
            double x,
            double z,
            out ChaseNavigationPoint projected)
        {
            projected = default(ChaseNavigationPoint);
            return false;
        }

        public bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
        {
            return false;
        }

        public ChaseRoutePlan RequestRoute(
            ChaseNavigationPoint start,
            ChaseNavigationPoint goal,
            ChaseRouteSearchLimits limits)
        {
            return ChaseRoutePlan.Failed(
                ChaseRoutePlanStatus.Unsupported,
                string.Empty,
                0,
                0);
        }

        public bool IsRouteCurrent(ChaseRoutePlan route)
        {
            return false;
        }
    }
}
