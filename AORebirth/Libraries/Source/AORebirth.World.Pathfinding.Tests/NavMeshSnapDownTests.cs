namespace AORebirth.World.Pathfinding.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;
    using AORebirth.World.Pathfinding;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Vector3 = System.Numerics.Vector3;

    /// <summary>
    /// Ground at y=0 (40x40), a bridge at y=6 over x 10..30 / z 15..25, and a ledge at y=3 over x 32..40.
    /// </summary>
    [TestClass]
    public sealed class NavMeshSnapDownTests
    {
        const int PlayfieldId = 900010;
        const float Tolerance = 0.5f;

        static string? _root;
        static NavMeshPathfinder? _finder;

        [ClassInitialize]
        public static void Bake(TestContext _)
        {
            _root = Path.Combine(Path.GetTempPath(), "AORebirth.World.Pathfinding.Tests", Guid.NewGuid().ToString("N"));
            string gameData = Path.Combine(_root, "GameData");
            Directory.CreateDirectory(Path.Combine(_root, "Config"));
            File.WriteAllText(Path.Combine(_root, "Config", NavMeshBuildSettings.ConfigFileName), "{}");

            NavMeshBakeResult baked = NavMeshBaker.Bake(StackedScene());
            NavMeshFile.Write(Path.Combine(gameData, GameDataPaths.PlayfieldNavMeshRelativePath(PlayfieldId)), baked);
            Assert.IsTrue(NavMeshPathfinder.TryLoad(gameData, PlayfieldId, out _finder, out string? failure), failure);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _finder?.Dispose();
            if (_root != null && Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [TestMethod]
        public void AboveTheBridgeLandsOnTheBridge()
            => AssertSnapsTo(new Vector3(20f, 10f, 20f), 6f);

        [TestMethod]
        public void UnderTheBridgeLandsOnTheGroundNotTheBridgeOverhead()
            => AssertSnapsTo(new Vector3(20f, 4f, 20f), 0f);

        [TestMethod]
        public void BesideAHigherLedgeLandsStraightDownNotOnTheNearerLedge()
        {
            var spawn = new Vector3(30.5f, 9f, 5f);

            // The nearest polygon in 3D is the ledge beside the spawn; straight down is the ground.
            Assert.IsTrue(_finder!.TrySnap(spawn, NavMeshPathfinder.SpawnSnapExtent, out Vector3 nearest));
            Assert.AreEqual(3f, nearest.Y, Tolerance, "fixture: nearest-in-3D should be the ledge");

            AssertSnapsTo(spawn, 0f);
        }

        [TestMethod]
        public void FeetSlightlyUnderTheFloorStillSnapToIt()
            => AssertSnapsTo(new Vector3(20f, 5.5f, 20f), 6f);

        [TestMethod]
        public void NothingBelowWithinTheDropFails()
            => Assert.IsFalse(_finder!.TrySnapDown(new Vector3(20f, 30f, 20f), maxDrop: 5f, out _));

        [TestMethod]
        public void OffTheMeshFails()
            => Assert.IsFalse(_finder!.TrySnapDown(new Vector3(60f, 10f, 60f), NavMeshPathfinder.SpawnSnapExtent, out _));

        static void AssertSnapsTo(Vector3 spawn, float floorY)
        {
            Assert.IsTrue(_finder!.TrySnapDown(spawn, NavMeshPathfinder.SpawnSnapExtent, out Vector3 snapped));
            Assert.AreEqual(spawn.X, snapped.X, 1e-3);
            Assert.AreEqual(spawn.Z, snapped.Z, 1e-3);
            Assert.AreEqual(floorY, snapped.Y, Tolerance);
        }

        static PlayfieldCollisionSet StackedScene()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<CollisionTriangle>();
            Floor(vertices, triangles, 0f, 0f, 40f, 0f, 40f);
            Floor(vertices, triangles, 6f, 10f, 30f, 15f, 25f);
            Floor(vertices, triangles, 3f, 32f, 40f, 0f, 40f);
            return new PlayfieldCollisionSet(
                PlayfieldId,
                new[] { new CollisionTriangleMesh(vertices.ToArray(), triangles.ToArray(), cellId: null, source: "stacked") },
                terrain: null);
        }

        /// <summary>A horizontal slab, both windings, so it is walkable whichever way the baker reads it.</summary>
        static void Floor(List<Vector3> vertices, List<CollisionTriangle> triangles, float y, float x0, float x1, float z0, float z1)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, y, z0));
            vertices.Add(new Vector3(x1, y, z0));
            vertices.Add(new Vector3(x1, y, z1));
            vertices.Add(new Vector3(x0, y, z1));
            triangles.Add(new CollisionTriangle(start, start + 1, start + 3));
            triangles.Add(new CollisionTriangle(start + 1, start + 2, start + 3));
            triangles.Add(new CollisionTriangle(start, start + 3, start + 1));
            triangles.Add(new CollisionTriangle(start + 1, start + 3, start + 2));
        }
    }
}
