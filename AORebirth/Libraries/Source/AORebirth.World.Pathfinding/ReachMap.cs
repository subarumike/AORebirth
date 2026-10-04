namespace AORebirth.World.Pathfinding
{
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>What a <see cref="ReachMap"/> knows about its target.</summary>
    public enum ReachMapKind
    {
        /// <summary>Flooded: <see cref="ReachMap.Contains"/> answers for every polygon.</summary>
        Flooded = 0,

        /// <summary>The target is nowhere near the navmesh; reachability cannot be judged, so it is not denied.</summary>
        TargetNoMesh,

        /// <summary>The target snaps to the mesh but is not standing on it (a ledge or rock top): nobody can path there.</summary>
        TargetOffMesh,

        /// <summary>The flood around the target passed its polygon limit before finishing: too costly to answer.</summary>
        TooLarge
    }

    /// <summary>A walker's answer from a <see cref="ReachMap"/>.</summary>
    public enum ReachAnswer
    {
        Reachable = 0,
        Unreachable,

        /// <summary>The map was too large to build; the caller decides (an NPC resets).</summary>
        TooLarge
    }

    /// <summary>
    /// Every navmesh polygon from which a walker can reach one target by a route that stays within a radius of it,
    /// built once by flooding outward from the target. Any number of walkers then answer "can I reach it?" with a
    /// polygon lookup instead of each running its own search. Immutable once built.
    /// </summary>
    public sealed class ReachMap
    {
        readonly HashSet<long>? _polys;

        internal ReachMap(ReachMapKind kind, HashSet<long>? polys, Vector3 endPoint, int polyCount)
        {
            Kind = kind;
            _polys = polys;
            EndPoint = endPoint;
            PolyCount = polyCount;
        }

        public ReachMapKind Kind { get; }

        /// <summary>The target's position snapped onto the navmesh (where a route to it would end).</summary>
        public Vector3 EndPoint { get; }

        /// <summary>Polygons flooded (the work it took).</summary>
        public int PolyCount { get; }

        internal bool Contains(long polyRef) => _polys != null && _polys.Contains(polyRef);
    }
}
