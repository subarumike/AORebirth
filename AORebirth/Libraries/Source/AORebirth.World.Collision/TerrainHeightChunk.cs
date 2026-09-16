namespace AORebirth.World.Collision
{
    using System;

    /// <summary>
    /// One CHGA heightmap chunk. Samples are raw (unscaled) and indexed <c>[x, z]</c>.
    /// World origin of sample (0,0) is (<see cref="OriginX"/>, <see cref="OriginZ"/>).
    /// </summary>
    public sealed class TerrainHeightChunk
    {
        public TerrainHeightChunk(float[,] heights, float originX, float originZ)
        {
            Heights = heights ?? throw new ArgumentNullException(nameof(heights));
            OriginX = originX;
            OriginZ = originZ;
        }

        public float[,] Heights { get; }

        public float OriginX { get; }

        public float OriginZ { get; }
    }
}
