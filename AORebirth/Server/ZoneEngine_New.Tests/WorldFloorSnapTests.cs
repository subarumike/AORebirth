namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;

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
                "Borealis spawn must sit on the heightfield that Bepu baked.");

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
    }
}
