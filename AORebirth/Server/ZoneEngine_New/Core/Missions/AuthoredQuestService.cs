namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;
using DomainState = ZoneEngine.Core.Missions.MissionLifecycleState;
using DomainStatus = ZoneEngine.Core.Missions.MissionOperationStatus;
using DaoState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

/// <summary>Generic editable authored actions; inventory, rewards and mission transitions commit together.</summary>
public sealed partial class AuthoredQuestService
{
    readonly IMissionDao _dao;
    readonly InventoryFlushService _flush;
    readonly IItemBuilder _items;
    readonly IItemTemplateCatalog _templates;
    readonly IItemInstanceIdAllocator _ids;
    readonly AuthoredQuestCatalog _catalog;
    readonly IZoneLogger _logger;
    readonly Func<DateTime> _now;
    public InteractionContent Content => _catalog.Content;
    public AuthoredQuestService(IMissionDao dao, InventoryFlushService flush, IItemBuilder items,
        IItemTemplateCatalog templates, IItemInstanceIdAllocator ids, AuthoredQuestCatalog catalog, IZoneLogger logger)
        : this(dao, flush, items, templates, ids, catalog, logger, () => DateTime.UtcNow) { }
    public AuthoredQuestService(IMissionDao dao, InventoryFlushService flush, IItemBuilder items,
        IItemTemplateCatalog templates, IItemInstanceIdAllocator ids, AuthoredQuestCatalog catalog, IZoneLogger logger, Func<DateTime> now)
    { _dao = dao; _flush = flush; _items = items; _templates = templates; _ids = ids; _catalog = catalog; _logger = logger; _now = now; }

    public bool IsAuthoredItem(Item item) => Content.Actions.Values.Any(x => x.DirectItemUse
        && (x.ItemIds.Contains(item.LowId) || x.ItemIds.Contains(item.HighId)))
        || Content.TimedTurnIns.Any(x => x.Items.Any(y => y.ItemId == item.LowId || y.ItemId == item.HighId));

    public bool TryUseItem(Player player, Identity slot, Item item)
    {
        var timed = Content.TimedTurnIns.FirstOrDefault(x => x.Items.Any(y => y.ItemId == item.LowId || y.ItemId == item.HighId));
        if (timed != null) return TryUseTimedItem(player, slot, item, timed);
        // NPC and prop interactions require their bound owner route; carrying an item is not authority to turn it in.
        var action = Content.Actions.FirstOrDefault(x => x.Value.DirectItemUse && x.Value.ItemIds.Any(id => id == item.LowId || id == item.HighId));
        return action.Key != null && TryExecuteAction(player, action.Key, slot, item);
    }

