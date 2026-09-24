namespace ZoneEngine_New.Tests
{
    using AORebirth.World.Collision;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;
    using NumVector3 = System.Numerics.Vector3;

    [TestClass]
    public sealed class WorldFloorSnapTests
    {
        const int PlayfieldId = 800;
        const float SpawnX = 783.17334f;
        const float SpawnZ = 518.6351f;

        [TestMethod]
        public void SnapToFloor_FromFeetUnderTheSurface_HitsTheTerrain()
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            using var world = PlayfieldWorldSimulation.Create(
                PlayfieldId,
                data.GetPlayfieldGeometry(PlayfieldId),
                data.GetPlayfieldMetaData(PlayfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            var terrain = data.GetPlayfieldGeometry(PlayfieldId).Collision?.Terrain;
            Assert.IsNotNull(terrain, "Borealis Collision.dat must expose a heightfield.");
            Assert.IsTrue(
                terrain.TryGetHeight(SpawnX, SpawnZ, out float terrainY),
                "Borealis spawn must sit on the heightfield the vehicle surface is built from.");

            var buried = new Vector3(SpawnX, terrainY - 2.0, SpawnZ);
            Assert.IsFalse(
                world.TryRaycastDown(
                    buried,
                    MovementConfig.GroundProbeLift + MovementConfig.GroundSnapTolerance,
                    out _),
                "A downward ray that starts under a one-sided terrain triangle cannot hit it.");

            Assert.IsTrue(world.TrySnapToFloor(buried, out Vector3 snapped));
            Assert.AreEqual(terrainY, (float)snapped.y, 0.01f);
        }

        [TestMethod]
        public void Heightfield_AtInaKern_IsTheHillNotWorldZero()
        {
            var data = new GameDataStore(new StubLogger());
            var terrain = data.GetPlayfieldGeometry(PlayfieldId).Collision?.Terrain;
            Assert.IsNotNull(terrain);
            Assert.IsTrue(terrain.TryGetHeight(651.901f, 652.239f, out float inaY));
            Assert.IsTrue(inaY > 50f, "Ina is on the hill; heightfield Y was " + inaY.ToString("F3"));
            Assert.IsTrue(terrain.TryGetHeight(655.773f, 664.199f, out float playerY));
            Assert.IsTrue(playerY > 50f, "Player XZ is on the same hill; heightfield Y was " + playerY.ToString("F3"));
        }

        [TestMethod]
        public void SnapToFloor_OnASurfaceAboveTheTerrain_UsesTheSurface()
        {
            const float terrainY = 100f;
            const float platformY = 112f;
            const float x = 2f;
            const float z = 2f;
            using PlayfieldWorldSimulation world = CreateTerrainWithPlatform(terrainY, platformY);

            Assert.IsTrue(world.TrySnapToFloor(new Vector3(x, platformY, z), out Vector3 onPlatform));
            Assert.AreEqual(platformY, (float)onPlatform.y, 0.05f);

            Assert.IsTrue(world.TrySnapToFloor(new Vector3(x, terrainY, z), out Vector3 onTerrain));
            Assert.AreEqual(terrainY, (float)onTerrain.y, 0.05f);

            Assert.IsTrue(world.TrySnapToFloor(new Vector3(x, terrainY - 2f, z), out Vector3 buried));
            Assert.AreEqual(terrainY, (float)buried.y, 0.05f);

            Assert.IsFalse(
                world.TrySnapToFloor(new Vector3(x, platformY + 10f, z), out _),
                "A body more than a snap tolerance above every surface is airborne.");
        }

        static PlayfieldWorldSimulation CreateTerrainWithPlatform(float terrainY, float platformY)
        {
            var heights = new float[2, 2];
            heights[0, 0] = terrainY;
            heights[1, 0] = terrainY;
            heights[0, 1] = terrainY;
            heights[1, 1] = terrainY;
            var terrain = new TerrainHeightfield(
                tileSize: 4f,
                heightScale: 1f,
                chunkSize: 2,
                gridWidth: 1,
                chunks: new[] { new TerrainHeightChunk(heights, 0f, 0f) });

            var platform = new CollisionTriangleMesh(
                new[]
                {
                    new NumVector3(0f, platformY, 0f),
                    new NumVector3(4f, platformY, 0f),
                    new NumVector3(0f, platformY, 4f),
                    new NumVector3(4f, platformY, 4f)
                },
                new[]
                {
                    new CollisionTriangle(0, 2, 1),
                    new CollisionTriangle(1, 2, 3)
                });

            var geometry = new PlayfieldGeometryData
            {
                Collision = new PlayfieldCollisionSet(
                    910001,
                    new[] { platform },
                    terrain)
            };

            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            return PlayfieldWorldSimulation.Create(
                910001,
                geometry,
                meta: null,
                DestinationsCatalog.Instance,
                data,
                new StubLogger());
        }
    }
}
