namespace AORebirth.World.Pathfinding
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;

    using DotRecast.Core.Numerics;
    using DotRecast.Detour;

    /// <summary>
    /// Tick-thread Detour query over a GameData <c>Navmesh.dat</c>. One instance per playfield.
    /// </summary>
    public sealed class NavMeshPathfinder : IDisposable
    {
        const int MaxPathPolys = 256;
        const int MaxStraightPath = 256;
        // DotRecast's default node pool grows without bound. Native Detour stops at maxNodes.
        const int MaxSearchIters = 4096;
        const float StartSkipEpsilon = 0.05f;
        const float PathQueryHorizontal = 8f;
        const float OnMeshHorizontal = 2f;

        /// <summary>Search radius used when placing an NPC onto the mesh at spawn.</summary>
        public const float SpawnSnapExtent = 64f;

        /// <summary>XZ half-width of the column <see cref="TrySnapDown"/> gathers polygons from.</summary>
        const float ColumnHalfWidth = 0.05f;

        /// <summary>Stacked floors a column can hold before the rest are ignored.</summary>
        const int MaxColumnPolys = 64;

        readonly DtNavMeshQuery _query;
        readonly DtQueryDefaultFilter _filter;
        readonly RcVec3f _extents;
        readonly NavMeshIslands _islands;

        // Reach-map flood scratch, grown on demand.
        long[] _reachRefs = [];
        long[] _reachParents = [];
        float[] _reachCosts = [];

        NavMeshPathfinder(int playfieldId, DtNavMesh mesh, NavMeshBuildSettings settings)
        {
            PlayfieldId = playfieldId;
            Settings = settings;
            _query = new DtNavMeshQuery(mesh);
            _filter = new DtQueryDefaultFilter();
            float pathHoriz = MathF.Max(settings.AgentRadius, PathQueryHorizontal);
            float pathVert = settings.AgentHeight + settings.AgentMaxClimb + PathQueryHorizontal;
            _extents = new RcVec3f(pathHoriz, pathVert, pathHoriz);
            _islands = NavMeshIslands.Build(mesh);
        }

        /// <summary>Separate connected regions of this navmesh.</summary>
        public int IslandCount => _islands.Count;

        public int PlayfieldId { get; }

        public NavMeshBuildSettings Settings { get; }

        /// <summary>How the most recent <see cref="TryFindPath"/> ended. Diagnostics only; tick-thread state.</summary>
        public PathSearchOutcome LastOutcome { get; private set; }

        /// <summary>Corridor search iterations the most recent <see cref="TryFindPath"/> used (cap <see cref="SearchIterationCap"/>).</summary>
        public int LastIterations { get; private set; }

        public static int SearchIterationCap => MaxSearchIters;

        public static bool TryLoad(string gameDataRoot, int playfieldId, out NavMeshPathfinder? pathfinder)
        {
            return TryLoad(gameDataRoot, playfieldId, out pathfinder, out _);
        }

        /// <param name="failure">Why a present mesh was rejected; null when it loaded or there is none.</param>
        public static bool TryLoad(
            string gameDataRoot,
            int playfieldId,
            out NavMeshPathfinder? pathfinder,
            out string? failure)
        {
            pathfinder = null;
            failure = null;
            if (string.IsNullOrWhiteSpace(gameDataRoot) || playfieldId <= 0)
                return false;
            if (DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, playfieldId))
                return false;

            string meshPath = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldNavMeshRelativePath(playfieldId));
            if (!File.Exists(meshPath))
                return false;

            NavMeshBuildSettings settings;
            try
            {
                settings = NavMeshBuildSettings.Load(NavMeshBuildSettings.ResolvePath(gameDataRoot));
            }
            catch (Exception exception)
            {
                failure = "settings: " + exception.Message;
                return false;
            }

            DtNavMesh mesh;
            try
            {
                mesh = NavMeshFile.Read(meshPath, out _);
            }
            catch (Exception exception)
            {
                failure = "mesh: " + exception.Message;
                return false;
            }

            pathfinder = new NavMeshPathfinder(playfieldId, mesh, settings);
            return true;
        }

        public bool TrySnap(Vector3 position, out Vector3 snapped)
            => TrySnap(position, MathF.Max(Settings.AgentRadius, PathQueryHorizontal), out snapped);

        public bool TrySnap(Vector3 position, float extent, out Vector3 snapped)
        {
            snapped = default;
            if (extent <= 0f)
                return false;

            float vert = MathF.Max(extent, Settings.AgentHeight + Settings.AgentMaxClimb);
            DtStatus status = _query.FindNearestPoly(
                position,
                new RcVec3f(extent, vert, extent),
                _filter,
                out long polyRef,
                out RcVec3f nearest,
                out _);
            if (status.Failed() || polyRef == 0)
                return false;

            snapped = new Vector3(nearest.X, nearest.Y, nearest.Z);
            return true;
        }

        /// <summary>
        /// Snaps straight down: the highest polygon under (<paramref name="position"/>.X, .Z) whose surface is at
        /// or below the position (plus <see cref="NavMeshBuildSettings.AgentMaxClimb"/> for feet placed slightly
        /// under the floor), searching at most <paramref name="maxDrop"/> down. Taking the first surface below,
        /// not the nearest in 3D, keeps a placement over a pit or stairwell on the floor under it rather than on
        /// the lip beside it; a bridge overhead is ignored and a bridge underneath wins over the ground below it.
        /// </summary>
        public bool TrySnapDown(Vector3 position, float maxDrop, out Vector3 snapped)
        {
            snapped = default;
            if (maxDrop <= 0f)
                return false;

            float above = Settings.AgentMaxClimb;
            float half = (maxDrop + above) * 0.5f;
            var center = new RcVec3f(position.X, position.Y + above - half, position.Z);
            var extents = new RcVec3f(ColumnHalfWidth, half, ColumnHalfWidth);

            var polys = new long[MaxColumnPolys];
            var collect = new DtCollectPolysQuery(polys, polys.Length);
            if (_query.QueryPolygons(center, extents, _filter, ref collect).Failed())
                return false;

            bool found = false;
            float best = float.NegativeInfinity;
            var at = new RcVec3f(position.X, position.Y, position.Z);
            for (int i = 0; i < collect.NumCollected(); i++)
            {
                // GetPolyHeight only succeeds when the XZ point lies inside the polygon.
                if (_query.GetPolyHeight(polys[i], at, out float height).Failed())
                    continue;
                if (height > position.Y + above || height < position.Y - maxDrop)
                    continue;
                if (height <= best)
                    continue;

                best = height;
                found = true;
            }

            if (!found)
                return false;

            snapped = new Vector3(position.X, best, position.Z);
            return true;
        }

        public bool TryCanReach(Vector3 start, Vector3 end)
        {
            if (!TryResolveEnds(start, end, out PolyEnds ends))
                return false;
            if (!IsOnMesh(end, ends.EndPt))
                return false;

            Span<long> path = stackalloc long[MaxPathPolys];
            return TrySearchCorridor(ends, path, out int pathCount)
                && pathCount > 0
                && path[pathCount - 1] == ends.EndRef;
        }

        public bool TryFindPath(Vector3 start, Vector3 end, List<Vector3> waypoints)
        {
            ArgumentNullException.ThrowIfNull(waypoints);
            waypoints.Clear();
            LastIterations = 0;

            if (!TryResolveEnds(start, end, out PolyEnds ends))
            {
                LastOutcome = PathSearchOutcome.NoEnds;
                return false;
            }
            if (!IsOnMesh(end, ends.EndPt))
            {
                LastOutcome = PathSearchOutcome.EndOffMesh;
                return false;
            }

            // No route exists between separate islands; skip the search that would run to its cap proving it.
            if (_islands.AreDisconnected(ends.StartRef, ends.EndRef))
            {
                LastOutcome = PathSearchOutcome.Disconnected;
                return false;
            }

            Span<long> path = stackalloc long[MaxPathPolys];
            if (!TrySearchCorridor(ends, path, out int pathCount) || pathCount <= 0)
                return false;

            Span<DtStraightPath> straight = stackalloc DtStraightPath[MaxStraightPath];
            DtStatus straightStatus = _query.FindStraightPath(
                ends.StartPt,
                ends.EndPt,
                path,
                pathCount,
                straight,
                out int straightCount,
                straight.Length,
                0);
            if (straightStatus.Failed() || straightCount <= 0)
            {
                LastOutcome = PathSearchOutcome.NoStraightPath;
                return false;
            }

            int first = 0;
            if (straightCount > 1 && IsNear(straight[0].pos, start))
                first = 1;

            for (int i = first; i < straightCount; i++)
                waypoints.Add(straight[i].pos);

            LastOutcome = waypoints.Count > 0 ? PathSearchOutcome.Found : PathSearchOutcome.NoStraightPath;
            return waypoints.Count > 0;
        }

        /// <summary>
        /// Floods outward from <paramref name="target"/> over polygons whose route stays within
        /// <paramref name="radius"/> of it, so walkers can test reachability with <see cref="Reaches"/>. The end is
        /// resolved exactly as <see cref="TryFindPath"/> resolves it. More than <paramref name="maxPolys"/> polygons
        /// gives <see cref="ReachMapKind.TooLarge"/>. Tick-thread state; not thread safe.
        /// </summary>
        public ReachMap BuildReachMap(Vector3 target, float radius, int maxPolys)
        {
            if (!TrySnap(target, out _))
                return new ReachMap(ReachMapKind.TargetNoMesh, null, target, 0);

            DtStatus status = _query.FindNearestPoly(target, _extents, _filter, out long endRef, out RcVec3f endPt, out _);
            if (status.Failed() || endRef == 0)
                return new ReachMap(ReachMapKind.TargetNoMesh, null, target, 0);

            var end = new Vector3(endPt.X, endPt.Y, endPt.Z);
            if (!IsOnMesh(target, endPt))
                return new ReachMap(ReachMapKind.TargetOffMesh, null, end, 0);

            // One slot past the limit tells "exactly full" from "ran out of room".
            int capacity = maxPolys + 1;
            if (_reachRefs.Length < capacity)
            {
                _reachRefs = new long[capacity];
                _reachParents = new long[capacity];
                _reachCosts = new float[capacity];
            }

            DtStatus flood = _query.FindPolysAroundCircle(
                endRef,
                endPt,
                radius,
                _filter,
                _reachRefs.AsSpan(0, capacity),
                _reachParents.AsSpan(0, capacity),
                _reachCosts.AsSpan(0, capacity),
                out int count,
                capacity);
            if (flood.Failed() || count > maxPolys)
                return new ReachMap(ReachMapKind.TooLarge, null, end, count);

            var polys = new HashSet<long>(count);
            for (int i = 0; i < count; i++)
                polys.Add(_reachRefs[i]);
            return new ReachMap(ReachMapKind.Flooded, polys, end, count);
        }

        /// <summary>
        /// Whether a walker at <paramref name="start"/> reaches <paramref name="map"/>'s target. A walker off the mesh
        /// counts as reaching (holes and off-mesh links are not walls), as does any walker when the target is nowhere
        /// near the mesh.
        /// </summary>
        public ReachAnswer Reaches(ReachMap map, Vector3 start)
        {
            ArgumentNullException.ThrowIfNull(map);
            if (!TrySnap(start, out _))
                return ReachAnswer.Reachable;

            switch (map.Kind)
            {
                case ReachMapKind.TargetNoMesh:
                    return ReachAnswer.Reachable;
                case ReachMapKind.TargetOffMesh:
                    return ReachAnswer.Unreachable;
                case ReachMapKind.TooLarge:
                    return ReachAnswer.TooLarge;
            }

            DtStatus status = _query.FindNearestPoly(start, _extents, _filter, out long startRef, out _, out _);
            if (status.Failed() || startRef == 0)
                return ReachAnswer.Reachable;

            return map.Contains(startRef) ? ReachAnswer.Reachable : ReachAnswer.Unreachable;
        }

        public void Dispose()
        {
        }

        bool TrySearchCorridor(in PolyEnds ends, Span<long> path, out int pathCount)
        {
            pathCount = 0;
            DtStatus init = _query.InitSlicedFindPath(
                ends.StartRef,
                ends.EndRef,
                ends.StartPt,
                ends.EndPt,
                _filter,
                0);
            if (init.Failed())
            {
                LastOutcome = PathSearchOutcome.SearchFailed;
                return false;
            }

            DtStatus update = _query.UpdateSlicedFindPath(MaxSearchIters, out int iterations);
            LastIterations = iterations;
            if (update.InProgress())
            {
                LastOutcome = PathSearchOutcome.IterationCap;
                return false;
            }
            if (update.Failed() || update.IsPartial() || !update.Succeeded())
            {
                LastOutcome = update.IsPartial() ? PathSearchOutcome.Partial : PathSearchOutcome.SearchFailed;
                return false;
            }

            DtStatus finalized = _query.FinalizeSlicedFindPath(path, out pathCount, path.Length);
            if (finalized.Failed() || finalized.IsPartial() || pathCount <= 0)
            {
                LastOutcome = finalized.IsPartial() ? PathSearchOutcome.Partial : PathSearchOutcome.SearchFailed;
                return false;
            }

            return true;
        }

        bool TryResolveEnds(Vector3 start, Vector3 end, out PolyEnds ends)
        {
            ends = default;
            RcVec3f startPos = start;
            RcVec3f endPos = end;
            DtStatus startStatus = _query.FindNearestPoly(
                startPos,
                _extents,
                _filter,
                out long startRef,
                out RcVec3f startPt,
                out _);
            if (startStatus.Failed() || startRef == 0)
                return false;

            DtStatus endStatus = _query.FindNearestPoly(
                endPos,
                _extents,
                _filter,
                out long endRef,
                out RcVec3f endPt,
                out _);
            if (endStatus.Failed() || endRef == 0)
                return false;

            ends = new PolyEnds(startRef, endRef, startPt, endPt);
            return true;
        }

        bool IsOnMesh(Vector3 destination, RcVec3f snapped)
        {
            float dx = snapped.X - destination.X;
            float dz = snapped.Z - destination.Z;
            float horiz = MathF.Sqrt((dx * dx) + (dz * dz));
            if (horiz > OnMeshHorizontal)
                return false;

            float vertSlack = Settings.AgentHeight + Settings.AgentMaxClimb;
            return MathF.Abs(snapped.Y - destination.Y) <= vertSlack;
        }

        static bool IsNear(RcVec3f a, RcVec3f b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (dx * dx) + (dy * dy) + (dz * dz) <= StartSkipEpsilon * StartSkipEpsilon;
        }

        readonly struct PolyEnds
        {
            public PolyEnds(long startRef, long endRef, RcVec3f startPt, RcVec3f endPt)
            {
                StartRef = startRef;
                EndRef = endRef;
                StartPt = startPt;
                EndPt = endPt;
            }

            public long StartRef { get; }

            public long EndRef { get; }

            public RcVec3f StartPt { get; }

            public RcVec3f EndPt { get; }
        }
    }

    /// <summary>How a <see cref="NavMeshPathfinder.TryFindPath"/> call ended.</summary>
    public enum PathSearchOutcome
    {
        Found = 0,

        /// <summary>Start or end had no navmesh polygon nearby.</summary>
        NoEnds,

        /// <summary>The end snapped to a polygon too far from the requested point.</summary>
        EndOffMesh,

        /// <summary>The corridor search failed outright.</summary>
        SearchFailed,

        /// <summary>The corridor search ran out of iterations before reaching the end.</summary>
        IterationCap,

        /// <summary>The corridor search only reached part of the way (end unreachable).</summary>
        Partial,

        /// <summary>A corridor was found but no straight path could be built along it.</summary>
        NoStraightPath,

        /// <summary>Start and end are on separate navmesh islands; answered without searching.</summary>
        Disconnected
    }
}
