namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    [property: JsonRequired] int LevelMod);

public sealed record NpcEditorLootSave([property: JsonRequired] int ExpectedRevision, int? RowIndex, NpcEditorLootEntry? Entry);

public sealed record NpcEditorLootRevert([property: JsonRequired] int ExpectedRevision, int? Revision = null);

public readonly record struct NpcEditorLootTarget(string TemplateHash, string TemplateSignature);

internal sealed record NpcEditorLootRevision(int Revision, string Actor, DateTimeOffset AtUtc, string Action, NpcEditorLootEntry[] Entries);

public sealed record NpcEditorLootRevisionSummary(int Revision, string Actor, DateTimeOffset AtUtc, string Action, int EntryCount);

public sealed record NpcEditorLootState(string TemplateHash, bool CanSave, string? UnavailableReason,
    int Revision, NpcEditorLootEntry[] Entries, NpcEditorLootRevisionSummary[] History);

public sealed record NpcEditorLootItem(string Hash, string Name, int[] TemplateIds);

public sealed record NpcEditorLootGroup(string Hash, bool SpawnAll, IReadOnlyList<string> Children);

public sealed record NpcEditorLootRow(string Source, string Hash, int? Chance, int? Repeats, int? LevelMod,
    int? MinQuality, int? MaxQuality, string Behavior, string Provenance, IReadOnlyList<NpcEditorLootItem> Items,
    string Conditions, int? MinQuantity, int? MaxQuantity, IReadOnlyList<NpcEditorLootGroup> Groups);

public sealed record NpcEditorLootView(string TemplateHash, IReadOnlyList<NpcEditorLootRow> Rows, NpcEditorLootState Saved);

public sealed class NpcEditorLootConflictException(string message) : InvalidOperationException(message);

/// <summary>Projects the corpse loot inputs and edits additive rows for one resolved NPC template.</summary>
public sealed class NpcEditorLoot(GameDataStore gameData, IItemTemplateCatalog items)
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

        if (template != null)
        {
            int level = npc.Stats.GetOrOne(CharacterStat.Level);
            for (int index = 0; index < template.ItemTable.Count; index++)
            {
                MobItemTableEntry entry = template.ItemTable[index];
                bool editorRow = index >= template.ItemTable.Count - template.EditorLootEntryCount;
                (int min, int max) = LootableDynel.QualityWindow(level, entry.LevelMod > 0 ? entry.LevelMod : variance);
                IReadOnlyList<NpcEditorLootGroup> groups = DescribeGroups(entry.Hash);
                string behavior = "Independent chance checks";
                if (groups.Count > 0)
                    behavior += groups[0].SpawnAll ? " / spawn all branches" : " / choose one eligible child";
                rows.Add(new(editorRow ? "Saved editor overlay" : "NpcTemplates.json / " + templateHash,
                    entry.Hash, entry.Chance, entry.Repeats, entry.LevelMod, min, max, behavior,
                    editorRow ? "GM-authored override" : "Configured server template. Retail evidence unclassified.",
                    ItemsUnder(entry.Hash), "Item-family QL eligibility. " + CorpseConditions,
                    0, groups.Count == 0 ? Math.Clamp(entry.Repeats, 0, Corpse.LootCapacity) : null, groups));
            }
        }

        return new(templateHash, rows, StateFor(npc, TargetOf(npc)));
    }

    public NpcEditorLootTarget CaptureSaveTarget(NpcCharacter npc)
    {
        NpcEditorLootTarget target = TargetOf(npc);
        NpcEditorLootState state = StateFor(npc, target);
        if (!state.CanSave)
            throw new InvalidOperationException(state.UnavailableReason);
        return target;
    }

    public NpcEditorLootState Save(NpcEditorLootTarget target, NpcEditorLootSave request, string actor)
        => gameData.ChangeEditorLoot(target, request.ExpectedRevision, actor, "save", history =>
        {
            NpcEditorLootEntry[] current = history.Length > 0 ? history[^1].Entries : [];
            if (request.RowIndex is not int index)
                return [.. current, request.Entry ?? throw new ArgumentException("An appended row requires an item.")];
            if (index < 0 || index >= current.Length)
                throw new ArgumentException("The selected row does not exist in the saved template overlay.");
            if (request.Entry == null)
                return [.. current.Take(index), .. current.Skip(index + 1)];

            var replaced = (NpcEditorLootEntry[])current.Clone();
            replaced[index] = request.Entry;
            return replaced;
        }, HasRdbTemplates);

    public NpcEditorLootState Revert(NpcEditorLootTarget target, NpcEditorLootRevert request, string actor)
        => gameData.ChangeEditorLoot(target, request.ExpectedRevision, actor, "revert", history =>
        {
            int revision = request.Revision ?? history.Length - 1;
            if (revision < 0 || revision >= history.Length)
                throw new ArgumentException("Choose an earlier loot revision.");
            return revision == 0 ? [] : history[revision - 1].Entries;
        }, HasRdbTemplates);

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

    NpcEditorLootState StateFor(NpcCharacter npc, NpcEditorLootTarget target)
    {
        NpcEditorLootState state = gameData.GetEditorLoot(target);
        if (npc.Pet != null || npc.MobTemplate?.UnresolvedPlaceholder != false)
            return state with { CanSave = false, UnavailableReason = "Loot editing requires a resolved NPC template on a non-pet NPC." };
        return state;
    }

    static NpcEditorLootTarget TargetOf(NpcCharacter npc) => new(npc.MobTemplate?.Hash ?? string.Empty,
        npc.MobTemplate?.EditorLootLeaf?.EditorLootTemplateSignature ?? string.Empty);

    bool HasRdbTemplates(string hash) => gameData.TryGetHashInstance(hash, out HashInstance family)
        && family.TemplateIds.All(id => items.TryGet(id, out _));

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
