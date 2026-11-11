namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using AORebirth.Core.GameData;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;

internal sealed record LootTargetTemplate(string Hash, string Name, int BandIndex, int Level, string[] FamilyHashes, bool IsBoss);
internal sealed record LootTargetSpawn(int PlayfieldId, int AuthoredIndex, string AuthoredSignature, string Hash,
    int MinimumLevel, int MaximumLevel);
internal sealed record LootTargets(LootTargetTemplate[] Templates, LootTargetSpawn[] Spawns);
internal sealed record LootCoverageQuery(string? Search, int? PlayfieldId, string? Family, int? MinLevel, int? MaxLevel,
    string? Boss, string? Category, string? Status, int Offset, int Limit);
internal sealed record LootCoverageRow(string Kind, string Key, string Name, int? PlayfieldId, int? Level, string[] FamilyHashes,
    string Boss, string? Category, string Status, string Detail, string[]? Categories = null);
internal sealed record LootCoveragePage(int Total, LootCoverageRow[] Rows, string Note);
internal sealed record LootComparisonRow(string Status, string Item, int LowId, int? HighId, int? Quality, string? CaptureSource,
    string? Classification, string Detail);
internal sealed record LootComparisonResult(string TemplateHash, int Total, LootComparisonRow[] Rows, string Note);

