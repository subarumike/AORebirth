using System.Text.Json;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal sealed record HistoricalEvidenceBundle
{
    public int FormatVersion { get; init; } = 1;
    public string ItemsDatSha256 { get; init; } = "";
    public HistoricalSource[] Sources { get; init; } = [];
    public HistoricalPatch[] Patches { get; init; } = [];
    public HistoricalAssertion[] Assertions { get; init; } = [];
    public string[] Gaps { get; init; } = [];
}

internal sealed record HistoricalSource
{
    public string Id { get; init; } = "";
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Sha256 { get; init; }
    public string? CachePath { get; init; }
    public string Status { get; init; } = "UNKNOWN";
    public string? SourceDate { get; init; }
    public string? Scope { get; init; }
}

internal sealed record HistoricalPatch
{
    public string Version { get; init; } = "";
    public string? DatabaseBuild { get; init; }
    public string? BuildDate { get; init; }
    public string[] ReleaseDates { get; init; } = [];
    public string[] SourceUrls { get; init; } = [];
    public string Availability { get; init; } = "UNKNOWN";
    public string ItemChangesStatus { get; init; } = "UNRECOVERED";
    public int? NewTemplateCount { get; init; }
    public int? ModifiedTemplateCount { get; init; }
    public int? DeletedTemplateCount { get; init; }
    public bool ExplicitNoTemplateChanges { get; init; }
    public string[] Notes { get; init; } = [];
}

internal sealed record HistoricalAssertion
{
    public int TemplateId { get; init; }
    public string[] ContentAssociations { get; init; } = [];
    public HistoricalAcquisition[] AcquisitionSources { get; init; } = [];
    public string[] Roles { get; init; } = [];
    public string ChestStatus { get; init; } = "NO_VERIFIED_CHEST_SOURCE";
    public string Confidence { get; init; } = "UNVERIFIED";
    public string IdentityMatch { get; init; } = "UNVERIFIED";
    public string[] SourceUrls { get; init; } = [];
    public string[] SourceIds { get; init; } = [];
    public string? Reason { get; init; }
    public int? DocumentedQuality { get; init; }
    public int? DocumentedMinimumQL { get; init; }
    public int? DocumentedMaximumQL { get; init; }
    public bool RetailChestAbsenceProven { get; init; }
    public string? DocumentedIntroductionPatch { get; init; }
    public string? DocumentedIntroductionScope { get; init; }
}

internal sealed record HistoricalAcquisition
{
    public string Kind { get; init; } = "UNKNOWN";
    public string? Location { get; init; }
    public string TemporalScope { get; init; } = "DOCUMENTATION_OBSERVATION_NOT_ALL_HISTORY";
    public bool Exclusive { get; init; }
    public string[] SourceUrls { get; init; } = [];
    public string Confidence { get; init; } = "UNVERIFIED";
}

internal sealed record HistoricalObservation
{
    public int TemplateId { get; init; }
    public string PatchVersion { get; init; } = "";
    public string? DatabaseBuild { get; init; }
    public string Kind { get; init; } = "UNKNOWN";
    public string[] SourceUrls { get; init; } = [];
    public string[] SourceHashes { get; init; } = [];
    public string Confidence { get; init; } = "UNVERIFIED";
    public JsonElement? Detail { get; init; }
}

internal sealed record HistoricalRecord
{
    public int SourceRecordIndex { get; init; }
    public int TemplateId { get; init; }
    public string? Name { get; init; }
    public int StoredQuality { get; init; }
    public int? MinimumSupportedQuality { get; init; }
    public int? MaximumSupportedQuality { get; init; }
    public CatalogQualityEvidence[] QualityRangeEvidence { get; init; } = [];
    public CategoryMetadata? Category { get; init; }
    public int? CurrentCanFlags { get; init; }
    public bool? CurrentCanCarry { get; init; }
    public string PrimaryClassification { get; init; } = "Unknown";
    public string[] ContentAssociations { get; init; } = [];
    public string ContentConfidence { get; init; } = "UNVERIFIED";
    public string[] RequiredExpansionAssociations { get; init; } = [];
    public ClassificationEvidence[] RequirementEvidence { get; init; } = [];
    public string? FirstKnownPatch { get; init; }
    public string? FirstRecordedAdditionPatch { get; init; }
    public string ActualIntroductionStatus { get; init; } = "UNVERIFIED";
    public string? DocumentedIntroductionPatch { get; init; }
    public HistoricalObservation[] PatchObservations { get; init; } = [];
    public HistoricalAcquisition[] AcquisitionSources { get; init; } = [];
    public HistoricalAssertion[] ClassificationEvidence { get; init; } = [];
    public string[] SpecialPurposeRoles { get; init; } = [];
    public string ChestDisposition { get; init; } = "NO_POSITIVE_CHEST_EVIDENCE";
    public bool VerifiedSpecialPurposeExclusion { get; init; }
    public bool RetailChestAbsenceProven { get; init; }
    public bool VerifiedRkAccessible { get; init; }
    public string[] SourceUrls { get; init; } = [];
    public string[] Unresolved { get; init; } = [];
}

internal sealed record BaselineFile(string File, string Sha256, long Bytes);
internal sealed record BaselineState(string ItemsDatSha256, string? ItemTemplatesSha256,
    BaselineFile[] Files, Dictionary<string, int> ClassificationCounts);

internal sealed record HistoricalSummary
{
    public int FormatVersion { get; init; } = 1;
    public string ItemsDatSha256 { get; init; } = "";
    public string? ItemTemplatesSha256 { get; init; }
    public string EvidenceBundleSha256 { get; init; } = "";
    public string ObservationsSha256 { get; init; } = "";
    public string ToolAssemblySha256 { get; init; } = "";
    public int TotalInputRecords { get; init; }
    public int DistinctTemplateIds { get; init; }
    public Dictionary<string, int> BeforeClassificationCounts { get; init; } = [];
    public Dictionary<string, int> ContentClassificationCounts { get; init; } = [];
    public Dictionary<string, int> RequiredExpansionCounts { get; init; } = [];
    public int ItemsWithHistoricalObservations { get; init; }
    public int ItemsWithRecordedAddition { get; init; }
    public int HistoricalOnlyTemplateIds { get; init; }
    public int VerifiedRkAccessible { get; init; }
    public int MissionChestEligibleRk { get; init; }
    public int VerifiedSpecialPurposeExcluded { get; init; }
    public int UnverifiedChestEligibility { get; init; }
    public int ObservedPatchLabels { get; init; }
    public int AvailableSources { get; init; }
    public int UnavailableSourceRoutes { get; init; }
    public Dictionary<string, int> ObservationsByKind { get; init; } = [];
    public OutputIdentity[] PrimaryFiles { get; init; } = [];
    public string Coverage { get; init; } = "PASS";
    public string BaselinePreservation { get; init; } = "PASS";
    public string ReconstructionStatus { get; init; } = "PARTIAL_EVIDENCE_WITH_EXPLICIT_GAPS";
    public string LootIntegrationReadiness { get; init; } = "REVIEW_QUALIFIED_ALLOWLIST_ONLY_NOT_COMPLETE";
}
