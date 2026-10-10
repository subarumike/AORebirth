namespace Utility.GameData.Missions
{
    using System.Runtime.Serialization;

    [DataContract]
    internal sealed class MissionSelectionData
    {
        [DataMember(IsRequired = true)] public MissionSelectionManifestData Manifest { get; set; }
        [DataMember(IsRequired = true)] public MissionSelectionPayloadData Payload { get; set; }
    }

    [DataContract]
    internal sealed class MissionSelectionManifestData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public string SourceEvidenceCommit { get; set; }
        [DataMember(IsRequired = true)] public string PayloadSha256 { get; set; }
        [DataMember(IsRequired = true)] public SourceHashData[] FoundationFiles { get; set; }
        [DataMember(IsRequired = true)] public SourceHashData[] Sources { get; set; }
        [DataMember(IsRequired = true)] public SourceHashData Generator { get; set; }
    }

    // Alphabetical DataContract member order is the canonical ASCII payload hash contract.
    [DataContract]
    internal sealed class MissionSelectionPayloadData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public string CatalogKind { get; set; }
        [DataMember(IsRequired = true)] public string SelectionPolicy { get; set; }
        [DataMember(IsRequired = true)] public int RawBackedObservationCount { get; set; }
        [DataMember(IsRequired = true)] public MissionWorldPositionData[] WorldPositions { get; set; }
        [DataMember(IsRequired = true)] public MissionConditionData[] Conditions { get; set; }
    }

    [DataContract]
    internal sealed class MissionWorldPositionData
    {
        [DataMember(IsRequired = true)] public uint IdentityType { get; set; }
        [DataMember(IsRequired = true)] public uint IdentityInstance { get; set; }
        [DataMember(IsRequired = true)] public uint PlayfieldIdentityType { get; set; }
        [DataMember(IsRequired = true)] public int WorldOffsetX { get; set; }
        [DataMember(IsRequired = true)] public int WorldOffsetZ { get; set; }
    }

    [DataContract]
    internal sealed class MissionConditionData
    {
        [DataMember(IsRequired = true)] public int CharacterLevel { get; set; }
        [DataMember(IsRequired = true)] public int ExpectedMissionQl { get; set; }
        [DataMember(IsRequired = true)] public int DifficultyDetent { get; set; }
        [DataMember(IsRequired = true)] public int FactionSide { get; set; }
        [DataMember(IsRequired = true)] public int Breed { get; set; }
        [DataMember(IsRequired = true)] public int Profession { get; set; }
        [DataMember(IsRequired = true)] public int TerminalPlayfieldId { get; set; }
        [DataMember(IsRequired = true)] public uint TerminalIdentityType { get; set; }
        [DataMember(IsRequired = true)] public uint TerminalIdentityInstance { get; set; }
        [DataMember(IsRequired = true)] public int[] SecondarySliderBytes { get; set; }
        [DataMember(IsRequired = true)] public string MissionType { get; set; }
        [DataMember(IsRequired = true)] public MissionIdentityData[] DestinationIdentities { get; set; }
    }

    [DataContract]
    internal sealed class MissionIdentityData
    {
        [DataMember(IsRequired = true)] public uint IdentityType { get; set; }
        [DataMember(IsRequired = true)] public uint IdentityInstance { get; set; }
    }

    // Serialization objects remain internal and never escape into the catalog's immutable API.
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
        [DataMember(IsRequired = true)] public NameProvenanceData NameProvenance { get; set; }
        [DataMember(IsRequired = true)] public uint TemplateInstance { get; set; }
        [DataMember(IsRequired = true)] public PlacementContainerData SourcePlacementContainer { get; set; }
        [DataMember(IsRequired = true)] public long SourceRecordOffset { get; set; }
        [DataMember(IsRequired = true)] public int SourceRecordLength { get; set; }
        [DataMember(IsRequired = true)] public string SourceRecordSha256 { get; set; }
        [DataMember(IsRequired = true)] public string SourceDatabaseSha256 { get; set; }
        [DataMember(IsRequired = true)] public string Classification { get; set; }
        [DataMember(IsRequired = true)] public string ObservationClassification { get; set; }
        [DataMember(IsRequired = true)] public int? OperationalEntranceKey { get; set; }
        [DataMember(IsRequired = true)] public int? EffectiveStatBd { get; set; }
    }

    [DataContract]
    internal sealed class NameProvenanceData
    {
        [DataMember(IsRequired = true)] public string Encoding { get; set; }
        [DataMember(IsRequired = true)] public string ResolutionPath { get; set; }
        [DataMember(IsRequired = true)] public uint ResourceType { get; set; }
        [DataMember(IsRequired = true)] public uint ResourceInstance { get; set; }
        [DataMember(IsRequired = true)] public long SourceOffset { get; set; }
    }

    [DataContract]
    internal sealed class PlacementContainerData
    {
        [DataMember(IsRequired = true)] public uint ResourceType { get; set; }
        [DataMember(IsRequired = true)] public uint ResourceInstance { get; set; }
    }

    [DataContract]
    internal sealed class ObservedCatalogData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public string CatalogKind { get; set; }
        [DataMember(IsRequired = true)] public ObservedDestinationData[] Destinations { get; set; }
    }

    [DataContract]
    internal sealed class ObservedDestinationData
    {
        [DataMember(IsRequired = true)] public uint IdentityType { get; set; }
        [DataMember(IsRequired = true)] public uint IdentityInstance { get; set; }
        [DataMember(IsRequired = true)] public string Classification { get; set; }
        [DataMember(IsRequired = true)] public int ObservationCount { get; set; }
        [DataMember(IsRequired = true)] public int RequestCount { get; set; }
        [DataMember(IsRequired = true)] public int CohortCount { get; set; }
        [DataMember(IsRequired = true)] public int SessionCount { get; set; }
        [DataMember(IsRequired = true)] public int[] ObservedExpectedMissionQls { get; set; }
        [DataMember(IsRequired = true)] public int[] ObservedCharacterLevels { get; set; }
        [DataMember(IsRequired = true)] public string[] ObservedMissionTypes { get; set; }
        [DataMember(IsRequired = true)] public int[] ObservedTerminalPlayfields { get; set; }
        [DataMember(IsRequired = true)] public string[] ObservedFactionSides { get; set; }
        [DataMember(IsRequired = true)] public int[] ObservedFactionSideValues { get; set; }
        [DataMember(IsRequired = true)] public string FactionEvidenceClassification { get; set; }
    }

    [DataContract]
    internal sealed class CatalogManifestData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion { get; set; }
        [DataMember(IsRequired = true)] public string SourceEvidenceCommit { get; set; }
        [DataMember(IsRequired = true)] public int FullPlacementCount { get; set; }
        [DataMember(IsRequired = true)] public int ObservedDestinationCount { get; set; }
        [DataMember(IsRequired = true)] public int ObservedPlayfieldCount { get; set; }
        [DataMember(IsRequired = true)] public int UnobservedPlacementCount { get; set; }
        [DataMember(IsRequired = true)] public int RawBackedObservationCount { get; set; }
        [DataMember(IsRequired = true)] public int MissingRawOffersExcluded { get; set; }
        [DataMember(IsRequired = true)] public int MissingRawOffersPromoted { get; set; }
        [DataMember(IsRequired = true)] public bool DestinationUniquenessWithinCohortRequired { get; set; }
        [DataMember(IsRequired = true)] public int OperationalEntranceKeysResolved { get; set; }
        [DataMember(IsRequired = true)] public CatalogFileData[] Files { get; set; }
        [DataMember(IsRequired = true)] public SourceHashData[] Sources { get; set; }
        [DataMember(IsRequired = true)] public SourceHashData Generator { get; set; }
    }

    [DataContract]
    internal sealed class CatalogFileData
    {
        [DataMember(IsRequired = true)] public string Path { get; set; }
        [DataMember(IsRequired = true)] public string Sha256 { get; set; }
        [DataMember(IsRequired = true)] public int RowCount { get; set; }
    }

    [DataContract]
    internal sealed class SourceHashData
    {
        [DataMember(IsRequired = true)] public string Path { get; set; }
        [DataMember(IsRequired = true)] public string Sha256 { get; set; }
    }
}
