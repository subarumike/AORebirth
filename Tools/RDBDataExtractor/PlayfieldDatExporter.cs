namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using AODB;
    using AODB.Common.RDBObjects;
    using AODB.Common.Structs;
    using AORebirth.Core.GameData;

    internal sealed class PlayfieldDatExporter
    {
        private const int PlayfieldRecordType = 1000001;
        private const int TilemapRecordType = 1000009;

        private readonly RdbController controller;
        private readonly string outputDirectory;

        internal PlayfieldDatExporter(RdbController controller, string outputDirectory)
        {
            if (controller == null)
                throw new ArgumentNullException("controller");

            this.controller = controller;
            this.outputDirectory = outputDirectory;
        }

        internal bool HasPlayfieldRecordType()
        {
            return this.controller.RecordTypeToId.ContainsKey(PlayfieldRecordType);
        }

        internal IEnumerable<int> EnumeratePlayfieldIds()
        {
            if (!this.HasPlayfieldRecordType())
            {
                throw new InvalidOperationException(
                    "RDB playfield record type "
                    + PlayfieldRecordType
                    + " was not found.");
            }

            return this.controller.RecordTypeToId[PlayfieldRecordType].Keys.OrderBy(id => id);
        }

        internal bool TryHasPlayfieldRecord(int playfieldId)
        {
            if (!this.HasPlayfieldRecordType())
                return false;

            return this.controller.RecordTypeToId[PlayfieldRecordType].ContainsKey(playfieldId);
        }

        /// <summary>
        /// Resolves the tilemap resource id from the RDBPlayfield header fields
        /// (<c>Version</c>, <c>Id</c>, 32-byte name, then <c>TilemapId</c>).
        /// Reads raw bytes only so compressed/partial indoor records still resolve.
        /// Falls back to <paramref name="playfieldId"/> when the record is missing
        /// or <c>TilemapId</c> is not positive.
        /// </summary>
        internal int ResolveTilemapId(int playfieldId)
        {
            byte[] raw = TryGetRaw(PlayfieldRecordType, playfieldId);
            if (raw == null)
                return playfieldId;

            byte[] body = StripRdbRecordHeader(raw);
            // Version(4) + Id(4) + Name(32) + TilemapId(4)
            const int tilemapIdOffset = 40;
            if (body == null || body.Length < tilemapIdOffset + 4)
                return playfieldId;

            int tilemapId = BitConverter.ToInt32(body, tilemapIdOffset);
            if (tilemapId <= 0)
                return playfieldId;

            return tilemapId;
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Writes Walls.dat, Dynels.dat, Doors.dat, Destinations.dat, Rooms.json, Water.dat,
        /// and/or Collision.dat. Playfield header fields are merged into metadata.json.
        /// EnvironmentData is not exported. Existing files are skipped unless
        /// <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(int playfieldId, bool overwrite)
        {
            string folder = Path.Combine(
                this.outputDirectory,
                playfieldId.ToString());
            string wallsPath = Path.Combine(folder, GameDataPaths.WallsFileName);
            string dynelsPath = Path.Combine(folder, GameDataPaths.DynelsFileName);
            string doorsPath = Path.Combine(folder, GameDataPaths.DoorsFileName);
            string destinationsPath = Path.Combine(folder, GameDataPaths.DestinationsFileName);
            string roomsPath = Path.Combine(folder, GameDataPaths.RoomsFileName);
            string waterPath = Path.Combine(folder, GameDataPaths.WaterFileName);
            string metadataPath = Path.Combine(folder, GameDataPaths.MetadataFileName);
            string collisionPath = Path.Combine(folder, GameDataPaths.CollisionFileName);
            string surfacesPath = Path.Combine(folder, GameDataPaths.SurfacesFileName);

            int tilemapId = ResolveTilemapId(playfieldId);
            byte[] wallsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldWalls, playfieldId);
            byte[] dynelsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldDynels, playfieldId);
            byte[] doorsPayload = TryGetRaw((int)ResourceTypeId.PlayfieldDoors, playfieldId);
            byte[] destinationsPayload = TryGetRaw(PlayfieldRecordType, playfieldId);
            byte[] tilemapPayload = TryGetRaw(TilemapRecordType, tilemapId);
            byte[] surfacePayload = TryGetRaw((int)ResourceTypeId.SurfaceResource, playfieldId);
            if (tilemapPayload != null)
                tilemapPayload = StripRdbRecordHeader(tilemapPayload);
            if (surfacePayload != null)
                surfacePayload = StripRdbRecordHeader(surfacePayload);
            byte[] collisionPayload = null;
            if ((tilemapPayload != null && tilemapPayload.Length > 0)
                || (surfacePayload != null && surfacePayload.Length > 0))
            {
                collisionPayload = PlayfieldCollisionDat.Build(tilemapPayload, surfacePayload);
            }

            List<PlayfieldSurfaceEntry> cellSurfaces = CollectCellSurfaces(playfieldId);
            byte[] surfacesPayload = cellSurfaces.Count > 0
                ? PlayfieldSurfacesDat.Build(cellSurfaces)
                : null;

            int written = 0;
            int skipped = 0;

            if (wallsPayload != null)
            {
                if (TryWriteFile(wallsPath, StripRdbRecordHeader(wallsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            if (dynelsPayload != null)
            {
                if (TryWriteFile(dynelsPath, StripRdbRecordHeader(dynelsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            if (doorsPayload != null)
            {
                if (TryWriteFile(doorsPath, StripRdbRecordHeader(doorsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            if (destinationsPayload != null)
            {
                if (TryWriteFile(destinationsPath, StripRdbRecordHeader(destinationsPayload), overwrite))
                    written++;
                else
                    skipped++;
            }

            RDBPlayfield playfield = TryGetPlayfield(playfieldId);
            if (playfield != null)
            {
                if (TryWriteRooms(roomsPath, playfield, overwrite))
                    written++;
                else if (File.Exists(roomsPath))
                    skipped++;

                if (TryWriteWater(waterPath, playfield, overwrite))
                    written++;
                else if (File.Exists(waterPath))
                    skipped++;

                if (TryMergePlayfieldMetadata(metadataPath, playfield, overwrite))
                    written++;
                else if (File.Exists(metadataPath))
                    skipped++;
            }

            if (collisionPayload != null)
            {
                if (TryWriteFile(collisionPath, collisionPayload, overwrite))
                    written++;
                else
                    skipped++;
            }

            if (surfacesPayload != null)
            {
                if (TryWriteFile(surfacesPath, surfacesPayload, overwrite))
                    written++;
                else
                    skipped++;
            }

            return new ExportFileCounts(written, skipped);
        }

        /// <summary>
        /// Outdoor playfields store their static geometry as one SurfaceResource per locality cell,
        /// keyed <c>(playfieldId &lt;&lt; 16) | cellId</c>, so the record whose id is the bare
        /// playfield id usually does not exist. Enumerate the RDB index for the whole id range
        /// instead of guessing which cells are populated.
        /// </summary>
        private List<PlayfieldSurfaceEntry> CollectCellSurfaces(int playfieldId)
        {
            List<PlayfieldSurfaceEntry> entries = new List<PlayfieldSurfaceEntry>();
            int surfaceType = (int)ResourceTypeId.SurfaceResource;
            if (!this.controller.RecordTypeToId.ContainsKey(surfaceType))
                return entries;

            List<int> recordIds = new List<int>();
            foreach (int recordId in this.controller.RecordTypeToId[surfaceType].Keys)
            {
                if ((recordId >> 16) == playfieldId && (recordId & 0xFFFF) != 0)
                    recordIds.Add(recordId);
            }

            recordIds.Sort();
            for (int i = 0; i < recordIds.Count; i++)
            {
                byte[] raw = this.controller.GetRaw(surfaceType, recordIds[i]);
                if (raw == null || raw.Length == 0)
                    continue;

                entries.Add(new PlayfieldSurfaceEntry(
                    recordIds[i] & 0xFFFF,
                    StripRdbRecordHeader(raw)));
            }

            return entries;
        }

        /// <summary>
        /// AODB GetRaw includes type+id+version (12 bytes) before the body that
        /// <c>RDBObject.Deserialize</c> expects.
        /// </summary>
        private static byte[] StripRdbRecordHeader(byte[] payload)
        {
            if (payload == null || payload.Length < 12)
                return payload;

            uint typeId = unchecked((uint)BitConverter.ToInt32(payload, 0));
            bool looksLikeRdbHeader =
                typeId >= 0x000F4200 && typeId <= 0x000F42FF
                || typeId >= 0x000F6900 && typeId <= 0x000F69FF
                || typeId == 0x000FDE97;
            if (!looksLikeRdbHeader)
                return payload;

            byte[] body = new byte[payload.Length - 12];
            Buffer.BlockCopy(payload, 12, body, 0, body.Length);
            return body;
        }

        private byte[] TryGetRaw(int recordType, int recordId)
        {
            if (!HasRecord(recordType, recordId))
                return null;

            byte[] raw = this.controller.GetRaw(recordType, recordId);
            if (raw == null || raw.Length == 0)
                return null;

            return raw;
        }

        private bool HasRecord(int recordType, int recordId)
        {
            if (!this.controller.RecordTypeToId.ContainsKey(recordType))
                return false;

            return this.controller.RecordTypeToId[recordType].ContainsKey(recordId);
        }

        private RDBPlayfield TryGetPlayfield(int playfieldId)
        {
            try
            {
                return this.controller.Get<RDBPlayfield>(playfieldId);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryWriteRooms(string path, RDBPlayfield playfield, bool overwrite)
        {
            if (!overwrite && File.Exists(path))
                return false;

            PlayfieldRoomsData rooms = MapRooms(playfield);
            if (rooms == null || rooms.Rooms == null || rooms.Rooms.Length == 0)
                return false;

            WriteJson(path, rooms);
            return true;
        }

        private static bool TryWriteWater(string path, RDBPlayfield playfield, bool overwrite)
        {
            if (!overwrite && File.Exists(path))
                return false;

            List<PlayfieldWaterEntry> entries = CollectWater(playfield);
            if (entries.Count == 0)
                return false;

            return TryWriteFile(path, PlayfieldWaterDat.Build(playfield.Id, entries), overwrite: true);
        }

        private static bool TryMergePlayfieldMetadata(string path, RDBPlayfield playfield, bool overwrite)
        {
            if (!File.Exists(path))
                return false;

            PlayfieldMetaData metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<PlayfieldMetaData>(File.ReadAllText(path), JsonOptions);
            }
            catch
            {
                return false;
            }

            if (metadata == null)
                return false;

            if (!overwrite && metadata.PlayfieldId == playfield.Id)
                return false;

            ApplyPlayfieldHeader(metadata, playfield);
            string error;
            if (!metadata.IsValid(out error))
                return false;

            WriteJson(path, metadata);
            return true;
        }

        private static PlayfieldRoomsData MapRooms(RDBPlayfield playfield)
        {
            if (playfield.Rooms == null || playfield.Rooms.Count == 0)
                return null;

            PlayfieldRoomEntry[] rooms = new PlayfieldRoomEntry[playfield.Rooms.Count];
            for (int i = 0; i < playfield.Rooms.Count; i++)
            {
                RoomTemplate room = playfield.Rooms[i];
                IList<CameraAttractor> attractors = room.CameraAttractors;
                if ((attractors == null || attractors.Count == 0) && playfield.Zones != null)
                {
                    for (int z = 0; z < playfield.Zones.Count; z++)
                    {
                        if (playfield.Zones[z] != null && playfield.Zones[z].Index == room.Index)
                        {
                            attractors = playfield.Zones[z].CameraAttractors;
                            break;
                        }
                    }
                }

                rooms[i] = new PlayfieldRoomEntry
                {
                    Index = room.Index,
                    Flags = room.Flags,
                    Unused = room.Unused,
                    TileX1 = room.TileX1,
                    TileY1 = room.TileY1,
                    TileX2 = room.TileX2,
                    TileY2 = room.TileY2,
                    Center = new float[] { room.CenterX, room.CenterY, room.CenterZ },
                    Template = new float[] { room.TemplateX, room.TemplateY, room.TemplateZ },
                    Rotation = room.Rotation,
                    DoorConnections = MapDoors(room.DoorConnections),
                    Name = room.Name ?? string.Empty,
                    Lightmap = room.Lightmap ?? Array.Empty<byte>(),
                    CameraAttractors = MapAttractors(attractors),
                };
            }

            return new PlayfieldRoomsData
            {
                SchemaVersion = PlayfieldRoomsData.SupportedSchemaVersion,
                RecordType = playfield.RecordType != 0 ? playfield.RecordType : PlayfieldRecordType,
                RecordId = playfield.Id,
                RecordVersion = playfield.RecordVersion,
                Rooms = rooms,
            };
        }

        private static PlayfieldRoomDoorLink[] MapDoors(IList<DoorConnection> source)
        {
            if (source == null || source.Count == 0)
                return new PlayfieldRoomDoorLink[0];

            PlayfieldRoomDoorLink[] mapped = new PlayfieldRoomDoorLink[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                DoorConnection door = source[i] ?? new DoorConnection();
                mapped[i] = new PlayfieldRoomDoorLink
                {
                    ZoneLink = door.ZoneLink,
                    PosRot = door.PosRot,
                };
            }

            return mapped;
        }

        private static PlayfieldRoomAttractor[] MapAttractors(IList<CameraAttractor> source)
        {
            if (source == null || source.Count == 0)
                return new PlayfieldRoomAttractor[0];

            PlayfieldRoomAttractor[] mapped = new PlayfieldRoomAttractor[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                CameraAttractor item = source[i] ?? new CameraAttractor();
                mapped[i] = new PlayfieldRoomAttractor
                {
                    Position = ToArray(item.Position),
                    Orientation = ToArray(item.Orientation),
                    Target = ToArray(item.Target),
                    Range = item.Range,
                    Enabled = item.Enabled,
                };
            }

            return mapped;
        }

        private static List<PlayfieldWaterEntry> CollectWater(RDBPlayfield playfield)
        {
            List<PlayfieldWaterEntry> entries = new List<PlayfieldWaterEntry>();
            AppendWater(entries, -1, playfield.GlobalWaterData);
            if (playfield.Rooms == null)
                return entries;

            for (int i = 0; i < playfield.Rooms.Count; i++)
            {
                RoomTemplate room = playfield.Rooms[i];
                if (room == null)
                    continue;
                AppendWater(entries, room.Index, room.Water);
            }

            return entries;
        }

        private static void AppendWater(
            List<PlayfieldWaterEntry> entries,
            int roomIndex,
            IList<WaterData> source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                WaterData water = source[i];
                float[] vertices = FlattenVertices(water.Vertices);
                short[] triangles = CopyShorts(water.Triangles);
                entries.Add(new PlayfieldWaterEntry(roomIndex, water.Flags, vertices, triangles));
            }
        }

        private static void ApplyPlayfieldHeader(PlayfieldMetaData metadata, RDBPlayfield playfield)
        {
            metadata.PlayfieldId = playfield.Id;
            metadata.PlayfieldName = playfield.Name ?? string.Empty;
            metadata.PlayfieldFormatVersion = playfield.Version;
            metadata.PlayfieldRecordVersion = playfield.RecordVersion;
            metadata.IsIndoor = playfield.IsIndoor;
            metadata.ZoneSize = playfield.ZoneSize;
            metadata.ZoneCount = playfield.ZoneCount;
            metadata.UnknownV9A = playfield.UnknownV9A;
            metadata.UnknownV9B = playfield.UnknownV9B;
            metadata.PlayfieldTypeBits = playfield.PlayfieldTypeBits;
            metadata.UnknownV9C = playfield.UnknownV9C;
            metadata.UnknownV9D = playfield.UnknownV9D;
            metadata.ReservedV9 = playfield.ReservedV9 ?? Array.Empty<byte>();
            metadata.Unknown3 = playfield.Unknown3;
            metadata.Unknown4 = playfield.Unknown4;
        }

        private static float[] FlattenVertices(IList<Vector3> source)
        {
            if (source == null || source.Count == 0)
                return new float[0];

            float[] vertices = new float[source.Count * 3];
            for (int i = 0; i < source.Count; i++)
            {
                Vector3 vertex = source[i];
                vertices[(i * 3)] = vertex.X;
                vertices[(i * 3) + 1] = vertex.Y;
                vertices[(i * 3) + 2] = vertex.Z;
            }

            return vertices;
        }

        private static short[] CopyShorts(IList<short> source)
        {
            if (source == null || source.Count == 0)
                return new short[0];

            short[] copy = new short[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }

        private static float[] ToArray(Vector3 value)
        {
            return new float[] { value.X, value.Y, value.Z };
        }

        private static float[] ToArray(Quaternion value)
        {
            return new float[] { value.X, value.Y, value.Z, value.W };
        }

        private static void WriteJson(string path, object value)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            string json = JsonSerializer.Serialize(value, JsonOptions);
            File.WriteAllText(path, json + Environment.NewLine);
        }

        private static bool TryWriteFile(string path, byte[] payload, bool overwrite)
        {
            if (!overwrite && File.Exists(path))
                return false;

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllBytes(path, payload);
            return true;
        }
    }
}
