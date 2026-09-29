namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    /// <summary>
    /// Engine-agnostic collision geometry for one playfield: surface meshes + optional terrain, plus the teleportals
    /// the zone surfaces carry.
    /// </summary>
    public sealed class PlayfieldCollisionSet
    {
        public PlayfieldCollisionSet(
            int playfieldId,
            IReadOnlyList<CollisionTriangleMesh> surfaceMeshes,
            TerrainHeightfield? terrain,
            IReadOnlyList<SurfaceTeleportal>? teleportals = null)
        {
            if (playfieldId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playfieldId));

            PlayfieldId = playfieldId;
            SurfaceMeshes = surfaceMeshes ?? throw new ArgumentNullException(nameof(surfaceMeshes));
            Terrain = terrain;
            Teleportals = teleportals ?? Array.Empty<SurfaceTeleportal>();
        }

        public int PlayfieldId { get; }

        public IReadOnlyList<CollisionTriangleMesh> SurfaceMeshes { get; }

        public TerrainHeightfield? Terrain { get; }

        /// <summary>
        /// Zone surface teleportals (walk into the X/Z area, land on the destination playfield's destination line),
        /// one per distinct area: every locality cell a teleportal overlaps carries its own copy.
        /// </summary>
        public IReadOnlyList<SurfaceTeleportal> Teleportals { get; }

        public bool HasCollision => Terrain != null || SurfaceMeshes.Count > 0;
    }
}
