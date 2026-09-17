namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    /// Tessellates a <see cref="TerrainHeightfield"/> into world-space triangles.
    /// Winding is origin → +z → +x so (+Z)×(+X)=+Y. Matches <c>TileCollisionBaker</c>.
    /// </summary>
    public static class TerrainHeightfieldMesher
    {
        public static CollisionTriangleMesh? TryBuild(TerrainHeightfield terrain)
        {
            ArgumentNullException.ThrowIfNull(terrain);
            if (terrain.Chunks.Count == 0)
                return null;

            float tileSize = terrain.TileSize > 0 ? terrain.TileSize : 1f;
            float heightScale = terrain.HeightScale > 0 ? terrain.HeightScale : 1f;

            var vertices = new List<Vector3>();
            var triangles = new List<CollisionTriangle>();
            for (int i = 0; i < terrain.Chunks.Count; i++)
                AppendChunk(terrain.Chunks[i], tileSize, heightScale, vertices, triangles);

            if (triangles.Count == 0)
                return null;

            return new CollisionTriangleMesh(vertices.ToArray(), triangles.ToArray(), cellId: null, source: "terrain");
        }

        static void AppendChunk(
            TerrainHeightChunk chunk,
            float tileSize,
            float heightScale,
            List<Vector3> vertices,
            List<CollisionTriangle> triangles)
        {
            float[,] heights = chunk.Heights;
            int sizeX = heights.GetLength(0);
            int sizeZ = heights.GetLength(1);
            if (sizeX < 2 || sizeZ < 2)
                return;

            int vertexBase = vertices.Count;
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    vertices.Add(new Vector3(
                        chunk.OriginX + (x * tileSize),
                        heights[x, z] * heightScale,
                        chunk.OriginZ + (z * tileSize)));
                }
            }

            for (int z = 0; z < sizeZ - 1; z++)
            {
                for (int x = 0; x < sizeX - 1; x++)
                {
                    int i00 = vertexBase + (z * sizeX) + x;
                    int i10 = i00 + 1;
                    int i01 = i00 + sizeX;
                    int i11 = i01 + 1;
                    triangles.Add(new CollisionTriangle(i00, i01, i10));
                    triangles.Add(new CollisionTriangle(i10, i01, i11));
                }
            }
        }
    }
}
