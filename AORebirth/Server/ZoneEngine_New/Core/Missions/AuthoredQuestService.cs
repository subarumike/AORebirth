namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;
using DomainState = ZoneEngine.Core.Missions.MissionLifecycleState;
using DomainStatus = ZoneEngine.Core.Missions.MissionOperationStatus;
using DaoState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

/// <summary>Accepted authored item transitions, using the existing mission service inside ONE DAO transaction.</summary>
public sealed partial class AuthoredQuestService
{
    public const string TalkStan = "Mission:555B4366", BuyLockpick = "Mission:555BD124", Strongbox = "Mission:555BE9C5";
    public const string DeliverFactory = "Mission:555BE9F2", TalkSarah = "Mission:555BE9F3", BuyNano = "Mission:555BE9F4";
    readonly IMissionDao _dao;
    readonly InventoryFlushService _flush;
    readonly IItemBuilder _items;
    readonly IItemTemplateCatalog _templates;
    readonly IItemInstanceIdAllocator _ids;
    readonly AuthoredQuestCatalog _catalog;
    readonly IZoneLogger _logger;
    readonly Func<DateTime> _now;

    public AuthoredQuestService(IMissionDao dao, InventoryFlushService flush, IItemBuilder items,
        IItemTemplateCatalog templates, IItemInstanceIdAllocator ids, AuthoredQuestCatalog catalog, IZoneLogger logger)
        : this(dao, flush, items, templates, ids, catalog, logger, () => DateTime.UtcNow) { }

    public AuthoredQuestService(IMissionDao dao, InventoryFlushService flush, IItemBuilder items,
        IItemTemplateCatalog templates, IItemInstanceIdAllocator ids, AuthoredQuestCatalog catalog, IZoneLogger logger, Func<DateTime> now)
    { _dao = dao; _flush = flush; _items = items; _templates = templates; _ids = ids; _catalog = catalog; _logger = logger; _now = now; }

    public static bool IsAuthoredItem(Item item) => item.LowId == 295999 || item.HighId == 295999
        || CapturedAreteMarcoSpidaVendorContentProvider.TryGetNanoPackage(item.LowId, out _)
        || CapturedAreteMarcoSpidaVendorContentProvider.TryGetNanoPackage(item.HighId, out _)
        || ZoneEngine.Core.Doja.DojaChipInteractionRules.IsKnownDojaChip(item.LowId, item.HighId);

