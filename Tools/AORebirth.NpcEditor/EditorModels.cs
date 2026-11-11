using System.Text.Json;

namespace AORebirth.NpcEditor;

internal sealed record EditorSession(string Token, DateTimeOffset ExpiresAt, bool CanWrite, bool CanSave);

internal sealed record NpcIdentity(int PlayfieldId, int IdentityType, int InstanceId, Guid Generation)
{
    public string Path => FormattableString.Invariant($"npcs/{PlayfieldId}/{IdentityType}/{InstanceId}/{Generation:D}");
    public override string ToString() => $"PF {PlayfieldId}  /  {IdentityType}:{InstanceId}  /  {Generation:D}";
}

internal sealed record NpcSummary(NpcIdentity Identity, string Name, int Level, int? TemplateId,
    string? TemplateHash, string? SpawnHash, int? MonsterData, string? SpawnSource, int ClientPlayfieldId, string[]? FamilyHashes);

internal sealed record NpcDetail(NpcSummary Npc, double X, double Y, double Z, string? SpawnPointKey,
    JsonElement? Respawn, StatRow[] Stats, EquipmentRow[] Equipment, ConfiguredEquipmentRow[] ConfiguredEquipment,
    NanoRow[] UploadedNanos, NanoRow[] ActiveNanos, JsonElement Brain, ChangeRow[] Changes, bool CanAddArmor);

internal sealed record StatRow(int StatId, string Name, int? RuntimeBase, int? RuntimeBonus, int? RuntimeValue,
    int? ConfiguredValue, bool Editable, string? ConfiguredSource);

internal sealed record EquipmentRow(int Slot, int InstanceId, int LowId, int HighId, int Quality, string Name,
    string ItemClass, string Role, int DefaultPosition, bool Replaceable, bool Removable);

internal sealed record ConfiguredEquipmentRow(int LowId, int HighId, string Name);

internal sealed record NanoRow(int Id, string Name);

internal sealed record ChangeRow(Guid ChangeId, DateTimeOffset AtUtc, string Description, bool Reverted, bool Revertible,
    string? Scope, string? Actor, string? Before, string? After, string? RevertedBy);

internal sealed record ItemRow(int Id, string Name, int Quality, string ItemClass, string? Hash,
    int[]? TemplateIds, int MinQuality, int MaxQuality)
{
    public string QualityRange => Hash == null || MinQuality == MaxQuality ? Quality.ToString() : $"{MinQuality}–{MaxQuality}";
    public string Preview => $"{Name}  ·  ID {Id}  ·  QL {QualityRange}  ·  {ItemClass}"
        + (Hash == null ? "" : $"  ·  {Hash}  ·  IDs {string.Join(", ", TemplateIds ?? [])}");
}

internal sealed record ItemPage(ItemRow[] Rows, bool HasMore, int NextOffset);

internal sealed record ItemDetails(int Id, string Name, int Quality, string ItemClass, string? Description, ItemStat[] Stats);

internal sealed record ItemStat(int Id, string Name, int Value);

internal sealed record LootItem(string Hash, string Name, int[] TemplateIds)
{
    public string Preview => $"{Name}  •  {Hash}  •  IDs {string.Join(", ", TemplateIds)}";
}

internal sealed record LootDetail(string TemplateHash, LootRow[] Rows, SavedLootState Saved,
    LootRow[]? ConfiguredRows, LiveLootState? Live);

internal sealed record LootRow(string Source, string Hash, int? Chance, int? Repeats, int? LevelMod, int? MinQuality,
    int? MaxQuality, string Behavior, string Provenance, LootItem[] Items, string Conditions, int? MinQuantity,
    int? MaxQuantity, LootGroup[] Groups, LootRowKey? Edit, LootEntry? Entry)
{
    public string ItemNames => string.Join(", ", Items.Select(item => item.Name));
    public string QualityRange => MinQuality.HasValue ? $"{MinQuality} – {MaxQuality}" : "—";
    public string QuantityRange => MaxQuantity.HasValue ? $"{MinQuantity}–{MaxQuantity}" : "Varies";
}

internal sealed record LootGroup(string Hash, bool SpawnAll, string[] Children)
{
    public string Preview => $"{Hash} · {(SpawnAll ? "all branches" : "one eligible child")} · {string.Join(", ", Children)}";
}

internal sealed record LootRowKey(int Index, string Signature);

internal sealed record LootEntry(string ItemHash, int Chance, int Repeats, int LevelMod, int? MinQuality = null, int? MaxQuality = null)
{
    public string Summary => $"{ItemHash} · {Chance}% · {Repeats} attempt(s) · "
        + (MinQuality.HasValue ? $"QL {MinQuality}–{MaxQuality}" : $"QL variance {LevelMod}%");
}

internal sealed record LootHistoryRow(int Revision, string Actor, DateTimeOffset AtUtc, string Action, int EntryCount,
    string? Scope, int? BandIndex, int? RowIndex, LootEntry? Before, LootEntry? After, int? RestoredRevision);

internal sealed record LiveLootChange(Guid ChangeId, string Actor, DateTimeOffset AtUtc, string Action,
    int? RowIndex, LootEntry? Before, LootEntry? After, bool Reverted, bool Revertible)
{
    public string BeforeText => Before?.Summary ?? "—";
    public string AfterText => After?.Summary ?? "—";
}

internal sealed record LiveLootState(bool CanApply, int Revision, LiveLootChange[] History);

internal sealed record SavedLootState(string TemplateHash, bool CanSave, int Revision, LootEntry[] Entries,
    LootHistoryRow[] History, string? UnavailableReason, string? Definition, int? EstimatedAffectedNpcs);

internal sealed record LootSimulation(int Rolls, int EmptyCorpses, double EmptyPercent, double AverageItemsPerCorpse,
    LootSimulationItem[] Items);

internal sealed record LootSimulationItem(int LowId, int HighId, string Name, int TotalDrops, int CorpsesWithItem,
    double CorpseFrequencyPercent, LootQualityCount[] Qualities)
{
    public string QualityDistribution => string.Join(", ", Qualities.Select(row => $"{row.Quality} × {row.Count}"));
}

internal sealed record LootQualityCount(int Quality, int Count);

internal sealed record EquipmentEdit(int Slot, int ExpectedInstanceId, int? ItemId, int? Quality);

internal sealed record EquipmentSaveEdit(int ExpectedRevision, int Index, int ExpectedLowId, int ExpectedHighId,
    int? ItemId, int? Quality);

internal sealed record SavedEquipmentState(string Scope, string Definition, string TemplateHash, int? BandLevel,
    int? EstimatedAffectedNpcs, string? UnavailableReason, int Revision, SavedEquipmentRow[] Entries,
    EquipmentHistoryRow[] History, bool CanAddArmor);

internal sealed record SavedEquipmentRow(int Index, int LowId, int HighId, string Name, string Role,
    int DefaultPosition, bool Replaceable, bool Removable);

internal sealed record EquipmentHistoryRow(int Revision, DateTimeOffset AtUtc, string Actor, string Action,
    SavedEquipmentRow[] Before, SavedEquipmentRow[] After);
