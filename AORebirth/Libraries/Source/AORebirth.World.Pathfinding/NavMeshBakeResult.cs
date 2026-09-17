namespace AORebirth.World.Pathfinding
{
    using System;

    using DotRecast.Detour;

    public sealed class NavMeshBakeResult
    {
        public NavMeshBakeResult(
            int playfieldId,
            int sourceTriangles,
            int tileCount,
            DtNavMesh mesh,
            NavMeshBuildSettings settings)
        {
            ArgumentNullException.ThrowIfNull(mesh);
            ArgumentNullException.ThrowIfNull(settings);
            PlayfieldId = playfieldId;
            SourceTriangles = sourceTriangles;
            TileCount = tileCount;
            Mesh = mesh;
            Settings = settings;
        }

        public int PlayfieldId { get; }

        public int SourceTriangles { get; }

        public int TileCount { get; }

        public DtNavMesh Mesh { get; }

        public NavMeshBuildSettings Settings { get; }
    }
}
