using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AORebirth.NpcEditor;

internal sealed record NamedLootRegistry(int Revision, NamedLootTable[] Tables, NamedLootAssignment[] Assignments,
    NamedLootRevision[] History, string? UnavailableReason);

internal sealed record NamedLootRevision(int Revision, DateTimeOffset AtUtc, string Actor, string Action, string Target);

internal sealed record NamedLootTable(string Id, string Name, string Description, string Category,
    NamedLootGroup[] Groups, string? EvidenceReference);

internal sealed record NamedLootGroup(string Id, string Mode, int RollCount, int ChanceBasisPoints, int EmptyWeight,
    NamedLootCondition? Condition, NamedLootEntry[] Entries);

internal sealed record NamedLootEntry(string Id, int LowId, int HighId, int MinQuality, int MaxQuality,
    bool LevelQuality, int MinQuantity, int MaxQuantity, int ChanceBasisPoints, int Weight, bool UniquePerCorpse,
    NamedLootCondition? Condition, string? EvidenceClassification, string? EvidenceReference);

internal sealed record NamedLootCondition(int? MinimumLevel, int? MaximumLevel, int? PlayfieldId, bool BossOnly);

internal sealed record NamedLootAssignment(string Id, string Scope, int Priority, string Merge, string? TableId,
    string? FamilyHash, string? TemplateHash, int? BandIndex, int? PlayfieldId, int? AuthoredIndex, string? AuthoredSignature,
    string? TargetGroup, string? SourceGroupId, NamedLootCondition? Condition);

internal sealed record NamedLootAssignmentImpact(string Definition, int? LoadedMatches, string? AffectedTable, string Description);

internal sealed record NamedLootTargets(NamedLootTemplateTarget[] Templates, NamedLootSpawnTarget[] Spawns);

internal sealed record NamedLootTemplateTarget(string Hash, string Name, int BandIndex, int Level, string[] FamilyHashes, bool IsBoss);

internal sealed record NamedLootSpawnTarget(int PlayfieldId, int AuthoredIndex, string AuthoredSignature, string Hash,
    int MinimumLevel, int MaximumLevel);

internal sealed record NamedLootEffectiveView(LootRow[] NativeRows, NamedLootResolution Resolution, NamedLootCandidate[] Candidates);

internal sealed record NamedLootResolution(NamedLootResolvedGroup[] Groups, NamedLootTraceRow[] Trace);

internal sealed record NamedLootResolvedGroup(string QualifiedId, string TableId, string AssignmentId, NamedLootGroup Group);

internal sealed record NamedLootTraceRow(string AssignmentId, string Scope, string? TableId, string Action, string? GroupId, string Reason);

internal sealed record NamedLootCandidate(string QualifiedGroupId, string EntryId, int LowId, int HighId, string Name,
    int? MinQuality, int? MaxQuality, bool Eligible, string Reason, int Weight, double? ConditionalProbabilityPercent)
{
    public string ItemIds => LowId == HighId ? $"{LowId}" : $"{LowId}/{HighId}";
    public string QualityRange => MinQuality == null || MaxQuality == null ? "—"
        : MinQuality == MaxQuality ? $"{MinQuality}" : $"{MinQuality}–{MaxQuality}";
}

internal abstract class NamedLootConditionDraft : INotifyPropertyChanged
{
    protected NamedLootConditionDraft(NamedLootCondition? source)
    {
        MinimumLevel = NumberText(source?.MinimumLevel);
        MaximumLevel = NumberText(source?.MaximumLevel);
        PlayfieldId = NumberText(source?.PlayfieldId);
        BossOnly = source?.BossOnly == true;
    }

    public string MinimumLevel { get; set; }
    public string MaximumLevel { get; set; }
    public string PlayfieldId { get; set; }
    public bool BossOnly { get; set; }

    protected NamedLootCondition? BuildCondition()
    {
        int? minimum = OptionalNumber(MinimumLevel, "minimum NPC level", 1, ushort.MaxValue);
        int? maximum = OptionalNumber(MaximumLevel, "maximum NPC level", 1, ushort.MaxValue);
        int? playfield = OptionalNumber(PlayfieldId, "playfield condition", 1);
        if (minimum > maximum)
            throw new ArgumentException("The minimum NPC level must be at most the maximum.");
        return minimum.HasValue || maximum.HasValue || playfield.HasValue || BossOnly
            ? new NamedLootCondition(minimum, maximum, playfield, BossOnly) : null;
    }

    internal static string NumberText(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    internal static int? OptionalNumber(string text, string label, int minimum = 0, int maximum = int.MaxValue) =>
        string.IsNullOrWhiteSpace(text) ? null : Number(text, label, minimum, maximum);

    internal static int Number(string text, string label, int minimum = 0, int maximum = int.MaxValue)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            || value < minimum || value > maximum)
            throw new ArgumentException($"Enter a valid {label}.");
        return value;
    }

    internal static string Required(string text, string label) => Optional(text)
        ?? throw new ArgumentException($"Enter {label}.");

    internal static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetText(ref string field, string value, [CallerMemberName] string property = "")
    {
        if (field == value)
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}

