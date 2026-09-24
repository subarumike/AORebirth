namespace AORebirth.World.Collision.Tests
{
    using System.Numerics;

    using LostEden.Vehicles;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class PlayfieldSurfaceFactoryTests
    {
        [TestMethod]
        public void Build_TwoChunkTerrain_MatchesHeightfieldAtEverySample()
        {
            const int size = 3;
            var left = new float[size, size];
            var right = new float[size, size];
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    left[x, z] = 10f + x + (3f * z);
                    right[x, z] = 12f + x + (3f * z);
                }
            }

            var terrain = new TerrainHeightfield(
                tileSize: 4f,
                heightScale: 0.5f,
                chunkSize: size,
                gridWidth: 2,
                chunks: new[]
                {
                    new TerrainHeightChunk(left, originX: 0f, originZ: 0f),
                    new TerrainHeightChunk(right, originX: 8f, originZ: 0f)
                });
            var set = new PlayfieldCollisionSet(1, System.Array.Empty<CollisionTriangleMesh>(), terrain);

            PlayfieldSurface? surface = PlayfieldSurfaceFactory.Build(set);

            Assert.IsNotNull(surface?.Terrain);
            Assert.AreSame(surface.Terrain, surface.Root);
            for (int x = 0; x < 4; x++)
            {
                for (int z = 0; z < size - 1; z++)
                {
                    float wx = x * 4f;
                    float wz = z * 4f;
                    Assert.IsTrue(terrain.TryGetHeight(wx, wz, out float expected));
                    float actual = surface.Terrain.GroundHeightAt(new Vec3(wx, 0f, wz), out _);
                    Assert.AreEqual(expected, actual, 0.001f, $"sample ({x},{z})");
                }
            }
        }

        [TestMethod]
        public void Build_FloorWithoutTerrain_IsTheRootAndStopsADownwardRay()
        {
            PlayfieldSurface? surface = PlayfieldSurfaceFactory.Build(FloorSet(y: 7f, extent: 20f));

            Assert.IsNotNull(surface);
            Assert.IsNull(surface.Terrain);
            Assert.AreSame(surface.Cells, surface.Root);
            Assert.IsTrue(surface.Root.GetLineIntersection(
                new Vec3(5f, 10f, 5f), new Vec3(5f, 0f, 5f), out Vec3 hit, out Vec3 normal, false, null));
            Assert.AreEqual(7f, hit.Y, 0.001f);
            Assert.IsTrue(normal.Y > 0.99f);

            surface.Root.CalculateClosestPoint(new Vec3(5f, 8f, 5f), out Vec3 closest, out _, null);
            Assert.AreEqual(7f, closest.Y, 0.001f);
        }

        [TestMethod]
        public void Build_FloorSpanningCells_IsReachableFromEveryCell()
        {
            PlayfieldSurface? surface = PlayfieldSurfaceFactory.Build(FloorSet(y: 3f, extent: 100f));

            Assert.IsNotNull(surface);
            Assert.IsTrue(surface.PopulatedCellCount > 1, "A 100 m floor should spread over several 40 m cells.");
            Assert.AreEqual(0, surface.OutsideTriangleCount);
            foreach (float at in new[] { 5f, 45f, 95f })
            {
                Assert.IsTrue(surface.Root.GetLineIntersection(
                    new Vec3(at, 10f, at), new Vec3(at, 0f, at), out Vec3 hit, out _, false, null),
                    $"no floor at {at}");
                Assert.AreEqual(3f, hit.Y, 0.001f);
            }
        }

        [TestMethod]
        public void Build_NoGeometry_ReturnsNull()
        {
            var empty = new PlayfieldCollisionSet(1, System.Array.Empty<CollisionTriangleMesh>(), null);
            Assert.IsNull(PlayfieldSurfaceFactory.Build(empty));
            Assert.IsNull(PlayfieldSurfaceFactory.Build(null));
        }

        /// <summary>One upward-facing quad in record order, as dungeon ground and SurfaceResource meshes are.</summary>
        static PlayfieldCollisionSet FloorSet(float y, float extent)
        {
            var floor = new CollisionTriangleMesh(
                new[]
                {
                    new Vector3(0f, y, 0f),
                    new Vector3(extent, y, 0f),
                    new Vector3(0f, y, extent),
                    new Vector3(extent, y, extent)
                },
                new[]
                {
                    new CollisionTriangle(0, 2, 1),
                    new CollisionTriangle(1, 2, 3)
                });
            return new PlayfieldCollisionSet(1, new[] { floor }, null);
        }
    }
}
