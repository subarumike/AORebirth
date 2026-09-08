namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Globalization;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

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
    /// Builds Bepu static meshes from Tilemap CHGA heightmap data.
    /// <para>
    /// Two conventions are load bearing here and both were validated against live client poses
    /// (see tools-temp/WorldSimSmoke): chunk <c>i</c> of <c>Heightmap</c> sits at grid
    /// (<c>i % gridWidth</c>, <c>i / gridWidth</c>), and samples are indexed <c>[x, z]</c>.
    /// </para>
    /// <para>
    /// Bepu triangles are one-sided: a ray or sweep only hits the face whose winding appears
    /// clockwise from the ray's side. For an upward-facing terrain face the winding must be
    /// origin → +x → +z, matching Bepu's own deformed-plane heightmap sample.
    /// </para>
    /// </summary>
    public static class TileCollisionBaker
    {
        public static int BakeAll(
            Tilemap? tilemap,
            PlayfieldMetaData? meta,
            BufferPool pool,
            Simulation simulation,
            out TileBakeReport report)
        {
            report = default;
            if (tilemap == null)
                return 0;

            float tileSize = meta?.TileSize > 0
                ? meta.TileSize
                : GetFloatField(tilemap, "MapScale", 1f);
            float heightScale = meta?.HeightScale > 0
                ? meta.HeightScale
                : GetFloatField(tilemap, "HeightMod", 1f);
            if (tileSize <= 0)
                tileSize = 1f;
            if (heightScale <= 0)
                heightScale = 1f;

            int chunkSize = GetIntField(tilemap, "ChunkSize", 0);
            int gridWidth = GetIntField(tilemap, "GridWidth", 0);

            // AODB CHGA: Heightmap is List<ushort[,]> of chunkSize×chunkSize grids.
            if (GetField(tilemap, "Heightmap") is IList heightList && heightList.Count > 0)
            {
                if (heightList[0] is ushort[,] first)
                {
                    if (chunkSize <= 0)
                        chunkSize = first.GetLength(0);
                    if (gridWidth <= 0)
                        gridWidth = (int)MathF.Ceiling(MathF.Sqrt(heightList.Count));

                    return BakeHeightmapList(
                        heightList,
                        chunkSize,
                        gridWidth,
                        tileSize,
                        heightScale,
                        pool,
                        simulation,
                        out report);
                }

                if (heightList[0] is float[,] floats)
                {
                    return BakeSingleGrid(floats, 0f, 0f, tileSize, heightScale, pool, simulation, out report);
                }
            }

            Array? heightmap = GetProp(tilemap, "Heightmap") as Array;
            if (heightmap is float[,] heights2d)
                return BakeSingleGrid(heights2d, 0f, 0f, tileSize, heightScale, pool, simulation, out report);

            if (heightmap is ushort[,] uheights)
                return BakeSingleGrid(ToFloatGrid(uheights), 0f, 0f, tileSize, heightScale, pool, simulation, out report);

            return 0;
        }

        static int BakeHeightmapList(
            IList chunks,
            int chunkSize,
            int gridWidth,
            float tileSize,
            float heightScale,
            BufferPool pool,
            Simulation simulation,
            out TileBakeReport report)
        {
            report = default;
            if (chunkSize < 2 || gridWidth <= 0)
                return 0;

            // Adjacent chunks share their border sample row, so a chunk spans chunkSize-1 tiles.
            float chunkSpan = (chunkSize - 1) * tileSize;
            int gridHeight = (int)MathF.Ceiling(chunks.Count / (float)gridWidth);

            int added = 0;
            int triangles = 0;
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] is not ushort[,] heights)
                    continue;

                int gridX = i % gridWidth;
                int gridZ = i / gridWidth;
                int chunkTriangles = BakeSingleGrid(
                    ToFloatGrid(heights),
                    gridX * chunkSpan,
                    gridZ * chunkSpan,
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
                chunks.Count,
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

                    // Upward-facing winding: origin → +x → +z.
                    triangles[t++] = new Triangle(v00, v10, v01);
                    triangles[t++] = new Triangle(v10, v11, v01);

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

        static float[,] ToFloatGrid(ushort[,] heights)
        {
            int sizeX = heights.GetLength(0);
            int sizeZ = heights.GetLength(1);
            float[,] floats = new float[sizeX, sizeZ];
            for (int z = 0; z < sizeZ; z++)
            {
                for (int x = 0; x < sizeX; x++)
                    floats[x, z] = heights[x, z];
            }

            return floats;
        }

        static object? GetProp(object obj, string name) =>
            obj.GetType().GetProperty(name)?.GetValue(obj);

        static object? GetField(object obj, string name) =>
            obj.GetType().GetField(name)?.GetValue(obj);

        static float GetFloatField(object obj, string name, float fallback)
        {
            object? v = GetField(obj, name) ?? GetProp(obj, name);
            return v is float f ? f : fallback;
        }

        static int GetIntField(object obj, string name, int fallback)
        {
            object? v = GetField(obj, name) ?? GetProp(obj, name);
            return v is int i ? i : fallback;
        }
    }
}
