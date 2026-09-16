namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Engine-agnostic collision geometry for one playfield: surface meshes + optional terrain.
    /// </summary>
    public sealed class PlayfieldCollisionSet
    {
        public PlayfieldCollisionSet(
            int playfieldId,
            IReadOnlyList<CollisionTriangleMesh> surfaceMeshes,
            TerrainHeightfield? terrain)
        {
            if (playfieldId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playfieldId));

            PlayfieldId = playfieldId;
            SurfaceMeshes = surfaceMeshes ?? throw new ArgumentNullException(nameof(surfaceMeshes));
            Terrain = terrain;
        }

        public int PlayfieldId { get; }

        public IReadOnlyList<CollisionTriangleMesh> SurfaceMeshes { get; }

        public TerrainHeightfield? Terrain { get; }

        public bool HasCollision => Terrain != null || SurfaceMeshes.Count > 0;
    }
}
