namespace AORebirth.Core.GameData
{
    /// <summary>
    /// Contract for GameData\Playfields\{id}\Rooms.json, written by the RDB extractor from
    /// RDBPlayfield.Rooms. Water meshes are written to Water.dat instead.
    /// Property declaration order defines the serialized field order and must not change.
    /// </summary>
    public sealed class PlayfieldRoomsData
    {
        public const int SupportedSchemaVersion = 1;

        public int SchemaVersion { get; set; }

        public int RecordType { get; set; }

        public int RecordId { get; set; }

        public int RecordVersion { get; set; }

        public PlayfieldRoomEntry[] Rooms { get; set; }
    }

    /// <summary>
    /// One RDBPlayfield room template without Water.
    /// </summary>
    public sealed class PlayfieldRoomEntry
    {
        public int Index { get; set; }

        public int Flags { get; set; }

        public int Unused { get; set; }

        public int TileX1 { get; set; }

        public int TileY1 { get; set; }

        public int TileX2 { get; set; }

        public int TileY2 { get; set; }

        public float[] Center { get; set; }

        public float[] Template { get; set; }

        public int Rotation { get; set; }

        public PlayfieldRoomDoorLink[] DoorConnections { get; set; }

        public string Name { get; set; }

        public byte[] Lightmap { get; set; }

        public PlayfieldRoomAttractor[] CameraAttractors { get; set; }
    }

    public sealed class PlayfieldRoomDoorLink
    {
        public int ZoneLink { get; set; }

        public int PosRot { get; set; }
    }

    public sealed class PlayfieldRoomAttractor
    {
        public float[] Position { get; set; }

        public float[] Orientation { get; set; }

        public float[] Target { get; set; }

        public float Range { get; set; }

        public bool Enabled { get; set; }
    }
}
