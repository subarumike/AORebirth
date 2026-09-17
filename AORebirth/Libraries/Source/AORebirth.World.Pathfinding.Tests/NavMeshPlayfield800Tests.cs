namespace AORebirth.World.Pathfinding.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Numerics;

    using AORebirth.Core.GameData;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class NavMeshPlayfield800Tests
    {
        // First two Borealis district-1 spawns from GameData/Playfields/800/Spawns.json
        static readonly Vector3 SpawnA = new(783.17334f, 32.582794f, 518.6351f);
        static readonly Vector3 SpawnB = new(763.72656f, 37.512455f, 503.61322f);

        [TestMethod]
        public void Borealis_SnapsSpawnThenWalksAlongMesh()
        {
            string gameDataRoot = FindGameDataRoot();
            string meshPath = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldNavMeshRelativePath(800));
            if (!File.Exists(meshPath))
                Assert.Inconclusive("GameData playfield 800 Navmesh.dat is not packaged.");

            Assert.IsTrue(NavMeshPathfinder.TryLoad(gameDataRoot, 800, out NavMeshPathfinder? pathfinder));
            Assert.IsNotNull(pathfinder);
            using (pathfinder)
            {
                Assert.IsTrue(
                    pathfinder.TrySnap(SpawnA, NavMeshPathfinder.SpawnSnapExtent, out Vector3 home),
                    "Spawn A must snap onto the Borealis navmesh.");
                Assert.IsTrue(
                    pathfinder.TrySnap(SpawnA + new Vector3(8f, 0f, 0f), NavMeshPathfinder.SpawnSnapExtent, out Vector3 near),
                    "A point 8m east of spawn A must snap onto the mesh.");

                var waypoints = new List<Vector3>();
                Assert.IsTrue(
                    pathfinder.TryFindPath(home, near, waypoints),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"No corridor after spawn snap home=({home.X:F2},{home.Y:F2},{home.Z:F2}) near=({near.X:F2},{near.Y:F2},{near.Z:F2})"));
                Assert.IsTrue(waypoints.Count >= 1);
            }
        }

        [TestMethod]
        public void Borealis_SnappedSpawns_HaveCorridor()
        {
            string gameDataRoot = FindGameDataRoot();
            string meshPath = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldNavMeshRelativePath(800));
            if (!File.Exists(meshPath))
                Assert.Inconclusive("GameData playfield 800 Navmesh.dat is not packaged.");

            Assert.IsTrue(NavMeshPathfinder.TryLoad(gameDataRoot, 800, out NavMeshPathfinder? pathfinder));
            Assert.IsNotNull(pathfinder);
            using (pathfinder)
            {
                Assert.IsTrue(pathfinder.TrySnap(SpawnA, NavMeshPathfinder.SpawnSnapExtent, out Vector3 a));
                Assert.IsTrue(pathfinder.TrySnap(SpawnB, NavMeshPathfinder.SpawnSnapExtent, out Vector3 b));

                var waypoints = new List<Vector3>();
                Assert.IsTrue(
                    pathfinder.TryFindPath(a, b, waypoints),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"No corridor between snapped Borealis spawns a=({a.X:F2},{a.Y:F2},{a.Z:F2}) b=({b.X:F2},{b.Y:F2},{b.Z:F2})"));
            }
        }

        static string FindGameDataRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string gameData = Path.Combine(dir, "AORebirth", "GameData");
                if (File.Exists(Path.Combine(dir, "AGENTS.md")) && Directory.Exists(gameData))
                    return gameData;

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException("Could not locate GameData from " + AppContext.BaseDirectory);
        }
    }
}
