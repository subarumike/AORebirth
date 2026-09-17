namespace AORebirth.World.Collision.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    using AORebirth.Core.GameData;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class DungeonCollisionBuilderTests
    {
        [TestMethod]
        public void Place_Facing0_AddsLocalOriginAndGrid()
        {
            StyleRoomTemplate room = Room5x5(templateY: 4f);
            var spec = new DungeonRoomSpec { RoomId = 0, Floor = 0, GridX = 0, GridZ = 0, Facing = 0 };

            Vector3 placed = DungeonRoomPlacer.Place(room, spec, tileSize: 2f, widthBlocks: 10, heightBlocks: 5, floorHeight: 64, minFloor: 0);

            Assert.AreEqual(6f, placed.X, 0.001f);
            Assert.AreEqual(4f, placed.Y, 0.001f);
            Assert.AreEqual(46f, placed.Z, 0.001f);
        }

        [TestMethod]
        public void Place_Facing1_RotatesAndAppliesSizeBump()
        {
            StyleRoomTemplate room = Room5x5(templateY: 0f);
            var spec = new DungeonRoomSpec { RoomId = 0, Floor = 0, GridX = 1, GridZ = 2, Facing = 1 };

            Vector3 placed = DungeonRoomPlacer.Place(room, spec, tileSize: 2f, widthBlocks: 10, heightBlocks: 5, floorHeight: 64, minFloor: 0);

            Assert.AreEqual(16f, placed.X, 0.001f);
            Assert.AreEqual(24f, placed.Z, 0.001f);
        }

        [TestMethod]
        public void Place_Facing2And3_MatchClientQuadrants()
        {
            StyleRoomTemplate room = Room5x5(templateY: 0f);
            var spec2 = new DungeonRoomSpec { Facing = 2 };
            var spec3 = new DungeonRoomSpec { Facing = 3 };

            Vector3 facing2 = DungeonRoomPlacer.Place(room, spec2, 2f, 10, 5, 64, 0);
            Vector3 facing3 = DungeonRoomPlacer.Place(room, spec3, 2f, 10, 5, 64, 0);

            Assert.AreEqual(4f, facing2.X, 0.001f);
            Assert.AreEqual(44f, facing2.Z, 0.001f);
            Assert.AreEqual(4f, facing3.X, 0.001f);
            Assert.AreEqual(46f, facing3.Z, 0.001f);
        }

        [TestMethod]
        public void Place_FloorHeight64_RaisesYByDelta()
        {
            StyleRoomTemplate room = Room5x5(templateY: 3f);
            var spec = new DungeonRoomSpec { Floor = 2, Facing = 0 };

            Vector3 placed = DungeonRoomPlacer.Place(room, spec, 2f, 10, 5, floorHeight: 64, minFloor: 0);

            Assert.AreEqual(131f, placed.Y, 0.001f);
            Assert.AreEqual(6f, placed.X, 0.001f);
        }

        [TestMethod]
        public void Place_FloorHeight0_ShiftsXSideBySide()
        {
            StyleRoomTemplate room = Room5x5(templateY: 3f);
            var spec = new DungeonRoomSpec { Floor = 1, Facing = 0 };

            Vector3 placed = DungeonRoomPlacer.Place(room, spec, 2f, widthBlocks: 10, heightBlocks: 5, floorHeight: 0, minFloor: 0);

            Assert.AreEqual(3f, placed.Y, 0.001f);
            Assert.AreEqual(206f, placed.X, 0.001f);
        }

        [TestMethod]
        public void Place_MinFloorNotZero_UsesRelativeDelta()
        {
            StyleRoomTemplate room = Room5x5(templateY: 0f);
            var spec = new DungeonRoomSpec { Floor = -1, Facing = 0 };

            Vector3 placed = DungeonRoomPlacer.Place(room, spec, 2f, 10, 5, floorHeight: 64, minFloor: -1);

            Assert.AreEqual(0f, placed.Y, 0.001f);
        }

        [TestMethod]
        public void ComputeYOffset_IgnoresZeroTypeTiles()
        {
            var gnda = new byte[16];
            var dcga = new byte[16];
            gnda[0] = 10;
            gnda[1] = 50;
            dcga[0] = 0;
            dcga[1] = 3;
            var room = new StyleRoomTemplate(0, 0, 0, 4, 4, Vector3.Zero);

            float yOffset = DungeonRoomPlacer.ComputeYOffset(gnda, dcga, 4, 4, room, heightScale: 0.2f);

            Assert.AreEqual(10f, yOffset, 0.001f);
        }

        [TestMethod]
        public void Mesher_FiveByFiveOccupiedRoom_EmitsTrianglesAtPlacedOrigin()
        {
            PlayfieldCollisionSet set = BuildSingleRoomDungeon(facing: 0, floorHeight: 64);

            Assert.IsTrue(set.HasCollision);
            Assert.IsNull(set.Terrain);
            Assert.IsTrue(set.SurfaceMeshes.Count >= 1);
            CollisionTriangleMesh ground = set.SurfaceMeshes[0];
            Assert.AreEqual("ground-room0", ground.Source);
            Assert.AreEqual(25 * 4, ground.Vertices.Length);
            Assert.AreEqual(25 * 2, ground.Triangles.Length);
            Assert.IsTrue(ground.Vertices.All(v => Math.Abs(v.Y - 1f) < 0.001f));
            Assert.IsTrue(ground.Triangles.All(tri => TriangleNormalY(ground.Vertices, tri) > 0f));
            Assert.AreEqual(0f, ground.Vertices[0].X, 0.001f);
            Assert.AreEqual(40f, ground.Vertices[0].Z, 0.001f);
        }

        static float TriangleNormalY(Vector3[] vertices, CollisionTriangle tri)
        {
            Vector3 ab = vertices[tri.B] - vertices[tri.A];
            Vector3 ac = vertices[tri.C] - vertices[tri.A];
            return Vector3.Cross(ab, ac).Y;
        }

        [TestMethod]
        public void DumpObj_WritesNamedObjectsAndFaces()
        {
            PlayfieldCollisionSet set = BuildSingleRoomDungeon(facing: 0, floorHeight: 64);
            string path = Path.Combine(Path.GetTempPath(), "AORebirth.World.Collision.Tests", Guid.NewGuid().ToString("N"), "dungeon.obj");

            try
            {
                CollisionObjDumper.DumpObj(set, path);
                string text = File.ReadAllText(path);
                Assert.IsTrue(text.Contains("o ground-room0"));
                Assert.IsTrue(text.Contains("\nv "));
                Assert.IsTrue(text.Contains("\nf "));
            }
            finally
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        [TestMethod]
        public void Build_Style341_WhenPackaged_HasCollision()
        {
            string gameDataRoot = FindGameDataRoot();
            string gnda = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRelativeDirectory(341), "GNDA.png");
            if (!File.Exists(gnda))
                Assert.Inconclusive("GameData playfield 341 GNDA.png is not packaged.");

            if (!DungeonStyleCatalog.TryLoad(gameDataRoot, 341, out DungeonStyleCatalog? catalog)
                || catalog == null
                || catalog.Rooms.Count == 0)
                Assert.Inconclusive("Style 341 Rooms.json did not yield rooms.");

            ushort roomId = 0;
            for (int i = 0; i < catalog.Rooms.Count; i++)
            {
                if (catalog.Rooms[i].IsAcgSizeValid)
                {
                    roomId = (ushort)i;
                    break;
                }
            }

            var rooms = new[]
            {
                new DungeonRoomSpec { RoomId = roomId, Floor = 0, GridX = 0, GridZ = 0, Facing = 0 }
            };

            PlayfieldCollisionSet set = DungeonCollisionBuilder.Build(catalog, rooms, widthBlocks: 10, heightBlocks: 5, floorHeight: 64);
            Assert.AreEqual(341, set.PlayfieldId);
            Assert.IsTrue(set.HasCollision);
        }

        static PlayfieldCollisionSet BuildSingleRoomDungeon(byte facing, int floorHeight)
        {
            var gnda = new byte[25];
            var dcga = new byte[25];
            for (int i = 0; i < 25; i++)
            {
                gnda[i] = 50;
                dcga[i] = 1;
            }

            var meta = new PlayfieldMetaData
            {
                SchemaVersion = PlayfieldMetaData.SupportedSchemaVersion,
                RecordType = 1000009,
                TilemapResource = 1,
                Width = 5,
                Height = 5,
                TileSize = 2f,
                HeightScale = 0.2f,
                TilemapFormat = PlayfieldMetaData.EmbeddedGroundFormat
            };

            DungeonStyleCatalog catalog = DungeonStyleCatalog.CreateForTest(
                341,
                meta,
                gnda,
                dcga,
                new[] { Room5x5(templateY: 1f) });

            var rooms = new[]
            {
                new DungeonRoomSpec { RoomId = 0, Floor = 0, GridX = 0, GridZ = 0, Facing = facing }
            };

            return DungeonCollisionBuilder.Build(catalog, rooms, widthBlocks: 10, heightBlocks: 5, floorHeight);
        }

        static StyleRoomTemplate Room5x5(float templateY)
        {
            return new StyleRoomTemplate(0, 0, 0, 5, 5, new Vector3(2f, templateY, 2f));
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
