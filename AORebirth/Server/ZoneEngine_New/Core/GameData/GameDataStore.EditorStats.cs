namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using ZoneEngine_New.Core.NpcEditor;

internal sealed record NpcEditorStatsSnapshot(
    NpcLevelBand? Band, string? UnavailableReason, NpcEditorStatsRevision[] History,
    IReadOnlyDictionary<int, int>? Effective = null, IReadOnlyDictionary<int, string>? Sources = null, string[]? Overlaps = null)
{
    internal int Revision => History.Length;
    internal IReadOnlyDictionary<int, int> Values => History.Length > 0 ? History[^1].Values : Empty;
    static readonly Dictionary<int, int> Empty = new();
}

public sealed partial class GameDataStore
{
    readonly NpcEditorStatsJournalFile _editorStatsFile;

    NpcTemplateCatalog WithLoadedEditorStats(NpcTemplateCatalog catalog)
    {
        try
        {
            return catalog.WithEditorStats(_editorStatsFile.Read(catalog));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            const string reason = "The NPC stat overlay failed validation. See the ZoneEngine log and reload NpcTemplates after repair.";
            _logger.Error(error, reason);
            return catalog.WithEditorStats(_npcTemplates.EditorStats with { LoadError = reason });
        }
    }

    internal NpcEditorStatsSnapshot GetEditorStats(NpcEditorStatsTarget target)
        => GetEditorStats(_npcTemplates, target);

    internal NpcEditorStatFamilyBinding? EditorStatsFamily(string family) => _npcTemplates.EditorStatsFamily(family);

    internal IReadOnlyDictionary<int, string>? ApplyScopedEditorStats(
        Mobs.MobTemplate template, int resourcePlayfieldId, Dictionary<int, int> resolvedStats)
        => _npcTemplates.ApplyScopedEditorStats(template, resourcePlayfieldId, resolvedStats);

    NpcEditorStatsSnapshot GetEditorStats(NpcTemplateCatalog catalog, NpcEditorStatsTarget target)
    {
        if (!catalog.TryGetStatsBand(target.TemplateHash, target.BandIndex, out NpcLeaf leaf))
            return new(null, target.UnavailableReason ?? "Select an NPC with an authored template band.", []);
        NpcLevelBand band = leaf.Bands[target.BandIndex];
        string? unavailable = target.UnavailableReason ?? _editorStatsFile.ConfigurationProblem ?? catalog.EditorStats.LoadError;
        if (target.Signature != leaf.EditorLootTemplateSignature)
            return new(band, unavailable ?? "The authored NPC template changed. Select a fresh spawn to edit its stats.", []);

        NpcEditorStatsHistory? exact = catalog.EditorStats.Find(target.TemplateHash, target.BandIndex);
        NpcEditorStatsRevision[] exactHistory = exact?.Signature == target.Signature ? exact.Revisions : [];
        NpcEditorStatsRevision[] history = exactHistory;
        NpcEditorStatFamilyBinding? familyBinding = null;
        if (target.Scope == "family")
        {
            if (target.FamilyHash is not { } family || catalog.EditorStatsFamily(family) is not { } binding
                || target.ScopeSignature != binding.Signature || !binding.Leaves.Contains(target.TemplateHash, StringComparer.Ordinal))
                return new(band, "The authored family membership changed or is unavailable. Refresh the selected scope.", []);
            familyBinding = binding;
            var scoped = catalog.EditorStats.Scopes?.GetValueOrDefault(NpcEditorStatsJournal.FamilyKey(family));
            history = scoped?.Signature == binding.Signature ? scoped.Revisions : [];
        }
        else if (target.Scope == "playfield")
        {
            if (target.ResourcePlayfieldId is not > 0)
                return new(band, "The selected NPC has no authoritative resource playfield.", []);
            history = catalog.EditorPlayfieldStats(target.ResourcePlayfieldId.Value, target.TemplateHash,
                target.BandIndex, target.Signature)?.Revisions ?? [];
        }
        else if (target.Scope != "templateBand")
        {
            return new(band, "Select a supported persistent stat scope.", []);
        }

        var projected = new Mobs.MobTemplate
        {
            Hash = leaf.Hash, AuthoredBandIndex = target.BandIndex, EditorLootLeaf = leaf,
            Stats = new(band.Stats ?? new())
        };
        if (exactHistory.Length > 0)
        {
            var exactSources = new Dictionary<int, string>();
            if (NpcEditorStatPolicy.TryApplyConfigured(projected.Stats, exactHistory[^1].Values, exactSources, out _))
                projected.EditorStatSources = exactSources;
        }
        var sources = catalog.ApplyScopedEditorStats(projected, target.ResourcePlayfieldId ?? 0, projected.Stats)
            ?? projected.EditorStatSources;
        string[] overlaps = StatScopeOverlaps(catalog, target, familyBinding, exactHistory);
        return new(band, unavailable, history, projected.Stats, sources, overlaps);
    }

