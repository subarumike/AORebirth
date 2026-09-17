namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Normalized Tilemap CHGA / single-grid terrain for collision and nav consumers.
    /// Chunk layout matches ZoneEngine bake rules: chunk <c>i</c> at grid
    /// (<c>i % gridWidth</c>, <c>i / gridWidth</c>), samples <c>[x, z]</c>.
    /// </summary>
    public sealed class TerrainHeightfield
    {
        public TerrainHeightfield(
            float tileSize,
            float heightScale,
            int chunkSize,
            int gridWidth,
            IReadOnlyList<TerrainHeightChunk> chunks)
        {
            TileSize = tileSize;
            HeightScale = heightScale;
            ChunkSize = chunkSize;
            GridWidth = gridWidth;
            Chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
        }

        public float TileSize { get; }

        public float HeightScale { get; }

        public int ChunkSize { get; }

        public int GridWidth { get; }

        public IReadOnlyList<TerrainHeightChunk> Chunks { get; }

        /// <summary>
        /// Bilinear sample of the same heightfield baked into Bepu. A downward
        /// ray that starts under that one-sided mesh cannot hit it; this is the
        /// height to stand above before casting.
        /// </summary>
        public bool TryGetHeight(float x, float z, out float y)
        {
            y = 0f;
            float tileSize = TileSize > 0f ? TileSize : 1f;
            float scale = HeightScale > 0f ? HeightScale : 1f;
            for (int i = 0; i < Chunks.Count; i++)
            {
                TerrainHeightChunk chunk = Chunks[i];
                float[,] heights = chunk.Heights;
                int sizeX = heights.GetLength(0);
                int sizeZ = heights.GetLength(1);
                float localX = (x - chunk.OriginX) / tileSize;
                float localZ = (z - chunk.OriginZ) / tileSize;
                if (localX < 0f || localZ < 0f || localX >= sizeX - 1 || localZ >= sizeZ - 1)
                    continue;

                int ix = (int)localX;
                int iz = (int)localZ;
                float fx = localX - ix;
                float fz = localZ - iz;
                float h00 = heights[ix, iz];
                float h10 = heights[ix + 1, iz];
                float h01 = heights[ix, iz + 1];
                float h11 = heights[ix + 1, iz + 1];
                float h0 = h00 + ((h10 - h00) * fx);
                float h1 = h01 + ((h11 - h01) * fx);
                y = (h0 + ((h1 - h0) * fz)) * scale;
                return true;
            }

            return false;
        }
    }
}
