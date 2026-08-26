namespace AORebirth.Core.Playfields.OfficialPlacements
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// One normalized official static playfield-placement evidence record.
    /// Identity, behavior readiness, and runtime activation remain independently governed.
    /// </summary>
    internal sealed class OfficialPlayfieldPlacement
    {
        public string OfficialSpawnRecordId { get; set; }

        public string SourceClientVariant { get; set; }

        public string SourceClientBuild { get; set; }

        public int? ResourceType { get; set; }

        public int? ResourceInstance { get; set; }

        public int? PlayfieldId { get; set; }

        public int? DistrictIndex { get; set; }

        public string DistrictName { get; set; }

        public int? DistrictRecordOrdinal { get; set; }

        public long? ResourceOffset { get; set; }

        public long? RecordOffset { get; set; }

        public int? SerializedSize { get; set; }

        public double? PositionX { get; set; }

        public double? PositionY { get; set; }

        public double? PositionZ { get; set; }

        public int? LevelMinimum { get; set; }

        public int? LevelMaximum { get; set; }

        public double? Radius { get; set; }

        public int? RotationMidEncoded { get; set; }

        public int? RotationWidthEncoded { get; set; }

        public int? RespawnChance { get; set; }

        public double? RespawnTime { get; set; }

        public int? AssistanceRadius { get; set; }

        public int? NativeFlags { get; set; }

        public int? MoreFlags { get; set; }

        public int? SerializedOptionalFlags { get; set; }

        public int? UnknownOptionalU8 { get; set; }

        public string CanonicalAcgHashText { get; set; }

        public string OfficialAcgHashWireBytes { get; set; }

        public uint? OfficialAcgHashNativeUInt32 { get; set; }

        public string ParseStatus { get; set; }

        public Dictionary<string, object> UnknownFields { get; set; }

        public int? SourceNpcId { get; set; }

        public string ExistingAoRebirthProfile { get; set; }

        public bool? CurrentRuntimeActive { get; set; }

        public bool? PlacementKnown { get; set; }

        public bool? IdentityResolved { get; set; }

        public bool? BehaviorReady { get; set; }

        public string IdentityResolutionStatus { get; set; }

        public string BehaviorReadiness { get; set; }

        public bool? RuntimeActivationAuthorized { get; set; }

        public string ResolvedMobTemplateHash { get; set; }

        public int? ResolvedMobTemplateId { get; set; }

        public string ResolvedMobTemplateName { get; set; }

        public int? ResolvedMonsterData { get; set; }

        public string MobTemplateResolutionStatus { get; set; }

        public string MobTemplateEvidenceSource { get; set; }
    }

    /// <summary>
    /// Typed metadata for one official district, including districts with zero placements.
    /// </summary>
    internal sealed class OfficialPlayfieldPlacementDistrict
    {
        public int? DistrictIndex { get; set; }

        public string DistrictName { get; set; }

        public long? DistrictRecordOffset { get; set; }

        public int? DistrictSerializedSize { get; set; }

        public int? HashSpawnRecordCount { get; set; }

        public string OfficialDistrictId { get; set; }

        public string OfficialResourceId { get; set; }

        public Dictionary<string, int> OtherCollectionCountsWhereDecoded { get; set; }

        public string RecordSha256 { get; set; }

        public Dictionary<string, object> UnknownFields { get; set; }
    }

    /// <summary>
    /// Serialization envelope for one generated playfield shard.
    /// </summary>
    internal sealed class OfficialPlayfieldPlacementShard
    {
        public int? SchemaVersion { get; set; }

        public string SourceClientVariant { get; set; }

        public string SourceClientBuild { get; set; }

        public int? ResourceType { get; set; }

        public int? ResourceInstance { get; set; }

        public int? PlayfieldId { get; set; }

        public int? FormatVersion { get; set; }

        public string ParseStatus { get; set; }

        public string ParseError { get; set; }

        public int? DistrictCount { get; set; }

        public int? OfficialSpawnCount { get; set; }

        public Dictionary<string, object> UnknownFields { get; set; }

        public OfficialPlayfieldPlacementDistrict[] Districts { get; set; }

        public OfficialPlayfieldPlacement[] Records { get; set; }
    }

    internal sealed class OfficialPlayfieldPlacementCorpusManifest
    {
        public int? SchemaVersion { get; set; }

        public string CorpusVersion { get; set; }

        public string SourceClientVariant { get; set; }

        public string SourceClientBuild { get; set; }

        public int? ResourceType { get; set; }

        public string SourceManifestSha256 { get; set; }

        public string IndexSha256 { get; set; }

        public string SummarySha256 { get; set; }

        public string AcgHashInventorySha256 { get; set; }

        public OfficialPlayfieldPlacementCorpusMetrics Metrics { get; set; }

        public int[] ParserLimitedPlayfieldIds { get; set; }

        public OfficialPlayfieldPlacementCorpusPolicy Policy { get; set; }

        public OfficialPlayfieldPlacementManifestEntry[] Playfields { get; set; }
    }

    internal sealed class OfficialPlayfieldPlacementCorpusMetrics
    {
        public int? ResourceCount { get; set; }

        public int? ParsedResourceCount { get; set; }

        public int? ParserLimitedResourceCount { get; set; }

        public int? DistrictCount { get; set; }

        public int? PlacementCount { get; set; }

        public int? UniqueAcgHashCount { get; set; }

        public int? RuntimeActivationAuthorizedCount { get; set; }
    }

    internal sealed class OfficialPlayfieldPlacementCorpusPolicy
    {
        public bool? MassPlacementActivation { get; set; }

        public bool? UnresolvedAcgHashActivated { get; set; }

        public bool? ExistingRuntimeBehaviorChanged { get; set; }
    }

    internal sealed class OfficialPlayfieldPlacementManifestEntry
    {
        public int? PlayfieldId { get; set; }

        public string Path { get; set; }

        public string ParseStatus { get; set; }

        public int? DistrictCount { get; set; }

        public int? PlacementCount { get; set; }

        public string SourceResourceSha256 { get; set; }

        public string ShardSha256 { get; set; }

        public int? RuntimeActivationAuthorizedCount { get; set; }
    }
}
