namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AODB;
    using AODB.Common.RDBObjects;
    using AORebirth.Core.GameData;

    internal sealed class TilemapExporter
    {
        private const int TilemapRecordType = 1000009;
        private const string GndaImageName = "GNDA.png";
        private const string ChgaImageName = "CHGA.png";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly RdbController controller;
        private readonly string outputDirectory;

        internal TilemapExporter(RdbController controller, string outputDirectory)
        {
            if (controller == null)
            {
                throw new ArgumentNullException("controller");
            }

            this.controller = controller;
            this.outputDirectory = outputDirectory;
        }

        internal bool HasTilemapRecordType()
        {
            return this.controller.RecordTypeToId.ContainsKey(TilemapRecordType);
        }

        internal IEnumerable<int> EnumerateTilemapIds()
        {
            if (!this.HasTilemapRecordType())
            {
                throw new InvalidOperationException(
                    "RDB tilemap record type "
                    + TilemapRecordType
                    + " was not found.");
            }

            return this.controller.RecordTypeToId[TilemapRecordType].Keys.OrderBy(id => id);
        }

        internal bool TryHasTilemapRecord(int tilemapId)
        {
            if (!this.HasTilemapRecordType())
            {
                return false;
            }

            return this.controller.RecordTypeToId[TilemapRecordType].ContainsKey(tilemapId);
        }

        /// <summary>
        /// Writes metadata.json and/or the height PNG. Existing files are skipped unless
        /// <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(int tilemapId, bool overwrite)
        {
            string folder = GetTilemapFolder(tilemapId);
            string metadataPath = Path.Combine(folder, GameDataPaths.MetadataFileName);
            string gndaPath = Path.Combine(folder, GndaImageName);
            string chgaPath = Path.Combine(folder, ChgaImageName);
            bool heightExists = File.Exists(gndaPath) || File.Exists(chgaPath);
            bool writeMetadata = overwrite || !File.Exists(metadataPath);
            bool writeHeight = overwrite || !heightExists;
            if (!writeMetadata && !writeHeight)
            {
                return new ExportFileCounts(0, 2);
            }

            byte[] raw = this.controller.GetRaw(TilemapRecordType, tilemapId);
            if (raw == null || raw.Length == 0)
            {
                throw new InvalidOperationException("Tilemap raw record was empty.");
            }

            Directory.CreateDirectory(folder);

            EmbeddedPngHeightmapDecoder.DecodedPng gndaHeightmap;
            int width;
            int height;
            float tileSize;
            float heightScale;
            if (EmbeddedPngHeightmapDecoder.TryDecodeGndaHeightmap(
                    raw,
                    out gndaHeightmap,
                    out width,
                    out height,
                    out tileSize,
                    out heightScale))
            {
                int written = 0;
                int skipped = 0;
                if (writeHeight)
                {
                    if (overwrite)
                    {
                        DeleteIfExists(chgaPath);
                    }

                    HeightmapPngWriter.WriteGndaPng(
                        gndaPath,
                        gndaHeightmap.Pixels,
                        width,
                        height);
                    written++;
                }
                else
                {
                    skipped++;
                }

                if (writeMetadata)
                {
                    PlayfieldMetaData metadata = new PlayfieldMetaData
                    {
                        SchemaVersion = PlayfieldMetaData.SupportedSchemaVersion,
                        RecordType = TilemapRecordType,
                        TilemapResource = tilemapId,
                        RawRecordSha256 = HashHelper.Sha256Hex(raw),
                        Width = width,
                        Height = height,
                        TileSize = tileSize,
                        HeightScale = heightScale,
                        TilemapFormat = PlayfieldMetaData.EmbeddedGroundFormat,
                        HeightFormat = "embeddedPng8",
                        HeightPixelsSha256 = HashHelper.Sha256Hex(gndaHeightmap.Pixels),
                        HeightImage = GndaImageName,
                        TextureIds = ReadTextureIds(tilemapId),
                    };
                    WriteMetadata(metadataPath, metadata);
                    written++;
                }
                else
                {
                    skipped++;
                }

                return new ExportFileCounts(written, skipped);
            }

            ChunkedHeightmapFlattener.ChunkedGroundData chunked;
            if (ChunkedHeightmapFlattener.TryParseChunkedGround(raw, out chunked))
            {
                int written = 0;
                int skipped = 0;
                if (writeHeight)
                {
                    if (overwrite)
                    {
                        DeleteIfExists(gndaPath);
                    }

                    HeightmapPngWriter.WriteChgaPng(
                        chgaPath,
                        chunked.Heights,
                        chunked.Width,
                        chunked.Height);
                    written++;
                }
                else
                {
                    skipped++;
                }

                if (writeMetadata)
                {
                    byte[] heightBytes = ChunkedHeightsToBytes(chunked.Heights);
                    PlayfieldMetaData metadata = new PlayfieldMetaData
                    {
                        SchemaVersion = PlayfieldMetaData.SupportedSchemaVersion,
                        RecordType = TilemapRecordType,
                        TilemapResource = tilemapId,
                        RawRecordSha256 = HashHelper.Sha256Hex(raw),
                        Width = chunked.Width,
                        Height = chunked.Height,
                        TileSize = chunked.TileSize,
                        HeightScale = chunked.HeightScale,
                        ChunkSize = chunked.ChunkSize,
                        GridWidth = chunked.GridWidth,
                        BitsPerSample = chunked.BitsPerSample,
                        TilemapFormat = PlayfieldMetaData.ChunkedGroundFormat,
                        HeightFormat = "chunkedUshortGreyAlpha",
                        HeightPixelsSha256 = HashHelper.Sha256Hex(heightBytes),
                        HeightImage = ChgaImageName,
                        TextureIds = ConvertTextureIds(chunked.TextureIds),
                    };
                    WriteMetadata(metadataPath, metadata);
                    written++;
                }
                else
                {
                    skipped++;
                }

                return new ExportFileCounts(written, skipped);
            }

            throw new InvalidOperationException(
                "Tilemap "
                + tilemapId
                + " is not a supported GNDA or CHGA format.");
        }

        private string GetTilemapFolder(int tilemapId)
        {
            return Path.Combine(this.outputDirectory, tilemapId.ToString());
        }

        private int[] ReadTextureIds(int tilemapId)
        {
            Tilemap tilemap = this.controller.Get<Tilemap>(tilemapId);
            if (tilemap == null)
            {
                return new int[0];
            }

            System.Reflection.FieldInfo textureIdsField = typeof(Tilemap).GetField(
                "TextureIds",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
            short[] textureIds = (short[])textureIdsField.GetValue(tilemap);
            return ConvertTextureIds(textureIds);
        }

        private static int[] ConvertTextureIds(short[] textureIds)
        {
            if (textureIds == null || textureIds.Length == 0)
            {
                return new int[0];
            }

            int[] converted = new int[textureIds.Length];
            for (int index = 0; index < textureIds.Length; index++)
            {
                converted[index] = textureIds[index];
            }

            return converted;
        }

        private static byte[] ChunkedHeightsToBytes(ushort[] heights)
        {
            byte[] bytes = new byte[heights.Length * 2];
            for (int index = 0; index < heights.Length; index++)
            {
                ushort value = heights[index];
                bytes[(index * 2)] = (byte)(value & 0xFF);
                bytes[(index * 2) + 1] = (byte)(value >> 8);
            }

            return bytes;
        }

        private static void WriteMetadata(string metadataPath, PlayfieldMetaData metadata)
        {
            string error;
            if (!metadata.IsValid(out error))
            {
                throw new InvalidOperationException(
                    "Tilemap "
                    + metadata.TilemapResource
                    + " produced invalid metadata: "
                    + error);
            }

            string json = JsonSerializer.Serialize(metadata, JsonOptions);
            File.WriteAllText(metadataPath, json + Environment.NewLine);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
