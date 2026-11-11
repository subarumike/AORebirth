namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AORebirth.Core.GameData;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;
using ZoneEngine_New.Core.Playfield;

public sealed partial class GameDataStore
{
    readonly NamedLootJournalFile _namedLootFile;
    readonly ConditionalWeakTable<HashSpawnPoint, PointIdentity> _namedLootPointIdentities = new();
    IItemTemplateCatalog? _namedLootItems;
    volatile NamedLootSnapshot _namedLoot = new(NamedLootJournal.Empty, NamedLootRegistry.Empty);

    sealed record PointIdentity(int Index, string Signature);
    sealed record NamedLootSnapshot(NamedLootJournal Journal, NamedLootRegistry Registry, NpcTemplateCatalog? Catalog = null);

    internal void InitializeNamedLoot(IItemTemplateCatalog items)
    {
        lock (_lootCatalogSync)
        {
            if (_namedLootItems != null)
            {
                if (!ReferenceEquals(_namedLootItems, items))
                    throw new InvalidOperationException("Named loot already uses a different item catalog.");
                return;
            }
            _namedLootItems = items;
            ReloadNamedLoot();
        }
    }

    string? ReloadNamedLoot()
    {
        if (_namedLootItems == null)
            return null;
        NpcTemplateCatalog catalog = _npcTemplates;
        try
        {
            NamedLootJournal journal = _namedLootFile.Read();
            NamedLootRegistry registry = NamedLootRegistry.Empty;
            if (journal.Latest is { } revision)
            {
                if (ValidateNamedLoot(catalog, revision.Tables, revision.Assignments) is string problem)
                    throw new InvalidDataException(problem);
                registry = new(revision.Tables, revision.Assignments);
            }
            _namedLoot = new(journal, registry, catalog);
            return null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            const string error = "The named NPC loot overlay failed validation. Named assignments are inactive until repaired and reloaded.";
            _logger.Error(exception, error);
            _namedLoot = new(_namedLoot.Journal with { LoadError = error }, NamedLootRegistry.Empty, catalog);
            return error;
        }
    }

    internal NamedLootRegistryState DescribeNamedLoot()
    {
        NamedLootJournal journal = _namedLoot.Journal;
        return new(journal.Revisions.Length, journal.Latest?.Tables ?? [], journal.Latest?.Assignments ?? [],
            journal.Revisions.Select(revision => new NamedLootHistoryRow(
                revision.Revision, revision.AtUtc, revision.Actor, revision.Action, revision.Target)).ToArray(),
            _namedLootFile.ConfigurationProblem ?? journal.LoadError);
    }

    internal NamedLootResolution ResolveNamedLoot(NpcCharacter npc, bool includeAuthoringContext = false)
    {
        NamedLootSnapshot current = _namedLoot;
        NamedLootContext context = CaptureNamedLootContext(npc, current.Catalog ?? _npcTemplates,
            includeAuthoringContext || current.Registry.NeedsFamilies, includeAuthoringContext || current.Registry.NeedsAuthoredPoint);
        return current.Registry.Resolve(context);
    }

    internal NamedLootContext CaptureNamedLootContext(NpcCharacter npc)
        => CaptureNamedLootContext(npc, _namedLoot.Catalog ?? _npcTemplates, includeFamilies: true, includePoint: true);

    NamedLootContext CaptureNamedLootContext(NpcCharacter npc, NpcTemplateCatalog catalog, bool includeFamilies, bool includePoint)
    {
        MobTemplate? template = npc.MobTemplate;
        string? hash = template is { UnresolvedPlaceholder: false } ? template.Hash : null;
        PointIdentity? authored = null;
        if (includePoint && npc.Playfield?.GetService<HashSpawnSystem>() is { } spawns
            && spawns.TryGetSpawnPoint(npc, out HashSpawnPoint point) && !point.IsStatic && point.AuthoredSpawnIndex >= 0)
        {
            authored = _namedLootPointIdentities.GetValue(point,
                static found => new(found.AuthoredSpawnIndex, NpcEditorSpawnStore.Signature(found.Source)));
        }
        return new(npc.Stats.GetOrOne(CharacterStat.Level), npc.Playfield?.ResourceId ?? 0,
            hash, template?.AuthoredBandIndex is >= 0 ? template.AuthoredBandIndex : null,
            includeFamilies && hash != null ? catalog.EditorFamiliesFor(hash) : null,
            authored?.Index, authored?.Signature, template?.Has(NpcFeature.Boss) == true,
            npc.Pet != null || npc.SpawnSource == SpawnSource.Summoned);
    }

    internal NamedLootRegistryState ChangeNamedLoot(
        int expectedRevision, string actor, string action, string target,
        Func<NamedLootJournal, (NamedLootTable[] Tables, NamedLootAssignment[] Assignments)> change)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new NpcEditorRuntimeException(400, "A valid GM actor is required.");
        if (!NamedLootValidation.ValidKey(target))
            throw new NpcEditorRuntimeException(400, "A stable named loot ID is required.");

