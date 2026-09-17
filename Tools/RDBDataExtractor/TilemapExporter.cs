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
        private const string DcgaImageName = "DCGA.png";
        private const string HcdaImageName = "HCDA.png";
        private const string TahaImageName = "TAHA.png";

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
                throw new ArgumentNullException("controller");

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
                return false;

            return this.controller.RecordTypeToId[TilemapRecordType].ContainsKey(tilemapId);
        }

        /// <summary>
        /// Writes metadata.json and tilemap PNG companions into the playfield folder.
        /// Loads RDB tilemap <paramref name="tilemapId"/> (may differ from
        /// <paramref name="playfieldFolderId"/>). Existing files are skipped unless
        /// <paramref name="overwrite"/> is true. Missing indoor sibling PNGs are still written.
        /// </summary>
        internal ExportFileCounts Export(int tilemapId, int playfieldFolderId, bool overwrite)
        {
            string folder = Path.Combine(this.outputDirectory, playfieldFolderId.ToString());
            string metadataPath = Path.Combine(folder, GameDataPaths.MetadataFileName);
            string gndaPath = Path.Combine(folder, GndaImageName);
            string chgaPath = Path.Combine(folder, ChgaImageName);

            byte[] raw = this.controller.GetRaw(TilemapRecordType, tilemapId);
            if (raw == null || raw.Length == 0)
                throw new InvalidOperationException("Tilemap raw record was empty.");

            Directory.CreateDirectory(folder);

            Tilemap tilemap = this.controller.Get<Tilemap>(tilemapId);
            if (tilemap != null && tilemap.IsIndoor)
                return ExportIndoor(tilemapId, tilemap, raw, folder, metadataPath, gndaPath, chgaPath, overwrite);

            ChunkedHeightmapFlattener.ChunkedGroundData chunked;
            if (ChunkedHeightmapFlattener.TryParseChunkedGround(raw, out chunked))
                return ExportChunked(tilemapId, chunked, raw, metadataPath, gndaPath, chgaPath, overwrite);

            // Fallback for older packages / partial indoor deserializes: scrape embedded height PNG.
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
                return ExportLegacyGnda(
                    tilemapId,
                    gndaHeightmap,
                    width,
                    height,
                    tileSize,
                    heightScale,
                    raw,
                    metadataPath,
                    gndaPath,
                    chgaPath,
                    overwrite);
            }

            throw new InvalidOperationException(
                "Tilemap "
                + tilemapId
                + " is not a supported GNDA or CHGA format.");
        }

        private ExportFileCounts ExportIndoor(
            int tilemapId,
            Tilemap tilemap,
            byte[] raw,
            string folder,
            string metadataPath,
            string gndaPath,
            string chgaPath,
            bool overwrite)
        {
            int width = (int)tilemap.MapWidth;
            int height = (int)tilemap.MapHeight;
            float tileSize = tilemap.TileScale > 0f ? tilemap.TileScale : tilemap.MapScale;
            float heightScale = tilemap.VerticalScale > 0f ? tilemap.VerticalScale : tilemap.HeightMod;
            if (tileSize <= 0f)
                tileSize = Tilemap.DefaultIndoorTileScale;
            if (heightScale <= 0f)
                heightScale = 1f;

            if (tilemap.HeightChannelPng == null || tilemap.HeightChannelPng.Length == 0)
            {
                throw new InvalidOperationException(
                    "Indoor tilemap " + tilemapId + " has no DHGA/height PNG.");
            }

            int written = 0;
            int skipped = 0;

            if (overwrite)
                DeleteIfExists(chgaPath);

            written += WriteRawPngIfNeeded(
                Path.Combine(folder, DcgaImageName),
                tilemap.TileTypePng,
                overwrite,
                ref skipped);
            written += WriteRawPngIfNeeded(gndaPath, tilemap.HeightChannelPng, overwrite, ref skipped);
            written += WriteRawPngIfNeeded(
                Path.Combine(folder, HcdaImageName),
                tilemap.CornerHeightPng,
                overwrite,
                ref skipped);

            if (tilemap.TahaChannelPng != null && tilemap.TahaChannelPng.Length > 0)
            {
                written += WriteRawPngIfNeeded(
                    Path.Combine(folder, TahaImageName),
                    tilemap.TahaChannelPng,
                    overwrite,
                    ref skipped);
            }

            if (tilemap.MaterialPngs != null)
            {
                for (int index = 0; index < tilemap.MaterialPngs.Count; index++)
                {
                    string name = "XTHA" + index + ".png";
                    written += WriteRawPngIfNeeded(
                        Path.Combine(folder, name),
                        tilemap.MaterialPngs[index],
                        overwrite,
                        ref skipped);
                }
            }

            if (overwrite || !File.Exists(metadataPath))
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
                    HeightPixelsSha256 = HashHelper.Sha256Hex(tilemap.HeightChannel),
                    HeightImage = GndaImageName,
                    TextureIds = ConvertTextureIds(tilemap.TextureIds),
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

        private ExportFileCounts ExportChunked(
            int tilemapId,
            ChunkedHeightmapFlattener.ChunkedGroundData chunked,
            byte[] raw,
            string metadataPath,
            string gndaPath,
            string chgaPath,
            bool overwrite)
        {
            bool writeMetadata = overwrite || !File.Exists(metadataPath);
            bool writeHeight = overwrite || !File.Exists(chgaPath);
            if (!writeMetadata && !writeHeight)
                return new ExportFileCounts(0, 2);

            int written = 0;
            int skipped = 0;
            if (writeHeight)
            {
                if (overwrite)
                    DeleteIfExists(gndaPath);

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

        private ExportFileCounts ExportLegacyGnda(
            int tilemapId,
            EmbeddedPngHeightmapDecoder.DecodedPng gndaHeightmap,
            int width,
            int height,
            float tileSize,
            float heightScale,
            byte[] raw,
            string metadataPath,
            string gndaPath,
            string chgaPath,
            bool overwrite)
        {
            bool writeMetadata = overwrite || !File.Exists(metadataPath);
            bool writeHeight = overwrite || !File.Exists(gndaPath);
            if (!writeMetadata && !writeHeight)
                return new ExportFileCounts(0, 2);

            int written = 0;
            int skipped = 0;
            if (writeHeight)
            {
                if (overwrite)
                    DeleteIfExists(chgaPath);

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

        private static int WriteRawPngIfNeeded(
            string path,
            byte[] pngBytes,
            bool overwrite,
            ref int skipped)
        {
            if (pngBytes == null || pngBytes.Length == 0)
                return 0;

            if (!overwrite && File.Exists(path))
            {
                skipped++;
                return 0;
            }

            File.WriteAllBytes(path, pngBytes);
            return 1;
        }

        private int[] ReadTextureIds(int tilemapId)
        {
            Tilemap tilemap = this.controller.Get<Tilemap>(tilemapId);
            if (tilemap == null)
                return new int[0];

            return ConvertTextureIds(tilemap.TextureIds);
        }

        private static int[] ConvertTextureIds(short[] textureIds)
        {
            if (textureIds == null || textureIds.Length == 0)
                return new int[0];

            int[] converted = new int[textureIds.Length];
            for (int index = 0; index < textureIds.Length; index++)
                converted[index] = textureIds[index];

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
                File.Delete(path);
        }
    }
}
