namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;

internal sealed partial class NpcEditorRuntime(PlayfieldManager playfields, GameDataStore? gameData = null)
{
    const int MaxBrowserRows = 10_000;
    const int MaxCatalogRows = 100;

    readonly ConditionalWeakTable<NpcCharacter, SpawnState> _state = new();
    readonly IItemTemplateCatalog _catalog = playfields.ItemTemplates;

    public async Task<NpcEditorNpc[]> BrowseAsync(string? query, CancellationToken cancellationToken)
    {
        string filter = query?.Trim() ?? string.Empty;
        var rows = new List<NpcEditorNpc>();
        foreach (Playfield field in playfields.SnapshotPlayfields())
        {
            try
            {
                NpcEditorNpc[] matches = await OnTickAsync(field, () =>
                {
                    int remaining = MaxBrowserRows - rows.Count;
                    return field.GetRequiredService<DynelRegistry>().Dynels()
                        .OfType<NpcCharacter>().Where(npc => IsLive(field, npc))
                        .Select(npc => Summarize(field, npc))
                        .Where(row => Matches(row, filter)).Take(remaining + 1).ToArray();
                }, cancellationToken).ConfigureAwait(false);
                rows.AddRange(matches);
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 404)
            {
                // An empty playfield may retire after the manager took its snapshot.
            }

            if (rows.Count > MaxBrowserRows)
                throw new NpcEditorRuntimeException(400, "Too many NPCs match. Narrow the browser search.");
        }

        return rows.OrderBy(row => row.Identity.PlayfieldId)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Identity.InstanceId).ToArray();
    }

    public async Task<NpcEditorIdentity> ResolveAsync(
        int clientPlayfieldId, int identityType, int instanceId, CancellationToken cancellationToken)
    {
        ValidateCoordinates(clientPlayfieldId, identityType, instanceId);
        NpcEditorIdentity? match = null;
        foreach (Playfield field in playfields.SnapshotPlayfields())
        {
            if (field.ClientPlayfieldId != clientPlayfieldId)
                continue;

            NpcEditorIdentity? candidate;
            try
            {
                candidate = await OnTickAsync(field, () =>
                {
                    NpcCharacter? npc = Find(field, identityType, instanceId);
                    return npc == null ? null : Identify(field, npc);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 404)
            {
                continue;
            }

            if (candidate == null)
                continue;
            if (match != null)
                throw new NpcEditorRuntimeException(409, "This client identity matches multiple playfield instances. Select an NPC manually.");
            match = candidate;
        }

        return match ?? throw new NpcEditorRuntimeException(404, "No currently spawned NPC matches the client target.");
    }

    public Task<NpcEditorNpcDetails> InspectAsync(NpcEditorIdentity identity, CancellationToken cancellationToken)
        => WithNpcAsync(identity, Snapshot, cancellationToken);

    public Task<NpcEditorStat[]> ReadStatsAsync(NpcEditorIdentity identity, CancellationToken cancellationToken)
        => WithNpcAsync(identity, StatRows, cancellationToken);

    public async Task<NpcEditorEquipmentTarget> CaptureEquipmentAsync(
        NpcEditorIdentity identity, CancellationToken cancellationToken, bool estimate = true)
    {
        NpcEditorEquipmentTarget target = await WithNpcAsync(identity, CaptureEquipmentTarget, cancellationToken)
            .ConfigureAwait(false);
        if (estimate && target.UnavailableReason == null)
        {
            int? affected = await EstimateTemplateBandAsync(target.TemplateHash, target.BandIndex, cancellationToken)
                .ConfigureAwait(false);
            target = target with { EstimatedAffectedNpcs = affected };
        }
        return target;
    }

    internal static NpcEditorEquipmentTarget CaptureEquipmentTarget(NpcCharacter npc)
    {
        if (npc.MobTemplate is not { UnresolvedPlaceholder: false, EditorLootLeaf: { } leaf } template
            || template.AuthoredBandIndex < 0 || template.AuthoredBandIndex >= leaf.Bands.Length
            || !npc.Attackable || npc.Pet != null || npc.Shop != null)
            return new(npc.MobTemplate?.Hash ?? string.Empty, -1, string.Empty,
                "Select an attackable, authored template NPC without a pet owner or shop.");
        return new(leaf.Hash, template.AuthoredBandIndex,
            NpcEditorEquipmentJournalFile.Signature(leaf.Bands[template.AuthoredBandIndex]), null);
    }

    public async Task<int?> EstimateTemplateBandAsync(string hash, int bandIndex, CancellationToken cancellationToken)
    {
        int matches = 0;
        foreach (Playfield field in playfields.SnapshotPlayfields())
        {
            try
            {
                matches += await OnTickAsync(field, () => field.GetRequiredService<DynelRegistry>().Dynels()
                    .OfType<NpcCharacter>()
                    .Count(npc => IsLive(field, npc) && npc.MobTemplate is { } template
                        && template.Hash == hash && template.AuthoredBandIndex == bandIndex), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 404)
            {
                // A playfield can retire before this estimate reaches its tick.
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 503)
            {
                return null;
            }
        }
        return matches;
    }

    public Task<T> WithNpcAsync<T>(
        NpcEditorIdentity identity, Func<NpcCharacter, T> operation, CancellationToken cancellationToken)
    {
        ValidateCoordinates(identity.PlayfieldId, identity.IdentityType, identity.InstanceId);
        if (identity.Generation == Guid.Empty)
            throw new NpcEditorRuntimeException(400, "An NPC spawn generation is required.");
        if (!playfields.TryGet(identity.PlayfieldId, out Playfield? field) || field == null)
            throw new NpcEditorRuntimeException(404, "The selected playfield is no longer loaded.");

        return OnTickAsync(field, () =>
        {
            NpcCharacter? npc = Find(field, identity.IdentityType, identity.InstanceId);
            if (npc == null)
                throw new NpcEditorRuntimeException(404, "The selected NPC is no longer spawned.");
            RequireGeneration(identity, State(npc).Generation);
            return operation(npc);
        }, cancellationToken);
    }

    internal Task<T> WithPlayfieldAsync<T>(int instanceId, Func<Playfield, T> operation, CancellationToken cancellationToken)
    {
        if (!playfields.TryGet(instanceId, out Playfield? field) || field == null)
            throw new NpcEditorRuntimeException(404, "The selected playfield is no longer loaded.");
        return OnTickAsync(field, () => operation(field), cancellationToken);
    }

    internal static void RequireGeneration(NpcEditorIdentity identity, Guid currentGeneration)
    {
        if (currentGeneration != identity.Generation)
            throw new NpcEditorRuntimeException(409, "That NPC identity now belongs to another spawn. Select it again.");
    }

    public NpcEditorCatalogItem[] SearchItems(string? query)
    {
        string filter = query?.Trim() ?? string.Empty;
        if (filter.Length == 0)
            return [];
        if (int.TryParse(filter, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            return _catalog.TryGet(id, out ItemTemplate item) ? [CatalogRow(item)] : [];

        return _catalog.Templates.Where(item => Contains(item.Name, filter))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id)
            .Take(MaxCatalogRows).Select(CatalogRow).ToArray();
    }

    static NpcEditorCatalogItem CatalogRow(ItemTemplate item)
        => new(item.Id, item.Name, item.Quality, ClassName(item.Stats.GetValueOrDefault(CharacterStat.ItemClass)));

    static bool Contains(string? value, string filter)
        => value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;

    static bool Matches(NpcEditorNpc row, string filter)
    {
        if (filter.Length == 0 || Contains(row.Name, filter) || Contains(row.TemplateHash, filter)
            || Contains(row.SpawnHash, filter) || row.FamilyHashes?.Any(hash => Contains(hash, filter)) == true)
            return true;
        return int.TryParse(filter, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
            && (row.TemplateId == id || row.MonsterData == id || row.Identity.InstanceId == id
                || row.Identity.PlayfieldId == id || row.ClientPlayfieldId == id);
    }

    static void ValidateCoordinates(int playfieldId, int identityType, int instanceId)
    {
        if (playfieldId <= 0 || identityType <= 0 || instanceId <= 0)
            throw new NpcEditorRuntimeException(400, "A valid playfield and typed NPC instance are required.");
    }

    static bool IsLive(Playfield field, NpcCharacter npc) => ReferenceEquals(npc.Playfield, field) && !npc.IsDead;

    static NpcCharacter? Find(Playfield field, int identityType, int instanceId)
        => Find(field.GetRequiredService<DynelRegistry>(), field, identityType, instanceId);

    internal static NpcCharacter? Find(DynelRegistry registry, Playfield field, int identityType, int instanceId)
    {
        var identity = new Identity { Type = (IdentityType)identityType, Instance = instanceId };
        return registry.TryGet(identity, out Dynel? dynel) && dynel is NpcCharacter npc && IsLive(field, npc)
            ? npc
            : null;
    }

    SpawnState State(NpcCharacter npc) => _state.GetValue(npc, static _ => new SpawnState());

    NpcEditorIdentity Identify(Playfield field, NpcCharacter npc)
        => new(field.Identity.Instance, (int)npc.Identity.Type, npc.Identity.Instance, State(npc).Generation);

    NpcEditorNpc Summarize(Playfield field, NpcCharacter npc)
        => new(Identify(field, npc), field.ClientPlayfieldId, npc.Name ?? string.Empty,
            npc.Stats.GetOrZero(CharacterStat.Level), npc.MobTemplate?.TemplateId, npc.MobTemplate?.Hash,
            npc.SpawnHash, ReadStat(npc, CharacterStat.MonsterData, StatDetail.Full), npc.SpawnSource.ToString(),
            npc.MobTemplate is { UnresolvedPlaceholder: false } template ? gameData?.EditorFamiliesFor(template.Hash) : null);

    NpcEditorNpcDetails Snapshot(NpcCharacter npc)
    {
        Playfield field = npc.Playfield!;
        NpcEditorRespawn? respawn = null;
        if (field.GetService<HashSpawnSystem>() is { } spawns && spawns.TryGetSpawnPoint(npc, out HashSpawnPoint point))
        {
            respawn = new NpcEditorRespawn(point.RespawnTimeSeconds, point.RespawnChance, point.RespawnsAfterDeath,
                point.Retired ? "Retired" : point.State.ToString(),
                point.State == HashSpawnState.Dead && point.NextSpawnTime != default ? point.NextSpawnTime : null);
        }

        MobTemplate? template = npc.MobTemplate;
        NpcEditorEquippedItem[] equipment = npc.Equipment.EnumerateSlots()
            .Select(slot => EquipmentRow(npc, slot.Key, slot.Value)).ToArray();
        NpcEditorConfiguredItem[] configuredEquipment = (npc.SpawnEquipment ?? template?.Equipment)?.Where(pair => pair?.Count > 0)
            .Select(pair => new NpcEditorConfiguredItem(pair[0], pair.Count > 1 ? pair[1] : pair[0],
                ItemName(pair[0]))).ToArray() ?? [];

        var brain = new NpcEditorBrain(npc.Attackable, npc.Brain != null, npc.IsAiBusy, npc.Brain?.PatrolEnabled == true,
            npc.Brain?.PatrolRun == true, npc.Brain?.PatrolWaypoints.Select(position =>
                new NpcEditorPoint(position.x, position.y, position.z)).ToArray() ?? [],
            template?.Features.Select(feature => feature.ToString()).Order().ToArray() ?? [], npc.Scripts?.Program.Id);

        var position = npc.Position;
        return new NpcEditorNpcDetails(Summarize(field, npc), position.x, position.y, position.z, npc.SpawnPointKey,
            respawn, StatRows(npc), equipment, configuredEquipment, npc.UploadedNanoIds.Select(NanoRow).ToArray(),
            npc.Buffs.Select(buff => NanoRow(buff.Id)).ToArray(), brain, CanEditEquipment(npc) && npc.Equipment.FindFreeSlot() >= 0,
            State(npc).History.AsEnumerable().Reverse().Select(change => new NpcEditorChange(
                change.Id, change.AtUtc, change.Description, change.Reverted, CanRevert(npc, change), "liveInstance",
                change.Actor, change.Before, change.After, change.RevertedBy)).ToArray());
    }

    static NpcEditorStat[] StatRows(NpcCharacter npc)
    {
        MobTemplate? template = npc.MobTemplate;
        IReadOnlyDictionary<int, int>? configured = template?.Stats ?? WorldNpcFactory.DefinitionFor(npc)?.Stats;
        string authoredSource = template == null ? "Authored world NPC placement" : "Resolved NPC template";
        return npc.Stats.GetEntries().Select(entry => (int)entry.Stat)
            .Union(configured?.Keys ?? Enumerable.Empty<int>()).Union(npc.EditorScopedStats?.Keys ?? []).Order().Select(id =>
            {
                var stat = (CharacterStat)id;
                int? liveBase = ReadStat(npc, stat, StatDetail.Base);
                int? configuredValue = configured != null && configured.TryGetValue(id, out int value) ? value : null;
                string? source = configuredValue.HasValue ? template?.EditorStatSources?.GetValueOrDefault(id) ?? authoredSource : null;
                if (npc.EditorScopedStats?.TryGetValue(id, out var scoped) == true)
                    (configuredValue, source) = scoped;
                return new NpcEditorStat(id, stat.ToString(), liveBase, ReadStat(npc, stat, StatDetail.Bonus),
                    ReadStat(npc, stat, StatDetail.Full), configuredValue, liveBase.HasValue && CanEditStat(npc, stat), source);
            }).ToArray();
    }

    static int? ReadStat(NpcCharacter npc, CharacterStat stat, StatDetail detail)
        => npc.Stats.TryGetValue(stat, out int value, detail) && !StatCollection.IsUnset(value) ? value : null;

    NpcEditorNano NanoRow(int id) => new(id, ItemName(id));
    string ItemName(int id) => _catalog.TryGet(id, out ItemTemplate item) ? item.Name : string.Empty;
    static string ClassName(int itemClass) => ((ItemClass)itemClass).ToString();

    // Cancellation may withdraw a queued action. Once a mutation claims the queue slot, its
    // result must complete even if the requester disconnects, so a timeout cannot conceal a late write.
    static async Task<T> OnTickAsync<T>(Playfield field, Func<T> action, CancellationToken cancellationToken)
    {
        using var queueDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        queueDeadline.CancelAfter(TimeSpan.FromSeconds(3));
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        int claimed = 0;
        using var registration = queueDeadline.Token.Register(() =>
        {
            if (Interlocked.Exchange(ref claimed, 1) != 0)
                return;
            if (cancellationToken.IsCancellationRequested)
                completion.TrySetCanceled(cancellationToken);
            else
                completion.TrySetException(new NpcEditorRuntimeException(503, "The playfield did not accept work in time."));
        });

        if (!field.Dispatch(() =>
        {
            if (Interlocked.Exchange(ref claimed, 1) != 0)
                return;
            try
            {
                if (field.IsDisposed)
                    throw new NpcEditorRuntimeException(404, "The selected playfield is no longer loaded.");
                completion.SetResult(action());
            }
            catch (Exception error)
            {
                completion.SetException(error);
            }
        }))
        {
            if (Interlocked.Exchange(ref claimed, 1) == 0)
                completion.TrySetException(new NpcEditorRuntimeException(404, "The selected playfield is no longer loaded."));
        }

        return await completion.Task.ConfigureAwait(false);
    }
}