internal sealed class NamedLootGroupDraft : NamedLootConditionDraft
{
    private string id;
    private string mode;

    internal NamedLootGroupDraft(NamedLootGroup? source = null) : base(source?.Condition)
    {
        id = source?.Id ?? "";
        mode = source?.Mode ?? "INDEPENDENT";
        RollCount = NumberText(source?.RollCount ?? 1);
        ChanceBasisPoints = NumberText(source?.ChanceBasisPoints ?? 10_000);
        EmptyWeight = NumberText(source?.EmptyWeight ?? 0);
        Entries = new ObservableCollection<NamedLootEntryDraft>((source?.Entries ?? []).Select(entry => new NamedLootEntryDraft(entry)));
    }

    public string Id { get => id; set => SetText(ref id, value); }
    public string Mode { get => mode; set => SetText(ref mode, value); }
    public string RollCount { get; set; }
    public string ChanceBasisPoints { get; set; }
    public string EmptyWeight { get; set; }
    public ObservableCollection<NamedLootEntryDraft> Entries { get; }

    internal NamedLootGroup Build()
    {
        bool weighted = Mode is "WEIGHTED_ONE" or "WEIGHTED_MANY";
        return new(Required(Id, "the group ID"), Mode, Mode == "WEIGHTED_ONE" ? 1 : Number(RollCount, "roll count", 1, 16),
            Number(ChanceBasisPoints, "group chance in basis points", 0, 10_000),
            weighted ? Number(EmptyWeight, "empty weight", 0, 1_000_000) : 0, BuildCondition(),
            Entries.Select(entry => entry.Build(Mode)).ToArray());
    }
}

internal sealed class NamedLootEntryDraft : NamedLootConditionDraft
{
    private string id;
    private string lowId;
    private string highId;
    private string minQuality;
    private string maxQuality;

    internal NamedLootEntryDraft(NamedLootEntry? source = null) : base(source?.Condition)
    {
        id = source?.Id ?? "";
        lowId = NumberText(source?.LowId);
        highId = NumberText(source?.HighId);
        minQuality = NumberText(source?.MinQuality);
        maxQuality = NumberText(source?.MaxQuality);
        LevelQuality = source?.LevelQuality == true;
        MinQuantity = NumberText(source?.MinQuantity ?? 1);
        MaxQuantity = NumberText(source?.MaxQuantity ?? 1);
        ChanceBasisPoints = NumberText(source?.ChanceBasisPoints);
        Weight = NumberText(source?.Weight);
        UniquePerCorpse = source?.UniquePerCorpse == true;
        EvidenceClassification = source?.EvidenceClassification ?? "";
        EvidenceReference = source?.EvidenceReference ?? "";
    }

    public string Id { get => id; set => SetText(ref id, value); }
    public string LowId { get => lowId; set => SetText(ref lowId, value); }
    public string HighId { get => highId; set => SetText(ref highId, value); }
    public string MinQuality { get => minQuality; set => SetText(ref minQuality, value); }
    public string MaxQuality { get => maxQuality; set => SetText(ref maxQuality, value); }
    public bool LevelQuality { get; set; }
    public string MinQuantity { get; set; }
    public string MaxQuantity { get; set; }
    public string ChanceBasisPoints { get; set; }
    public string Weight { get; set; }
    public bool UniquePerCorpse { get; set; }
    public string EvidenceClassification { get; set; }
    public string EvidenceReference { get; set; }

    internal NamedLootEntry Build(string mode)
    {
        int minimumQuality = Number(MinQuality, "minimum QL", 1, ushort.MaxValue);
        int maximumQuantity = UniquePerCorpse ? 1 : 20;
        int minimumQuantity = Number(MinQuantity, "minimum quantity", 1, maximumQuantity);
        int chance = mode == "INDEPENDENT" ? Number(ChanceBasisPoints, "entry chance in basis points", 0, 10_000)
            : 10_000;
        int weight = mode is "WEIGHTED_ONE" or "WEIGHTED_MANY" ? Number(Weight, "entry weight", 1, 1_000_000)
            : 0;
        return new(Required(Id, "the entry ID"), Number(LowId, "low AOID", 1), Number(HighId, "high AOID", 1),
            minimumQuality, Number(MaxQuality, "maximum QL", minimumQuality, ushort.MaxValue), LevelQuality, minimumQuantity,
            Number(MaxQuantity, "maximum quantity", minimumQuantity, maximumQuantity), chance, weight, UniquePerCorpse, BuildCondition(),
            Optional(EvidenceClassification), Optional(EvidenceReference));
    }
}