    public bool TryUseItem(Player player, Identity slot, Item item)
    {
        if (!IsAuthoredItem(item)) return false;
        if (ZoneEngine.Core.Doja.DojaChipInteractionRules.IsKnownDojaChip(item.LowId, item.HighId)) return TryUseDoja(player, slot, item);
        return Mutate(player, null, (tx, service) =>
        {
            RequireSource(player, slot, item);
            if (item.LowId == 295999 || item.HighId == 295999)
            {
                var grants = HasCarried(player, 95577) ? Array.Empty<Item>() : new[] { CreateItem(95577, 1) };
                var plan = Plan(player, grants);
                CompleteIfPresent(service, player.Identity.Instance, BuyLockpick, "mission_555BD124_buy_lockpick");
                Accept(service, player.Identity.Instance, Strongbox);
                ApplyRows(tx, plan, player, slot, item);
                return () =>
                {
                    plan.PublishAfterCommit(false);
                    foreach (var grant in grants) SendOverflowGrant(player, grant);
                    PublishConsumption(player, slot, item);
                    AuthoredQuestJournal.Delete(player, unchecked((int)0x555BD124));
                    AuthoredQuestJournal.Send(player, Strongbox, _now());
                };
            }

            if (!CapturedAreteMarcoSpidaVendorContentProvider.TryGetNanoPackage(item.LowId, out var package)
                && !CapturedAreteMarcoSpidaVendorContentProvider.TryGetNanoPackage(item.HighId, out package))
                throw new InvalidOperationException("Unsupported authored package.");
            bool completeTip = tx.GetMission(new(player.Identity.Instance, BuyNano))?.State == DaoState.Active;
            // Validate every accepted template even when its unique item is already owned.
            var contents = package.Contents.Select(entry => CreateItem(entry.ItemId, entry.Quality)).ToArray();
            var grantsList = contents.Where(value => !ZoneEngine_New.Core.Trade.TradeRules.IsUnique(value)
                || !ZoneEngine_New.Core.Trade.TradeRules.WouldDuplicateUnique(player, value.LowId, value.HighId)).ToList();
            Item? reward = null;
            if (completeTip)
            {
                reward = CreateItem(CapturedAreteMarcoSpidaVendorContentProvider.BuyNanoTipRewardItemId,
                    CapturedAreteMarcoSpidaVendorContentProvider.BuyNanoTipRewardQuality);
                if (!HasCarried(player, reward.LowId)) grantsList.Add(reward);
            }
            var grantPlan = Plan(player, grantsList);
            IList<MissionStatValueData> stats = Array.Empty<MissionStatValueData>();
            if (completeTip)
            {
                CompleteIfPresent(service, player.Identity.Instance, BuyNano, "mission_555BE9F4_buy_nano");
                stats = ApplyStanStats(tx, player, BuyNano, "captured-buy-nano-tip-xp-credits", 2569, 1240,
                    "capture:20260721-nanoprogramsvendor:buy-nano-tip-xp-credits", _now().Ticks);
            }
            ApplyRows(tx, grantPlan, player, slot, item);
            return () =>
            {
                grantPlan.PublishAfterCommit(false);
                foreach (var value in grantsList.Where(value => !ReferenceEquals(value, reward))) SendOverflowGrant(player, value);
                PublishConsumption(player, slot, item);
                if (completeTip)
                {
                    PublishStats(player, stats);
                    player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1,
                        FormattedMessage = "~&!!!\":$'O\"ui!!!?4i!!!/S~" });
                    SendOverflowGrant(player, reward!);
                    player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, MessageId = 108871108, CategoryId = 110 });
                    AuthoredQuestJournal.Delete(player, unchecked((int)0x555BE9F4));
                }
            };
        });
    }

    /// <summary>Trusted accepted Stan dialogue branch; packet/NPC identity validation is the caller's responsibility.</summary>
    public bool AcceptStanJob(Player player) => Mutate(player, null, (tx, service) =>
    {
        if (new[] { BuyLockpick, Strongbox, DeliverFactory, TalkSarah, BuyNano }.Any(quest =>
            tx.GetMission(new(player.Identity.Instance, quest))?.State is DaoState.Active or DaoState.Completed)) return null;
        CompleteIfPresent(service, player.Identity.Instance, TalkStan, "mission_555B4366_talk_to_stan");
        Accept(service, player.Identity.Instance, BuyLockpick);
        return () => { AuthoredQuestJournal.Delete(player, unchecked((int)0x555B4366)); AuthoredQuestJournal.Send(player, BuyLockpick, _now()); };
    });

    public void Restore(Player player)
    {
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player)) return;
            foreach (var mission in _dao.GetMissions(player.Identity.Instance).Where(value => value.State == DaoState.Active))
                AuthoredQuestJournal.Send(player, mission.QuestId, _now());
            RestoreDoja(player);
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
            [ToMissionItem(InventoryActionService.ToRecord(consumed, player.Inventory.Inventory.Identity, slot.Instance, consumed.StackCount))]);
    }

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    { InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance, ContainerPlacement = row.ContainerPlacement,
        ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId, Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source };

    static void Accept(PersistentMissionService service, int owner, string quest)
    {
        RequireSuccess(service.OfferMission(owner, quest));
        RequireSuccess(service.AcceptMission(owner, quest));
    }

    static void CompleteIfPresent(PersistentMissionService service, int owner, string quest, string objective)
    {
        var state = service.GetMission(owner, quest);
        if (state == null || state.State == DomainState.Completed) return;
        if (state.State == DomainState.Offered) RequireSuccess(service.AcceptMission(owner, quest));
        RequireSuccess(service.ObserveObjective(new MissionObjectiveObservation { CharacterId = owner, QuestId = quest,
            ObjectiveId = objective, ObservationKey = "stan-goodman-force-complete", Amount = 1, EventType = "StanGoodmanQuestRuntime", SourceIdentity = string.Empty, TargetIdentity = string.Empty }));
        RequireSuccess(service.CompleteMission(owner, quest));
    }

    static void RequireSuccess(MissionOperationResult result)
    { if (result.Status is not (DomainStatus.Applied or DomainStatus.AlreadyApplied)) throw new InvalidOperationException(result.Message); }

    static IList<MissionStatValueData> ApplyStanStats(IMissionDaoTransaction tx, Player player, string quest, string rewardKey, int xp, int cash, string evidence, long now)
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
            Quality = item.Quality, Unknown1 = 1, Unknown2 = 87, Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 } });
        player.Session?.Send(new ContainerAddItemMessage { Identity = player.Identity,
            SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
            Target = new Identity { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance }, TargetPlacement = 0x6f });
    }

    static void PublishConsumption(Player player, Identity slot, Item item)
    {
        player.Inventory.Inventory.Content.Remove(slot.Instance);
        player.Session?.Send(new TemplateActionMessage { Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId, Quality = item.Quality,
            Placement = slot, Unknown1 = 1, Unknown2 = 3, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance });
        player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
    }
}
