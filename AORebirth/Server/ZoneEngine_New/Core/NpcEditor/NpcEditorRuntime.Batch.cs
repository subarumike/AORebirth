namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;

internal sealed partial class NpcEditorRuntime
{
    internal bool CanBatchSpawnInPlayfield(int instanceId, int resourceId)
        => playfields.SnapshotPlayfields().All(field => field.ResourceId != resourceId || field.Identity.Instance == instanceId);

    internal async Task<NpcEditorSpawnView> EstimateSpawnImpactAsync(NpcEditorSpawnView view, CancellationToken ct)
    {
        if (view.AuthoredIndex is not int index)
            return view;
        int count = view.LoadedNpcs;
        foreach (Playfield field in playfields.SnapshotPlayfields())
        {
            if (field.ResourceId != view.ResourcePlayfieldId || field.Identity.Instance == view.PlayfieldId)
                continue;
            try
            {
                count += await OnTickAsync(field, () => field.GetService<HashSpawnSystem>()?.CountAuthoredSpawnNpcs(index) ?? 0, ct);
            }
            catch (NpcEditorRuntimeException error) when (error.StatusCode == 404)
            {
                // A playfield copy retired after the snapshot.
            }
        }
        return view with { LoadedNpcs = count };
    }

    internal Task<NpcEditorBatchTarget> PrepareLiveBatchAsync(NpcEditorIdentity identity, NpcEditorBatchRequest edit,
        NpcEditorLoot loot, CancellationToken ct)
        => WithNpcAsync(identity, npc => edit.Operation switch
        {
            "stat" when edit.StatId is int stat && edit.Value is int value => PrepareStat(npc, identity, (CharacterStat)stat, value),
            "aggression" when edit.Value is int value => PrepareStat(npc, identity, CharacterStat.BreedHostility, value),
            "armor" when edit.ItemId is > 0 => PrepareArmor(npc, identity, new(-1, 0, edit.ItemId, edit.Quality)),
            "loot" when edit.LootEntry is { } entry => PrepareLoot(npc, identity, loot, entry),
            _ => throw new NpcEditorRuntimeException(400, "Supply the values required for the selected batch operation.")
        }, ct);

    NpcEditorBatchTarget PrepareStat(NpcCharacter npc, NpcEditorIdentity identity, CharacterStat stat, int value)
    {
        if (ReadStat(npc, stat, StatDetail.Base) is not int current)
            throw new NpcEditorRuntimeException(400, "The selected NPC has no base value for this stat.");
        if (current == value)
            throw new NpcEditorRuntimeException(400, "The selected NPC already has that base value.");
        if (PlanLiveStat(npc, stat, value, restoring: false, null, out int? after) is string error)
            throw new NpcEditorRuntimeException(400, error);

        CharacterStat? dependent = NpcEditorStatPolicy.DependentVital(stat);
        int? before = dependent is { } vital ? ReadStat(npc, vital, StatDetail.Base) : null;
        var row = new NpcEditorBatchRow(identity, npc.Name ?? "", $"{stat} base",
            DescribeStat(current, dependent, before), DescribeStat(value, dependent, after));
        return new(row, async (actor, ct) =>
        {
            Guid change = await WithNpcAsync(identity, selected =>
            {
                if (dependent is { } vital
                    && (ReadStat(selected, vital, StatDetail.Base) != before
                        || PlanLiveStat(selected, stat, value, restoring: false, null, out int? projected) != null
                        || projected != after))
                    throw new NpcEditorRuntimeException(409, "The vital values changed. Preview the batch again.");
                return ApplyStatWithHistory(selected, stat, current, value, actor);
            }, ct);
            return (undoActor, undoCt) => RevertAsync(identity, change, undoCt, undoActor);
        });
    }

    NpcEditorBatchTarget PrepareArmor(NpcCharacter npc, NpcEditorIdentity identity, NpcEditorEquipmentEdit edit)
    {
        EquipmentChange armor = EditEquipment(npc, _catalog, npc.Playfield!.GetRequiredService<IItemBuilder>(), edit, preview: true);
        var row = new NpcEditorBatchRow(identity, npc.Name ?? "", $"Equipment slot {armor.Slot}",
            DescribeEquipment(armor.Before), DescribeEquipment(armor.After));
        return new(row, async (actor, ct) =>
        {
            Guid change = await WithNpcAsync(identity, selected => ApplyEquipmentWithHistory(selected, edit, actor, armor.Slot), ct);
            return (undoActor, undoCt) => RevertAsync(identity, change, undoCt, undoActor);
        });
    }

    NpcEditorBatchTarget PrepareLoot(NpcCharacter npc, NpcEditorIdentity identity, NpcEditorLoot loot, NpcEditorLootEntry entry)
    {
        NpcEditorLootLiveEdit edit = loot.PrepareLiveAppend(npc, entry);
        var row = new NpcEditorBatchRow(identity, npc.Name ?? "", "Live loot table",
            "No added row", $"{entry.ItemHash}, chance {entry.Chance}, repeats {entry.Repeats}, level modifier {entry.LevelMod}, QL {entry.MinQuality}–{entry.MaxQuality}");
        return new(row, async (actor, ct) =>
        {
            NpcEditorLootView result = await WithNpcAsync(identity, selected => loot.ApplyLive(selected, edit, actor), ct);
            NpcEditorLootLiveRevert undo = new(result.Live.Revision, result.Live.History.Last().ChangeId);
            return (undoActor, undoCt) => WithNpcAsync(identity, selected => loot.RevertLive(selected, undo, undoActor), undoCt);
        });
    }
}
