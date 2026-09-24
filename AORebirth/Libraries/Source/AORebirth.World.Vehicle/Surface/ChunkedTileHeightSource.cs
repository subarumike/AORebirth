using System;

namespace LostEden.Vehicles.Surfaces
{
    /// <summary>
    /// The outdoor heightmap lookup, ported from <c>FUN_10017bb9</c> / <c>FUN_10017b46</c> /
    /// <c>FUN_10017c3e</c> (<c>Docs/Movement.md</c> §5.2a).
    ///
    /// <para>
    /// Takes AODB's parsed chunk arrays directly, so it stays free of both Unity and AODB types.
    /// AODB's <c>Tilemap.Heightmap</c> is a list of <c>ushort[side, side]</c> indexed
    /// <c>[x, z]</c>, which is the same order stock stores a patch in
    /// (<c>flat[side * z + x]</c>), so the two line up without a transpose.
    /// </para>
    ///
    /// <para>
    /// <b>8-bit maps need no special case.</b> Stock's decompressor expands each byte to a
    /// <c>u16</c> as <c>byte * 0x100</c> (<c>10035db8</c>) and the sampler shifts it back down by 8
    /// (<c>10017bb9</c>); the two cancel, so the height is <c>rawByte * HeightMod</c> either way.
    /// AODB already stores the recovered sample, so this class just multiplies.
    /// </para>
    /// </summary>
    public sealed class ChunkedTileHeightSource : ITileHeightSource
    {
        readonly ushort[][,] _chunks;
        readonly int _gridWidth;
        readonly int _shift;
        readonly int _mask;
        readonly int _side;
        readonly float _heightMod;

        public int Width { get; }
        public int Height { get; }
        public float TileSize { get; }

        /// <param name="chunks">AODB <c>Tilemap.Heightmap</c>, row-major with <paramref name="gridWidth"/> as the stride.</param>
        /// <param name="gridWidth">AODB <c>Tilemap.GridWidth</c> — chunks per row.</param>
        /// <param name="chunkSide">AODB <c>Tilemap.ChunkSize</c>. This is the <b>sample</b> side, one more than the tile step, because chunks share their edge samples.</param>
        /// <param name="heightMod">AODB <c>Tilemap.HeightMod</c>, resource <c>+0x1c</c>.</param>
        /// <param name="width">AODB <c>Tilemap.MapWidth</c>.</param>
        /// <param name="height">AODB <c>Tilemap.MapHeight</c>.</param>
        /// <param name="tileSize">AODB <c>Tilemap.MapScale</c>.</param>
        public ChunkedTileHeightSource(
            ushort[][,] chunks,
            int gridWidth,
            int chunkSide,
            float heightMod,
            int width,
            int height,
            float tileSize)
        {
            if (chunks == null)
                throw new ArgumentNullException(nameof(chunks));
            if (gridWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(gridWidth));
            if (chunkSide <= 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSide));

            int step = chunkSide - 1;
            if ((step & (step - 1)) != 0)
                throw new ArgumentException(
                    $"chunk side {chunkSide} implies a step of {step}, which is not a power of two; " +
                    "the client's chunk lookup shifts and masks, so it cannot address this.",
                    nameof(chunkSide));

            _chunks = chunks;
            _gridWidth = gridWidth;
            _side = chunkSide;
            _mask = step - 1;
            _heightMod = heightMod;

            _shift = 0;
            while ((1 << _shift) < step)
                _shift++;

            Width = width;
            Height = height;
            TileSize = tileSize;
        }

        /// <summary>
        /// <c>FUN_10017bb9</c> then <c>FUN_10017c3e</c>:
        /// <code>
        /// chunk  = patches[(z >> shift) * gridWidth + (x >> shift)]
        /// sample = chunk[x &amp; mask, z &amp; mask]
        /// height = sample * HeightMod
        /// </code>
        /// Out-of-range lookups return 0 rather than throwing: the DDA in
        /// <see cref="TilemapSurface"/> range-checks tiles before calling, but a tile on the far edge
        /// legitimately reads the sample one past it.
        /// </summary>
        public float SampleHeight(int x, int z)
        {
            if (x < 0 || z < 0)
                return 0f;

            int cx = x >> _shift;
            int cz = z >> _shift;
            if (cx >= _gridWidth)
                return 0f;

            int index = cz * _gridWidth + cx;
            if (index < 0 || index >= _chunks.Length)
                return 0f;

            ushort[,] chunk = _chunks[index];
            if (chunk == null)
                return 0f;

            int lx = x & _mask;
            int lz = z & _mask;
            if (lx >= chunk.GetLength(0) || lz >= chunk.GetLength(1))
                return 0f;

            return chunk[lx, lz] * _heightMod;
        }

        /// <summary>The sample side of a chunk, for tests and diagnostics.</summary>
        public int ChunkSide => _side;
    }
}
