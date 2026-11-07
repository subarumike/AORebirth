using System.Text.Json;

namespace AORebirth.NpcEditor;

internal sealed record EditorSession(string Token, DateTimeOffset ExpiresAt, bool CanWrite, bool CanSave);

internal sealed record NpcIdentity(int PlayfieldId, int IdentityType, int InstanceId, Guid Generation)
{
    public string Path => FormattableString.Invariant($"npcs/{PlayfieldId}/{IdentityType}/{InstanceId}/{Generation:D}");
    public override string ToString() => $"PF {PlayfieldId}  /  {IdentityType}:{InstanceId}  /  {Generation:D}";
}

internal sealed record NpcSummary(NpcIdentity Identity, string Name, int Level, int? TemplateId,
    string? TemplateHash, string? SpawnHash, int? MonsterData, string? SpawnSource, int ClientPlayfieldId);

internal sealed record NpcDetail(NpcSummary Npc, double X, double Y, double Z, string? SpawnPointKey,
    JsonElement? Respawn, StatRow[] Stats, EquipmentRow[] Equipment, ConfiguredEquipmentRow[] ConfiguredEquipment,
    NanoRow[] UploadedNanos, NanoRow[] ActiveNanos, JsonElement Brain, ChangeRow[] Changes, bool CanAddArmor);

internal sealed record StatRow(int StatId, string Name, int? RuntimeBase, int? RuntimeBonus, int? RuntimeValue,
    int? ConfiguredValue, bool Editable);

internal sealed record EquipmentRow(int Slot, int InstanceId, int LowId, int HighId, int Quality, string Name,
    string ItemClass, string Role, int DefaultPosition, bool Replaceable, bool Removable);

internal sealed record ConfiguredEquipmentRow(int LowId, int HighId, string Name);

internal sealed record NanoRow(int Id, string Name);

internal sealed record ChangeRow(Guid ChangeId, DateTimeOffset AtUtc, string Description, bool Reverted, bool Revertible);

internal sealed record ItemRow(int Id, string Name, int Quality, string ItemClass)
{
    public string Preview => $"{Name}  •  ID {Id}  •  QL {Quality}  •  {ItemClass}";
}

internal sealed record LootItem(string Hash, string Name, int[] TemplateIds)
{
    public string Preview => $"{Name}  •  {Hash}  •  IDs {string.Join(", ", TemplateIds)}";
}

internal sealed record LootDetail(string TemplateHash, LootRow[] Rows, SavedLootState Saved);

internal sealed record LootRow(string Source, string Hash, int? Chance, int? Repeats, int? LevelMod, int? MinQuality,
    int? MaxQuality, string Behavior, string Provenance, LootItem[] Items, string Conditions, int? MinQuantity,
    int? MaxQuantity, LootGroup[] Groups)
{
    public string ItemNames => string.Join(", ", Items.Select(item => item.Name));
    public string QualityRange => MinQuality.HasValue ? $"{MinQuality} – {MaxQuality}" : "—";
}

internal sealed record LootGroup(string Hash, bool SpawnAll, string[] Children)
{
    public string Preview => $"{Hash} · {(SpawnAll ? "all branches" : "one eligible child")} · {string.Join(", ", Children)}";
}

internal sealed record LootEntry(string ItemHash, int Chance, int Repeats, int LevelMod);

internal sealed record LootHistoryRow(int Revision, string Actor, DateTimeOffset AtUtc, string Action, int EntryCount);

internal sealed record SavedLootState(string TemplateHash, bool CanSave, int Revision, LootEntry[] Entries,
    LootHistoryRow[] History, string? UnavailableReason);

internal sealed record EquipmentEdit(int Slot, int ExpectedInstanceId, int? ItemId, int? Quality);
