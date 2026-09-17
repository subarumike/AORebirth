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
        const float StartSkipEpsilon = 0.05f;
        const float PathQueryHorizontal = 8f;
        const float OnMeshHorizontal = 2f;

        /// <summary>Search radius used when placing an NPC onto the mesh at spawn.</summary>
        public const float SpawnSnapExtent = 64f;

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
            pathfinder = null;
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
                settings = NavMeshBuildSettings.Load(NavMeshBuildSettings.DefaultPathBesideGameData(gameDataRoot));
            }
            catch
            {
                return false;
            }

            DtNavMesh mesh;
            try
            {
                mesh = NavMeshFile.Read(meshPath, out _);
            }
            catch
            {
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

        public bool TryCanReach(Vector3 start, Vector3 end)
        {
            if (!TryResolveEnds(start, end, out PolyEnds ends))
                return false;
            if (!IsOnMesh(end, ends.EndPt))
                return false;

            Span<long> path = stackalloc long[MaxPathPolys];
            DtStatus pathStatus = _query.FindPath(
                ends.StartRef,
                ends.EndRef,
                ends.StartPt,
                ends.EndPt,
                _filter,
                path,
                out int pathCount,
                path.Length);
            if (pathStatus.Failed() || pathStatus.IsPartial() || pathCount <= 0)
                return false;

            return path[pathCount - 1] == ends.EndRef;
        }

        public bool TryFindPath(Vector3 start, Vector3 end, List<Vector3> waypoints)
        {
            ArgumentNullException.ThrowIfNull(waypoints);
            waypoints.Clear();

            if (!TryResolveEnds(start, end, out PolyEnds ends))
                return false;

            Span<long> path = stackalloc long[MaxPathPolys];
            DtStatus pathStatus = _query.FindPath(
                ends.StartRef,
                ends.EndRef,
                ends.StartPt,
                ends.EndPt,
                _filter,
                path,
                out int pathCount,
                path.Length);
            if (pathStatus.Failed() || pathCount <= 0)
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
