namespace AORebirth.World.Collision.Tests
{
    using System;
    using System.IO;
    using System.Numerics;

    using AORebirth.Core.GameData;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class DungeonWorldLayoutTests
    {
        [TestMethod]
        public void FromRoomEntry_MapsTilesAndTemplatePose()
        {
            var entry = new PlayfieldRoomEntry
            {
                Index = 7,
                TileX1 = 2,
                TileY1 = 70,
                TileX2 = 17,
                TileY2 = 85,
                Template = new[] { 32f, 5f, 378f },
                Rotation = 1
            };

            Assert.IsTrue(StyleRoomTemplate.TryFromRoomEntry(entry, out StyleRoomTemplate? room));
            Assert.IsNotNull(room);
            Assert.AreEqual(7, room.InstanceId);
            Assert.AreEqual(2, room.TileMinX);
            Assert.AreEqual(70, room.TileMinZ);
            Assert.AreEqual(17, room.TileMaxX);
            Assert.AreEqual(85, room.TileMaxZ);
            Assert.AreEqual(32f, room.TemplatePos.X, 0.001f);
            Assert.AreEqual(5f, room.TemplatePos.Y, 0.001f);
            Assert.AreEqual(378f, room.TemplatePos.Z, 0.001f);
            Assert.AreEqual(1, room.TemplateFacing);
        }

        [TestMethod]
        public void StaticLayout_PlacesRoomsAtTemplatePose()
        {
            StyleRoomTemplate room = Room5x5(templateY: 4f);
            DungeonStyleCatalog catalog = CatalogWith(room);

            DungeonWorldLayout layout = DungeonCollisionBuilder.BuildStatic(catalog, playfieldId: 127);

            Assert.AreEqual(1, layout.Rooms.Count);
            DungeonRoomBounds bounds = layout.Rooms[0];
            Assert.AreEqual(0, bounds.Index);
            Assert.IsTrue(bounds.Contains(new Vector3(6f, 4f, 4f)));
            Assert.AreEqual(127, layout.Collision.PlayfieldId);
        }

        [TestMethod]
        public void NestedRooms_InnermostVolumeWins()
        {
            var outer = new DungeonRoomBounds(0, new Vector3(0f, 0f, 0f), new Vector3(40f, 16f, 40f));
            var inner = new DungeonRoomBounds(1, new Vector3(10f, 0f, 10f), new Vector3(20f, 16f, 20f));

            int cell = DungeonRoomCellResolver.Resolve(new[] { outer, inner }, new Vector3(15f, 4f, 15f));

            Assert.AreEqual(1, cell);
        }

        [TestMethod]
        public void NestedRooms_PointOnlyInOuter_SelectsOuter()
        {
            var outer = new DungeonRoomBounds(0, new Vector3(0f, 0f, 0f), new Vector3(40f, 16f, 40f));
            var inner = new DungeonRoomBounds(1, new Vector3(10f, 0f, 10f), new Vector3(20f, 16f, 20f));

            int cell = DungeonRoomCellResolver.Resolve(new[] { outer, inner }, new Vector3(2f, 4f, 2f));

            Assert.AreEqual(0, cell);
        }

        [TestMethod]
        public void SameXzDifferentFloors_UsesContainingY()
        {
            var lower = new DungeonRoomBounds(0, new Vector3(0f, 0f, 0f), new Vector3(20f, 32f, 20f));
            var upper = new DungeonRoomBounds(1, new Vector3(0f, 64f, 0f), new Vector3(20f, 96f, 20f));

            Assert.AreEqual(0, DungeonRoomCellResolver.Resolve(new[] { lower, upper }, new Vector3(10f, 8f, 10f)));
            Assert.AreEqual(1, DungeonRoomCellResolver.Resolve(new[] { lower, upper }, new Vector3(10f, 70f, 10f)));
        }

        [TestMethod]
        public void Miss_PicksNearestRoom()
        {
            var a = new DungeonRoomBounds(3, new Vector3(0f, 0f, 0f), new Vector3(10f, 10f, 10f));
            var b = new DungeonRoomBounds(8, new Vector3(100f, 0f, 100f), new Vector3(110f, 10f, 110f));

            int cell = DungeonRoomCellResolver.Resolve(new[] { a, b }, new Vector3(12f, 5f, 5f));

            Assert.AreEqual(3, cell);
        }

        [TestMethod]
        public void TryLoad_Packaged127_UsesRoomsJson()
        {
            string gameDataRoot = FindGameDataRoot();
            if (!DungeonStyleCatalog.TryLoad(gameDataRoot, 127, out DungeonStyleCatalog? catalog)
                || catalog == null)
                Assert.Inconclusive("GameData playfield 127 Rooms.json is not packaged.");

            Assert.IsTrue(catalog.Rooms.Count > 0);
            DungeonWorldLayout layout = DungeonCollisionBuilder.BuildStatic(catalog, 127);
            Assert.AreEqual(catalog.Rooms.Count, layout.Rooms.Count);
            Assert.AreEqual(127, layout.Collision.PlayfieldId);
        }

        [TestMethod]
        public void StyleTemplate_AllNegativeZoneLinks_IsTemplate()
        {
            var rooms = new PlayfieldRoomsData
            {
                Rooms = new[]
                {
                    new PlayfieldRoomEntry
                    {
                        DoorConnections = new[]
                        {
                            new PlayfieldRoomDoorLink { ZoneLink = -1 },
                            new PlayfieldRoomDoorLink { ZoneLink = -1 }
                        }
                    }
                }
            };

            Assert.IsTrue(DungeonPlayfieldKinds.IsStyleTemplate(rooms));
        }

        [TestMethod]
        public void StyleTemplate_AnyRealZoneLink_IsNotTemplate()
        {
            var rooms = new PlayfieldRoomsData
            {
                Rooms = new[]
                {
                    new PlayfieldRoomEntry
                    {
                        DoorConnections = new[]
                        {
                            new PlayfieldRoomDoorLink { ZoneLink = -1 },
                            new PlayfieldRoomDoorLink { ZoneLink = 6 }
                        }
                    }
                }
            };

            Assert.IsFalse(DungeonPlayfieldKinds.IsStyleTemplate(rooms));
        }

        [TestMethod]
        public void StyleTemplate_MissingOrEmptyRooms_IsNotTemplate()
        {
            Assert.IsFalse(DungeonPlayfieldKinds.IsStyleTemplate((PlayfieldRoomsData?)null));
            Assert.IsFalse(DungeonPlayfieldKinds.IsStyleTemplate(new PlayfieldRoomsData()));
            Assert.IsFalse(DungeonPlayfieldKinds.IsStyleTemplate(new PlayfieldRoomsData { Rooms = Array.Empty<PlayfieldRoomEntry>() }));
            Assert.IsFalse(
                DungeonPlayfieldKinds.IsStyleTemplate(
                    new PlayfieldRoomsData { Rooms = new[] { new PlayfieldRoomEntry() } }));
        }

        [TestMethod]
        public void StyleTemplate_PackagedPlayfields_MatchDoorLinks()
        {
            string gameDataRoot = FindGameDataRoot();
            if (!File.Exists(Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRoomsRelativePath(324))))
                Assert.Inconclusive("GameData playfield Rooms.json is not packaged.");

            Assert.IsTrue(DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, 324));
            Assert.IsTrue(DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, 320));
            Assert.IsFalse(DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, 127));
            Assert.IsFalse(DungeonPlayfieldKinds.IsStyleTemplate(gameDataRoot, 100));
        }

        static DungeonStyleCatalog CatalogWith(StyleRoomTemplate room)
        {
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
            return DungeonStyleCatalog.CreateForTest(127, meta, new byte[25], new byte[25], new[] { room });
        }

        static StyleRoomTemplate Room5x5(float templateY)
            => new(0, 0, 0, 5, 5, new Vector3(6f, templateY, 4f));

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
