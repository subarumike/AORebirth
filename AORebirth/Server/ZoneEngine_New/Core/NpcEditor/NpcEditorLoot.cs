namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

public sealed record NpcEditorLootEntry(
    [property: JsonRequired] string ItemHash,
    [property: JsonRequired] int Chance,
    [property: JsonRequired] int Repeats,
    [property: JsonRequired] int LevelMod,
    int? MinQuality = null, int? MaxQuality = null)
{
    internal static NpcEditorLootEntry FromNative(MobItemTableEntry entry)
        => new(entry.Hash, entry.Chance, entry.Repeats, entry.LevelMod, entry.EditorMinQuality, entry.EditorMaxQuality);

    internal MobItemTableEntry ToNative(bool editorOverride = true) => new()
    {
        Hash = ItemHash, Chance = Chance, Repeats = Repeats, LevelMod = LevelMod,
        EditorMinQuality = MinQuality, EditorMaxQuality = MaxQuality, EditorOverride = editorOverride
    };
}

public sealed record NpcEditorLootRowKey(int Index, string Signature);

public sealed record NpcEditorLootSave([property: JsonRequired] int ExpectedRevision, int? RowIndex, NpcEditorLootEntry? Entry,
    NpcEditorLootRowKey? Row = null, string? Scope = null);

public sealed record NpcEditorLootRevert([property: JsonRequired] int ExpectedRevision, int? Revision = null, string? Scope = null);

public readonly record struct NpcEditorLootTarget(string TemplateHash, string TemplateSignature, int BandIndex = -1);

internal sealed record NpcEditorLootRevision(int Revision, string Actor, DateTimeOffset AtUtc, string Action, NpcEditorLootEntry[] Entries,
    NpcEditorLootBand[]? Bands = null, NpcEditorLootAudit? Audit = null)
{
    internal IEnumerable<NpcEditorLootEntry> Overrides => Entries.Concat(
        (Bands ?? []).SelectMany(band => band.Rows).Select(row => row.Entry).OfType<NpcEditorLootEntry>());
}

internal sealed record NpcEditorLootAudit(int? BandIndex, int? RowIndex, NpcEditorLootEntry? Before,
    NpcEditorLootEntry? After, int? RestoredRevision = null);

internal sealed record NpcEditorLootRevisionEdit(NpcEditorLootEntry[] Entries, NpcEditorLootBand[]? Bands, NpcEditorLootAudit Audit);

internal sealed record NpcEditorLootBand(int BandIndex, NpcEditorLootBandRow[] Rows);

internal sealed record NpcEditorLootBandRow(int? AuthoredIndex, NpcEditorLootEntry? Entry)
{
    internal MobItemTableEntry Materialize(NpcLevelBand band)
        => Entry?.ToNative() ?? NpcEditorLootEntry.FromNative(band.LootTable[AuthoredIndex!.Value]).ToNative(editorOverride: false);
}

public sealed record NpcEditorLootRevisionSummary(int Revision, string Actor, DateTimeOffset AtUtc, string Action, int EntryCount,
    string Scope, int? BandIndex, int? RowIndex, NpcEditorLootEntry? Before, NpcEditorLootEntry? After, int? RestoredRevision);

public sealed record NpcEditorLootState(string TemplateHash, bool CanSave, string? UnavailableReason,
    int Revision, NpcEditorLootEntry[] Entries, NpcEditorLootRevisionSummary[] History,
    string? Definition = null, int BandIndex = -1, int? EstimatedAffectedNpcs = null);

public sealed record NpcEditorLootItem(string Hash, string Name, int[] TemplateIds);

public sealed record NpcEditorLootGroup(string Hash, bool SpawnAll, IReadOnlyList<string> Children);

public sealed record NpcEditorLootRow(string Source, string Hash, int? Chance, int? Repeats, int? LevelMod,
    int? MinQuality, int? MaxQuality, string Behavior, string Provenance, IReadOnlyList<NpcEditorLootItem> Items,
    string Conditions, int? MinQuantity, int? MaxQuantity, IReadOnlyList<NpcEditorLootGroup> Groups,
    NpcEditorLootRowKey? Edit = null, NpcEditorLootEntry? Entry = null);

