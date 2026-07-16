namespace ZoneEngine.Core.Navigation
{
    using System;
    using System.Collections.Generic;

    internal sealed class BoundedGridChaseRoutePlanner
    {
        private static readonly GridOffset[] NeighborOffsets =
        {
            new GridOffset(1, 0),
            new GridOffset(0, 1),
            new GridOffset(-1, 0),
            new GridOffset(0, -1),
            new GridOffset(1, 1),
            new GridOffset(-1, 1),
            new GridOffset(-1, -1),
            new GridOffset(1, -1)
        };

        internal ChaseRoutePlan Plan(
            IPlayfieldChaseNavigationProvider provider,
            ChaseNavigationPoint start,
            ChaseNavigationPoint goal,
            ChaseRouteSearchLimits limits)
        {
            if (provider == null || limits == null || !start.IsFinite || !goal.IsFinite)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.InvalidRequest,
                    provider == null ? string.Empty : provider.GeometryVersion,
                    0,
                    0);
            }

            if (provider.Capability == ChaseNavigationCapability.Unsupported)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.Unsupported,
                    provider.GeometryVersion,
                    0,
                    0);
            }

            if (provider.Capability != ChaseNavigationCapability.Supported)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.Unavailable,
                    provider.GeometryVersion,
                    0,
                    0);
            }

            if (start.Distance2D(goal) > limits.MaximumStartGoalDistance)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.SearchLimitReached,
                    provider.GeometryVersion,
                    0,
                    0);
            }

            var context = new PlannerContext(provider, limits, start, goal);
            if (context.TrySegment(start, goal))
            {
                return ChaseRoutePlan.Success(
                    new[] { goal },
                    provider.GeometryVersion,
                    0,
                    context.SegmentChecks);
            }

            GridKey startKey = GridKey.FromPoint(start, limits.CellSize);
            var open = new OpenHeap();
            var closed = new HashSet<GridKey>();
            var costs = new Dictionary<GridKey, double>();
            var parents = new Dictionary<GridKey, GridKey>();
            var points = new Dictionary<GridKey, ChaseNavigationPoint>();
            costs[startKey] = 0.0;
            points[startKey] = start;
            open.Push(new OpenEntry(startKey, start.Distance2D(goal), 0.0));

            while (open.Count > 0
                   && context.ExpandedNodes < limits.MaximumExpandedNodes
                   && context.SegmentChecks < limits.MaximumSegmentChecks)
            {
                OpenEntry entry = open.Pop();
                if (closed.Contains(entry.Key))
                {
                    continue;
                }

                double knownCost;
                if (!costs.TryGetValue(entry.Key, out knownCost)
                    || entry.Cost > knownCost + 1.0e-8)
                {
                    continue;
                }

                closed.Add(entry.Key);
                context.ExpandedNodes++;
                ChaseNavigationPoint current = points[entry.Key];
                if (current.Distance2D(goal) <= limits.GoalConnectionDistance
                    && context.TrySegment(current, goal))
                {
                    ChaseNavigationPoint[] raw = Reconstruct(
                        startKey,
                        entry.Key,
                        goal,
                        parents,
                        points);
                    ChaseNavigationPoint[] smoothed = context.Smooth(start, raw);
                    return ChaseRoutePlan.Success(
                        smoothed,
                        provider.GeometryVersion,
                        context.ExpandedNodes,
                        context.SegmentChecks);
                }

                for (int offsetIndex = 0; offsetIndex < NeighborOffsets.Length; offsetIndex++)
                {
                    GridOffset offset = NeighborOffsets[offsetIndex];
                    var neighborKey = new GridKey(entry.Key.X + offset.X, entry.Key.Z + offset.Z);
                    if (closed.Contains(neighborKey) || !context.IsWithinBounds(neighborKey))
                    {
                        continue;
                    }

                    ChaseNavigationPoint neighbor;
                    if (!context.TryProject(neighborKey, current, out neighbor)
                        || Math.Abs(neighbor.Y - current.Y) > limits.MaximumVerticalStep
                        || !context.TrySegment(current, neighbor))
                    {
                        continue;
                    }

                    double tentativeCost = knownCost + Distance3D(current, neighbor);
                    double previousCost;
                    if (costs.TryGetValue(neighborKey, out previousCost)
                        && tentativeCost >= previousCost - 1.0e-8)
                    {
                        continue;
                    }

                    costs[neighborKey] = tentativeCost;
                    parents[neighborKey] = entry.Key;
                    points[neighborKey] = neighbor;
                    double heuristic = neighbor.Distance2D(goal);
                    open.Push(new OpenEntry(neighborKey, tentativeCost + heuristic, tentativeCost));
                }
            }

            ChaseRoutePlanStatus failure =
                context.ExpandedNodes >= limits.MaximumExpandedNodes
                || context.SegmentChecks >= limits.MaximumSegmentChecks
                    ? ChaseRoutePlanStatus.SearchLimitReached
                    : ChaseRoutePlanStatus.Unreachable;
            return ChaseRoutePlan.Failed(
                failure,
                provider.GeometryVersion,
                context.ExpandedNodes,
                context.SegmentChecks);
        }

        private static ChaseNavigationPoint[] Reconstruct(
            GridKey startKey,
            GridKey endKey,
            ChaseNavigationPoint goal,
            IDictionary<GridKey, GridKey> parents,
            IDictionary<GridKey, ChaseNavigationPoint> points)
        {
            var reverse = new List<ChaseNavigationPoint> { goal };
            GridKey current = endKey;
            while (!current.Equals(startKey))
            {
                reverse.Add(points[current]);
                GridKey parent;
                if (!parents.TryGetValue(current, out parent))
                {
                    break;
                }

                current = parent;
            }

            reverse.Reverse();
            return reverse.ToArray();
        }

        private static double Distance3D(ChaseNavigationPoint left, ChaseNavigationPoint right)
        {
            double x = left.X - right.X;
            double y = left.Y - right.Y;
            double z = left.Z - right.Z;
            return Math.Sqrt((x * x) + (y * y) + (z * z));
        }

        private sealed class PlannerContext
        {
            private readonly IPlayfieldChaseNavigationProvider provider;

            private readonly ChaseRouteSearchLimits limits;

            private readonly double minimumX;

            private readonly double maximumX;

            private readonly double minimumZ;

            private readonly double maximumZ;

            private readonly Dictionary<SurfaceKey, SurfaceProjection> projections =
                new Dictionary<SurfaceKey, SurfaceProjection>();

            internal PlannerContext(
                IPlayfieldChaseNavigationProvider provider,
                ChaseRouteSearchLimits limits,
                ChaseNavigationPoint start,
                ChaseNavigationPoint goal)
            {
                this.provider = provider;
                this.limits = limits;
                this.minimumX = Math.Min(start.X, goal.X) - limits.DetourMargin;
                this.maximumX = Math.Max(start.X, goal.X) + limits.DetourMargin;
                this.minimumZ = Math.Min(start.Z, goal.Z) - limits.DetourMargin;
                this.maximumZ = Math.Max(start.Z, goal.Z) + limits.DetourMargin;
            }

            internal int ExpandedNodes { get; set; }

            internal int SegmentChecks { get; private set; }

            internal bool IsWithinBounds(GridKey key)
            {
                double x = key.X * this.limits.CellSize;
                double z = key.Z * this.limits.CellSize;
                return x >= this.minimumX
                       && x <= this.maximumX
                       && z >= this.minimumZ
                       && z <= this.maximumZ;
            }

            internal bool TryProject(
                GridKey key,
                ChaseNavigationPoint reference,
                out ChaseNavigationPoint projected)
            {
                int layer = (int)Math.Round(reference.Y / this.limits.MaximumVerticalStep);
                var surfaceKey = new SurfaceKey(key, layer);
                SurfaceProjection cached;
                if (this.projections.TryGetValue(surfaceKey, out cached))
                {
                    projected = cached.Point;
                    return cached.Found;
                }

                bool found = this.provider.TryProjectToSurface(
                    reference,
                    key.X * this.limits.CellSize,
                    key.Z * this.limits.CellSize,
                    out projected);
                this.projections[surfaceKey] = new SurfaceProjection(found, projected);
                return found;
            }

            internal bool TrySegment(ChaseNavigationPoint start, ChaseNavigationPoint end)
            {
                if (this.SegmentChecks >= this.limits.MaximumSegmentChecks)
                {
                    return false;
                }

                this.SegmentChecks++;
                return this.provider.IsSegmentTraversable(start, end);
            }

            internal ChaseNavigationPoint[] Smooth(
                ChaseNavigationPoint start,
                ChaseNavigationPoint[] route)
            {
                if (route == null || route.Length < 2 || this.limits.MaximumSmoothingChecks == 0)
                {
                    return route ?? new ChaseNavigationPoint[0];
                }

                var result = new List<ChaseNavigationPoint>();
                ChaseNavigationPoint anchor = start;
                int index = 0;
                int smoothingChecks = 0;
                while (index < route.Length)
                {
                    int selected = index;
                    for (int candidate = route.Length - 1;
                         candidate > index && smoothingChecks < this.limits.MaximumSmoothingChecks;
                         candidate--)
                    {
                        smoothingChecks++;
                        if (this.TrySegment(anchor, route[candidate]))
                        {
                            selected = candidate;
                            break;
                        }
                    }

                    result.Add(route[selected]);
                    anchor = route[selected];
                    index = selected + 1;
                }

                return result.ToArray();
            }
        }

        private struct GridOffset
        {
            internal GridOffset(int x, int z)
            {
                this.X = x;
                this.Z = z;
            }

            internal int X { get; private set; }

            internal int Z { get; private set; }
        }

        private struct GridKey : IEquatable<GridKey>
        {
            internal GridKey(int x, int z)
            {
                this.X = x;
                this.Z = z;
            }

            internal int X { get; private set; }

            internal int Z { get; private set; }

            internal static GridKey FromPoint(ChaseNavigationPoint point, double cellSize)
            {
                return new GridKey(
                    (int)Math.Round(point.X / cellSize),
                    (int)Math.Round(point.Z / cellSize));
            }

            public bool Equals(GridKey other)
            {
                return this.X == other.X && this.Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is GridKey && this.Equals((GridKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (this.X * 397) ^ this.Z;
                }
            }
        }

        private struct SurfaceKey : IEquatable<SurfaceKey>
        {
            internal SurfaceKey(GridKey key, int layer)
            {
                this.Key = key;
                this.Layer = layer;
            }

            internal GridKey Key { get; private set; }

            internal int Layer { get; private set; }

            public bool Equals(SurfaceKey other)
            {
                return this.Key.Equals(other.Key) && this.Layer == other.Layer;
            }

            public override bool Equals(object obj)
            {
                return obj is SurfaceKey && this.Equals((SurfaceKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (this.Key.GetHashCode() * 397) ^ this.Layer;
                }
            }
        }

        private struct SurfaceProjection
        {
            internal SurfaceProjection(bool found, ChaseNavigationPoint point)
            {
                this.Found = found;
                this.Point = point;
            }

            internal bool Found { get; private set; }

            internal ChaseNavigationPoint Point { get; private set; }
        }

        private struct OpenEntry
        {
            internal OpenEntry(GridKey key, double priority, double cost)
            {
                this.Key = key;
                this.Priority = priority;
                this.Cost = cost;
            }

            internal GridKey Key { get; private set; }

            internal double Priority { get; private set; }

            internal double Cost { get; private set; }
        }

        private sealed class OpenHeap
        {
            private readonly List<OpenEntry> entries = new List<OpenEntry>();

            internal int Count
            {
                get { return this.entries.Count; }
            }

            internal void Push(OpenEntry entry)
            {
                this.entries.Add(entry);
                int index = this.entries.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (Compare(this.entries[parent], entry) <= 0)
                    {
                        break;
                    }

                    this.entries[index] = this.entries[parent];
                    index = parent;
                }

                this.entries[index] = entry;
            }

            internal OpenEntry Pop()
            {
                OpenEntry result = this.entries[0];
                int lastIndex = this.entries.Count - 1;
                OpenEntry tail = this.entries[lastIndex];
                this.entries.RemoveAt(lastIndex);
                if (lastIndex == 0)
                {
                    return result;
                }

                int index = 0;
                while (true)
                {
                    int left = (index * 2) + 1;
                    if (left >= this.entries.Count)
                    {
                        break;
                    }

                    int right = left + 1;
                    int child = right < this.entries.Count
                                && Compare(this.entries[right], this.entries[left]) < 0
                                    ? right
                                    : left;
                    if (Compare(tail, this.entries[child]) <= 0)
                    {
                        break;
                    }

                    this.entries[index] = this.entries[child];
                    index = child;
                }

                this.entries[index] = tail;
                return result;
            }

            private static int Compare(OpenEntry left, OpenEntry right)
            {
                int result = left.Priority.CompareTo(right.Priority);
                if (result != 0)
                {
                    return result;
                }

                result = left.Cost.CompareTo(right.Cost);
                if (result != 0)
                {
                    return result;
                }

                result = left.Key.X.CompareTo(right.Key.X);
                return result != 0 ? result : left.Key.Z.CompareTo(right.Key.Z);
            }
        }
    }
}
