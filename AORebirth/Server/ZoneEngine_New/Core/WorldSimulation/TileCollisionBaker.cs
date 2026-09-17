namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Globalization;

    using AORebirth.World.Collision;

    using BepuPhysics;
    using BepuPhysics.Collidables;
    using BepuUtilities.Memory;

    using System.Numerics;

    /// <summary>Counters describing what a tilemap bake produced, for startup logging.</summary>
    public readonly struct TileBakeReport
    {
        public TileBakeReport(
            int chunksExpected,
            int chunksBaked,
            int triangles,
            float minHeight,
            float maxHeight,
            float coverageX,
            float coverageZ)
        {
            ChunksExpected = chunksExpected;
            ChunksBaked = chunksBaked;
            Triangles = triangles;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            CoverageX = coverageX;
            CoverageZ = coverageZ;
        }

        public int ChunksExpected { get; }

        public int ChunksBaked { get; }

        public int Triangles { get; }

        public float MinHeight { get; }

        public float MaxHeight { get; }

        public float CoverageX { get; }

        public float CoverageZ { get; }

        public bool Complete => ChunksExpected > 0 && ChunksBaked == ChunksExpected;

        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture,
            "chunks={0}/{1} triangles={2} heightRange=[{3:F2},{4:F2}] coverage={5:F0}x{6:F0}",
            ChunksBaked,
            ChunksExpected,
            Triangles,
            MinHeight,
            MaxHeight,
            CoverageX,
            CoverageZ);
    }

    /// <summary>
    /// Builds Bepu static meshes from normalized terrain heightfields.
    /// <para>
    /// Chunk origins and sample indexing are established by
    /// <see cref="AORebirth.World.Collision.PlayfieldCollisionLoader"/> conventions:
    /// chunk <c>i</c> at grid (<c>i % gridWidth</c>, <c>i / gridWidth</c>), samples <c>[x, z]</c>.
    /// </para>
    /// <para>
    /// Bepu triangles are one-sided: upward-facing terrain winding is origin → +z → +x.
    /// </para>
    /// </summary>
    public static class TileCollisionBaker
    {
        public static int BakeAll(
            TerrainHeightfield? terrain,
            BufferPool pool,
            Simulation simulation,
            out TileBakeReport report)
        {
            report = default;
            if (terrain == null || terrain.Chunks.Count == 0)
                return 0;

            float tileSize = terrain.TileSize > 0 ? terrain.TileSize : 1f;
            float heightScale = terrain.HeightScale > 0 ? terrain.HeightScale : 1f;
            float chunkSpan = terrain.ChunkSize > 1
                ? (terrain.ChunkSize - 1) * tileSize
                : 0f;
            int gridWidth = terrain.GridWidth > 0 ? terrain.GridWidth : 1;
            int gridHeight = (int)MathF.Ceiling(terrain.Chunks.Count / (float)gridWidth);

            int added = 0;
            int triangles = 0;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            for (int i = 0; i < terrain.Chunks.Count; i++)
            {
                TerrainHeightChunk chunk = terrain.Chunks[i];
                int chunkTriangles = BakeSingleGrid(
                    chunk.Heights,
                    chunk.OriginX,
                    chunk.OriginZ,
                    tileSize,
                    heightScale,
                    pool,
                    simulation,
                    out TileBakeReport chunkReport);
                if (chunkTriangles == 0)
                    continue;

                added++;
                triangles += chunkReport.Triangles;
                minHeight = MathF.Min(minHeight, chunkReport.MinHeight);
                maxHeight = MathF.Max(maxHeight, chunkReport.MaxHeight);
            }

            report = new TileBakeReport(
                terrain.Chunks.Count,
                added,
                triangles,
                added > 0 ? minHeight : 0f,
                added > 0 ? maxHeight : 0f,
                gridWidth * chunkSpan,
                gridHeight * chunkSpan);
            return added;
        }

        /// <summary>Bakes one height grid as a single Bepu static mesh. Returns 1 when added.</summary>
        static int BakeSingleGrid(
            float[,] heights,
            float originX,
            float originZ,
            float tileSize,
            float heightScale,
            BufferPool pool,
            Simulation simulation,
            out TileBakeReport report)
        {
            report = default;
            int sizeX = heights.GetLength(0);
            int sizeZ = heights.GetLength(1);
            if (sizeX < 2 || sizeZ < 2)
                return 0;

            int quadCount = (sizeX - 1) * (sizeZ - 1);
            pool.Take<Triangle>(quadCount * 2, out Buffer<Triangle> triangles);
            int t = 0;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            for (int z = 0; z < sizeZ - 1; z++)
            {
                for (int x = 0; x < sizeX - 1; x++)
                {
                    float y00 = heights[x, z] * heightScale;
                    float y10 = heights[x + 1, z] * heightScale;
                    float y01 = heights[x, z + 1] * heightScale;
                    float y11 = heights[x + 1, z + 1] * heightScale;

                    float x0 = originX + (x * tileSize);
                    float x1 = originX + ((x + 1) * tileSize);
                    float z0 = originZ + (z * tileSize);
                    float z1 = originZ + ((z + 1) * tileSize);

                    Vector3 v00 = new(x0, y00, z0);
                    Vector3 v10 = new(x1, y10, z0);
                    Vector3 v01 = new(x0, y01, z1);
                    Vector3 v11 = new(x1, y11, z1);

                    // Upward-facing winding: origin → +z → +x so (+Z)×(+X)=+Y.
                    triangles[t++] = new Triangle(v00, v01, v10);
                    triangles[t++] = new Triangle(v10, v01, v11);

                    minHeight = MathF.Min(minHeight, MathF.Min(MathF.Min(y00, y10), MathF.Min(y01, y11)));
                    maxHeight = MathF.Max(maxHeight, MathF.Max(MathF.Max(y00, y10), MathF.Max(y01, y11)));
                }
            }

            var mesh = new Mesh(triangles, Vector3.One, pool);
            simulation.Statics.Add(new StaticDescription(RigidPose.Identity, simulation.Shapes.Add(mesh)));
            report = new TileBakeReport(
                1,
                1,
                t,
                minHeight,
                maxHeight,
                (sizeX - 1) * tileSize,
                (sizeZ - 1) * tileSize);
            return 1;
        }
    }
}
