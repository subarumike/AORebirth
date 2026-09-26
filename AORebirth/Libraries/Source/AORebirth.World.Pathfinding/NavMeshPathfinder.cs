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

        NavMeshPathfinder(int playfieldId, DtNavMesh mesh, NavMeshBuildSettings settings)
        {
            PlayfieldId = playfieldId;
            Settings = settings;
            _query = new DtNavMeshQuery(mesh);
            _filter = new DtQueryDefaultFilter();
            float pathHoriz = MathF.Max(settings.AgentRadius, PathQueryHorizontal);
            float pathVert = settings.AgentHeight + settings.AgentMaxClimb + PathQueryHorizontal;
            _extents = new RcVec3f(pathHoriz, pathVert, pathHoriz);
        }

        public int PlayfieldId { get; }

        public NavMeshBuildSettings Settings { get; }

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

            if (!TryResolveEnds(start, end, out PolyEnds ends))
                return false;
            if (!IsOnMesh(end, ends.EndPt))
                return false;

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
                return false;

            int first = 0;
            if (straightCount > 1 && IsNear(straight[0].pos, start))
                first = 1;

            for (int i = first; i < straightCount; i++)
                waypoints.Add(straight[i].pos);

            return waypoints.Count > 0;
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
                return false;

            DtStatus update = _query.UpdateSlicedFindPath(MaxSearchIters, out _);
            if (update.Failed() || update.InProgress() || update.IsPartial() || !update.Succeeded())
                return false;

            DtStatus finalized = _query.FinalizeSlicedFindPath(path, out pathCount, path.Length);
            return !finalized.Failed() && !finalized.IsPartial() && pathCount > 0;
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
}
