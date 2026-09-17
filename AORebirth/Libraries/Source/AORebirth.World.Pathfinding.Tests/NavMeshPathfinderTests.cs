namespace AORebirth.World.Pathfinding.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;
    using AORebirth.World.Pathfinding;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Vector3 = System.Numerics.Vector3;

    [TestClass]
    public sealed class NavMeshPathfinderTests
    {
        [TestMethod]
        public void TryLoad_MissingFile_ReturnsFalse()
        {
            string gameDataRoot = CreateTempGameData(copyConfig: true);
            try
            {
                Assert.IsFalse(NavMeshPathfinder.TryLoad(gameDataRoot, 900010, out NavMeshPathfinder? pathfinder));
                Assert.IsNull(pathfinder);
            }
            finally
            {
                DeleteTree(Directory.GetParent(gameDataRoot)?.FullName);
            }
        }

        [TestMethod]
        public void TryLoad_TemplatePlayfield_ReturnsFalse()
        {
            string gameDataRoot = FindGameDataRoot();
            if (!File.Exists(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRoomsRelativePath(324))))
                Assert.Inconclusive("GameData playfield Rooms.json is not packaged.");

            Assert.IsFalse(NavMeshPathfinder.TryLoad(gameDataRoot, 324, out NavMeshPathfinder? pathfinder));
            Assert.IsNull(pathfinder);
        }

        [TestMethod]
        public void TryLoad_BakedFloor_FindsPathWithCheckedInAgentSettings()
        {
            NavMeshBuildSettings settings = NavMeshBuildSettings.Load(
                Path.Combine(FindAoRebirthRoot(), "Config", NavMeshBuildSettings.ConfigFileName));
            Assert.AreEqual(0.8f, settings.AgentRadius);
            Assert.AreEqual(1.7f, settings.AgentHeight);

            PlayfieldCollisionSet set = FlatFloor(playfieldId: 900011);
            NavMeshBakeResult baked = NavMeshBaker.Bake(set, settings);

            string gameDataRoot = CreateTempGameData(copyConfig: true);
            try
            {
                string meshPath = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldNavMeshRelativePath(900011));
                NavMeshFile.Write(meshPath, baked);

                Assert.IsTrue(NavMeshPathfinder.TryLoad(gameDataRoot, 900011, out NavMeshPathfinder? pathfinder));
                Assert.IsNotNull(pathfinder);
                using (pathfinder)
                {
                    Assert.AreEqual(900011, pathfinder.PlayfieldId);
                    Assert.AreEqual(settings.AgentRadius, pathfinder.Settings.AgentRadius);
                    Assert.AreEqual(settings.AgentHeight, pathfinder.Settings.AgentHeight);

                    var waypoints = new List<Vector3>();
                    Assert.IsTrue(pathfinder.TryFindPath(
                        new Vector3(5f, 0.2f, 5f),
                        new Vector3(15f, 0.2f, 15f),
                        waypoints));
                    Assert.IsTrue(waypoints.Count >= 1);
                    Vector3 last = waypoints[waypoints.Count - 1];
                    Assert.IsTrue(Vector3.Distance(last, new Vector3(15f, last.Y, 15f)) < 2f);
                    Assert.IsTrue(pathfinder.TryCanReach(
                        new Vector3(5f, 0.2f, 5f),
                        new Vector3(15f, 0.2f, 15f)));
                    Assert.IsFalse(pathfinder.TryCanReach(
                        new Vector3(5f, 0.2f, 5f),
                        new Vector3(5f, 8f, 5f)));
                    Assert.IsFalse(pathfinder.TryFindPath(
                        new Vector3(5f, 0.2f, 5f),
                        new Vector3(15f, -50f, 15f),
                        waypoints));

                    Assert.IsTrue(pathfinder.TrySnap(new Vector3(5f, 1.7f, 5f), out Vector3 snapped));
                    Assert.IsTrue(Vector3.Distance(new Vector3(5f, snapped.Y, 5f), snapped) < 1f);
                    Assert.IsTrue(snapped.Y < 1f);
                }
            }
            finally
            {
                DeleteTree(Directory.GetParent(gameDataRoot)?.FullName);
            }
        }

        static PlayfieldCollisionSet FlatFloor(int playfieldId)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<CollisionTriangle>();
            AppendQuad(vertices, triangles, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f), new Vector3(20f, 0f, 20f), new Vector3(20f, 0f, 0f));
            return new PlayfieldCollisionSet(
                playfieldId,
                new[] { new CollisionTriangleMesh(vertices.ToArray(), triangles.ToArray(), cellId: null, source: "box") },
                terrain: null);
        }

        static void AppendQuad(
            List<Vector3> vertices,
            List<CollisionTriangle> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(new CollisionTriangle(start, start + 1, start + 3));
            triangles.Add(new CollisionTriangle(start + 1, start + 2, start + 3));
        }

        static string CreateTempGameData(bool copyConfig)
        {
            string root = Path.Combine(Path.GetTempPath(), "AORebirth.World.Pathfinding.Tests", Guid.NewGuid().ToString("N"));
            string gameData = Path.Combine(root, "GameData");
            Directory.CreateDirectory(gameData);
            if (copyConfig)
            {
                string configDir = Path.Combine(root, "Config");
                Directory.CreateDirectory(configDir);
                File.Copy(
                    Path.Combine(FindAoRebirthRoot(), "Config", NavMeshBuildSettings.ConfigFileName),
                    Path.Combine(configDir, NavMeshBuildSettings.ConfigFileName));
            }

            return gameData;
        }

        static void DeleteTree(string? path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        static string FindAoRebirthRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string config = Path.Combine(dir, "AORebirth", "Config", NavMeshBuildSettings.ConfigFileName);
                if (File.Exists(config))
                    return Path.Combine(dir, "AORebirth");

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException("Could not locate AORebirth/Config/NavAgent.json from " + AppContext.BaseDirectory);
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
