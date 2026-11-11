namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ZoneEngine_New.Core.Mobs;

internal sealed record NpcEditorStatFamilyBinding(string Signature, string[] Leaves);

public sealed partial class NpcTemplateCatalog
{
    readonly ConcurrentDictionary<string, NpcEditorStatFamilyBinding> _editorStatFamilies = new(StringComparer.Ordinal);

    internal NpcEditorStatFamilyBinding? EditorStatsFamily(string family)
    {
        if (!_families.ContainsKey(family))
            return null;
        var binding = _editorStatFamilies.GetOrAdd(family, BuildStatFamily);
        return binding.Leaves.Length > 0 ? binding : null;
    }

    NpcEditorStatFamilyBinding BuildStatFamily(string family)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var leaves = new List<string>();
        var pending = new Stack<string>();
        pending.Push(family);
        while (pending.TryPop(out string? hash))
        {
            if (string.IsNullOrEmpty(hash) || !seen.Add(hash))
                continue;
            if (hash != MobTemplate.FallbackHash && _leaves.ContainsKey(hash))
                leaves.Add(hash);
            if (_families.TryGetValue(hash, out string[]? children))
            {
                foreach (string child in children)
                    pending.Push(child);
            }
        }
        string[] ordered = leaves.Order(StringComparer.Ordinal).ToArray();
        return new(NpcEditorOverrideFile.Fingerprint(JsonSerializer.SerializeToUtf8Bytes(ordered)), ordered);
    }

    internal IEnumerable<NpcEditorScopedStatsHistory> MatchingEditorStatFamilies(string hash)
    {
        if (EditorStats.Scopes is not { Count: > 0 } scopes)
            yield break;
        foreach (string family in EditorFamiliesFor(hash))
        {
            if (scopes.TryGetValue(NpcEditorStatsJournal.FamilyKey(family), out NpcEditorScopedStatsHistory? history)
                && history.Revisions[^1].Values.Count > 0
                && (EditorStats.LoadError == null || EditorStatsFamily(family)?.Signature == history.Signature))
                yield return history;
        }
    }

    internal NpcEditorScopedStatsHistory? EditorPlayfieldStats(int resourceId, string hash, int bandIndex, string signature)
    {
        if (resourceId <= 0 || EditorStats.Scopes == null)
            return null;
        var history = EditorStats.Scopes.GetValueOrDefault(NpcEditorStatsJournal.PlayfieldKey(resourceId, hash, bandIndex));
        return history?.Signature == signature ? history : null;
    }

    internal IReadOnlyDictionary<int, string>? ApplyScopedEditorStats(
        MobTemplate template, int resourceId, Dictionary<int, int> resolvedStats)
    {
        if (EditorStats.Scopes is not { Count: > 0 } || template.UnresolvedPlaceholder || template.EditorLootLeaf == null
            || !TryGetStatsBand(template.Hash, template.AuthoredBandIndex, out NpcLeaf leaf)
            || template.EditorLootLeaf.EditorLootTemplateSignature != leaf.EditorLootTemplateSignature)
            return null;

        Dictionary<int, string>? sources = null;
        foreach (NpcEditorScopedStatsHistory history in MatchingEditorStatFamilies(template.Hash))
        {
            foreach ((int stat, int value) in history.Revisions[^1].Values)
            {
                if (template.EditorStatSources?.ContainsKey(stat) == true)
                    continue;
                Apply(stat, value, "NPC editor family " + history.FamilyHash);
            }
        }
        if (EditorPlayfieldStats(resourceId, template.Hash, template.AuthoredBandIndex, leaf.EditorLootTemplateSignature) is { } local)
        {
            foreach ((int stat, int value) in local.Revisions[^1].Values)
                Apply(stat, value, $"NPC editor resource playfield {resourceId} band override");
        }
        return sources;

        void Apply(int stat, int value, string source)
        {
            sources ??= template.EditorStatSources == null ? new() : new(template.EditorStatSources);
            resolvedStats[stat] = value;
            sources[stat] = source;
        }
    }
}
