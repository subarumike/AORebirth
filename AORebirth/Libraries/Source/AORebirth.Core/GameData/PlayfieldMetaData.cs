namespace AORebirth.Core.GameData
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Contract for GameData\Playfields\{id}\metadata.json, written by the RDB tilemap
    /// extractor and read by the zone runtime.
    /// Property declaration order defines the serialized field order and must not change.
    /// Instances are treated as read-only once loaded; setters exist for deserialization.
    /// </summary>
    public sealed class PlayfieldMetaData
    {
        public const int SupportedSchemaVersion = 1;

        /// <summary>Chunked ground format from the RDB tilemap extractor.</summary>
        public const string ChunkedGroundFormat = "CHGA";

        /// <summary>Embedded PNG ground format. Has no chunk grid.</summary>
        public const string EmbeddedGroundFormat = "GNDA";

        /// <summary>Outdoor locality cell size in world units (legacy AO CELL_SIZE).</summary>
        public const float CellSize = 40f;

        public int SchemaVersion { get; set; }

        public int RecordType { get; set; }

        public int TilemapResource { get; set; }

        public string RawRecordSha256 { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public float TileSize { get; set; }

        public float HeightScale { get; set; }

        public int? ChunkSize { get; set; }

        public int? GridWidth { get; set; }

        public int? BitsPerSample { get; set; }

        public string TilemapFormat { get; set; }

        public string HeightFormat { get; set; }

        public string HeightPixelsSha256 { get; set; }

        public string HeightImage { get; set; }

        public int[] TextureIds { get; set; }

        /// <summary>
        /// Checks the invariants the zone runtime depends on. Does not require chunk grid
        /// fields: a document without a usable grid is valid and resolves to an indoor layout.
        /// </summary>
        public bool IsValid(out string error)
        {
            if (this.SchemaVersion != SupportedSchemaVersion)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "schemaVersion must be {0} but was {1}",
                    SupportedSchemaVersion,
                    this.SchemaVersion);
                return false;
            }

            if (!string.Equals(this.TilemapFormat, ChunkedGroundFormat, StringComparison.Ordinal)
                && !string.Equals(this.TilemapFormat, EmbeddedGroundFormat, StringComparison.Ordinal))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "tilemapFormat must be {0} or {1} but was '{2}'",
                    ChunkedGroundFormat,
                    EmbeddedGroundFormat,
                    this.TilemapFormat ?? string.Empty);
                return false;
            }

            if (this.TilemapResource <= 0)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "tilemapResource must be positive but was {0}",
                    this.TilemapResource);
                return false;
            }

            if (this.Width <= 0 || this.Height <= 0)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "width and height must be positive but were {0}x{1}",
                    this.Width,
                    this.Height);
                return false;
            }

            if (this.TileSize <= 0f)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "tileSize must be positive but was {0}",
                    this.TileSize);
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Derives the outdoor cell grid from playfield dimensions and <see cref="CellSize"/>.
        /// Returns false for embedded ground and when width, height, or tileSize are invalid;
        /// those cases resolve to an indoor layout.
        /// </summary>
        public bool TryGetOutdoorGrid(out int numZonesX, out int numZonesZ, out float cellWorldSize)
        {
            numZonesX = 0;
            numZonesZ = 0;
            cellWorldSize = 0f;

            if (!string.Equals(this.TilemapFormat, ChunkedGroundFormat, StringComparison.Ordinal))
            {
                return false;
            }

            if (this.Width <= 0 || this.Height <= 0 || this.TileSize <= 0f || CellSize <= 0f)
            {
                return false;
            }

            numZonesX = (int)((this.Width * this.TileSize) / CellSize);
            numZonesZ = (int)((this.Height * this.TileSize) / CellSize);
            cellWorldSize = CellSize;
            return numZonesX > 0 && numZonesZ > 0;
        }
    }
}