public sealed record NpcEditorLootView(string TemplateHash, IReadOnlyList<NpcEditorLootRow> Rows, NpcEditorLootState Saved,
    IReadOnlyList<NpcEditorLootRow> ConfiguredRows, NpcEditorLootLiveView Live);

public sealed class NpcEditorLootConflictException(string message) : InvalidOperationException(message);

/// <summary>Projects the corpse loot inputs and edits additive rows for one resolved NPC template.</summary>
public sealed partial class NpcEditorLoot(GameDataStore gameData, IItemTemplateCatalog items, HashItemMinter? minter = null)
{
    const int SearchLimit = 100;
    const string CorpseConditions = "Corpse capacity and existing spoils rules apply.";

    // Run only on the selected NPC's playfield. File persistence stays off that tick.
    public NpcEditorLootView Describe(NpcCharacter npc)
    {
        MobTemplate? template = npc.MobTemplate;
        string templateHash = template?.Hash ?? string.Empty;
        var rows = new List<NpcEditorLootRow>();
        int variance = npc.Playfield?.GetService<GameplaySettings>()?.Loot.QualityVariancePercent
            ?? new GameplayLoot().QualityVariancePercent;

        foreach (Item item in npc.InspectCarriedLoot())
        {
            rows.Add(new("Carried SpawnItem", string.Empty, null, null, null, item.Quality, item.Quality,
                "Placed before equipment and table rolls", "Observed runtime item",
                [new(string.Empty, item.Name, item.LowId == item.HighId ? [item.LowId] : [item.LowId, item.HighId])],
                "Corpse capacity applies.", item.StackCount, item.StackCount, []));
        }

        foreach (Item worn in npc.Equipment.Content.Values)
        {
            foreach (ItemSpell spell in EquipmentLoot.SpawnFunctions(worn))
            {
                if (!EquipmentLoot.TryReadSpawn(spell, out string hash, out int quality))
                    continue;
                (int min, int max) = LootableDynel.QualityWindow(quality, variance);
                rows.Add(new("Equipment / " + worn.Name, hash, null, null, null, min, max,
                    "OnWear SpawnItem, highest eligible QL at or below the roll", "Configured on currently equipped item",
                    ItemsUnder(hash), CorpseConditions, 0, 1, []));
            }
        }

        int level = npc.Stats.GetOrOne(CharacterStat.Level);
        NpcEditorLootTarget target = TargetOf(npc);
        (NpcEditorLootState saved, List<MobItemTableEntry> configured) = gameData.DescribeEditorLoot(target);
        NpcEditorLootLiveView live = LiveView(npc);
        rows.AddRange(DescribeTable(npc.ActiveLootTable, templateHash, level, variance, live.CanApply));
        return new(templateHash, rows, StateFor(npc, saved),
            DescribeTable(configured, saved.Definition ?? templateHash, level, variance, saved.CanSave), live);
    }

    public NpcEditorLootTarget CaptureSaveTarget(NpcCharacter npc)
    {
        NpcEditorLootTarget target = TargetOf(npc);
        NpcEditorLootState state = StateFor(npc, gameData.GetEditorLoot(target));
        if (!state.CanSave)
            throw new InvalidOperationException(state.UnavailableReason);
        return target;
    }

    public NpcEditorLootState Save(NpcEditorLootTarget target, NpcEditorLootSave request, string actor)
        => gameData.ChangeEditorLoot(target, request.ExpectedRevision, actor, "save", (leaf, history) =>
        {
            NpcEditorLootRevision? current = history.LastOrDefault();
            if (request.Scope == "templateBand" && request.RowIndex == null)
                return ChangeBand(leaf, target.BandIndex, current, request.Row, request.Entry);
            if (request.Scope != null || request.Row != null || current?.Bands is { Length: > 0 })
                throw new ArgumentException("Use the exact template band scope to edit these configured loot rows.");
            if (request.Entry != null && !gameData.TryGetHashInstance(request.Entry.ItemHash, out _))
                throw new ArgumentException("This save requires a concrete item family. Use the exact template band scope for category rows.");

            NpcEditorLootEntry[] entries = current?.Entries ?? [];
            int index = request.RowIndex ?? entries.Length;
            if (request.RowIndex != null && (index < 0 || index >= entries.Length))
                throw new ArgumentException("The selected row does not exist in the saved template overlay.");
            return new(EditRow(entries, index, request.Entry), null,
                new(null, index, index < entries.Length ? entries[index] : null, request.Entry));
        }, HasRdbTemplates);

