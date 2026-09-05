namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
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
            TestCollisionDatFraming();
            Console.WriteLine("RDBDataExtractor self-test PASS");
            return true;
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
                || GameDataPaths.CollisionFileName != "Collision.dat")
            {
                throw new InvalidOperationException(
                    "Playfield dat file names were unexpected.");
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
