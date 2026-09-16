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
    }
}