    public NpcEditorLootState Revert(NpcEditorLootTarget target, NpcEditorLootRevert request, string actor)
        => gameData.ChangeEditorLoot(target, request.ExpectedRevision, actor, "revert", (leaf, history) =>
        {
            int revision = request.Revision ?? history.Length - 1;
            if (revision < 0 || revision >= history.Length)
                throw new ArgumentException("Choose an earlier loot revision.");
            NpcEditorLootRevision? previous = revision == 0 ? null : history[revision - 1];
            if (request.Scope == "templateBand")
            {
                NpcEditorLootRevision current = history[^1];
                NpcLevelBand band = leaf.Bands[target.BandIndex];
                NpcEditorLootBandRow[] before = NpcEditorLootJournal.BandRows(band, target.BandIndex, current);
                NpcEditorLootBandRow[] after = NpcEditorLootJournal.BandRows(band, target.BandIndex, previous);
                if (before.SequenceEqual(after))
                    throw new ArgumentException("The selected band already matches that revision.");
                return new(current.Entries, ReplaceBand(current, new(target.BandIndex, after)),
                    new(target.BandIndex, null, null, null, revision));
            }

            if (request.Scope != null || history[^1].Bands is { Length: > 0 })
                throw new ArgumentException("Use the exact template band scope to restore these configured loot rows.");
            return new(previous?.Entries ?? [], null, new(null, null, null, null, revision));
        }, HasRdbTemplates);

    static NpcEditorLootRevisionEdit ChangeBand(NpcLeaf leaf, int bandIndex, NpcEditorLootRevision? current,
        NpcEditorLootRowKey? row, NpcEditorLootEntry? entry)
    {
        NpcLevelBand band = leaf.Bands[bandIndex];
        NpcEditorLootBandRow[] rows = NpcEditorLootJournal.BandRows(band, bandIndex, current);
        int index = row?.Index ?? rows.Length;
        if (row != null)
        {
            if (index < 0 || index >= rows.Length || Key(index, rows[index].Materialize(band)) != row)
                throw new NpcEditorLootConflictException("The configured loot row changed. Refresh and retry.");
            if (entry == NpcEditorLootEntry.FromNative(rows[index].Materialize(band)))
                throw new ArgumentException("The selected loot row already has these values.");
        }

        NpcEditorLootBandRow? replacement = entry == null ? null : new(index < rows.Length ? rows[index].AuthoredIndex : null, entry);
        NpcEditorLootBandRow[] edited = EditRow(rows, index, replacement);
        NpcEditorLootEntry? before = index < rows.Length ? NpcEditorLootEntry.FromNative(rows[index].Materialize(band)) : null;
        return new(current?.Entries ?? [], ReplaceBand(current, new(bandIndex, edited)), new(bandIndex, index, before, entry));
    }

    static NpcEditorLootBand[] ReplaceBand(NpcEditorLootRevision? current, NpcEditorLootBand band)
        => [.. (current?.Bands ?? []).Where(other => other.BandIndex != band.BandIndex), band];

    static T[] EditRow<T>(IReadOnlyList<T> rows, int index, T? replacement) where T : class
    {
        if (index == rows.Count)
            return [.. rows, replacement ?? throw new ArgumentException("An appended row requires an item.")];
        if (replacement == null)
            return [.. rows.Take(index), .. rows.Skip(index + 1)];
        if (EqualityComparer<T>.Default.Equals(rows[index], replacement))
            throw new ArgumentException("The selected loot row already has these values.");

        T[] changed = rows.ToArray();
        changed[index] = replacement;
        return changed;
    }

    static NpcEditorLootRowKey Key(int index, MobItemTableEntry entry)
        => new(index, NpcEditorOverrideFile.Fingerprint(JsonSerializer.SerializeToUtf8Bytes(NpcEditorLootEntry.FromNative(entry))));

