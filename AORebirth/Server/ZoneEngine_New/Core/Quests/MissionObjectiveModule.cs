namespace ZoneEngine_New.Core.Quests;

using System;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Modules;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZonePlayfield = ZoneEngine_New.Core.Playfield.Playfield;

public interface IMissionObjectiveModule
{
    void InspectNpc(Player player, NpcCharacter npc);
    bool TryPickup(Player player, MissionObjectiveItem objective);
    bool TryUseItem(Player player, Identity sourceSlot, Identity target, out bool handled);
}

/// <summary>Dispatches events for the exact objectives frozen at generated mission acceptance.</summary>
internal sealed class MissionObjectiveModule(QuestService quests, IItemBuilder items, Func<DateTime> now) : IMissionObjectiveModule
{
    static MissionRollType ObjectiveType(QuestTemplate template) => template.Action switch
    {
        QuestTemplate.KillOneAction or QuestTemplate.KillMultipleAction => MissionRollType.KillPerson,
        QuestTemplate.TargetNpcAction => MissionRollType.FindPerson,
        QuestTemplate.FindItemAction => MissionRollType.FindItem,
        QuestTemplate.ReturnItemAction => MissionRollType.FindItemReturn,
        QuestTemplate.RepairAction => MissionRollType.RepairMachine,
        _ => MissionRollType.Unknown
    };

    internal static bool RequiresBoundObjective(PlayerQuest quest) => quest.Source == QuestSource.Generated
        && (ObjectiveType(quest.Template) is MissionRollType.FindItem or MissionRollType.FindItemReturn or MissionRollType.RepairMachine
            || QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                && parameters.ObjectiveNpc != null);

    public void InspectNpc(Player player, NpcCharacter npc)
    {
        if (player.Playfield?.GetRequiredService<PlayfieldLocality>().IsVisibleTo(npc, player) != true)
            return;
        foreach (var quest in quests.GetLog(player).Quests.Values)
        {
            if (quest.IsActive && ObjectiveType(quest.Template) == MissionRollType.FindPerson && RequiresBoundObjective(quest))
                TryAdvanceNpc(player, quest, npc, MissionRollType.FindPerson);
        }
    }

    internal void AdvanceKill(Player player, PlayerQuest quest, NpcCharacter npc)
        => TryAdvanceNpc(player, quest, npc, MissionRollType.KillPerson);

    void TryAdvanceNpc(Player player, PlayerQuest quest, NpcCharacter npc, MissionRollType type)
    {
        lock (player.PersistenceGate)
        {
            if (!quests.CanProgressObjective(player) || !quest.IsActive || quest.IsRewardPending
                || !QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                || parameters.ObjectiveNpc is not { IsValid: true } binding
                || binding.MissionType != (int)type || binding.OwnerId != player.Identity.Instance
                || binding.QuestId != quest.QuestId || npc.Identity != binding.Identity
                || npc.Playfield is not QuestDungeonPlayfield world
                || !ReferenceEquals(player.Playfield, world) || world.Identity.Instance != parameters.DungeonPlayfield
                || world.QuestId != quest.QuestId
                || !world.GetRequiredService<DynelRegistry>().TryGet(npc.Identity, out var registered)
                || !ReferenceEquals(registered, npc))
                return;

            if (type == MissionRollType.FindPerson
                && (player.GetEdgeDistanceTo(npc) > LootableDynel.OpenRange || !player.HasLineOfSightTo(npc)))
                return;
            if (!quests.TryEarnBoundObjective(player, quest, parameters, null)
                && type == MissionRollType.KillPerson && !player.IsPersistenceQuarantined)
            {
                quest.NpcObjectiveCommitPending = true;
                quest.NextRewardRetryUtcTicks = now().AddSeconds(5).Ticks;
            }
        }
    }

    internal void RetryPendingKill(Player player, PlayerQuest quest)
    {
        lock (player.PersistenceGate)
        {
            quest.NextRewardRetryUtcTicks = now().AddSeconds(5).Ticks;
            if (QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                && parameters.ObjectiveNpc is { IsValid: true } binding && binding.MissionType == (int)MissionRollType.KillPerson)
                quests.TryEarnBoundObjective(player, quest, parameters, null);
        }
    }

