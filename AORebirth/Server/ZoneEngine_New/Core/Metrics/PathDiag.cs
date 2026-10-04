namespace ZoneEngine_New.Core.Metrics
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Numerics;

    using AORebirth.World.Pathfinding;

    /// <summary>
    /// Navmesh search entry point for tick code. Each search runs under its own stall-watch stage (so its time is
    /// not charged to whatever AI stage asked for it) and, while <see cref="TickProfiler"/> records, its outcome,
    /// iterations and time are kept per dynel and purpose for <c>.diag path</c>.
    /// </summary>
    internal static class PathDiag
    {
        /// <summary>NPC asking whether it could still reach a hate target (engage, leash and chase checks).</summary>
        public const string Reach = "path.reach";

        /// <summary>NPC planning the route it will walk.</summary>
        public const string Route = "path.route";

        /// <summary>NPC checking it can walk to a point (home).</summary>
        public const string Walk = "path.walk";

        /// <summary>Pet measuring the walk to its owner.</summary>
        public const string Pet = "path.pet";

        /// <summary>Character motor planning a navigation path.</summary>
        public const string Motor = "path.motor";

        /// <summary>Building one target's shared reach map (cache hits: NPC answers served by an existing map).</summary>
        public const string ReachMap = "path.reachmap";

        /// <summary>Builds a target's reach map under its own stage, recorded like a search (iterations = polygons).</summary>
        public static AORebirth.World.Pathfinding.ReachMap BuildReachMap(NavMeshPathfinder finder, Vector3 target, float radius, int maxPolys)
        {
            using TickStallWatch.StageScope scope = TickStallWatch.Enter(ReachMap, TickStallWatch.CurrentDynel?.Identity.Instance ?? 0);
            long started = Stopwatch.GetTimestamp();
            AORebirth.World.Pathfinding.ReachMap map = finder.BuildReachMap(target, radius, maxPolys);
            if (TickProfiler.IsRecording)
            {
                PathSearchOutcome outcome = map.Kind switch
                {
                    ReachMapKind.Flooded => PathSearchOutcome.Found,
                    ReachMapKind.TargetNoMesh => PathSearchOutcome.NoEnds,
                    ReachMapKind.TargetOffMesh => PathSearchOutcome.EndOffMesh,
                    _ => PathSearchOutcome.IterationCap
                };
                TickProfiler.RecordPathSearch(ReachMap, outcome, map.PolyCount, Stopwatch.GetTimestamp() - started);
            }

            return map;
        }

        public static bool TryFindPath(NavMeshPathfinder finder, Vector3 start, Vector3 end, List<Vector3> waypoints, string stage)
        {
            using TickStallWatch.StageScope scope = TickStallWatch.Enter(stage, TickStallWatch.CurrentDynel?.Identity.Instance ?? 0);
            long started = Stopwatch.GetTimestamp();
            bool found = finder.TryFindPath(start, end, waypoints);
            if (TickProfiler.IsRecording)
                TickProfiler.RecordPathSearch(stage, finder.LastOutcome, finder.LastIterations, Stopwatch.GetTimestamp() - started);
            return found;
        }

        /// <summary>A reachability answer reused from cache; counted so the hit rate is visible.</summary>
        public static void CacheHit(string stage)
        {
            if (TickProfiler.IsRecording)
                TickProfiler.RecordPathCacheHit(stage);
        }
    }
}