    static string[] StatScopeOverlaps(
        NpcTemplateCatalog catalog, NpcEditorStatsTarget target, NpcEditorStatFamilyBinding? family,
        NpcEditorStatsRevision[] exactHistory)
    {
        var definitions = new SortedSet<string>(StringComparer.Ordinal);
        if (target.Scope == "playfield" && exactHistory is { Length: > 0 } && exactHistory[^1].Values.Count > 0)
            definitions.Add($"Template band {target.TemplateHash}/{target.BandIndex + 1}");
        if (family != null)
        {
            var members = new HashSet<string>(family.Leaves, StringComparer.Ordinal);
            foreach (NpcEditorStatsHistory band in catalog.EditorStats.Bands.Values)
            {
                if (band.Revisions[^1].Values.Count > 0 && members.Contains(band.TemplateHash))
                    definitions.Add($"Template band {band.TemplateHash}/{band.BandIndex + 1}");
            }
            foreach (NpcEditorScopedStatsHistory scope in catalog.EditorStats.Scopes?.Values.AsEnumerable() ?? [])
            {
                if (scope.Revisions[^1].Values.Count == 0)
                    continue;
                if (scope is { Scope: "family", FamilyHash: { } other } && other != target.FamilyHash
                    && catalog.EditorStatsFamily(other)?.Leaves.Any(members.Contains) == true)
                    definitions.Add("Family " + other);
                if (scope is { Scope: "playfield", TemplateHash: { } hash } && members.Contains(hash))
                    definitions.Add($"Resource playfield {scope.ResourcePlayfieldId}, band {hash}/{scope.BandIndex + 1}");
            }
        }
        else
        {
            foreach (NpcEditorScopedStatsHistory scope in catalog.MatchingEditorStatFamilies(target.TemplateHash))
                definitions.Add("Family " + scope.FamilyHash);
            if (target.Scope == "templateBand")
            {
                foreach (NpcEditorScopedStatsHistory scope in catalog.EditorStats.Scopes?.Values.AsEnumerable() ?? [])
                {
                    if (scope.Scope == "playfield" && scope.TemplateHash == target.TemplateHash
                        && scope.BandIndex == target.BandIndex && scope.Revisions[^1].Values.Count > 0)
                        definitions.Add($"Resource playfield {scope.ResourcePlayfieldId}, band {target.TemplateHash}/{target.BandIndex + 1}");
                }
            }
        }
        return definitions.Take(64).ToArray();
    }

    internal NpcEditorStatsSnapshot ChangeEditorStats(
        NpcEditorStatsTarget target, int expectedRevision, string actor, string action,
        Func<NpcEditorStatsSnapshot, Dictionary<int, int>> edit)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new ArgumentException("A valid GM actor is required.", nameof(actor));

