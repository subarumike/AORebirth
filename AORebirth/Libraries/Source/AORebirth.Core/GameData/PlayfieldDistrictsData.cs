namespace AORebirth.Core.GameData
{
    /// <summary>
    /// Contract for GameData\Playfields\{id}\Districts.json, written by the RDB district
    /// extractor from PlayfieldDistrictInfo with HashSpawnPoints removed.
    /// Property declaration order defines the serialized field order and must not change.
    /// </summary>
    public sealed class PlayfieldDistrictsData
    {
        public const int SupportedSchemaVersion = 1;

        public int SchemaVersion { get; set; }

        public int RecordType { get; set; }

        public int RecordId { get; set; }

        public int RecordVersion { get; set; }

        public int FormatVersion { get; set; }

        public int ZoneCount { get; set; }

        public byte[] ZoneToDistrictMap { get; set; }

        public PlayfieldDistrictEntry[] Districts { get; set; }
    }

    /// <summary>
    /// One district envelope without HashSpawnPoints (those live in Spawns.json).
    /// </summary>
    public sealed class PlayfieldDistrictEntry
    {
        public int DistrictIndex { get; set; }

        public string Name { get; set; }

        public float[] Centre { get; set; }

        public ushort[] Stats { get; set; }

        public int NpcMinLevel { get; set; }

        public int NpcMaxLevel { get; set; }

        public int LandControlMinLevel { get; set; }

        public int LandControlMaxLevel { get; set; }

        public int RespawnChance { get; set; }

        public int RespawnTime { get; set; }

        public int FightMode { get; set; }

        public PlayfieldDistrictSpawnInfo[] SpawnInfos { get; set; }

        public PlayfieldDistrictMusicPair[] MusicPairs { get; set; }

        public PlayfieldDistrictSpawnPoint[] SpawnPoints { get; set; }
    }

    public sealed class PlayfieldDistrictSpawnInfo
    {
        public uint Hash { get; set; }

        public string HashText { get; set; }

        public int Count { get; set; }
    }

    public sealed class PlayfieldDistrictMusicPair
    {
        public int Id { get; set; }

        public int Value { get; set; }
    }

    public sealed class PlayfieldDistrictSpawnPoint
    {
        public float[] Position { get; set; }

        public float Radius { get; set; }
    }
}
