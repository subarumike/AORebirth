using System;
using LostEden.Vehicles;
using LostEden.Vehicles.Surfaces;
using Xunit;

namespace LostEden.Vehicle.Tests
{
    /// <summary>
    /// <see cref="TilemapSurface"/> against synthetic terrain, so the recovered maths is checked
    /// without a database or Unity. See <c>Docs/Movement.md</c> §5.2a and §5.2h.
    /// </summary>
    public class TilemapSurfaceTests
    {
        /// <summary>Terrain whose height is whatever the caller says, for exact expectations.</summary>
        sealed class FuncHeights : ITileHeightSource
        {
            readonly Func<int, int, float> _height;

            public FuncHeights(int width, int height, float tileSize, Func<int, int, float> f)
            {
                Width = width;
                Height = height;
                TileSize = tileSize;
                _height = f;
            }

            public int Width { get; }
            public int Height { get; }
            public float TileSize { get; }
            public float SampleHeight(int x, int z) => _height(x, z);
        }

        static TilemapSurface Flat(float y, float tileSize = 1f, int extent = 64)
            => new TilemapSurface(new FuncHeights(extent, extent, tileSize, (_, _) => y));

        // ---- the ground probe the walker actually makes ----------------------

        [Fact]
        public void ADownwardRayOntoFlatGroundHitsAtTheGroundHeight()
        {
            TilemapSurface surface = Flat(10f);

            bool hit = surface.GetLineIntersection(
                new Vec3(4.3f, 12f, 7.1f),
                new Vec3(4.3f, 0f, 7.1f),
                out Vec3 point, out Vec3 normal, true, null);

            Assert.True(hit);
            Assert.Equal(10f, point.Y, 4);
            Assert.Equal(4.3f, point.X, 4);
            Assert.Equal(7.1f, point.Z, 4);
        }

        [Fact]
        public void TheNormalHandedBackOpposesTheRay()
        {
            // 100190f8 negates cross(e0, e1) on success, so a downward probe gets an upward normal.
            TilemapSurface surface = Flat(0f);

            Assert.True(surface.GetLineIntersection(
                new Vec3(2.5f, 5f, 2.5f), new Vec3(2.5f, -5f, 2.5f),
                out _, out Vec3 normal, true, null));

            float length = normal.Length;
            Assert.True(length > 0f);
            Assert.Equal(1f, normal.Y / length, 3);
        }

        [Fact]
        public void ARayThatStopsAboveTheGroundDoesNotHit()
        {
            // The segment-containment test rejects a plane crossing beyond the ray's end.
            TilemapSurface surface = Flat(0f);

            Assert.False(surface.GetLineIntersection(
                new Vec3(2.5f, 10f, 2.5f), new Vec3(2.5f, 5f, 2.5f),
                out _, out _, true, null));
        }

        [Fact]
        public void AMissWritesTheMinusOneSentinel()
        {
            // 10018b72 line 162 writes (-1,-1,-1) rather than leaving the out parameter alone.
            TilemapSurface surface = Flat(0f);

            Assert.False(surface.GetLineIntersection(
                new Vec3(2.5f, 10f, 2.5f), new Vec3(2.5f, 5f, 2.5f),
                out Vec3 point, out _, true, null));

            Assert.Equal(new Vec3(-1f, -1f, -1f), point);
        }

        [Fact]
        public void AnUpwardRayFromBelowMissesBecauseTheTestIsOneSided()
        {
            // dot(direction, cross(e0,e1)) > 0 is required, so terrain is hit from above only.
            TilemapSurface surface = Flat(0f);

            Assert.False(surface.GetLineIntersection(
                new Vec3(2.5f, -5f, 2.5f), new Vec3(2.5f, 5f, 2.5f),
                out _, out _, true, null));
        }

        // ---- sloped ground ---------------------------------------------------

        [Fact]
        public void ARampIsSampledAtTheRightHeight()
        {
            // height = x, so the surface rises 1 m per tile along X.
            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, (x, _) => x));

            Assert.True(surface.GetLineIntersection(
                new Vec3(5f, 20f, 3f), new Vec3(5f, -1f, 3f),
                out Vec3 point, out Vec3 normal, true, null));

