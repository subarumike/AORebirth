namespace ZoneEngine_New.Core.Quests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Interfaces.Persistence.Missions;
using AORebirth.Modules.Events;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Modules;

/// <summary>Projects the existing starter-item grants for a follow-up quest into the owning mission transaction.</summary>
internal sealed class QuestStarterItemPlan(Player player)
{
    readonly record struct Retirement(Item Item, int Slot);
    readonly record struct Grant(Item Item, Container Page, int Slot, Item? Stack, int StackSlot, Retirement[] Retired);

    readonly List<Grant> _grants = [];
    readonly List<(int BeforeGrant, string Name)> _skippedUnique = [];
    bool _published;
    internal List<MissionItemInstanceData> StarterGrants { get; } = [];
    internal List<MissionItemInstanceData> RetiredOverflow { get; } = [];
    internal List<CharacterQuestStarterStack> StackMerges { get; } = [];

    internal static bool TryCreate(Player player, IReadOnlyList<Item> items, out QuestStarterItemPlan plan)
    {
        if (!Monitor.IsEntered(player.PersistenceGate))
            throw new InvalidOperationException("Starter item planning requires the player's persistence gate.");
        plan = new QuestStarterItemPlan(player);
        if (items.Count == 0)
            return true;

        Container main = player.Inventory.Inventory;
        Container overflow = player.Inventory.Overflow;
        var mainSlots = main.Content.Keys.ToHashSet();
        var overflowItems = new Dictionary<int, Item>(overflow.Content);
        var newOverflowAges = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance);
        var pendingStacks = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance);
        var retired = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        var introduced = new List<Item>();
        int overflowCapacity = Math.Max(0, OverflowGrants.Capacity(player));

        foreach (Item item in items)
        {
            if (item.InstanceId <= 0 || item.IsPersisted || item.StackCount <= 0)
                throw new InvalidOperationException("A starter grant requires a new, exact item instance.");
            if (UniqueItems.WouldDuplicate(player, item, out string name, retired.Contains)
                || (Trade.TradeRules.IsUnique(item) && introduced.Any(held => !retired.Contains(held)
                    && (held.LowId == item.LowId || held.LowId == item.HighId || held.HighId == item.LowId || held.HighId == item.HighId))))
            {
                plan._skippedUnique.Add((plan._grants.Count, string.IsNullOrEmpty(name) ? item.Name : name));
                continue;
            }

            int slot = FirstFree(main, mainSlots.Contains);
            if (slot >= 0 && InventoryStacking.TryFindStackTarget(main, item, pendingStacks, out int targetSlot, out Item target))
            {
                pendingStacks[target] = pendingStacks.GetValueOrDefault(target) + item.StackCount;
                plan._grants.Add(new Grant(item, main, slot, target, targetSlot, []));
                continue;
            }
            introduced.Add(item);
            if (slot >= 0)
            {
                mainSlots.Add(slot);
                plan._grants.Add(new Grant(item, main, slot, null, 0, []));
                continue;
            }

            slot = FirstFree(overflow, overflowItems.ContainsKey);
            if (slot < 0)
                return false;
            overflowItems.Add(slot, item);
            newOverflowAges.Add(item, newOverflowAges.Count);
            var retirements = new List<Retirement>();
            while (overflowItems.Count > overflowCapacity)
            {
                var oldest = overflowItems.OrderBy(entry => newOverflowAges.ContainsKey(entry.Value))
                    .ThenBy(entry => newOverflowAges.TryGetValue(entry.Value, out int age)
                        ? age : player.Inventory.OverflowArrivalOrder(entry.Value))
                    .ThenBy(entry => entry.Key).First();
                overflowItems.Remove(oldest.Key);
                retired.Add(oldest.Value);
                retirements.Add(new Retirement(oldest.Value, oldest.Key));
            }
            plan._grants.Add(new Grant(item, overflow, slot, null, 0, retirements.ToArray()));
        }

        foreach (Grant grant in plan._grants)
        {
            if (grant.Stack == null && !retired.Contains(grant.Item))
                plan.StarterGrants.Add(ToMissionItem(grant.Item.ToRecord(grant.Page.Identity, grant.Slot, grant.Item.StackCount)));
            foreach (Retirement retirement in grant.Retired)
            {
                if (newOverflowAges.ContainsKey(retirement.Item)) continue;
                plan.RetiredOverflow.Add(ToMissionItem(retirement.Item.ToRecord(overflow.Identity, retirement.Slot, retirement.Item.StackCount)));
            }
        }
        foreach (var (target, increase) in pendingStacks)
        {
            Grant merge = plan._grants.First(grant => ReferenceEquals(grant.Stack, target));
            plan.StackMerges.Add(new CharacterQuestStarterStack
            {
                Existing = ToMissionItem(target.ToRecord(main.Identity, merge.StackSlot, target.StackCount)),
                FinalCount = target.StackCount + increase
            });
        }
        return true;
    }

    static int FirstFree(Container page, Func<int, bool> occupied)
    {
        for (int slot = page.Offset; slot < page.Offset + page.Capacity; slot++)
            if (!occupied(slot)) return slot;
        return -1;
    }

    static MissionItemInstanceData ToMissionItem(ItemInstanceRecord row) => new()
    {
        InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
        ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
        Quality = row.Quality, StackCount = row.StackCount, Source = (byte)row.Source
    };

    internal void PublishAfterCommit()
    {
        if (!Monitor.IsEntered(player.PersistenceGate) || _published)
            throw new InvalidOperationException("Committed starter items must publish once through the persistence gate.");
        _published = true;
        int skipped = 0;
        var committedIds = StarterGrants.Select(item => item.InstanceId).ToHashSet();
        for (int i = 0; i < _grants.Count; i++)
        {
            PublishSkippedUniques(i, ref skipped);
            Grant grant = _grants[i];
            Item item = grant.Item;
            if (grant.Stack != null)
            {
                if (!grant.Page.Content.TryGetValue(grant.StackSlot, out var target) || !ReferenceEquals(target, grant.Stack))
                    throw new InvalidOperationException("Starter item stack changed after commit.");
                target.StackCount += item.StackCount;
                PublishArrival(item);
                InventoryStacking.SendGrantAndJoin(player, item, grant.Page, grant.StackSlot, grant.Slot);
                continue;
            }
            if (!grant.Page.Add(grant.Slot, item))
                throw new InvalidOperationException("Starter item slot changed after commit.");
            item.IsPersisted = committedIds.Contains(item.InstanceId);
            bool overflow = ReferenceEquals(grant.Page, player.Inventory.Overflow);
            if (overflow)
                player.Inventory.NoteOverflowArrival(item);
            PublishArrival(item);
            if (!overflow)
            {
                player.Session?.Send(new AddTemplateMessage
                {
                    Identity = player.Identity, HighId = item.HighId, LowId = item.LowId,
                    Quality = item.Quality, Count = item.StackCount
                });
                continue;
            }
            OverflowGrants.Send(player, item);
            foreach (Retirement retirement in grant.Retired)
            {
                player.Inventory.RemoveCommittedOverflow(retirement.Slot, retirement.Item);
                OverflowGrants.SendRetirement(player, retirement.Item, retirement.Slot);
            }
            if (grant.Page.Content.Count > 0)
                OverflowGrants.SendWindow(player);
        }
        PublishSkippedUniques(_grants.Count, ref skipped);
    }

    void PublishSkippedUniques(int beforeGrant, ref int skipped)
    {
        while (skipped < _skippedUnique.Count && _skippedUnique[skipped].BeforeGrant == beforeGrant)
            ClientFeedback.SendFormatted(player, ClientFeedback.AlreadyHaveUniqueItem, _skippedUnique[skipped++].Name);
    }

    void PublishArrival(Item item) => ModuleHooks.ItemMoved(player, item, ModuleHooks.Place(ItemPlaceKind.None),
        ModuleHooks.Place(ItemPlaceKind.Inventory));
}
