namespace AORebirth.Core.GameData
{
    /// <summary>
    /// Contract for GameData\Playfields\{id}\CharacterAppearance.json: playfield-local
    /// character presentation overrides applied to SimpleCharFullUpdate payloads.
    /// </summary>
    public sealed class PlayfieldCharacterAppearanceData
    {
        public const int SupportedSchemaVersion = 1;

        public int SchemaVersion { get; set; }

        public int PlayfieldId { get; set; }

        public uint MonsterData { get; set; }
    }
}
