namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AORebirth.Core.GameData;
    using StbImageWriteSharp;

    internal static class SelfTests
    {
        internal static bool Run()
        {
            TestChgaPngWriter();
            TestHashHelper();
            TestPlayfieldMetaDataContract();
            TestDistrictAndSpawnContracts();
            TestPlayfieldDatFileNames();
            TestItemsDatFileName();
            TestItemsDatDynelTypeRoundTrip();
            TestHitFunctionArgOrdering();
            TestCollisionDatFraming();
            TestSurfacesDatFraming();
            Console.WriteLine("RDBDataExtractor self-test PASS");
            return true;
        }

        private static void TestSurfacesDatFraming()
        {
            List<PlayfieldSurfaceEntry> entries = new List<PlayfieldSurfaceEntry>
            {
                new PlayfieldSurfaceEntry(1, new byte[] { 1, 2, 3 }),
                new PlayfieldSurfaceEntry(4242, new byte[0]),
                new PlayfieldSurfaceEntry(65535, new byte[] { 9 }),
            };

            List<PlayfieldSurfaceEntry> parsed = PlayfieldSurfacesDat.Parse(
                PlayfieldSurfacesDat.Build(entries));
            if (parsed.Count != entries.Count)
            {
                throw new InvalidOperationException(
                    "Surfaces.dat round trip lost entries: " + parsed.Count);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (parsed[i].CellId != entries[i].CellId
                    || parsed[i].Payload.Length != entries[i].Payload.Length)
                {
                    throw new InvalidOperationException(
                        "Surfaces.dat round trip corrupted entry " + i + ".");
                }
            }

            if (PlayfieldSurfacesDat.Parse(
                    PlayfieldSurfacesDat.Build(new List<PlayfieldSurfaceEntry>())).Count != 0)
            {
                throw new InvalidOperationException(
                    "Surfaces.dat round trip of an empty list was not empty.");
            }
        }

        private static void TestChgaPngWriter()
        {
            ushort[] pixels = new ushort[]
            {
                0,
                255,
                256,
                65535,
            };
            string path = Path.Combine(Path.GetTempPath(), "rdbdataextractor-chga-selftest.png");
            try
            {
                HeightmapPngWriter.WriteChgaPng(path, pixels, 2, 2);
                byte[] png = File.ReadAllBytes(path);
                if (png.Length < 8
                    || png[0] != 0x89
                    || png[1] != (byte)'P'
                    || png[2] != (byte)'N'
                    || png[3] != (byte)'G')
                {
                    throw new InvalidOperationException("CHGA PNG signature was invalid.");
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void TestHashHelper()
        {
            string hash = HashHelper.Sha256Hex(new byte[] { 1, 2, 3 });
            if (hash.Length != 64)
            {
                throw new InvalidOperationException("SHA256 hex length was invalid.");
            }
        }

        private static void TestPlayfieldMetaDataContract()
        {
            PlayfieldMetaData chunked = new PlayfieldMetaData
            {
                SchemaVersion = PlayfieldMetaData.SupportedSchemaVersion,
                RecordType = 1000009,
                TilemapResource = 100,
                Width = 51,
                Height = 51,
                TileSize = 4f,
                HeightScale = 0.2f,
                ChunkSize = 9,
                GridWidth = 7,
                BitsPerSample = 8,
                TilemapFormat = PlayfieldMetaData.ChunkedGroundFormat,
                HeightFormat = "chunkedUshortGreyAlpha",
            };

            string error;
            if (!chunked.IsValid(out error))
            {
                throw new InvalidOperationException("Chunked metadata was rejected: " + error);
            }

            int numZonesX;
            int numZonesZ;
            float cellWorldSize;
            if (!chunked.TryGetOutdoorGrid(out numZonesX, out numZonesZ, out cellWorldSize)
                || numZonesX != 5
                || numZonesZ != 5
                || cellWorldSize != PlayfieldMetaData.CellSize)
            {
                throw new InvalidOperationException("Chunked outdoor grid derivation was invalid.");
            }

            PlayfieldMetaData embedded = new PlayfieldMetaData
            {
                SchemaVersion = PlayfieldMetaData.SupportedSchemaVersion,
                RecordType = 1000009,
                TilemapResource = 1420,
                Width = 50,
                Height = 50,
                TileSize = 2f,
                HeightScale = 0.2f,
                TilemapFormat = PlayfieldMetaData.EmbeddedGroundFormat,
                HeightFormat = "embeddedPng8",
            };

            if (!embedded.IsValid(out error))
            {
                throw new InvalidOperationException("Embedded metadata was rejected: " + error);
            }

            if (embedded.TryGetOutdoorGrid(out numZonesX, out numZonesZ, out cellWorldSize))
            {
                throw new InvalidOperationException("Embedded metadata must not yield an outdoor grid.");
            }
        }

        private static void TestDistrictAndSpawnContracts()
        {
            if (GameDataPaths.DistrictsFileName != "Districts.json"
                || GameDataPaths.SpawnsFileName != "Spawns.json")
            {
                throw new InvalidOperationException("District/spawn file names were unexpected.");
            }

            PlayfieldDistrictsData districts = new PlayfieldDistrictsData
            {
                SchemaVersion = PlayfieldDistrictsData.SupportedSchemaVersion,
                RecordType = 1000014,
                RecordId = 4582,
                FormatVersion = 7,
                ZoneCount = 1,
                ZoneToDistrictMap = new byte[] { 0 },
                Districts = new[]
                {
                    new PlayfieldDistrictEntry
                    {
                        DistrictIndex = 0,
                        Name = "Test",
                        Centre = new float[] { 1f, 2f, 3f },
                        Stats = new ushort[0],
                        SpawnInfos = new PlayfieldDistrictSpawnInfo[0],
                        MusicPairs = new PlayfieldDistrictMusicPair[0],
                        SpawnPoints = new PlayfieldDistrictSpawnPoint[0],
                    },
                },
            };

            if (districts.Districts.Length != 1 || districts.Districts[0].Name != "Test")
            {
                throw new InvalidOperationException("Districts contract smoke check failed.");
            }

            PlayfieldSpawnsData spawns = new PlayfieldSpawnsData
            {
                SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
                RecordType = 1000014,
                PlayfieldId = 4582,
                Spawns = new[]
                {
                    new PlayfieldSpawnEntry
                    {
                        DistrictIndex = 0,
                        Hash = 1,
                        HashText = "ABCD",
                        Position = new float[] { 10f, 20f, 30f },
                        AdditionalPoints = new PlayfieldRotationSpawnPoint[0],
                    },
                },
            };

            if (spawns.Spawns.Length != 1 || spawns.Spawns[0].HashText != "ABCD")
            {
                throw new InvalidOperationException("Spawns contract smoke check failed.");
            }
        }

        private static void TestPlayfieldDatFileNames()
        {
            if (GameDataPaths.WallsFileName != "Walls.dat"
                || GameDataPaths.DynelsFileName != "Dynels.dat"
                || GameDataPaths.DoorsFileName != "Doors.dat"
                || GameDataPaths.CollisionFileName != "Collision.dat"
                || GameDataPaths.DestinationsFileName != "Destinations.dat")
            {
                throw new InvalidOperationException(
                    "Playfield dat file names were unexpected.");
            }
        }

        private static void TestItemsDatFileName()
        {
            if (GameDataPaths.ItemsFileName != "items.dat")
            {
                throw new InvalidOperationException(
                    "items.dat file name was unexpected.");
            }
        }

        private static void TestHitFunctionArgOrdering()
        {
            var keyed = new Dictionary<AODB.Common.Enums.FunctionOperator, object>
            {
                { AODB.Common.Enums.FunctionOperator.Duration, 1 },
                { AODB.Common.Enums.FunctionOperator.Interval, 0u },
                { AODB.Common.Enums.FunctionOperator.ApplyOn, 3u },
                { AODB.Common.Enums.FunctionOperator.TargetList, 9u },
                { AODB.Common.Enums.FunctionOperator.Stat, 27u },
                { AODB.Common.Enums.FunctionOperator.Min, -12 },
                { AODB.Common.Enums.FunctionOperator.Max, -22 },
                { AODB.Common.Enums.FunctionOperator.DamageType, 90u },
            };

            ZoneEngine_New.Core.Inventory.Dat.DatFunction function =
                ItemRdbMapper.ToFunction((int)AODB.Common.Enums.FunctionType.Hit, keyed);

            if (function.Target != 3
                || function.TickCount != 1
                || function.Arguments.Values.Count != 4
                || function.Arguments.Values[0].AsInt32() != 27
                || function.Arguments.Values[1].AsInt32() != -12
                || function.Arguments.Values[2].AsInt32() != -22
                || function.Arguments.Values[3].AsInt32() != 90)
            {
                throw new InvalidOperationException(
                    "Hit function positional args did not match Weak Smiting Missile layout.");
            }
        }

        private static void TestItemsDatDynelTypeRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "rdbdataextractor-items-selftest.dat");
            try
            {
                var templates = new List<ZoneEngine_New.Core.Inventory.Dat.DatItemTemplate>
                {
                    new ZoneEngine_New.Core.Inventory.Dat.DatItemTemplate
                    {
                        ID = 99228,
                        DynelType = 51017,
                        Quality = 1,
                        ItemType = 0,
                    },
                    new ZoneEngine_New.Core.Inventory.Dat.DatItemTemplate
                    {
                        ID = 223372,
                        DynelType = 53051,
                        Quality = 1,
                        ItemType = 0,
                    },
                };

                ItemsDatWriter.Write(path, templates);
                List<ZoneEngine_New.Core.Inventory.Dat.DatItemTemplate> loaded =
                    ZoneEngine_New.Core.Inventory.Dat.ItemsDatReader.Read(path);
                if (loaded.Count != 2
                    || loaded[0].ID != 99228
                    || loaded[0].DynelType != 51017
                    || loaded[1].ID != 223372
                    || loaded[1].DynelType != 53051)
                {
                    throw new InvalidOperationException(
                        "items.dat item/nano DynelType round trip failed.");
                }
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void TestCollisionDatFraming()
        {
            byte[] tilemap = new byte[] { 1, 2, 3, 4 };
            byte[] surface = new byte[] { 9, 8, 7 };
            byte[] framed = PlayfieldCollisionDat.Build(tilemap, surface);
            byte[] parsedTilemap;
            byte[] parsedSurface;
            PlayfieldCollisionDat.Parse(
                framed,
                out parsedTilemap,
                out parsedSurface);

            if (parsedTilemap.Length != tilemap.Length
                || parsedSurface.Length != surface.Length)
            {
                throw new InvalidOperationException(
                    "Collision.dat framing lengths did not round-trip.");
            }

            for (int index = 0; index < tilemap.Length; index++)
            {
                if (parsedTilemap[index] != tilemap[index])
                {
                    throw new InvalidOperationException(
                        "Collision.dat tilemap payload did not round-trip.");
                }
            }

            for (int index = 0; index < surface.Length; index++)
            {
                if (parsedSurface[index] != surface[index])
                {
                    throw new InvalidOperationException(
                        "Collision.dat surface payload did not round-trip.");
                }
            }

            byte[] tilemapOnly = PlayfieldCollisionDat.Build(tilemap, null);
            PlayfieldCollisionDat.Parse(
                tilemapOnly,
                out parsedTilemap,
                out parsedSurface);
            if (parsedTilemap.Length != tilemap.Length || parsedSurface.Length != 0)
            {
                throw new InvalidOperationException(
                    "Collision.dat tilemap-only framing failed.");
            }

            byte[] surfaceOnly = PlayfieldCollisionDat.Build(null, surface);
            PlayfieldCollisionDat.Parse(
                surfaceOnly,
                out parsedTilemap,
                out parsedSurface);
            if (parsedTilemap.Length != 0 || parsedSurface.Length != surface.Length)
            {
                throw new InvalidOperationException(
                    "Collision.dat surface-only framing failed.");
            }
        }
    }
}