    public bool TryUseItem(Player player, Identity sourceSlot, Identity target, out bool handled)
    {
        handled = false;
        lock (player.PersistenceGate)
        {
            // Mission targets must never fall through to ordinary item spells, even for another key holder.
            if (player.Playfield is { } currentWorld
                && currentWorld.GetRequiredService<DynelRegistry>().TryGet(target, out var registeredTarget)
                && registeredTarget is MissionObjectiveItem)
                handled = true;
            if (!quests.CanProgressObjective(player))
                return false;
            player.Inventory.TryGetUseItem(sourceSlot, out Item source);
            foreach (var quest in quests.GetLog(player).Quests.Values)
            {
                if (quest.Source != QuestSource.Generated
                    || !QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters))
                    continue;
                MissionRollType type = ObjectiveType(quest.Template);
                MissionItemObjectiveBinding? binding = type switch
                {
                    MissionRollType.RepairMachine => parameters.ObjectiveTool,
                    MissionRollType.FindItemReturn => parameters.ObjectiveItem,
                    _ => null
                };
                bool ownsTarget = type == MissionRollType.RepairMachine && parameters.ObjectiveItem?.Identity == target;
                if (binding?.IsValid != true || (source?.Identity != binding.Identity && !ownsTarget))
                    continue;
                handled = true;
                if (!quest.IsActive || quest.IsRewardPending || source == null || source.Locked || !source.IsPersisted
                    || binding.OwnerId != player.Identity.Instance || binding.QuestId != quest.QuestId
                    || source.Identity != binding.Identity || source.InstanceId != binding.Instance
                    || source.LowId != binding.LowId || source.HighId != binding.HighId || source.Quality != binding.Quality
                    || source.StackCount != 1)
                    return false;

                var world = player.Playfield!;
                if (!world.GetRequiredService<DynelRegistry>().TryGet(target, out var destination))
                    return false;
                bool validDestination = type == MissionRollType.RepairMachine
                    ? IsAcceptedMachine(player, quest, parameters, world, destination)
                    : IsIssuingTerminal(player, quest, parameters, world, destination, target);
                if (!validDestination)
                    return false;
                if (sourceSlot.Type != IdentityType.Inventory)
                {
                    ClientFeedback.Send(player, "Move the mission item into your main inventory before using it.");
                    return false;
                }
                return UseAcceptedItem(player, quest, parameters, sourceSlot, source);
            }
            return false;
        }
    }

    static bool IsAcceptedMachine(Player player, PlayerQuest quest, QuestDungeonParameters parameters, ZonePlayfield world,
        Dynel? destination)
        => world is QuestDungeonPlayfield dungeon && dungeon.QuestId == quest.QuestId
            && dungeon.Identity.Instance == parameters.DungeonPlayfield
            && destination is MissionObjectiveItem machine && parameters.ObjectiveItem?.SameAs(machine.Binding) == true
            && !(player.GetEdgeDistanceTo(machine) > LootableDynel.OpenRange);

    static bool IsIssuingTerminal(Player player, PlayerQuest quest, QuestDungeonParameters parameters, ZonePlayfield world,
        Dynel? destination, Identity target)
        => quest.Progress == 1 && parameters.OriginPlayfield == world.Identity.Instance
            && parameters.OriginTerminal == target && destination is MissionTerminal terminal
            && !(player.GetEdgeDistanceTo(terminal) > LootableDynel.OpenRange);

    bool UseAcceptedItem(Player player, PlayerQuest quest, QuestDungeonParameters parameters, Identity sourceSlot, Item source)
    {
        Container page = player.Inventory.Inventory;
        int placement = sourceSlot.Instance;
        if (!quests.TryEarnBoundObjective(player, quest, parameters,
            QuestService.ToMissionItem(source.ToRecord(page.Identity, placement, source.StackCount))))
            return false;
        try
        {
            if (!ReferenceEquals(page.Remove(placement), source))
                throw new InvalidOperationException("Committed mission source changed before publication.");
            ModuleHooks.ItemMoved(player, source, ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Inventory),
                ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.Destroyed));
            player.Session?.Send(new TemplateActionMessage
            {
                Identity = player.Identity, ItemLowId = source.LowId, ItemHighId = source.HighId,
                Quality = source.Quality, Placement = sourceSlot, Count = 1,
                Action = TemplateActionType.Use, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance
            });
            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = sourceSlot
            });
            return true;
        }
        catch (Exception exception)
        {
            quests.CompletionFailure(player, exception, committed: true);
            return false;
        }
    }

    public bool TryPickup(Player player, MissionObjectiveItem objective)
    {
        var playfield = player.Playfield;
        var logger = playfield?.GetService<IZoneLogger>();
        double distance = objective.GetEdgeDistanceTo(player);
        bool collected = playfield is QuestDungeonPlayfield && objective.TryPickup(player);
        logger?.Info($"Mission pickup actor={player.Identity} target={objective.Identity} quest={objective.Binding.QuestId} edgeDistance={distance} collected={collected}");
        return collected;
    }

    // The dynel delegates its collection under the pickup gate and then publishes the original pickup packets.
    internal bool TryCollectItem(Player player, MissionObjectiveItem objective)
    {
        lock (player.PersistenceGate)
        {
            if (!quests.CanProgressObjective(player) || objective.Playfield is not QuestDungeonPlayfield world
                || !ReferenceEquals(player.Playfield, world) || player.Identity.Instance != objective.Binding.OwnerId
                || !quests.GetLog(player).Quests.TryGetValue(objective.Binding.QuestId, out PlayerQuest? quest)
                || !quest.IsActive || quest.IsRewardPending
                || ObjectiveType(quest.Template) is not (MissionRollType.FindItem or MissionRollType.FindItemReturn)
                || quest.Progress != 0
                || !QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out var parameters)
                || parameters.DungeonPlayfield != world.Identity.Instance
                || parameters.ObjectiveItem?.SameAs(objective.Binding) != true
                || player.GetEdgeDistanceTo(objective) > LootableDynel.OpenRange
                || !world.GetRequiredService<DynelRegistry>().TryGet(objective.Identity, out var current)
                || !ReferenceEquals(current, objective))
                return false;

            bool committed = false;
            try
            {
                Item item = items.Create(objective.LowId, objective.HighId, objective.Quality,
                    ItemSource.Quest, 1, objective.Identity.Instance, objective.Identity);
                if (!InventoryGrantPlan.TryCreate(player, [item], out var grant))
                {
                    ClientFeedback.Send(player, "Make inventory space before picking up the mission item.");
                    return false;
                }
                bool isReturn = ObjectiveType(quest.Template) == MissionRollType.FindItemReturn;
                if (!quests.TryCommitObjectivePickup(player, quest, world, objective.Binding, grant))
                    return false;
                committed = true;
                grant.PublishAfterCommit(notify: false,
                    from: ModuleHooks.Place(AORebirth.Modules.Events.ItemPlaceKind.World, objective.Identity));
                quest.Progress = isReturn ? 1 : quest.RequiredCount;
                quest.NextRewardRetryUtcTicks = 0;
                return true;
            }
            catch (Exception exception)
            {
                quests.CompletionFailure(player, exception, committed);
                return false;
            }
        }
    }
}