    public bool CanExecuteAction(Player player, string key)
    {
        if (Content.TimedTurnIns.FirstOrDefault(x => x.Key == key) is { } timed) return CanOpenTimedTrade(player, timed);
        if (!Content.Actions.TryGetValue(key, out var action)) return false;
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player)) return false;
            try { return _dao.Execute(player.Identity.Instance, null!, tx => Conditions(player, action, tx)); }
            catch (Exception e) { _logger.Error(e, "Interaction state could not be read."); return false; }
        }
    }

    public bool TryExecuteAction(Player player, string key, Identity slot = default, Item? item = null, Action? acknowledge = null)
    {
        if (Content.TimedTurnIns.FirstOrDefault(x => x.Key == key) is { } timed)
            return item != null && TryTurnInTimedItem(player, slot, item, acknowledge ?? (() => { }), timed);
        if (!Content.Actions.TryGetValue(key, out var action)) return false;
        return Mutate(player, null, (tx, service) =>
        {
            if (!Conditions(player, action, tx)) throw new InvalidOperationException("Interaction conditions are not satisfied.");
            if (action.ItemIds.Length != 0)
            {
                if (item == null || !action.ItemIds.Any(id => id == item.LowId || id == item.HighId)) throw new InvalidOperationException("Wrong source item.");
                RequireSource(player, slot, item);
            }
            var actions = new[] { action }.Concat(action.OptionalActions.Select(x => Content.Actions[x]).Where(x => Conditions(player, x, tx))).ToArray();
            var grantPairs = actions.SelectMany(x => x.Grants).Select(x => (Definition: x, Item: CreateItem(x.ItemId, x.Quality))).ToArray();
            var granted = grantPairs.Where(x => (!x.Definition.SkipIfCarried || !HasCarried(player, x.Item.LowId))
                && (!x.Definition.HonorUnique || !ZoneEngine_New.Core.Trade.TradeRules.IsUnique(x.Item)
                    || !ZoneEngine_New.Core.Trade.TradeRules.WouldDuplicateUnique(player, x.Item.LowId, x.Item.HighId))).ToArray();
            var plan = Plan(player, granted.Select(x => x.Item).ToArray());
            var stats = new List<MissionStatValueData>();
            foreach (var entry in actions)
            {
                foreach (var completion in entry.Complete) CompleteIfPresent(service, player.Identity.Instance, completion.Quest,
                    completion.Objective, completion.Observation, completion.Event);
                foreach (var quest in entry.Accept) Accept(service, player.Identity.Instance, quest);
                if (entry.StatReward is { } reward) stats.AddRange(ApplyStatReward(tx, player, reward.Quest, reward.Key, reward.Xp, reward.Cash,
                    string.IsNullOrWhiteSpace(reward.EffectReference) ? "content-action:" + reward.Quest + ":" + reward.Key : reward.EffectReference, _now().Ticks));
            }
            if (action.Consume) ApplyRows(tx, plan, player, slot, item!);
            else
            {
                if (tx is not IMissionInventoryMutationTransaction inventory) throw new InvalidOperationException("Mission DAO lacks atomic item effects.");
                inventory.ApplyInventoryMutation(plan.Rows.Select(ToMissionItem).ToArray(), []);
            }
            return () =>
            {
                plan.PublishAfterCommit(false);
                var notifications = grantPairs.Where(x => granted.Any(y => ReferenceEquals(y.Item, x.Item)) || x.Definition.NotifyIfCarried).ToArray();
                if (action.AcknowledgementBeforeConsumption) acknowledge?.Invoke();
                foreach (var grant in notifications.Where(x => x.Definition.PublishBeforeConsumption)) SendOverflowGrant(player, grant.Item);
                if (action.Consume)
                {
                    if (action.UseProjection) PublishConsumption(player, slot, item!);
                    else { player.Inventory.Inventory.Content.Remove(slot.Instance); player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot }); }
                }
                if (!action.AcknowledgementBeforeConsumption) acknowledge?.Invoke();
                PublishStats(player, stats);
                foreach (var entry in actions)
                    if (!string.IsNullOrEmpty(entry.Feedback)) player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1, FormattedMessage = entry.Feedback });
                foreach (var grant in notifications.Where(x => !x.Definition.PublishBeforeConsumption)) SendOverflowGrant(player, grant.Item);
                foreach (var entry in actions)
                {
                    if (entry.FeedbackMessage != 0) player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, CategoryId = entry.FeedbackCategory, MessageId = entry.FeedbackMessage });
                    foreach (var quest in entry.DeleteJournals) AuthoredQuestJournal.Delete(player, quest, Content);
                    foreach (var quest in entry.SendJournals) AuthoredQuestJournal.Send(player, quest, _now(), Content);
                }
            };
        });
    }

    bool Conditions(Player player, InteractionAction action, IMissionDaoTransaction tx)
    {
        bool Matches(MissionCondition c) => c.States.Contains(tx.GetMission(new(player.Identity.Instance, c.Quest))?.State.ToString(), StringComparer.Ordinal);
        return (action.Playfields.Length == 0 || action.Playfields.Contains(player.Playfield!.Identity.Instance))
            && action.RequireMissions.All(Matches) && !action.RejectMissions.Any(Matches)
            && !action.RejectCarried.Any(x => HasCarried(player, x))
            && (action.AnyMissions.Length + action.AnyCarried.Length == 0 || action.AnyMissions.Any(Matches) || action.AnyCarried.Any(x => HasCarried(player, x)));
    }

    public bool TryResolveDialogueStart(Player player, DialogueBinding binding, bool priorOpen, out string? node)
    {
        node = null;
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player) || !binding.Enabled || (binding.Playfields.Length != 0 && !binding.Playfields.Contains(player.Playfield!.Identity.Instance))) return false;
            try
            {
                var missions = _dao.GetMissions(player.Identity.Instance);
                node = binding.Starts.FirstOrDefault(x => x.Carried.Any(id => HasCarried(player, id)) || x.Missions.Any(c => missions.Any(m => m.QuestId == c.Quest && c.States.Contains(m.State.ToString()))))?.Node;
                if (node == null && priorOpen && !string.IsNullOrEmpty(binding.ReopenNode)) node = binding.ReopenNode;
                return true;
            }
            catch (Exception e) { _logger.Error(e, "Dialogue state could not be read."); return false; }
        }
    }

    public void Restore(Player player)
    {
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player)) return;
            var timedIds = Content.TimedTurnIns.SelectMany(x => new[] { x.Quest, x.CooldownQuest }).ToHashSet(StringComparer.Ordinal);
            foreach (var mission in _dao.GetMissions(player.Identity.Instance).Where(x => x.State == DaoState.Active && !timedIds.Contains(x.QuestId)))
                AuthoredQuestJournal.Send(player, mission.QuestId, _now(), Content);
            foreach (var timed in Content.TimedTurnIns) RestoreTimedTurnIn(player, timed);
        }
    }

    bool Mutate(Player player, string? account, Func<IMissionDaoTransaction, PersistentMissionService, Action?> operation)
    {
        bool committed = false, applied = false;
        try
        {
            _flush.WithExclusivePlayers(player, null, () =>
            {
                if (!IsCurrent(player)) return;
                _flush.HardFlush(player);
                Action? publish = _dao.Execute(player.Identity.Instance, account!, tx =>
                {
                    var service = new PersistentMissionService(new MissionDaoRepositoryAdapter(new AuthoredMissionTransactionScope(tx)), _catalog.Definitions, () => _now().Ticks);
                    return operation(tx, service);
                });
                committed = true;
                publish?.Invoke();
                applied = true;
            });
        }
        catch (Exception exception)
        {
            if (committed || exception is MissionCommitOutcomeUnknownException or DatabaseCommitOutcomeUnknownException || player.IsPersistenceQuarantined)
            { player.QuarantinePersistence(); player.Session?.Close(); }
            _logger.Error(exception, "Authored mission mutation failed; no success publication before durable commit.");
        }
        return applied;
    }

    static bool IsCurrent(Player player) => !player.IsDead && !player.IsPersistenceQuarantined && player.Inventory.IsHydrated
        && player.Session is { State: SessionState.InPlay } session && ReferenceEquals(session.Player, player) && player.Playfield != null;

    static void RequireSource(Player player, Identity slot, Item item)
    {
        if (slot.Type != IdentityType.Inventory || !player.Inventory.Inventory.Content.TryGetValue(slot.Instance, out var current)
            || !ReferenceEquals(current, item) || item.Locked || !item.IsPersisted || item.StackCount != 1 || InventoryMoveService.IsBagItem(item))
            throw new InvalidOperationException("Authored use requires the exact durable owned main-inventory instance.");
    }

    Item CreateItem(int id, int quality)
    {
        _templates.Require(id);
        return _items.Create(id, id, quality, ItemSource.Other, 1, _ids.Allocate());
    }

    static bool HasCarried(Player player, int templateId) => player.Inventory.Inventory.Content.Values.Concat(player.Inventory.Overflow.Content.Values)
        .Any(item => item.LowId == templateId || item.HighId == templateId);

    static InventoryGrantPlan Plan(Player player, IReadOnlyList<Item> grants)
        => InventoryGrantPlan.TryCreate(player, grants, out var plan) ? plan : throw new InvalidOperationException("No durable capacity for the complete authored reward.");

    static void ApplyRows(IMissionDaoTransaction tx, InventoryGrantPlan plan, Player player, Identity slot, Item consumed)
    {
        if (tx is not IMissionInventoryMutationTransaction inventory) throw new InvalidOperationException("Mission DAO does not support atomic item effects.");
        inventory.ApplyInventoryMutation(plan.Rows.Select(ToMissionItem).ToArray(),
            [ToMissionItem(consumed.ToRecord(player.Inventory.Inventory.Identity, slot.Instance, consumed.StackCount))]);
    }

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    { InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance, ContainerPlacement = row.ContainerPlacement,
        ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId, Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source };

    static void Accept(PersistentMissionService service, int owner, string quest)
    {
        RequireSuccess(service.OfferMission(owner, quest));
        RequireSuccess(service.AcceptMission(owner, quest));
    }

    static void CompleteIfPresent(PersistentMissionService service, int owner, string quest, string objective,
        string observationKey = "content-action-complete", string eventType = "ContentAction")
    {
        var state = service.GetMission(owner, quest);
        if (state == null || state.State == DomainState.Completed) return;
        if (state.State == DomainState.Offered) RequireSuccess(service.AcceptMission(owner, quest));
        RequireSuccess(service.ObserveObjective(new MissionObjectiveObservation { CharacterId = owner, QuestId = quest,
            ObjectiveId = objective, ObservationKey = observationKey, Amount = 1, EventType = eventType, SourceIdentity = string.Empty, TargetIdentity = string.Empty }));
        RequireSuccess(service.CompleteMission(owner, quest));
    }

    static void RequireSuccess(MissionOperationResult result)
    { if (result.Status is not (DomainStatus.Applied or DomainStatus.AlreadyApplied)) throw new InvalidOperationException(result.Message); }

    static IList<MissionStatValueData> ApplyStatReward(IMissionDaoTransaction tx, Player player, string quest, string rewardKey, int xp, int cash, string evidence, long now)
    {
        // Use the owner-gated live values, not potentially older snapshot rows. The accepted
        // AddClamped arithmetic is unchanged; its final values join the same ledger transaction.
        var mutations = new[] { (StatIds.cash, cash), (StatIds.xp, xp), (StatIds.unsavedxp, xp), (StatIds.lastxp, xp) }
            .Select(value => new MissionStatMutationData { StatIdentityType = (int)player.Identity.Type, StatId = (int)value.Item1,
                Kind = AORebirth.Interfaces.Persistence.Missions.MissionStatMutationKind.Set,
                Value = value.Item1 == StatIds.lastxp ? value.Item2 : Math.Min(uint.MaxValue,
                    (long)unchecked((uint)player.Stats.GetOrZero((CharacterStat)value.Item1, StatDetail.Base)) + value.Item2),
                MinimumValue = 0, MaximumValue = uint.MaxValue }).ToArray();
        var result = tx.TryApplyCharacterStatReward(new(new(player.Identity.Instance, quest), rewardKey), "character-stats", mutations, evidence, now);
        if (result.Status is not (AORebirth.Interfaces.Persistence.Missions.MissionAtomicRewardStatus.Applied
            or AORebirth.Interfaces.Persistence.Missions.MissionAtomicRewardStatus.AlreadyApplied)) throw new InvalidOperationException(result.Message);
        return result.StatValues;
    }

    static void PublishStats(Player player, IEnumerable<MissionStatValueData> stats)
    {
        foreach (var stat in stats) player.Stats.Set((CharacterStat)stat.StatId, unchecked((int)(uint)Math.Clamp(stat.Value, 0, uint.MaxValue)), StatDetail.Base, dirty: true);
        player.FlushDirtyStats();
    }

    static void SendOverflowGrant(Player player, Item item)
    {
        player.Session?.Send(new TemplateActionMessage { Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId,
            Quality = item.Quality, Unknown1 = 1, Action = TemplateActionType.Overflow, Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 } });
        player.Session?.Send(new ContainerAddItemMessage { Identity = player.Identity,
            SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
            Target = new Identity { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance }, TargetPlacement = 0x6f });
    }

    static void PublishConsumption(Player player, Identity slot, Item item)
    {
        player.Inventory.Inventory.Content.Remove(slot.Instance);
        player.Session?.Send(new TemplateActionMessage { Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId, Quality = item.Quality,
            Placement = slot, Unknown1 = 1, Action = TemplateActionType.Use, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance });
        player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
    }
}
