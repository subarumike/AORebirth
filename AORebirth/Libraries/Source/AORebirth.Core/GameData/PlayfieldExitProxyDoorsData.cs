namespace AORebirth.Core.GameData
{
    /// <summary>
    /// Contract for GameData\Playfields\{id}\ExitProxyDoors.json: explicit destination
    /// doors that may act as reverse ExitProxy surfaces for this playfield. Missing file means
    /// portal data remains authoritative for the playfield.
    /// </summary>
    public sealed class PlayfieldExitProxyDoorsData
    {
        public const int SupportedSchemaVersion = 1;

        public int SchemaVersion { get; set; }

        public int PlayfieldId { get; set; }

        public int[] DoorInstances { get; set; }
    }
}