        lock (_lootCatalogSync)
        {
            NamedLootJournal current = RequireNamedLootRevision(expectedRevision);
            if (current.Revisions.Length >= NamedLootJournalFile.MaxRevisions)
                throw new NpcEditorRuntimeException(409, "The named loot journal reached its revision limit.");

            var (tables, assignments) = change(current);
            NpcTemplateCatalog catalog = _npcTemplates;
            if (ValidateNamedLoot(catalog, tables, assignments) is string error)
                throw new NpcEditorRuntimeException(400, error);
            var revision = new NamedLootRevision(expectedRevision + 1, DateTimeOffset.UtcNow, actor, action, target, tables, assignments);
            NamedLootJournal saved = _namedLootFile.Write(current, current with { Revisions = [.. current.Revisions, revision] });
            _namedLoot = new(saved, new(tables, assignments), catalog);
            return DescribeNamedLoot();
        }
    }

    internal void PreviewNamedLoot(int expectedRevision, Func<NamedLootJournal, NamedLootAssignment[]> preview)
    {
        lock (_lootCatalogSync)
        {
            NamedLootJournal current = RequireNamedLootRevision(expectedRevision);
            if (ValidateNamedLoot(_npcTemplates, current.Latest?.Tables ?? [], preview(current)) is string error)
                throw new NpcEditorRuntimeException(400, error);
        }
    }

    NamedLootJournal RequireNamedLootRevision(int expectedRevision)
    {
        NamedLootJournal journal = _namedLoot.Journal;
        if ((_namedLootFile.ConfigurationProblem ?? journal.LoadError) is string unavailable)
            throw new NpcEditorRuntimeException(409, unavailable);
        if (_namedLootItems == null || journal.Revisions.Length != expectedRevision)
            throw new NpcEditorRuntimeException(409, "The named loot registry revision changed. Refresh before editing.");
        return journal;
    }

    string? ValidateNamedLoot(NpcTemplateCatalog catalog, NamedLootTable[] tables, NamedLootAssignment[] assignments)
    {
        var spawnFiles = new Dictionary<int, PlayfieldSpawnEntry[]>();
        return NamedLootValidation.Check(tables, assignments,
            entry => HashItemMinter.TryGetNamedLootQualityWindow(this, _namedLootItems!, entry.LowId, entry.HighId,
                entry.MinQuality, entry.MaxQuality, out _, out _),
            ValidTarget, MayOverlap);

        bool ValidTarget(NamedLootAssignment row)
        {
            switch (row.Scope)
            {
                case "global":
                    return true;
                case "family":
                    return catalog.EditorStatsFamily(row.FamilyHash!) != null;
                case "template":
                    return catalog.TryGetEditableLootLeaf(row.TemplateHash!, out _);
                case "templateBand":
                    return catalog.TryGetStatsBand(row.TemplateHash!, row.BandIndex!.Value, out _);
                case "namedBoss":
                    return catalog.EditorNamedLootBoss(row.TemplateHash!);
                case "playfield":
                    string relative = GameDataPaths.PlayfieldSpawnsRelativePath(row.PlayfieldId!.Value);
                    return Directory.Exists(Path.Combine(RootPath, Path.GetDirectoryName(relative)!));
                case "authoredPoint":
                    int field = row.PlayfieldId!.Value;
                    if (!spawnFiles.TryGetValue(field, out PlayfieldSpawnEntry[]? points))
                        spawnFiles[field] = points = ReadAuthoredEditorSpawns(field).Spawns;
                    int index = row.AuthoredIndex!.Value;
                    return index < points.Length && SpawnContentValidation.IsValid(points[index])
                        && points[index].StaticTemplateId == null && points[index].HashText is string hash
                        && !((IGameData)this).IsStaticSpawnHash(hash) && catalog.HasTemplate(hash)
                        && NpcEditorSpawnStore.Signature(points[index]) == row.AuthoredSignature;
                default:
                    return false;
            }
        }

        bool MayOverlap(NamedLootAssignment a, NamedLootAssignment b)
        {
            if (a.FamilyHash != null && b.FamilyHash != null)
            {
                var first = catalog.EditorStatsFamily(a.FamilyHash)!.Leaves;
                var second = catalog.EditorStatsFamily(b.FamilyHash)!.Leaves;
                return first.Intersect(second, StringComparer.Ordinal).Any();
            }
            if (a.FamilyHash != null && b.TemplateHash != null)
                return catalog.EditorFamiliesFor(b.TemplateHash).Contains(a.FamilyHash, StringComparer.Ordinal);
            if (b.FamilyHash != null && a.TemplateHash != null)
                return catalog.EditorFamiliesFor(a.TemplateHash).Contains(b.FamilyHash, StringComparer.Ordinal);
            return true;
        }
    }
}

public sealed partial class NpcTemplateCatalog
{
    internal bool EditorNamedLootBoss(string hash)
        => TryGetEditableLootLeaf(hash, out NpcLeaf leaf)
            && leaf.Bands.Any(band => EditorLootFeatures(leaf, band).Contains(NpcFeature.Boss));
}
