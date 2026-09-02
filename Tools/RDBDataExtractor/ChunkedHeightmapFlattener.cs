namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections;
    using System.Reflection;
    using AODB.Common.RDBObjects;

    internal static class ChunkedHeightmapFlattener
    {
        internal sealed class ChunkedGroundData
        {
            internal int Width { get; set; }

            internal int Height { get; set; }

            internal float TileSize { get; set; }

            internal float HeightScale { get; set; }

            internal int ChunkSize { get; set; }

            internal int GridWidth { get; set; }

            internal int BitsPerSample { get; set; }

            internal short[] TextureIds { get; set; }

            internal ushort[] Heights { get; set; }
        }

        internal static bool TryParseChunkedGround(byte[] rawRecord, out ChunkedGroundData data)
        {
            data = null;
            byte[] payload;
            if (!TilemapPayloadLocator.TryGetChgaPayload(rawRecord, out payload))
            {
                return false;
            }

            return TryParseChunkedPayload(payload, out data);
        }

        internal static bool TryParseChunkedPayload(byte[] payload, out ChunkedGroundData data)
        {
            data = null;
            if (payload == null || payload.Length < 4)
            {
                return false;
            }

            Type chunkedGroundType = typeof(Tilemap).GetNestedType(
                "ChunkedGround",
                BindingFlags.Public | BindingFlags.NonPublic);
            if (chunkedGroundType == null)
            {
                return false;
            }

            MethodInfo tryParse = chunkedGroundType.GetMethod(
                "TryParse",
                BindingFlags.Public | BindingFlags.Static);
            if (tryParse == null)
            {
                return false;
            }

            object parsed = tryParse.Invoke(null, new object[] { payload });
            if (parsed == null)
            {
                return false;
            }

            int width = (int)chunkedGroundType.GetProperty("Width").GetValue(parsed);
            int height = (int)chunkedGroundType.GetProperty("Height").GetValue(parsed);
            float tileSize = (float)chunkedGroundType.GetProperty("TileSize").GetValue(parsed);
            float heightScale = (float)chunkedGroundType.GetProperty("HeightScale").GetValue(parsed);
            int chunkSize = (int)chunkedGroundType.GetProperty("ChunkSize").GetValue(parsed);
            int gridWidth = (int)chunkedGroundType.GetProperty("GridWidth").GetValue(parsed);
            int bitsPerSample = (int)chunkedGroundType.GetProperty("BitsPerSample").GetValue(parsed);
            short[] textureIds = (short[])chunkedGroundType.GetProperty("TextureIds").GetValue(parsed);
            IEnumerable chunks = (IEnumerable)chunkedGroundType.GetProperty("Chunks").GetValue(parsed);

            Type groundChunkType = chunkedGroundType.GetNestedType(
                "GroundChunk",
                BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo gridXField = groundChunkType.GetField("GridX");
            FieldInfo gridZField = groundChunkType.GetField("GridZ");
            FieldInfo sizeField = groundChunkType.GetField("Size");
            FieldInfo heightsField = groundChunkType.GetField("Heights");

            int rasterWidth = width + 1;
            int rasterHeight = height + 1;
            ushort[] raster = new ushort[rasterWidth * rasterHeight];

            foreach (object chunk in chunks)
            {
                int gridX = (int)gridXField.GetValue(chunk);
                int gridZ = (int)gridZField.GetValue(chunk);
                int size = (int)sizeField.GetValue(chunk);
                ushort[,] heights = (ushort[,])heightsField.GetValue(chunk);
                int chunkWidth = heights.GetLength(0);
                int chunkHeight = heights.GetLength(1);
                int originX = gridX * chunkSize;
                int originZ = gridZ * chunkSize;

                for (int z = 0; z < chunkHeight; z++)
                {
                    for (int x = 0; x < chunkWidth; x++)
                    {
                        int mapX = originX + x;
                        int mapZ = originZ + z;
                        if (mapX < 0
                            || mapZ < 0
                            || mapX >= rasterWidth
                            || mapZ >= rasterHeight)
                        {
                            continue;
                        }

                        raster[(mapZ * rasterWidth) + mapX] = heights[x, z];
                    }
                }
            }

            data = new ChunkedGroundData
            {
                Width = rasterWidth,
                Height = rasterHeight,
                TileSize = tileSize,
                HeightScale = heightScale,
                ChunkSize = chunkSize,
                GridWidth = gridWidth,
                BitsPerSample = bitsPerSample,
                TextureIds = textureIds,
                Heights = raster,
            };
            return true;
        }
    }
}
