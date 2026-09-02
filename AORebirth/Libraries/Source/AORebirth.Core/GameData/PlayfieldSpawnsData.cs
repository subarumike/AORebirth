namespace AORebirth.Core.GameData
{
    /// <summary>
    /// Contract for GameData\Playfields\{id}\Spawns.json: HashSpawnPoints split out of
    /// PlayfieldDistrictInfo districts. Property declaration order defines the serialized
    /// field order and must not change.
    /// </summary>
    public sealed class PlayfieldSpawnsData
    {
        public const int SupportedSchemaVersion = 1;

        public int SchemaVersion { get; set; }

        public int RecordType { get; set; }

        public int PlayfieldId { get; set; }

        public PlayfieldSpawnEntry[] Spawns { get; set; }
    }

    /// <summary>
    /// One HashSpawnPoint plus the district index it was taken from.
    /// </summary>
    public sealed class PlayfieldSpawnEntry
    {
        public int DistrictIndex { get; set; }

        public uint Hash { get; set; }

        public string HashText { get; set; }

        public uint ManifestHash { get; set; }

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        public int RespawnChance { get; set; }

        public int Flags { get; set; }

        public int RespawnTime { get; set; }

        public uint Version7Extra { get; set; }

        public bool HasOptionalFlagBlock { get; set; }

        public int NativeFlags { get; set; }

        public int MoreFlags { get; set; }

        public int AssistanceRadius { get; set; }

        public int UnknownOptionalU8 { get; set; }

        public int Angle { get; set; }

        public int AngleW { get; set; }

        public float[] Position { get; set; }

        public float Radius { get; set; }

        public PlayfieldRotationSpawnPoint[] AdditionalPoints { get; set; }

        public PlayfieldHashSpawnExtensionBlock Extensions { get; set; }
    }

    public sealed class PlayfieldRotationSpawnPoint
    {
        public int Angle { get; set; }

        public int AngleW { get; set; }

        public float[] Position { get; set; }

        public float Radius { get; set; }
    }

    public sealed class PlayfieldHashSpawnExtensionBlock
    {
        public int Field0 { get; set; }

        public int Field1 { get; set; }

        public int Field2 { get; set; }

        public int Count3F1 { get; set; }

        public PlayfieldHashSpawnExtensionEvent[] Events { get; set; }

        public int TrailingUnknown { get; set; }

        public byte[] OpaqueTail { get; set; }
    }

    public sealed class PlayfieldHashSpawnExtensionEvent
    {
        public int Unknown1 { get; set; }

        public int Unknown2 { get; set; }

        public string Name { get; set; }
    }
}
