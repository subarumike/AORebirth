namespace AORebirth.World.Collision
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    /// <summary>Converts AODB <see cref="Tilemap"/> height data into <see cref="TerrainHeightfield"/>.</summary>
    internal static class TilemapNormalizer
    {
        public static TerrainHeightfield? Normalize(Tilemap? tilemap, PlayfieldMetaData? meta)
        {
            if (tilemap == null)
                return null;

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

            if (GetField(tilemap, "Heightmap") is IList heightList && heightList.Count > 0)
            {
                if (heightList[0] is ushort[,] first)
                {
                    if (chunkSize <= 0)
                        chunkSize = first.GetLength(0);
                    if (gridWidth <= 0)
                        gridWidth = (int)MathF.Ceiling(MathF.Sqrt(heightList.Count));

                    return FromChunkList(heightList, chunkSize, gridWidth, tileSize, heightScale);
                }

                if (heightList[0] is float[,] floats)
                {
                    return FromSingleGrid(floats, tileSize, heightScale);
                }
            }

            Array? heightmap = GetProp(tilemap, "Heightmap") as Array;
            if (heightmap is float[,] heights2d)
                return FromSingleGrid(heights2d, tileSize, heightScale);

            if (heightmap is ushort[,] uheights)
                return FromSingleGrid(ToFloatGrid(uheights), tileSize, heightScale);

            return null;
        }

        static TerrainHeightfield? FromChunkList(
            IList chunks,
            int chunkSize,
            int gridWidth,
            float tileSize,
            float heightScale)
        {
            if (chunkSize < 2 || gridWidth <= 0)
                return null;

            float chunkSpan = (chunkSize - 1) * tileSize;
            var result = new List<TerrainHeightChunk>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] is not ushort[,] heights)
                    continue;

                int gridX = i % gridWidth;
                int gridZ = i / gridWidth;
                result.Add(new TerrainHeightChunk(
                    ToFloatGrid(heights),
                    gridX * chunkSpan,
                    gridZ * chunkSpan));
            }

            if (result.Count == 0)
                return null;

            return new TerrainHeightfield(tileSize, heightScale, chunkSize, gridWidth, result);
        }

        static TerrainHeightfield FromSingleGrid(float[,] heights, float tileSize, float heightScale)
        {
            int sizeX = heights.GetLength(0);
            return new TerrainHeightfield(
                tileSize,
                heightScale,
                chunkSize: sizeX,
                gridWidth: 1,
                chunks: new[] { new TerrainHeightChunk(heights, 0f, 0f) });
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
