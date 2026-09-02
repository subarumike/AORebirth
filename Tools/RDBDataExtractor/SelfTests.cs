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
                || numZonesX != 7
                || numZonesZ != 7
                || cellWorldSize != 36f)
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
    }
}