    NpcEditorLootRow[] DescribeTable(IReadOnlyList<MobItemTableEntry>? table, string definition, int level, int variance, bool editable)
    {
        if (table == null)
            return [];
        var rows = new NpcEditorLootRow[table.Count];
        for (int index = 0; index < table.Count; index++)
        {
            MobItemTableEntry entry = table[index];
            (int min, int max) = LootableDynel.ItemTableQualityWindow(entry, level, variance);
            IReadOnlyList<NpcEditorLootGroup> groups = DescribeGroups(entry.Hash);
            string behavior = "Independent chance checks";
            if (groups.Count > 0)
                behavior += groups[0].SpawnAll ? " / spawn all branches" : " / choose one eligible child";
            rows[index] = new(entry.EditorOverride ? "Editor override / " + definition : definition,
                entry.Hash, entry.Chance, entry.Repeats, entry.LevelMod, min, max, behavior,
                entry.EditorOverride ? "GM-authored override" : "Configured server template. Retail evidence unclassified.",
                ItemsUnder(entry.Hash), "Item-family QL eligibility. " + CorpseConditions,
                0, groups.Count == 0 ? Math.Clamp(entry.Repeats, 0, Corpse.LootCapacity) : null, groups,
                editable ? Key(index, entry) : null, NpcEditorLootEntry.FromNative(entry));
        }

        return rows;
    }

    public IReadOnlyList<NpcEditorLootItem> SearchItemFamilies(string? query)
    {
        string search = query?.Trim() ?? string.Empty;
        if (search.Length > 128)
            throw new ArgumentException("Item search must be at most 128 characters.", nameof(query));
        bool isId = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out int id);
        return gameData.ItemFamilies
            .Where(family => family.TemplateIds.All(templateId => items.TryGet(templateId, out _)))
            .Where(family => search.Length == 0
                || (isId && family.TemplateIds.Contains(id))
                || family.Hash.Contains(search, StringComparison.OrdinalIgnoreCase)
                || family.TemplateIds.Any(templateId => items.TryGet(templateId, out ItemTemplate template)
                    && template.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Take(SearchLimit)
            .Select(DescribeItem)
            .ToArray();
    }

    static NpcEditorLootState StateFor(NpcCharacter npc, NpcEditorLootState state)
    {
        if (npc.Pet != null || npc.MobTemplate?.UnresolvedPlaceholder != false)
            return state with { CanSave = false, UnavailableReason = "Loot editing requires a resolved NPC template on a non-pet NPC." };
        return state;
    }

    static NpcEditorLootTarget TargetOf(NpcCharacter npc) => new(npc.MobTemplate?.Hash ?? string.Empty,
        npc.MobTemplate?.EditorLootLeaf?.EditorLootTemplateSignature ?? string.Empty, npc.MobTemplate?.AuthoredBandIndex ?? -1);

    bool HasRdbTemplates(string hash)
    {
        var leaves = new List<HashInstance>();
        gameData.CollectHashLeafInstances(hash, leaves);
        return leaves.Count > 0 && leaves.All(family => family.TemplateIds.All(id => items.TryGet(id, out _)));
    }

    IReadOnlyList<NpcEditorLootItem> ItemsUnder(string hash)
    {
        var leaves = new List<HashInstance>();
        gameData.CollectHashLeafInstances(hash, leaves);
        return leaves.Select(DescribeItem).ToArray();
    }

    IReadOnlyList<NpcEditorLootGroup> DescribeGroups(string hash)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<NpcEditorLootGroup>();
        var pending = new Stack<string>();
        pending.Push(hash);
        while (pending.TryPop(out string? candidate))
        {
            if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate)
                || !gameData.TryGetHashTemplate(candidate, out IReadOnlyList<string> children))
                continue;
            groups.Add(new(candidate, gameData.IsSpawnAllItemHash(candidate), children.ToArray()));
            for (int index = children.Count - 1; index >= 0; index--)
                pending.Push(children[index]);
        }

        return groups;
    }

    NpcEditorLootItem DescribeItem(HashInstance family)
    {
        foreach (int id in family.TemplateIds)
        {
            if (items.TryGet(id, out ItemTemplate template))
                return new(family.Hash, template.Name, (int[])family.TemplateIds.Clone());
        }

        return new(family.Hash, "Unresolved catalog item", (int[])family.TemplateIds.Clone());
    }
}
