namespace AORebirth.World.Pathfinding.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;
    using AORebirth.World.Pathfinding;

    using DotRecast.Detour;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Vector3 = System.Numerics.Vector3;

    [TestClass]
    public sealed class NavMeshBakerTests
    {
        [TestMethod]
        public void Bake_FlatFloor_WritesReloadableMeshSet()
        {
            PlayfieldCollisionSet set = FlatFloor(playfieldId: 900001);
            NavMeshBakeResult baked = NavMeshBaker.Bake(set);

            Assert.IsTrue(baked.TileCount > 0);
            Assert.IsTrue(baked.SourceTriangles > 0);

            string path = Path.Combine(Path.GetTempPath(), "AORebirth.World.Pathfinding.Tests", Guid.NewGuid().ToString("N"), GameDataPaths.NavMeshFileName);
            try
            {
                NavMeshFile.Write(path, baked);
                DtNavMesh reloaded = NavMeshFile.Read(path, out int playfieldId);
                Assert.AreEqual(900001, playfieldId);
                Assert.IsTrue(CountTiles(reloaded) > 0);
            }
            finally
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void Bake_EmptyCollision_Throws()
        {
            var empty = new PlayfieldCollisionSet(900002, Array.Empty<CollisionTriangleMesh>(), terrain: null);
            Assert.ThrowsException<InvalidDataException>(() => NavMeshBaker.Bake(empty));
        }

        [TestMethod]
        public void Resolver_TemplateRooms_Throws()
        {
            string gameDataRoot = FindGameDataRoot();
            if (!File.Exists(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRoomsRelativePath(324))))
                Assert.Inconclusive("GameData playfield Rooms.json is not packaged.");

            Assert.ThrowsException<InvalidDataException>(() => PlayfieldCollisionResolver.Resolve(gameDataRoot, 324));
        }

        [TestMethod]
        public void Bake_PackagedOutdoor100_WhenPresent()
        {
            string gameDataRoot = FindGameDataRoot();
            if (!File.Exists(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldCollisionRelativePath(100))))
                Assert.Inconclusive("GameData playfield 100 Collision.dat is not packaged.");

            NavMeshBakeResult baked = NavMeshBaker.Bake(gameDataRoot, 100);
            Assert.AreEqual(100, baked.PlayfieldId);
            Assert.IsTrue(baked.TileCount > 0);
        }

        [TestMethod]
        public void Bake_PackagedIndoor127_WhenPresent()
        {
            string gameDataRoot = FindGameDataRoot();
            if (!File.Exists(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRoomsRelativePath(127))))
                Assert.Inconclusive("GameData playfield 127 Rooms.json is not packaged.");

            NavMeshBakeResult baked = NavMeshBaker.Bake(gameDataRoot, 127);
            Assert.AreEqual(127, baked.PlayfieldId);
            Assert.IsTrue(baked.TileCount > 0);
        }

        [TestMethod]
        public void Bake_AcgGenerator_WhenStylePackaged()
        {
            string gameDataRoot = FindGameDataRoot();
            int style = FirstPackagedStyle(gameDataRoot);
            if (style <= 0)
                Assert.Inconclusive("No packaged ACG style catalog with GNDA.png.");

            if (!DungeonStyleCatalog.TryLoad(gameDataRoot, style, out DungeonStyleCatalog? catalog)
                || catalog == null)
                Assert.Inconclusive("Style catalog did not load.");

            ushort roomId = 0;
            bool found = false;
            for (int i = 0; i < catalog.Rooms.Count; i++)
            {
                if (!catalog.Rooms[i].IsAcgSizeValid)
                    continue;
                roomId = (ushort)catalog.Rooms[i].InstanceId;
                found = true;
                break;
            }

            if (!found)
                Assert.Inconclusive("Style catalog has no ACG-sized room.");

            var generator = new AcgBuildingGeneratorData
            {
                Identity = new Identity { Type = IdentityType.AcgBuildingGenerator, Instance = 1 },
                Style = style,
                Width = 10,
                Height = 5,
                RoomsPerFloor = 64,
                Rooms = new[]
                {
                    new BuildingRoomInfo { RoomId = roomId, Floor = 0, GridX = 0, GridZ = 0, Facing = 0 }
                }
            };

            NavMeshBakeResult baked = NavMeshBaker.Bake(gameDataRoot, generator);
            Assert.IsTrue(baked.TileCount > 0);
            Assert.IsFalse(File.Exists(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldNavMeshRelativePath(style))));
        }

        static PlayfieldCollisionSet FlatFloor(int playfieldId)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<CollisionTriangle>();
            AppendQuad(vertices, triangles, new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(20f, 0f, 20f), new Vector3(0f, 0f, 20f));
            AppendQuad(vertices, triangles, new Vector3(0f, 0f, 0f), new Vector3(0f, 3f, 0f), new Vector3(20f, 3f, 0f), new Vector3(20f, 0f, 0f));
            AppendQuad(vertices, triangles, new Vector3(20f, 0f, 0f), new Vector3(20f, 3f, 0f), new Vector3(20f, 3f, 20f), new Vector3(20f, 0f, 20f));
            AppendQuad(vertices, triangles, new Vector3(20f, 0f, 20f), new Vector3(20f, 3f, 20f), new Vector3(0f, 3f, 20f), new Vector3(0f, 0f, 20f));
            AppendQuad(vertices, triangles, new Vector3(0f, 0f, 20f), new Vector3(0f, 3f, 20f), new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, 0f));
            AppendQuad(vertices, triangles, new Vector3(0f, 3f, 0f), new Vector3(0f, 3f, 20f), new Vector3(20f, 3f, 20f), new Vector3(20f, 3f, 0f));
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

        static int CountTiles(DtNavMesh mesh)
        {
            int count = 0;
            for (int i = 0; i < mesh.GetMaxTiles(); i++)
            {
                DtMeshTile tile = mesh.GetTile(i);
                if (tile?.data?.header != null)
                    count++;
            }

            return count;
        }

        static int FirstPackagedStyle(string gameDataRoot)
        {
            int[] candidates = { 320, 321, 322, 324, 341 };
            for (int i = 0; i < candidates.Length; i++)
            {
                string gnda = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRelativeDirectory(candidates[i]), "GNDA.png");
                if (File.Exists(gnda))
                    return candidates[i];
            }

            return 0;
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