            // Exactly on the sample line x = 5, the interpolated height is 5.
            Assert.Equal(5f, point.Y, 3);
            // The normal leans back along -X and still points up.
            Assert.True(normal.Y > 0f);
            Assert.True(normal.X < 0f);
        }

        [Fact]
        public void HeightIsInterpolatedAcrossATile()
        {
            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, (x, _) => x));

            Assert.True(surface.GetLineIntersection(
                new Vec3(5.5f, 20f, 3.25f), new Vec3(5.5f, -1f, 3.25f),
                out Vec3 point, out _, true, null));

            Assert.Equal(5.5f, point.Y, 3);
        }

        // ---- the diagonal ----------------------------------------------------

        [Fact]
        public void TheDiagonalIsACheckerboardOfTileParity()
        {
            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, (_, _) => 0f));

            // (~tz ^ tx) & 1: 1 when tx and tz share parity, 0 when they differ.
            surface.GetTileCorners(0, 0, out _, out _, out _, out _, out int d00);
            surface.GetTileCorners(1, 1, out _, out _, out _, out _, out int d11);
            surface.GetTileCorners(1, 0, out _, out _, out _, out _, out int d10);
            surface.GetTileCorners(0, 1, out _, out _, out _, out _, out int d01);

            Assert.Equal(1, d00);
            Assert.Equal(1, d11);
            Assert.Equal(0, d10);
            Assert.Equal(0, d01);
        }

        [Fact]
        public void TheDiagonalDecidesWhichTriangleAProbeLandsOn()
        {
            // Tile (0,0), parity-equal, so diagonal 1 -> split along c0-c2, the MAIN diagonal.
            //   c0 = (0,0) h0   c1 = (1,0) h0   c2 = (1,1) h0   c3 = (0,1) h4
            // The raised corner c3 is off the split, so the two halves differ: the half below the
            // main diagonal is flat, the half above carries the bump. A test with the bump ON the
            // split line cannot tell the two triangulations apart, which is how an inverted
            // mapping survived 67,200 real-terrain probes.
            static float Bump(int x, int z) => (x == 0 && z == 1) ? 4f : 0f;

            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, Bump));

            // (0.8, 0.2) is below the main diagonal, in triangle (c0,c1,c2) -- all corners 0.
            Assert.True(surface.IntersectTile(0, 0,
                new Vec3(0.8f, 10f, 0.2f), new Vec3(0.8f, -10f, 0.2f),
                out Vec3 below, out _));
            Assert.Equal(0f, below.Y, 3);

            // (0.2, 0.8) is above it, in triangle (c2,c3,c0) through (1,1,0), (0,1,4), (0,0,0).
            // That plane is h = -4x + 4z, so the height there is 2.4.
            Assert.True(surface.IntersectTile(0, 0,
                new Vec3(0.2f, 10f, 0.8f), new Vec3(0.2f, -10f, 0.8f),
                out Vec3 above, out _));
            Assert.Equal(2.4f, above.Y, 3);
        }

        [Fact]
        public void ADiagonalZeroTileSplitsTheOtherWay()
        {
            // Tile (1,0) has mismatched parity -> diagonal 0 -> split along c1-c3, the ANTI-diagonal.
            //   c0 = (1,0) h4   c1 = (2,0) h0   c2 = (2,1) h0   c3 = (1,1) h0
            // The raised c0 sits in triangle (c0,c1,c3) only.
            static float Bump(int x, int z) => (x == 1 && z == 0) ? 4f : 0f;

            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, Bump));

            // local (0.2,0.2) -> inside (c0,c1,c3). Plane through (1,0,4),(2,0,0),(1,1,0)
            // is h = -4x - 4z + 8, so at (1.2, 0.2) the height is 2.4.
            Assert.True(surface.IntersectTile(1, 0,
                new Vec3(1.2f, 10f, 0.2f), new Vec3(1.2f, -10f, 0.2f),
                out Vec3 near, out _));
            Assert.Equal(2.4f, near.Y, 3);

            // local (0.8,0.8) -> inside (c1,c2,c3), all of which are 0.
            Assert.True(surface.IntersectTile(1, 0,
                new Vec3(1.8f, 10f, 0.8f), new Vec3(1.8f, -10f, 0.8f),
                out Vec3 far, out _));
            Assert.Equal(0f, far.Y, 3);
        }

        [Fact]
        public void AnAxisAlignedRayStillWalks()
        {
            // A delta of exactly zero on one axis must give that axis tDelta = +Infinity so it
            // never steps. Taking the negative arm instead yields -Infinity, which wins every
            // comparison and stalls the other axis. See SetupAxis.
            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, (x, _) => x >= 10 ? 6f : 0f));

            // dz is exactly 0 here.
            Assert.True(surface.GetLineIntersection(
                new Vec3(2f, 5f, 4.5f), new Vec3(14f, 4f, 4.5f),
                out Vec3 point, out _, true, null));
            Assert.True(point.X > 8f, $"expected to reach the rise, stopped at x={point.X}");
        }

        // ---- the walk --------------------------------------------------------

        [Fact]
        public void AShallowRayWalksAcrossTilesUntilItMeetsAWall()
        {
            // Flat at 0 except a tall block at tile x = 10, which the ray must reach by walking.
            static float Wall(int x, int z) => x >= 10 && x <= 11 ? 6f : 0f;
            var surface = new TilemapSurface(new FuncHeights(64, 64, 1f, Wall));

            // Start above the flat part, descend gently across X towards the block.
            bool hit = surface.GetLineIntersection(
                new Vec3(2f, 5f, 4.5f), new Vec3(14f, 4f, 4.5f),
                out Vec3 point, out _, true, null);

            Assert.True(hit);
            // It should stop on the near face/top of the raised tiles, not run to the end.
            Assert.True(point.X >= 9.5f && point.X <= 12.5f, $"walked to x={point.X}");
        }

        [Fact]
        public void ARayEntirelyBelowTheTerrainFindsNothing()
        {
            TilemapSurface surface = Flat(50f);

            Assert.False(surface.GetLineIntersection(
                new Vec3(1f, 0f, 1f), new Vec3(20f, 0f, 20f),
                out _, out _, true, null));
        }

        [Fact]
        public void TilesOutsideTheMapAreSkipped()
        {
            TilemapSurface surface = Flat(0f, tileSize: 1f, extent: 8);

            // Probe well outside the 8x8 map.
            Assert.False(surface.GetLineIntersection(
                new Vec3(40f, 5f, 40f), new Vec3(40f, -5f, 40f),
                out _, out _, true, null));
        }

        // ---- the nested surface ---------------------------------------------

        sealed class ConstantSurface : ISurface
        {
            readonly Vec3 _hit;
            readonly bool _result;

            public ConstantSurface(bool result, Vec3 hit)
            {
                _result = result;
                _hit = hit;
            }

            public bool GetLineIntersection(Vec3 start, Vec3 end, out Vec3 hit, out Vec3 normal, bool clip, object locality)
            {
                hit = _hit;
                normal = Vec3.ReferenceUp;
                return _result;
            }

            public bool VetoPosition(ref Vec3 position, object locality, Vec3 previous) => false;

            public void CalculateClosestPoint(Vec3 point, out Vec3 closest, out Vec3 normal, object locality)
            {
                closest = _hit;
                normal = Vec3.ReferenceUp;
            }
        }

        [Fact]
        public void TheNearerOfTheChildAndTheTerrainWins()
        {
            TilemapSurface surface = Flat(0f);
            // A child hit higher up is nearer to a downward ray's start, so it wins.
            surface.Child = new ConstantSurface(true, new Vec3(2.5f, 3f, 2.5f));

            Assert.True(surface.GetLineIntersection(
                new Vec3(2.5f, 10f, 2.5f), new Vec3(2.5f, -5f, 2.5f),
                out Vec3 point, out _, true, null));

            Assert.Equal(3f, point.Y, 4);
        }

        [Fact]
        public void TheTerrainWinsWhenItIsNearerThanTheChild()
        {
            TilemapSurface surface = Flat(0f);
            // A child hit far below the terrain is further from the ray's start.
            surface.Child = new ConstantSurface(true, new Vec3(2.5f, -4f, 2.5f));

            Assert.True(surface.GetLineIntersection(
                new Vec3(2.5f, 10f, 2.5f), new Vec3(2.5f, -5f, 2.5f),
                out Vec3 point, out _, true, null));

            Assert.Equal(0f, point.Y, 4);
        }

        [Fact]
        public void TheChildStillCountsWhenTheTerrainMisses()
        {
            TilemapSurface surface = Flat(-100f);
            surface.Child = new ConstantSurface(true, new Vec3(2.5f, 1f, 2.5f));

            Assert.True(surface.GetLineIntersection(
                new Vec3(2.5f, 10f, 2.5f), new Vec3(2.5f, 0f, 2.5f),
                out Vec3 point, out _, true, null));

            Assert.Equal(1f, point.Y, 4);
        }

        // ---- the two stock paths must agree ---------------------------------

        [Fact]
        public void TheDirectHeightQueryAgreesWithTheRayCastOnWarpedTerrain()
        {
            // GroundHeightAt (FUN_10017800) and the DDA ray cast (10018b72 -> Intersect_Tile) pick
            // their triangle by completely different means. On non-planar terrain they only agree
            // if the diagonal mapping is right in BOTH, which is exactly what a corner-range check
            // cannot tell you. See Docs/Movement.md §7.1a.
            var rng = new Random(20260923);
            var heights = new float[40, 40];
            for (int x = 0; x < 40; x++)
                for (int z = 0; z < 40; z++)
                    heights[x, z] = (float)(rng.NextDouble() * 8.0);

            var surface = new TilemapSurface(
                new FuncHeights(40, 40, 1f, (x, z) => heights[Math.Min(x, 39), Math.Min(z, 39)]));

            int compared = 0;
            for (int i = 0; i < 4000; i++)
            {
                float px = 1f + (float)(rng.NextDouble() * 36.0);
                float pz = 1f + (float)(rng.NextDouble() * 36.0);

                float direct = surface.GroundHeightAt(new Vec3(px, 0f, pz), out Vec3 directNormal);

                Assert.True(surface.GetLineIntersection(
                    new Vec3(px, 40f, pz), new Vec3(px, -40f, pz),
                    out Vec3 cast, out Vec3 castNormal, true, null));

                // The two solve the same plane by different arithmetic, so compare with an
                // absolute tolerance rather than by rounding to decimal places: a value like
                // 4.04550 rounds to either side of the boundary on a 2e-6 difference.
                Assert.True(Math.Abs(direct - cast.Y) < 1e-3f,
                    $"at ({px:F3},{pz:F3}) direct={direct} cast={cast.Y}");

                // and the normals must describe the same plane (the ray cast's opposes the ray,
                // the direct query's points up, so they agree in sign here)
                float dn = directNormal.Length;
                float cn = castNormal.Length;
                if (dn > 0f && cn > 0f)
                {
                    float alignment = Vec3.Dot(directNormal * (1f / dn), castNormal * (1f / cn));
                    Assert.True(alignment > 0.9999f,
                        $"at ({px:F3},{pz:F3}) normals disagree: {directNormal} vs {castNormal}");
                }

                compared++;
            }

            Assert.Equal(4000, compared);
        }

        [Fact]
        public void TheDirectQueryReturnsZeroOutsideTheMap()
        {
            TilemapSurface surface = Flat(5f, tileSize: 1f, extent: 8);

            Assert.Equal(0f, surface.GroundHeightAt(new Vec3(-1f, 0f, 4f), out _), 4);
            Assert.Equal(0f, surface.GroundHeightAt(new Vec3(4f, 0f, -1f), out _), 4);
            Assert.Equal(0f, surface.GroundHeightAt(new Vec3(8f, 0f, 4f), out _), 4);
            Assert.Equal(0f, surface.GroundHeightAt(new Vec3(4f, 0f, 8f), out _), 4);
            // inside is fine
            Assert.Equal(5f, surface.GroundHeightAt(new Vec3(4f, 0f, 4f), out _), 4);
        }

        // ---- the chunked lookup ---------------------------------------------

        [Fact]
        public void TheChunkedSourceIndexesTheSameWayTheClientDoes()
        {
            // Two chunks side by side, side 5 (step 4). Sample (x,z) -> chunk[x & 3, z & 3].
            const int side = 5;
            var a = new ushort[side, side];
            var b = new ushort[side, side];
            for (int x = 0; x < side; x++)
                for (int z = 0; z < side; z++)
                {
                    a[x, z] = (ushort)(100 + x + z * 10);
                    b[x, z] = (ushort)(200 + x + z * 10);
                }

            var source = new ChunkedTileHeightSource(
                new[] { a, b }, gridWidth: 2, chunkSide: side, heightMod: 0.5f,
                width: 8, height: 4, tileSize: 1f);

            // x = 1 lands in chunk 0 at local x = 1.
            Assert.Equal((100 + 1 + 20) * 0.5f, source.SampleHeight(1, 2), 4);
            // x = 5 lands in chunk 1 at local x = 1 (5 >> 2 == 1, 5 & 3 == 1).
            Assert.Equal((200 + 1 + 20) * 0.5f, source.SampleHeight(5, 2), 4);
        }

        [Fact]
        public void AChunkSideThatIsNotAPowerOfTwoPlusOneIsRejected()
        {
            // The client's lookup shifts and masks, so it cannot address such a chunk.
            var chunk = new ushort[6, 6];
            Assert.Throws<ArgumentException>(() => new ChunkedTileHeightSource(
                new[] { chunk }, 1, 6, 1f, 6, 6, 1f));
        }
    }
}