internal sealed class NpcEditorLootCoverage(GameDataStore data, IItemTemplateCatalog items,
    NpcEditorNamedLoot named, NpcEditorLootEvidence evidence)
{
    const int MaxRows = 200000;
    const int MaxExportRows = 10000;
    const string Note = "Coverage describes available definitions and reviewed projections. Missing observations do not establish missing retail loot.";
    readonly SemaphoreSlim _scanGate = new(1, 1);
    volatile CoverageSnapshot? _coverage;

    internal LootTargets Targets(string? query, int? playfieldId, CancellationToken ct)
    {
        if (query?.Length > 128 || playfieldId is <= 0)
            throw new ArgumentException("Supply a short template search and a positive resource playfield ID.");
        string term = query?.Trim() ?? "";
        NpcTemplateCatalog catalog = data.EditorLootCatalog;
        var templates = new List<LootTargetTemplate>();
        foreach (NpcLeaf leaf in catalog.EditorLootLeaves.OrderBy(row => row.Hash, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (leaf.Hash == MobTemplate.FallbackHash)
                continue;
            string[] family = catalog.EditorFamiliesFor(leaf.Hash);
            bool hashMatches = Match(leaf.Hash, term) || family.Any(hash => Match(hash, term));
            for (int index = 0; index < leaf.Bands.Length && templates.Count < 150; index++)
            {
                NpcLevelBand band = leaf.Bands[index];
                if (hashMatches || Match(band.Name, term))
                    templates.Add(new(leaf.Hash, band.Name ?? leaf.Hash, index, band.Level, family,
                        catalog.EditorLootFeatures(leaf, band).Contains(NpcFeature.Boss)));
            }
            if (templates.Count >= 150)
                break;
        }
        LootTargetSpawn[] spawns = playfieldId is int id
            ? AuthoredPoints(id).Where(point => term.Length == 0 || Match(point.Hash, term)
                || point.AuthoredIndex.ToString(CultureInfo.InvariantCulture) == term).Take(150).ToArray() : [];
        return new([.. templates], spawns);
    }

    internal LootCoveragePage Read(LootCoverageQuery query, CancellationToken ct, bool export = false)
    {
        if (query.Search?.Length > 128 || query.Family?.Length > 64 || query.PlayfieldId is <= 0
            || query.MinLevel is <= 0 || query.MaxLevel is <= 0 || query.MinLevel > query.MaxLevel
            || query.Offset < 0 || query.Limit is < 1 or > 200)
            throw new ArgumentException("Use a short search, an ordered level range, and a page size from 1 to 200.");
        string term = query.Search?.Trim() ?? "";
        LootCoverageRow[] matches = Scan(ct).Where(row =>
            (query.PlayfieldId == null || row.PlayfieldId == query.PlayfieldId)
            && (query.Family == null || row.FamilyHashes.Contains(query.Family, StringComparer.OrdinalIgnoreCase))
            && (query.MinLevel == null || row.Level >= query.MinLevel)
            && (query.MaxLevel == null || row.Level <= query.MaxLevel)
            && (query.Boss == null || row.Boss.Equals(query.Boss, StringComparison.OrdinalIgnoreCase))
            && (query.Category == null || string.Equals(row.Category, query.Category, StringComparison.OrdinalIgnoreCase)
                || row.Categories?.Contains(query.Category, StringComparer.OrdinalIgnoreCase) == true)
            && (query.Status == null || row.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
            && (Match(row.Name, term) || Match(row.Key, term) || Match(row.Detail, term))).ToArray();
        if (export && matches.Length > MaxExportRows)
            throw new ArgumentException("Export allows up to 10,000 rows. Narrow the coverage filters.");
        return new(matches.Length, export ? matches : matches.Skip(query.Offset).Take(query.Limit).ToArray(), Note);
    }

    LootCoverageRow[] Scan(CancellationToken ct)
    {
        var state = named.Describe();
        var staged = evidence.Snapshot();
        NpcTemplateCatalog catalog = data.EditorLootCatalog;
        if (Reusable(_coverage) is { } available)
            return available.Rows;
        if (!_scanGate.Wait(0, ct))
            throw new NpcEditorRuntimeException(429, "A coverage scan is already running. Try again after it completes.");
        try
        {
            if (Reusable(_coverage) is { } cached)
                return cached.Rows;
            var rows = new List<LootCoverageRow>();
            var registry = state.UnavailableReason == null ? new NamedLootRegistry(state.Tables, state.Assignments) : NamedLootRegistry.Empty;
            var categories = state.Tables.ToDictionary(table => table.Id, table => table.Category, StringComparer.Ordinal);
            var assignmentScopes = state.Assignments.ToDictionary(row => row.Id, row => row.Scope, StringComparer.Ordinal);
            if (state.UnavailableReason is { } registryProblem)
                rows.Add(new("Registry", "named-loot", "Named loot registry", null, null, [], "", null, "UNRESOLVED", registryProblem));
            var byTemplate = staged.Rows.Where(row => row.TemplateHash != null && row.Review != "rejected")
                .ToLookup(row => row.TemplateHash!, StringComparer.Ordinal);
            var observedContexts = new HashSet<string>(StringComparer.Ordinal);
            var representedEvidence = new HashSet<string>(StringComparer.Ordinal);
            var bands = new Dictionary<(string Hash, int Index), BandInputs>();
            var itemHashes = new Dictionary<string, (bool Valid, int[] Ids)>(StringComparer.Ordinal);
            foreach (NpcLeaf leaf in catalog.EditorLootLeaves.OrderBy(leaf => leaf.Hash, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                if (leaf.Hash == MobTemplate.FallbackHash)
                    continue;
                string[] families = catalog.EditorFamiliesFor(leaf.Hash);
                for (int index = 0; index < leaf.Bands.Length; index++)
                {
                    NpcLevelBand band = leaf.Bands[index];
                    HashSet<NpcFeature> features = catalog.EditorLootFeatures(leaf, band);
                    var context = new NamedLootContext(band.Level, 0, leaf.Hash, index, families, IsBoss: features.Contains(NpcFeature.Boss));
                    IReadOnlyList<MobItemTableEntry> native = catalog.EditorLootBandRows(leaf, index);
                    var hashes = native.Where(entry => entry != null && entry.Chance > 0 && entry.Repeats > 0)
                        .Select(entry => entry.Hash).Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.Ordinal).ToArray();
                    var input = new BandInputs(band.Name ?? leaf.Hash, context, hashes, features.Contains(NpcFeature.NoCombat));
                    bands[(leaf.Hash, index)] = input;
                    AddBand(input, context, "Template band", $"{leaf.Hash}/{index}", null);
                }
            }

            foreach (int playfield in AuthoredPlayfields())
            {
                ct.ThrowIfCancellationRequested();
                foreach (LootTargetSpawn point in AuthoredPoints(playfield))
                {
                    ct.ThrowIfCancellationRequested();
                    string[] hashes = catalog.EditorStatsFamily(point.Hash)?.Leaves ?? [point.Hash];
                    bool resolved = false;
                    foreach (string hash in hashes)
                    {
                        if (!catalog.TryGetLeaf(hash, out NpcLeaf leaf) || hash == MobTemplate.FallbackHash)
                            continue;
                        for (int index = 0; index < leaf.Bands.Length; index++)
                        {
                            int minimum = index == 0 ? 1 : Midpoint(leaf.Bands[index - 1].Level, leaf.Bands[index].Level) + 1;
                            int maximum = index == leaf.Bands.Length - 1 ? int.MaxValue
                                : Midpoint(leaf.Bands[index].Level, leaf.Bands[index + 1].Level);
                            int lower = Math.Max(minimum, point.MinimumLevel);
                            int upper = Math.Min(maximum, point.MaximumLevel);
                            if (lower > upper || !bands.TryGetValue((hash, index), out BandInputs? band))
                                continue;
                            resolved = true;
                            int level = Math.Clamp(Math.Clamp(band.Context.Level, lower, upper), leaf.MinLevel, leaf.MaxLevel);
                            var context = band.Context with
                            {
                                Level = level, PlayfieldId = playfield, AuthoredIndex = point.AuthoredIndex,
                                AuthoredSignature = point.AuthoredSignature
                            };
                            AddBand(band, context, "Authored point", $"{playfield}/{point.AuthoredIndex}/{hash}/{index}", playfield);
                        }
                    }
                    if (!resolved)
                        Add(new("Authored point", $"{playfield}/{point.AuthoredIndex}", point.Hash, playfield, point.MinimumLevel, [],
                            "", null, "UNRESOLVED", "The authored spawn hash has no resolved NPC template."));
                }
            }

            var usedTables = state.Assignments.Where(assignment => assignment.TableId != null)
                .Select(assignment => assignment.TableId!).ToHashSet(StringComparer.Ordinal);
            var duplicateTables = state.Tables.GroupBy(table => JsonSerializer.Serialize(table.Groups))
                .Where(group => group.Count() > 1).SelectMany(group => group.Select(table => table.Id)).ToHashSet(StringComparer.Ordinal);
            foreach (NamedLootTable table in state.Tables)
            {
                int[] invalid = table.Groups.SelectMany(group => group.Entries).SelectMany(entry => new[] { entry.LowId, entry.HighId })
                    .Distinct().Where(id => !items.TryGet(id, out _)).ToArray();
                string status = invalid.Length > 0 ? "UNRESOLVED" : !usedTables.Contains(table.Id) ? "UNUSED_TABLE"
                    : duplicateTables.Contains(table.Id) ? "DUPLICATE_DEFINITION" : "CONFIGURED";
                string detail = invalid.Length > 0 ? $"Unknown RDB template IDs {string.Join(", ", invalid)}."
                    : status == "DUPLICATE_DEFINITION" ? "Another named table has the same groups."
                    : status == "UNUSED_TABLE" ? "No assignment references this table." : "At least one assignment references this table.";
                Add(new("Named table", table.Id, table.Name, null, null, [], "", table.Category, status, detail));
            }
            foreach (NamedLootAssignment assignment in state.Assignments)
            {
                string? category = assignment.TableId == null ? null : categories.GetValueOrDefault(assignment.TableId);
                Add(new("Assignment", assignment.Id, AssignmentDefinition(assignment), assignment.PlayfieldId, null,
                    assignment.FamilyHash is { } family ? [family] : [], assignment.Scope == "namedBoss" ? "Named boss" : "",
                    category, state.UnavailableReason == null ? "CONFIGURED" : "UNRESOLVED",
                    $"{assignment.Merge} · {assignment.TableId ?? assignment.TargetGroup}"));
            }
            foreach (LootEvidenceObservation row in staged.Rows.Where(row => row.Review != "rejected"))
            {
                string status = row.TemplateHash == null || !items.TryGet(row.LowId, out _) || !items.TryGet(row.HighId, out _)
                    ? "UNRESOLVED" : representedEvidence.Contains(row.Id) ? "OBSERVATION_REPRESENTED"
                    : observedContexts.Contains(row.Id) && row.Classification == "OBSERVED_AVAILABLE_LOOT"
                        ? "OBSERVATION_NOT_CONFIGURED" : "EVIDENCE_INCOMPLETE";
                Add(new("Observation", row.Id, row.EnemyName ?? row.TemplateHash ?? "Unlinked observation", row.PlayfieldId,
                    row.Level, row.TemplateHash == null ? [] : catalog.EditorFamiliesFor(row.TemplateHash), "", null, status,
                    $"{row.LowId}/{row.HighId} QL {row.Quality} · {row.Classification} · {row.CaptureSource}"));
            }
            LootCoverageRow[] result = rows.ToArray();
            _coverage = new(catalog, state.Revision, staged.Revision, DateTimeOffset.UtcNow.AddMinutes(2), result);
            return result;

            void Add(LootCoverageRow row)
            {
                if (rows.Count >= MaxRows)
                    throw new NpcEditorRuntimeException(503, "The coverage scan exceeds 200,000 definition rows.");
                rows.Add(row);
            }

            void AddBand(BandInputs band, NamedLootContext context, string kind, string key, int? playfieldId)
            {
                NamedLootResolution resolution = registry.Resolve(context);
                NamedLootResolvedGroup[] active = resolution.Groups.Where(group => group.Group.ChanceBasisPoints > 0).ToArray();
                NamedLootEntry[] entries = active.SelectMany(group => group.Group.Entries)
                    .Where(entry => entry.ChanceBasisPoints > 0 && entry.Condition?.Matches(context) != false).ToArray();
                bool ordinary = band.Hashes.Length > 0;
                bool invalid = band.Hashes.Any(hash => !ResolveHash(hash).Valid)
                    || entries.Any(entry => !items.TryGet(entry.LowId, out _) || !items.TryGet(entry.HighId, out _));
                bool empty = active.Any(group => group.Group.EmptyWeight > 0) && entries.Length == 0 && !ordinary;
                LootEvidenceObservation[] observations = byTemplate[context.TemplateHash!]
                    .Where(row => row.PlayfieldId == null || row.PlayfieldId == playfieldId).ToArray();
                foreach (LootEvidenceObservation observation in observations)
                {
                    if (observation.Classification != "OBSERVED_AVAILABLE_LOOT")
                        continue;
                    if (observation.PlayfieldId == playfieldId && observation.Level == context.Level)
                        observedContexts.Add(observation.Id);
                    if (entries.Any(entry => entry.LowId == observation.LowId && entry.HighId == observation.HighId
                            && entry.MinQuality <= observation.Quality && entry.MaxQuality >= observation.Quality)
                        || band.Hashes.Any(hash =>
                        {
                            int[] ids = ResolveHash(hash).Ids;
                            return ids.Contains(observation.LowId) && ids.Contains(observation.HighId);
                        }))
                        representedEvidence.Add(observation.Id);
                }
                string status = invalid ? "UNRESOLVED" : empty || band.NoCombat && !ordinary && entries.Length == 0
                    ? "INTENTIONALLY_EMPTY" : !ordinary && entries.Length == 0 ? "UNCONFIGURED"
                    : observations.Any(row => row.Classification == "OBSERVED_AVAILABLE_LOOT") ? "CONFIGURED" : "EVIDENCE_INCOMPLETE";
                string[] selectedCategories = active.Select(group => categories[group.TableId]).Distinct(StringComparer.Ordinal).ToArray();
                string[] dedicated = context.IsBoss ? active.Where(group => assignmentScopes[group.AssignmentId] is "namedBoss" or "authoredPoint")
                    .Select(group => group.TableId).Distinct(StringComparer.Ordinal).ToArray() : [];
                string special = dedicated.Length > 0 ? $" Dedicated boss tables: {string.Join(", ", dedicated)}." : "";
                string detail = invalid ? "One or more configured item references cannot be resolved."
                    : $"{band.Hashes.Length} native item hashes, {active.Length} applicable named groups at level {context.Level}.{special}";
                Add(new(kind, key, band.Name, playfieldId, context.Level, context.FamilyHashes ?? [],
                    context.IsBoss ? "Named boss" : "Ordinary", selectedCategories.Length == 0 ? null : string.Join(", ", selectedCategories),
                    status, detail, selectedCategories));
            }

            (bool Valid, int[] Ids) ResolveHash(string hash)
            {
                if (itemHashes.TryGetValue(hash, out var found))
                    return found;
                var leafItems = new List<HashInstance>();
                data.CollectHashLeafInstances(hash, leafItems);
                int[] ids = leafItems.SelectMany(leaf => leaf.TemplateIds).Distinct().ToArray();
                return itemHashes[hash] = (ids.Length > 0 && ids.All(id => items.TryGet(id, out _)), ids);
            }
        }
        finally
        {
            _scanGate.Release();
        }

        CoverageSnapshot? Reusable(CoverageSnapshot? snapshot)
            => snapshot != null && ReferenceEquals(snapshot.Catalog, catalog) && snapshot.RegistryRevision == state.Revision
                && snapshot.EvidenceRevision == staged.Revision && snapshot.ExpiresUtc > DateTimeOffset.UtcNow ? snapshot : null;
    }

    internal LootComparisonResult Compare(LootComparisonInput input)
    {
        var rows = new List<LootComparisonRow>();
        var ordinary = input.ConfiguredRows.Where(row => row.Chance is null or > 0).SelectMany(row => row.Items
            .Select(item => (Item: item, Min: row.MinQuality, Max: row.MaxQuality))).ToArray();
        var configured = ordinary.SelectMany(row => row.Item.TemplateIds.Select(id => (Id: id, Name: row.Item.Name)))
            .Concat(input.Candidates.Where(candidate => candidate.Eligible).Select(candidate => (Id: candidate.LowId, candidate.Name)))
            .DistinctBy(item => item.Id).ToArray();
        var linked = new HashSet<int>();
        foreach (LootEvidenceObservation row in evidence.Snapshot().Rows)
        {
            bool exact = input.TemplateHash.Length > 0 && row.TemplateHash == input.TemplateHash;
            bool possible = row.TemplateHash == null && input.MonsterData.HasValue && row.MonsterData == input.MonsterData
                && string.Equals(input.Name, row.EnemyName, StringComparison.OrdinalIgnoreCase);
            if (row.Review == "rejected" || (!exact && !possible) || (row.PlayfieldId.HasValue && row.PlayfieldId != input.PlayfieldId))
                continue;
            bool itemExists = items.TryGet(row.LowId, out ItemTemplate template) && items.TryGet(row.HighId, out _);
            bool represented = ordinary.Any(candidate => candidate.Item.TemplateIds.Contains(row.LowId)
                && candidate.Item.TemplateIds.Contains(row.HighId) && (candidate.Min == null || candidate.Min <= row.Quality)
                && (candidate.Max == null || candidate.Max >= row.Quality))
                || input.Candidates.Any(candidate => candidate.Eligible && candidate.LowId == row.LowId && candidate.HighId == row.HighId
                    && candidate.MinQuality <= row.Quality && candidate.MaxQuality >= row.Quality);
            bool observed = exact && row.Classification == "OBSERVED_AVAILABLE_LOOT";
            if (observed && represented)
            {
                linked.Add(row.LowId);
                linked.Add(row.HighId);
            }
            string status = !itemExists ? "UNRESOLVED_ITEM" : !exact ? "POSSIBLE_NPC_MATCH" : !observed ? "EVIDENCE_INCOMPLETE"
                : represented ? "OBSERVATION_REPRESENTED" : "OBSERVATION_NOT_CONFIGURED";
            rows.Add(new(status, template?.Name ?? "Unknown RDB item", row.LowId, row.HighId, row.Quality, row.CaptureSource,
                row.Classification, !exact ? "Name and MonsterData suggest a candidate. The observation has no exact template binding."
                    : "Comparison covers the selected NPC configuration and known item/QL candidates."));
        }
        rows.AddRange(configured.Where(item => !linked.Contains(item.Id)).Select(item => new LootComparisonRow(
            "CONFIGURED_WITHOUT_EXACT_EVIDENCE", item.Name, item.Id, null, null, null, null,
            "No staged, exact observation supports this configured candidate.")));
        return new(input.TemplateHash, rows.Count, rows.Take(500).ToArray(), Note);
    }

    internal static string AssignmentDefinition(NamedLootAssignment assignment) => assignment.Scope switch
    {
        "global" => "Global defaults",
        "family" => $"NPC family {assignment.FamilyHash}",
        "template" => $"NPC template {assignment.TemplateHash}",
        "templateBand" => $"Template {assignment.TemplateHash}, band {assignment.BandIndex}",
        "playfield" => $"Resource playfield {assignment.PlayfieldId}",
        "authoredPoint" => $"Resource playfield {assignment.PlayfieldId}, authored point {assignment.AuthoredIndex}",
        "namedBoss" => $"Boss template {assignment.TemplateHash}",
        _ => assignment.Scope
    };

    IEnumerable<int> AuthoredPlayfields()
    {
        if (!Directory.Exists(data.PlayfieldsPath))
            yield break;
        foreach (DirectoryInfo directory in new DirectoryInfo(data.PlayfieldsPath).EnumerateDirectories())
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) == 0
                && int.TryParse(directory.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0)
                yield return id;
        }
    }

    IEnumerable<LootTargetSpawn> AuthoredPoints(int playfieldId)
    {
        PlayfieldSpawnEntry[] entries = data.ReadAuthoredEditorSpawns(playfieldId).Spawns ?? [];
        for (int index = 0; index < entries.Length; index++)
        {
            PlayfieldSpawnEntry entry = entries[index];
            if (!SpawnContentValidation.IsValid(entry) || entry.StaticTemplateId.HasValue
                || ((IGameData)data).IsStaticSpawnHash(entry.HashText) || data.SpawnExclusions.Excludes(entry.HashText, playfieldId))
                continue;
            string signature = NpcEditorSpawnStore.Signature(entry);
            var (history, _) = data.SpawnEditor.Describe(playfieldId, index, signature);
            NpcEditorSpawnSettings? saved = history.LastOrDefault()?.After;
            yield return new(playfieldId, index, signature, saved?.Hash ?? entry.HashText,
                saved?.MinLevel ?? entry.MinLevel, saved?.MaxLevel ?? entry.MaxLevel);
        }
    }

    static int Midpoint(int low, int high) => (int)(((long)low + high) / 2);
    static bool Match(string? value, string term) => term.Length == 0 || value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    sealed record BandInputs(string Name, NamedLootContext Context, string[] Hashes, bool NoCombat);
    sealed record CoverageSnapshot(NpcTemplateCatalog Catalog, int RegistryRevision, int EvidenceRevision,
        DateTimeOffset ExpiresUtc, LootCoverageRow[] Rows);
}
