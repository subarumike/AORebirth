namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using System;

    /// <summary>
    /// Client <c>BuildingRoomInfo_t</c> (6 bytes) from
    /// <c>ACGBuildingGeneratorData_t</c> room list.
    /// </summary>
    public sealed class BuildingRoomInfo
    {
        /// <summary>Room template / type id (validated &lt; 10001).</summary>
        public ushort RoomId { get; set; }

        /// <summary>Signed floor offset (validated -17..16).</summary>
        public sbyte Floor { get; set; }

        /// <summary>Grid X within generator width (validated &lt; 33).</summary>
        public byte GridX { get; set; }

        /// <summary>Grid Z within generator height (validated &lt; 33).</summary>
        public byte GridZ { get; set; }

        /// <summary>Facing / rotation (validated 0..3).</summary>
        public byte Facing { get; set; }
    }

    /// <summary>
    /// Client <c>ACGBuildingGeneratorData_t</c> network blob (WriteBlob v3 shape)
    /// used for PlayfieldAnarchyF generator payloads on C79F / C7A1 identities.
    /// </summary>
    public sealed class AcgBuildingGeneratorData
    {
        public Identity Identity { get; set; }

        /// <summary>
        /// Int written by <c>DbObject_t::WriteBlob</c> before the ACG subclass body.
        /// Observed 1 on Nascence ACGEntrance, 2 on mission C79F catalogs.
        /// </summary>
        public int DbObjectVersion { get; set; }

        /// <summary>ACG generator stream version; live WriteBlob uses 3.</summary>
        public ushort Version { get; set; }

        /// <summary>Dungeon width in blocks (<c>this+0x28</c>).</summary>
        public ushort Width { get; set; }

        /// <summary>Dungeon height in blocks (<c>this+0x2c</c>).</summary>
        public ushort Height { get; set; }

        /// <summary>Rooms-per-floor related short (<c>this+0x34</c>).</summary>
        public ushort RoomsPerFloor { get; set; }

        /// <summary>Style / template playfield id (<c>this+0xf8</c>).</summary>
        public int Style { get; set; }

        public byte AmbientRed { get; set; }

        public byte AmbientGreen { get; set; }

        public byte AmbientBlue { get; set; }

        public BuildingRoomInfo[] Rooms { get; set; }

        public static bool TryParse(byte[] payload, out AcgBuildingGeneratorData data)
        {
            data = null;
            if (payload == null || payload.Length < 8 + 4 + 2 + 2 + 2 + 2 + 4 + 3 + 4)
                return false;

            try
            {
                using (var stream = new System.IO.MemoryStream(payload, false))
                using (var reader = new Serialization.StreamReader(stream))
                {
                    var identity = reader.ReadIdentity();
                    int type = (int)identity.Type;
                    if (type != 0x0000C79F && type != 0x0000C7A1)
                        return false;

                    int dbObjectVersion = reader.ReadInt32();
                    ushort version = reader.ReadUInt16();
                    if (version != 3)
                        return false;

                    ushort width = reader.ReadUInt16();
                    ushort height = reader.ReadUInt16();
                    ushort roomsPerFloor = reader.ReadUInt16();
                    int style = reader.ReadInt32();
                    byte ambientRed = reader.ReadByte();
                    byte ambientGreen = reader.ReadByte();
                    byte ambientBlue = reader.ReadByte();
                    int roomCount = reader.ReadInt32();
                    if (roomCount < 0 || roomCount > 2500)
                        return false;

                    long remaining = stream.Length - stream.Position;
                    if (remaining != roomCount * 6L)
                        return false;

                    var rooms = new BuildingRoomInfo[roomCount];
                    for (int i = 0; i < roomCount; i++)
                    {
                        ushort roomId = reader.ReadUInt16();
                        sbyte floor = unchecked((sbyte)reader.ReadByte());
                        byte gridX = reader.ReadByte();
                        byte gridZ = reader.ReadByte();
                        byte facing = reader.ReadByte();
                        if (roomId >= 0x2711 || floor < -17 || floor > 16 || gridX >= 0x21 || gridZ >= 0x21
                            || facing >= 4)
                            return false;

                        rooms[i] = new BuildingRoomInfo
                                       {
                                           RoomId = roomId,
                                           Floor = floor,
                                           GridX = gridX,
                                           GridZ = gridZ,
                                           Facing = facing
                                       };
                    }

                    data = new AcgBuildingGeneratorData
                               {
                                   Identity = identity,
                                   DbObjectVersion = dbObjectVersion,
                                   Version = version,
                                   Width = width,
                                   Height = height,
                                   RoomsPerFloor = roomsPerFloor,
                                   Style = style,
                                   AmbientRed = ambientRed,
                                   AmbientGreen = ambientGreen,
                                   AmbientBlue = ambientBlue,
                                   Rooms = rooms
                               };
                    return true;
                }
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new Serialization.StreamWriter(stream))
            {
                writer.WriteIdentity(this.Identity);
                writer.WriteInt32(this.DbObjectVersion);
                writer.WriteUInt16(this.Version);
                writer.WriteUInt16(this.Width);
                writer.WriteUInt16(this.Height);
                writer.WriteUInt16(this.RoomsPerFloor);
                writer.WriteInt32(this.Style);
                writer.WriteByte(this.AmbientRed);
                writer.WriteByte(this.AmbientGreen);
                writer.WriteByte(this.AmbientBlue);
                BuildingRoomInfo[] rooms = this.Rooms ?? new BuildingRoomInfo[0];
                writer.WriteInt32(rooms.Length);
                for (int i = 0; i < rooms.Length; i++)
                {
                    BuildingRoomInfo room = rooms[i] ?? new BuildingRoomInfo();
                    writer.WriteUInt16(room.RoomId);
                    writer.WriteByte(unchecked((byte)room.Floor));
                    writer.WriteByte(room.GridX);
                    writer.WriteByte(room.GridZ);
                    writer.WriteByte(room.Facing);
                }

                return stream.ToArray();
            }
        }
    }
}
