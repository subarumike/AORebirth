namespace ZoneEngine_New.Core.Inventory;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;

/// <summary>
/// Opens ItemBehavior.json packages (a Sealed Lock Pick, a profession's nano kit): the sealed item leaves the inventory
/// and its products arrive in one durable commit, then the Quests.json steps the package names complete.
/// </summary>
public sealed class ItemPackageService
{
    readonly IMissionDao _dao;
    readonly InventoryFlushService _flush;
    readonly IItemBuilder _items;
    readonly IItemTemplateCatalog _templates;
    readonly IItemInstanceIdAllocator _ids;
    readonly IZoneLogger _logger;
    readonly ZoneEngine_New.Core.Quests.QuestService? _quests;
    readonly ItemBehaviorContent _content;

    public ItemPackageService(IMissionDao dao, InventoryFlushService flush, IItemBuilder items, IItemTemplateCatalog templates,
        IItemInstanceIdAllocator ids, IZoneLogger logger, ZoneEngine_New.Core.Quests.QuestService? quests = null)
        : this(dao, flush, items, templates, ids, logger, ItemBehaviorContent.Current, quests) { }

    public ItemPackageService(IMissionDao dao, InventoryFlushService flush, IItemBuilder items, IItemTemplateCatalog templates,
        IItemInstanceIdAllocator ids, IZoneLogger logger, ItemBehaviorContent content, ZoneEngine_New.Core.Quests.QuestService? quests = null)
    {
        _dao = dao ?? throw new ArgumentNullException(nameof(dao));
        _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _quests = quests;
    }

    public bool IsPackage(Item item) => _content.FindPackage(item) != null;

    /// <summary>Opens the package in <paramref name="slot"/>. False (and nothing changed) when it cannot be opened.</summary>
    public bool TryOpen(Player player, Identity slot, Item item)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(item);
        if (_content.FindPackage(item) is not { } package)
            return false;

        bool committed = false, applied = false;
        try
        {
            _flush.WithExclusivePlayers(player, null, () =>
            {
                if (!IsCurrent(player)) return;
                _flush.HardFlush(player);
                RequireSource(player, slot, item);
                var granted = new List<Item>();
                foreach (var product in package.AllProducts)
                {
                    if (product.SkipIfCarried && HasCarried(player, product.LowId, product.HighId))
                        continue;
                    _templates.Require(product.LowId);
                    var created = _items.Create(product.LowId, product.HighId, product.Quality, ItemSource.Other, 1, _ids.Allocate());
                    if (product.Unique && Trade.TradeRules.IsUnique(created)
                        && (Trade.TradeRules.WouldDuplicateUnique(player, created.LowId, created.HighId)
                            || granted.Any(x => x.LowId == created.LowId || x.HighId == created.HighId)))
                        continue;
                    granted.Add(created);
                }
                // What does not fit in the inventory goes to overflow, as other durable grants do.
                if (!InventoryGrantPlan.TryCreate(player, granted, out var plan, allowOverflow: true))
                    throw new InvalidOperationException("No inventory or overflow room for the package's contents.");

                _dao.Execute(player.Identity.Instance, null!, tx =>
                {
                    if (tx is not IMissionInventoryMutationTransaction inventory)
                        throw new InvalidOperationException("Mission DAO lacks atomic item effects.");
                    inventory.ApplyInventoryMutation(plan.Rows.Select(ToMissionItem).ToArray(),
                        [ToMissionItem(item.ToRecord(player.Inventory.Inventory.Identity, slot.Instance, item.StackCount))]);
                    return true;
                });
                committed = true;

                // Live order (Sealed Lock Pick, 2026-07-21): each product shows up first, then the package is used up.
                plan.PublishAfterCommit(notify: false);
                foreach (var product in granted) OverflowGrants.Send(player, product);
                PublishConsumption(player, slot, item);
                // Products that did not fit went to overflow; keep it to its capacity now the client has them.
                OverflowGrants.Trim(player);
                applied = true;
            });
        }
        catch (Exception exception)
        {
            if (committed || exception is MissionCommitOutcomeUnknownException or DatabaseCommitOutcomeUnknownException || player.IsPersistenceQuarantined)
            {
                player.QuarantinePersistence();
                player.Session?.Close();
            }
            _logger.Error(exception, "Item package open failed; nothing was published before the durable commit.");
        }

        // Quests.json steps commit on their own: completing saves, swaps the journal entry and assigns NextStep.
        if (applied && _quests != null)
            foreach (string hash in package.CompleteQuests)
                _quests.TryComplete(player, hash, out _);
        return applied;
    }

    static bool IsCurrent(Player player) => !player.IsDead && !player.IsPersistenceQuarantined && player.Inventory.IsHydrated
        && player.Session is { State: SessionState.InPlay } session && ReferenceEquals(session.Player, player) && player.Playfield != null;

    static void RequireSource(Player player, Identity slot, Item item)
    {
        if (slot.Type != IdentityType.Inventory || !player.Inventory.Inventory.Content.TryGetValue(slot.Instance, out var current)
            || !ReferenceEquals(current, item) || item.Locked || !item.IsPersisted || item.StackCount != 1 || InventoryMoveService.IsBagItem(item))
            throw new InvalidOperationException("A package opens only as the exact durable owned main-inventory instance.");
    }

    static bool HasCarried(Player player, int lowId, int highId) => player.Inventory.Inventory.Content.Values
        .Concat(player.Inventory.Overflow.Content.Values)
        .Any(item => item.LowId == lowId || item.HighId == highId);

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    {
        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
        Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
    };

    static void PublishConsumption(Player player, Identity slot, Item item)
    {
        player.Inventory.Inventory.Content.Remove(slot.Instance);
        player.Session?.Send(new TemplateActionMessage { Identity = player.Identity, ItemLowId = item.LowId, ItemHighId = item.HighId, Quality = item.Quality,
            Placement = slot, Unknown1 = 1, Action = TemplateActionType.Use, Unknown3 = (int)player.Identity.Type, Unknown4 = player.Identity.Instance });
        player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
    }
}
