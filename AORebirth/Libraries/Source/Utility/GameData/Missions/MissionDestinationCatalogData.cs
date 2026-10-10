namespace Utility.GameData.Missions
{
    using System.Runtime.Serialization;

    // Serialization objects remain internal; runtime callers receive immutable catalog records.
    [DataContract]
    internal sealed class PlacementCatalogData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public string CatalogKind { get; set; }
        [DataMember(IsRequired = true)] public PlacementData[] Placements { get; set; }
    }

    [DataContract]
    internal sealed class PlacementData
    {
        [DataMember(IsRequired = true)] public uint IdentityType { get; set; }
        [DataMember(IsRequired = true)] public uint IdentityInstance { get; set; }
        [DataMember(IsRequired = true)] public int PlayfieldId { get; set; }
        [DataMember(IsRequired = true)] public double LocalX { get; set; }
        [DataMember(IsRequired = true)] public double LocalY { get; set; }
        [DataMember(IsRequired = true)] public double LocalZ { get; set; }
        [DataMember(IsRequired = true)] public uint LocalXBits { get; set; }
        [DataMember(IsRequired = true)] public uint LocalYBits { get; set; }
        [DataMember(IsRequired = true)] public uint LocalZBits { get; set; }
        [DataMember(IsRequired = true)] public double RotationComponent0 { get; set; }
        [DataMember(IsRequired = true)] public double RotationComponent1 { get; set; }
        [DataMember(IsRequired = true)] public double RotationComponent2 { get; set; }
        [DataMember(IsRequired = true)] public double RotationComponent3 { get; set; }
        [DataMember(IsRequired = true)] public string DisplayName { get; set; }
        [DataMember(IsRequired = true)] public string RawNameHex { get; set; }
        [DataMember(IsRequired = true)] public MissionWorldPositionData WorldPos { get; set; }
    }

    [DataContract]
    internal sealed class MissionWorldPositionData
    {
        [DataMember(IsRequired = true)] public uint PlayfieldIdentityType { get; set; }
        [DataMember(IsRequired = true)] public int WorldOffsetX { get; set; }
        [DataMember(IsRequired = true)] public int WorldOffsetZ { get; set; }
    }

    [DataContract]
    internal sealed class MissionDestinationData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public string CatalogKind { get; set; }
        [DataMember(IsRequired = true)] public MissionDestinationPoolData[] Pools { get; set; }
    }

    [DataContract]
    internal sealed class MissionDestinationPoolData
    {
        [DataMember(IsRequired = true)] public int TerminalPlayfieldId { get; set; }
        [DataMember(IsRequired = true)] public int ExpectedMissionQl { get; set; }
        [DataMember(IsRequired = true)] public MissionIdentityData[] DestinationIdentities { get; set; }
    }

    [DataContract]
    internal sealed class MissionIdentityData
    {
        [DataMember(IsRequired = true)] public uint IdentityType { get; set; }
        [DataMember(IsRequired = true)] public uint IdentityInstance { get; set; }
    }
}
