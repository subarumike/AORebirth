namespace ZoneEngine_New.Tests
{
    using System;
    using System.Globalization;

    using AORebirth.World.Collision;

    using N3Lite;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;

    [TestClass]
    public sealed class BorealisGroundLoadTests
    {
        const int PlayfieldId = 800;
        const float SpawnX = 783.17334f;
        const float SpawnY = 32.582794f;
        const float SpawnZ = 518.6351f;

        [TestMethod]
        public void Borealis_TerrainIsPresentForVehicleSurfaceAndNavmeshSource()
        {
            var data = new GameDataStore(new StubLogger());
            PlayfieldGeometryData geometry = data.GetPlayfieldGeometry(PlayfieldId);
            Assert.IsNotNull(geometry.Collision, "Playfield 800 Collision.dat did not load.");

            TerrainHeightfield? terrain = geometry.Collision.Terrain;
            Assert.IsNotNull(
                terrain,
                "TerrainHeightfield is null. The vehicle surface and navmesh flatten share this field.");
            Assert.IsTrue(
                terrain.Chunks.Count > 0,
                "TerrainHeightfield has no chunks.");

            Assert.IsTrue(
                TrySampleHeight(terrain, SpawnX, SpawnZ, out float groundY),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"No terrain sample at spawn ({SpawnX:F1},{SpawnZ:F1}). chunks={terrain.Chunks.Count} tileSize={terrain.TileSize}"));
            Assert.IsTrue(
                MathF.Abs(groundY - SpawnY) < 8f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Terrain Y {groundY:F2} is far from spawn Y {SpawnY:F2}."));

            CollisionTriangleMesh? navSource = TerrainHeightfieldMesher.TryBuild(terrain);
            Assert.IsNotNull(navSource, "Navmesh flatten source (TerrainHeightfieldMesher) produced no triangles.");
            Assert.AreEqual("terrain", navSource.Source);
            Assert.IsTrue(
                navSource.Triangles.Length > 1000,
                "Navmesh terrain source is too small to be the outdoor ground. triangles="
                + navSource.Triangles.Length.ToString(CultureInfo.InvariantCulture));

            PlayfieldSurface? surface = PlayfieldSurfaceFactory.Build(geometry.Collision);
            Assert.IsNotNull(surface?.Terrain, "The vehicle surface has no terrain for playfield 800.");
            float surfaceY = surface.Terrain.GroundHeightAt(new Vec3(SpawnX, SpawnY, SpawnZ), out _);
            Assert.IsTrue(
                terrain.TryGetHeight(SpawnX, SpawnZ, out float heightfieldY),
                "The heightfield has no height at the spawn.");
            Assert.AreEqual(heightfieldY, surfaceY, 0.05f, "The vehicle terrain disagrees with the heightfield.");
        }

        static bool TrySampleHeight(TerrainHeightfield terrain, float x, float z, out float y)
        {
            y = 0f;
            float tileSize = terrain.TileSize > 0 ? terrain.TileSize : 1f;
            float scale = terrain.HeightScale > 0 ? terrain.HeightScale : 1f;
            for (int i = 0; i < terrain.Chunks.Count; i++)
            {
                TerrainHeightChunk chunk = terrain.Chunks[i];
                float[,] heights = chunk.Heights;
                int sizeX = heights.GetLength(0);
                int sizeZ = heights.GetLength(1);
                float localX = (x - chunk.OriginX) / tileSize;
                float localZ = (z - chunk.OriginZ) / tileSize;
                if (localX < 0f || localZ < 0f || localX >= sizeX - 1 || localZ >= sizeZ - 1)
                    continue;

                int ix = (int)localX;
                int iz = (int)localZ;
                y = heights[ix, iz] * scale;
                return true;
            }

            return false;
        }
    }
}
