namespace ZoneEngine_New.Core.GameData;

using System;

internal sealed record NamedLootCondition(
    int? MinimumLevel = null, int? MaximumLevel = null, int? PlayfieldId = null, bool BossOnly = false)
{
    internal bool Matches(NamedLootContext context)
        => (!MinimumLevel.HasValue || context.Level >= MinimumLevel.Value)
            && (!MaximumLevel.HasValue || context.Level <= MaximumLevel.Value)
            && (!PlayfieldId.HasValue || context.PlayfieldId == PlayfieldId.Value)
            && (!BossOnly || context.IsBoss);
}

internal sealed record NamedLootEntry(
    string Id, int LowId, int HighId, int MinQuality, int MaxQuality, bool LevelQuality,
    int MinQuantity, int MaxQuantity, int ChanceBasisPoints, int Weight, bool UniquePerCorpse,
    NamedLootCondition? Condition = null, string? EvidenceClassification = null, string? EvidenceReference = null);

internal sealed record NamedLootGroup(
    string Id, string Mode, int RollCount, int ChanceBasisPoints, int EmptyWeight,
    NamedLootCondition? Condition, NamedLootEntry[] Entries);

internal sealed record NamedLootTable(
    string Id, string Name, string Description, string Category, NamedLootGroup[] Groups, string? EvidenceReference = null);

internal sealed record NamedLootAssignment(
    string Id, string Scope, int Priority, string Merge, string? TableId = null, string? FamilyHash = null,
    string? TemplateHash = null, int? BandIndex = null, int? PlayfieldId = null, int? AuthoredIndex = null,
    string? AuthoredSignature = null, string? TargetGroup = null, string? SourceGroupId = null,
    NamedLootCondition? Condition = null);

internal sealed record NamedLootContext(
    int Level, int PlayfieldId, string? TemplateHash = null, int? BandIndex = null, string[]? FamilyHashes = null,
    int? AuthoredIndex = null, string? AuthoredSignature = null, bool IsBoss = false, bool IsOwnedSummon = false);

internal sealed record NamedLootResolvedGroup(string QualifiedId, string TableId, string AssignmentId, NamedLootGroup Group);

internal sealed record NamedLootResolutionNote(
    string AssignmentId, string Scope, string? TableId, string Action, string? GroupId, string Reason);

internal sealed record NamedLootResolution(
    NamedLootContext Context, NamedLootResolvedGroup[] Groups, NamedLootResolutionNote[] Trace);

internal sealed record NamedLootHistoryRow(int Revision, DateTimeOffset AtUtc, string Actor, string Action, string Target);

internal sealed record NamedLootRegistryState(
    int Revision, NamedLootTable[] Tables, NamedLootAssignment[] Assignments, NamedLootHistoryRow[] History,
    string? UnavailableReason);
