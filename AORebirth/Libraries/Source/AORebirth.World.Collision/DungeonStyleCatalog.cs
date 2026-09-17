namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    using StbImageSharp;

    /// <summary>
    /// Style / template playfield assets used to assemble a dungeon: GNDA/DCGA, room catalog, surfaces.
    /// </summary>
    public sealed class DungeonStyleCatalog
    {
        static readonly JsonSerializerOptions MetaDataJsonOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        DungeonStyleCatalog(
            int styleId,
            PlayfieldMetaData meta,
            byte[] gnda,
            byte[] dcga,
            IReadOnlyList<StyleRoomTemplate> rooms,
            IReadOnlyDictionary<int, byte[]> surfacePayloads)
        {
            StyleId = styleId;
            Meta = meta;
            Gnda = gnda;
            Dcga = dcga;
            Rooms = rooms;
            SurfacePayloads = surfacePayloads;
        }

        public int StyleId { get; }

        public PlayfieldMetaData Meta { get; }

        public byte[] Gnda { get; }

        public byte[] Dcga { get; }

        public IReadOnlyList<StyleRoomTemplate> Rooms { get; }

        public IReadOnlyDictionary<int, byte[]> SurfacePayloads { get; }

        public int MapWidth => Meta.Width;

        public int MapHeight => Meta.Height;

        public float TileSize => Meta.TileSize > 0f ? Meta.TileSize : 2f;

        public float HeightScale => Meta.HeightScale > 0f ? Meta.HeightScale : 1f;

        public static DungeonStyleCatalog CreateForTest(
            int styleId,
            PlayfieldMetaData meta,
            byte[] gnda,
            byte[] dcga,
            IReadOnlyList<StyleRoomTemplate> rooms,
            IReadOnlyDictionary<int, byte[]>? surfacePayloads = null)
        {
            ArgumentNullException.ThrowIfNull(meta);
            ArgumentNullException.ThrowIfNull(gnda);
            ArgumentNullException.ThrowIfNull(dcga);
            ArgumentNullException.ThrowIfNull(rooms);
            return new DungeonStyleCatalog(
                styleId,
                meta,
                gnda,
                dcga,
                rooms,
                surfacePayloads ?? new Dictionary<int, byte[]>());
        }

        public static DungeonStyleCatalog Load(string gameDataRoot, int styleId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            if (styleId <= 0)
                throw new ArgumentOutOfRangeException(nameof(styleId));

            if (!TryLoad(gameDataRoot, styleId, out DungeonStyleCatalog? catalog) || catalog == null)
                throw new InvalidDataException("Dungeon style catalog could not be loaded for playfield " + styleId + ".");

            return catalog;
        }

        public static bool TryLoad(string gameDataRoot, int styleId, out DungeonStyleCatalog? catalog)
        {
            catalog = null;
            if (string.IsNullOrWhiteSpace(gameDataRoot) || styleId <= 0)
                return false;

            if (!TryReadMetaData(gameDataRoot, styleId, out PlayfieldMetaData? meta) || meta == null)
                return false;

            IReadOnlyList<StyleRoomTemplate> rooms = ReadRooms(gameDataRoot, styleId);
            if (rooms.Count == 0)
                return false;

            string folder = Path.Combine(gameDataRoot, GameDataPaths.PlayfieldRelativeDirectory(styleId));
            byte[] gnda = TryReadGreyPng(Path.Combine(folder, "GNDA.png"), meta.Width, meta.Height);
            byte[] dcga = TryReadGreyPng(Path.Combine(folder, "DCGA.png"), meta.Width, meta.Height);
            catalog = new DungeonStyleCatalog(
                styleId,
                meta,
                gnda,
                dcga,
                rooms,
                ReadSurfaces(gameDataRoot, styleId));
            return true;
        }

        static bool TryReadMetaData(string gameDataRoot, int styleId, out PlayfieldMetaData? meta)
        {
            meta = null;
            string metadataPath = Path.Combine(
                gameDataRoot,
                GameDataPaths.PlayfieldMetadataRelativePath(styleId));
            if (!File.Exists(metadataPath))
                return false;

            try
            {
                meta = JsonSerializer.Deserialize<PlayfieldMetaData>(
                    File.ReadAllText(metadataPath),
                    MetaDataJsonOptions);
            }
            catch
            {
                return false;
            }

            return meta != null && meta.IsValid(out _);
        }

        static byte[] TryReadGreyPng(string path, int width, int height)
        {
            if (!File.Exists(path) || width <= 0 || height <= 0)
                return Array.Empty<byte>();

            ImageResult image;
            using (FileStream stream = File.OpenRead(path))
                image = ImageResult.FromStream(stream, ColorComponents.Grey);

            if (image.Width != width || image.Height != height)
                return Array.Empty<byte>();

            return image.Data;
        }

        static IReadOnlyList<StyleRoomTemplate> ReadRooms(string gameDataRoot, int styleId)
        {
            string roomsPath = Path.Combine(
                gameDataRoot,
                GameDataPaths.PlayfieldRoomsRelativePath(styleId));
            if (File.Exists(roomsPath))
            {
                try
                {
                    PlayfieldRoomsData? data = JsonSerializer.Deserialize<PlayfieldRoomsData>(
                        File.ReadAllText(roomsPath),
                        MetaDataJsonOptions);
                    if (data?.Rooms != null && data.Rooms.Length > 0)
                        return MapRoomEntries(data.Rooms);
                }
                catch
                {
                    return Array.Empty<StyleRoomTemplate>();
                }
            }

            return Array.Empty<StyleRoomTemplate>();
        }

        static IReadOnlyList<StyleRoomTemplate> MapRoomEntries(PlayfieldRoomEntry[] entries)
        {
            var rooms = new List<StyleRoomTemplate>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                if (StyleRoomTemplate.TryFromRoomEntry(entries[i], out StyleRoomTemplate? room) && room != null)
                    rooms.Add(room);
            }

            return rooms;
        }

        static IReadOnlyDictionary<int, byte[]> ReadSurfaces(string gameDataRoot, int styleId)
        {
            string surfacesPath = Path.Combine(
                gameDataRoot,
                GameDataPaths.PlayfieldSurfacesRelativePath(styleId));
            var map = new Dictionary<int, byte[]>();
            if (!File.Exists(surfacesPath))
                return map;

            List<PlayfieldSurfaceEntry> entries;
            try
            {
                entries = PlayfieldSurfacesDat.Parse(File.ReadAllBytes(surfacesPath));
            }
            catch
            {
                return map;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Payload == null || entries[i].Payload.Length == 0)
                    continue;
                map[entries[i].CellId] = entries[i].Payload;
            }

            return map;
        }
    }
}
