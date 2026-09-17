namespace AORebirth.World.Collision
{
    using System;
    using System.Numerics;

    /// <summary>
    /// World-space triangle mesh from a SurfaceResource (whole-playfield or one locality cell).
    /// </summary>
    public sealed class CollisionTriangleMesh
    {
        public CollisionTriangleMesh(
            Vector3[] vertices,
            CollisionTriangle[] triangles,
            int? cellId = null,
            string? source = null)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            Triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));
            CellId = cellId;
            Source = source;
        }

        /// <summary>Null for the Collision.dat whole-playfield surface; set for Surfaces.dat cells.</summary>
        public int? CellId { get; }

        /// <summary>Optional dump label (dungeon ground vs room surface).</summary>
        public string? Source { get; }

        public Vector3[] Vertices { get; }

        public CollisionTriangle[] Triangles { get; }
    }
}