        lock (_lootCatalogSync)
        {
            NpcTemplateCatalog catalog = _npcTemplates;
            NpcEditorStatsSnapshot current = GetEditorStats(catalog, target);
            if (current.UnavailableReason != null || !catalog.TryGetStatsBand(target.TemplateHash, target.BandIndex, out NpcLeaf leaf))
                throw new NpcEditorRuntimeException(409, current.UnavailableReason ?? "The authored NPC band is unavailable.");
            if (current.Revision != expectedRevision)
                throw new NpcEditorRuntimeException(409, "The stat revision changed. Refresh before saving.");
            if (current.Revision >= NpcEditorStatsJournalFile.MaxRevisionsPerBand)
                throw new NpcEditorRuntimeException(409, "The stat history reached its revision limit.");

            Dictionary<int, int> updatedValues = edit(current);
            if (updatedValues.Count == current.Values.Count
                && updatedValues.All(pair => current.Values.TryGetValue(pair.Key, out int value) && value == pair.Value))
                throw new NpcEditorRuntimeException(400, "The stats already match that configuration.");
            NpcEditorStatsJournal existing = catalog.EditorStats;
            NpcEditorStatsRevision[] history =
                [.. current.History, new(current.Revision + 1, DateTimeOffset.UtcNow, actor, action, updatedValues)];
            NpcEditorStatsJournal next;
            if (target.Scope == "templateBand")
            {
                if (!NpcEditorStatsJournalFile.ValidateBand(leaf, target.BandIndex, updatedValues, out string? error))
                    throw new NpcEditorRuntimeException(400, error!);
                var bands = new Dictionary<string, NpcEditorStatsHistory>(existing.Bands, StringComparer.Ordinal)
                {
                    [NpcEditorStatsJournal.Key(target.TemplateHash, target.BandIndex)] =
                        new(target.TemplateHash, target.BandIndex, target.Signature, history)
                };
                next = existing with { Bands = bands };
            }
            else
            {
                if (!NpcEditorStatsJournalFile.ValidScopedValues(updatedValues))
                    throw new NpcEditorRuntimeException(400, "Only supported non-vital stats can be saved at family or playfield scope.");
                Dictionary<string, NpcEditorScopedStatsHistory> scopes = existing.Scopes == null
                    ? new(StringComparer.Ordinal) : new(existing.Scopes, StringComparer.Ordinal);
                if (target.Scope == "family")
                {
                    RequireNoFamilyConflict(catalog, target.FamilyHash!, updatedValues.Keys);
                    scopes[NpcEditorStatsJournal.FamilyKey(target.FamilyHash!)] =
                        new("family", target.FamilyHash, null, null, null, target.ScopeSignature!, history);
                }
                else
                {
                    int resourceId = target.ResourcePlayfieldId!.Value;
                    scopes[NpcEditorStatsJournal.PlayfieldKey(resourceId, target.TemplateHash, target.BandIndex)] =
                        new("playfield", null, resourceId, target.TemplateHash, target.BandIndex, target.Signature, history);
                }
                next = existing with { Version = 2, Scopes = scopes };
            }
            NpcEditorStatsJournal updated = _editorStatsFile.Write(existing, next);
            NpcTemplateCatalog revised = catalog.WithEditorStats(updated);
            _npcTemplates = revised;
            return GetEditorStats(revised, target);
        }
    }

    static void RequireNoFamilyConflict(NpcTemplateCatalog catalog, string family, IEnumerable<int> stats)
    {
        if (catalog.EditorStats.Scopes == null || catalog.EditorStatsFamily(family) is not { } binding)
            return;
        var members = new HashSet<string>(binding.Leaves, StringComparer.Ordinal);
        foreach (NpcEditorScopedStatsHistory scope in catalog.EditorStats.Scopes.Values)
        {
            if (scope is { Scope: "family", FamilyHash: { } other } && other != family
                && stats.Any(scope.Revisions[^1].Values.ContainsKey)
                && catalog.EditorStatsFamily(other)?.Leaves.Any(members.Contains) == true)
                throw new NpcEditorRuntimeException(409, $"Family {other} already assigns the same stat to shared members.");
        }
    }
}
