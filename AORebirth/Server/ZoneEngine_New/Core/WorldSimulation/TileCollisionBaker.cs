namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    using BepuPhysics;
    using BepuPhysics.Collidables;
    using BepuUtilities.Memory;

    using System.Numerics;

    /// <summary>Builds Bepu static meshes from Tilemap CHGA / heightmap data.</summary>
    public static class TileCollisionBaker
    {
        public static int BakeAll(
            Tilemap? tilemap,
            PlayfieldMetaData? meta,
            BufferPool pool,
            Simulation simulation)
        {
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
                        simulation);
                }

                if (heightList[0] is float[,] floats)
                {
                    return BakeHeightGrid(floats, tileSize, heightScale, pool, simulation);
                }
            }

            Array? heightmap = GetProp(tilemap, "Heightmap") as Array;
            if (heightmap is float[,] heights2d)
                return BakeHeightGrid(heights2d, tileSize, heightScale, pool, simulation);

            if (heightmap is ushort[,] uheights)
                return BakeUshortGrid(uheights, tileSize, heightScale, pool, simulation);

            return 0;
        }

        static int BakeHeightmapList(
            IList chunks,
            int chunkSize,
            int gridWidth,
            float tileSize,
            float heightScale,
            BufferPool pool,
            Simulation simulation)
        {
            if (chunkSize < 2 || gridWidth <= 0)
                return 0;

            int added = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] is not ushort[,] heights)
                    continue;

                int size = heights.GetLength(0);
                if (size < 2)
                    continue;

                int gridX = i % gridWidth;
                int gridZ = i / gridWidth;
                int triCount = (size - 1) * (size - 1) * 4;
                pool.Take<Triangle>(triCount, out Buffer<Triangle> triangles);
                int t = 0;
                float originX = gridX * (chunkSize - 1) * tileSize;
                float originZ = gridZ * (chunkSize - 1) * tileSize;
                for (int z = 0; z < size - 1; z++)
                {
                    for (int x = 0; x < size - 1; x++)
                    {
                        Vector3 v00 = new(
                            originX + x * tileSize,
                            heights[x, z] * heightScale,
                            originZ + z * tileSize);
                        Vector3 v10 = new(
                            originX + (x + 1) * tileSize,
                            heights[x + 1, z] * heightScale,
                            originZ + z * tileSize);
                        Vector3 v01 = new(
                            originX + x * tileSize,
                            heights[x, z + 1] * heightScale,
                            originZ + (z + 1) * tileSize);
                        Vector3 v11 = new(
                            originX + (x + 1) * tileSize,
                            heights[x + 1, z + 1] * heightScale,
                            originZ + (z + 1) * tileSize);
                        // Bepu Mesh tests are one-sided; emit both windings.
                        triangles[t++] = new Triangle(v00, v10, v01);
                        triangles[t++] = new Triangle(v10, v11, v01);
                        triangles[t++] = new Triangle(v00, v01, v10);
                        triangles[t++] = new Triangle(v10, v01, v11);
                    }
                }

                var mesh = new Mesh(triangles, Vector3.One, pool);
                simulation.Statics.Add(
                    new StaticDescription(RigidPose.Identity, simulation.Shapes.Add(mesh)));
                added++;
            }

            return added;
        }

        static int BakeHeightGrid(
            float[,] heights,
            float tileSize,
            float heightScale,
            BufferPool pool,
            Simulation simulation)
        {
            int w = heights.GetLength(0);
            int h = heights.GetLength(1);
            if (w < 2 || h < 2)
                return 0;

            int triCount = (w - 1) * (h - 1) * 4;
            pool.Take<Triangle>(triCount, out Buffer<Triangle> triangles);
            int t = 0;
            for (int z = 0; z < h - 1; z++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    Vector3 v00 = new(x * tileSize, heights[x, z] * heightScale, z * tileSize);
                    Vector3 v10 = new((x + 1) * tileSize, heights[x + 1, z] * heightScale, z * tileSize);
                    Vector3 v01 = new(x * tileSize, heights[x, z + 1] * heightScale, (z + 1) * tileSize);
                    Vector3 v11 = new((x + 1) * tileSize, heights[x + 1, z + 1] * heightScale, (z + 1) * tileSize);
                    triangles[t++] = new Triangle(v00, v10, v01);
                    triangles[t++] = new Triangle(v10, v11, v01);
                    triangles[t++] = new Triangle(v00, v01, v10);
                    triangles[t++] = new Triangle(v10, v01, v11);
                }
            }

            var mesh = new Mesh(triangles, Vector3.One, pool);
            simulation.Statics.Add(new StaticDescription(RigidPose.Identity, simulation.Shapes.Add(mesh)));
            return 1;
        }

        static int BakeUshortGrid(
            ushort[,] heights,
            float tileSize,
            float heightScale,
            BufferPool pool,
            Simulation simulation)
        {
            int w = heights.GetLength(0);
            int h = heights.GetLength(1);
            float[,] floats = new float[w, h];
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                    floats[x, z] = heights[x, z];
            }

            return BakeHeightGrid(floats, tileSize, heightScale, pool, simulation);
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
