namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Pure capacity/identity plan for a mission-owned transaction. The caller holds
    /// PersistenceGate through its existing mission DAO commit and PublishAfterCommit.
    /// This class never starts an independent transaction or allocates an identity.
    /// </summary>
    public sealed class InventoryGrantPlan
    {
        readonly Player _player;
        readonly Item[] _items;
        readonly int[] _slots;
        bool _published;

        InventoryGrantPlan(Player player, Item[] items, int[] slots)
        {
            _player = player; _items = items; _slots = slots;
            Rows = items.Select((item, i) => InventoryActionService.ToRecord(
                item, player.Inventory.Inventory.Identity, slots[i], item.StackCount)).ToArray();
        }

        public IReadOnlyList<ItemInstanceRecord> Rows { get; }

        public static bool TryCreate(Player player, IReadOnlyList<Item> items, out InventoryGrantPlan plan)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(items);
            plan = null!;
            if (!Monitor.IsEntered(player.PersistenceGate)) throw new InvalidOperationException("Grant planning requires the player's persistence gate.");
            if (player.IsPersistenceQuarantined || !player.Inventory.IsHydrated
                || !player.Inventory.HasFreeInventorySlots(items.Count)
                || items.Any(item => item.InstanceId <= 0 || item.IsPersisted || item.StackCount <= 0)
                || items.Select(item => item.InstanceId).Distinct().Count() != items.Count) return false;
            Container page = player.Inventory.Inventory;
            int[] slots = Enumerable.Range(page.Offset, page.Capacity).Where(slot => !page.Content.ContainsKey(slot)).Take(items.Count).ToArray();
            plan = new InventoryGrantPlan(player, items.ToArray(), slots);
            return true;
        }

        public bool PublishAfterCommit(bool notify = true)
        {
            if (!Monitor.IsEntered(_player.PersistenceGate)) throw new InvalidOperationException("Grant publication requires the player's persistence gate.");
            if (_published) return false;
            Container page = _player.Inventory.Inventory;
            for (int i = 0; i < _items.Length; i++)
                if (page.Content.ContainsKey(_slots[i]) || _items[i].InstanceId != Rows[i].InstanceId || _items[i].IsPersisted)
                    throw new InvalidOperationException("Mission inventory grant plan changed before publication.");
            _published = true;
            for (int i = 0; i < _items.Length; i++)
            {
                page.Add(_slots[i], _items[i]);
                _items[i].IsPersisted = true;
            }
            if (notify)
                foreach (Item item in _items)
                    _player.Session?.Send(new AddTemplateMessage
                {
                    Identity = _player.Identity, LowId = item.LowId, HighId = item.HighId,
                    Quality = item.Quality, Count = item.StackCount
                });
            return true;
        }
    }
}
