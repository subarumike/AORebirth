namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public enum ItemUseStart
    {
        Rejected,
        Started,
        Executed,
    }

    /// <summary>
    /// Authoritative inventory item Use with the template's AttackDelay. The item stays locked
    /// while the delay runs; every gate is checked again before OnUse runs. One per playfield.
    /// </summary>
    public sealed class ItemUseService
    {
        /// <summary>Upper bound on a template delay so a bad stat cannot lock an item indefinitely.</summary>
        public const int MaxDelayCentiseconds = 6000;

        const string FailedText = "You could not use that item.";

        private readonly object _gate = new();
        private readonly Dictionary<int, PendingItemUse> _pending = new();
        private readonly Playfield _playfield;
        private readonly IZoneLogger _logger;
        private readonly InventoryMoveService _moves;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IItemBuilder _items;

        public ItemUseService(
            Playfield playfield,
            IZoneLogger logger,
            InventoryMoveService moves,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            _playfield = playfield ?? throw new ArgumentNullException(nameof(playfield));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _moves = moves ?? throw new ArgumentNullException(nameof(moves));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public static int ResolveDelayCentiseconds(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            int delay = StatCollection.Normalize(item.GetStat(CharacterStat.AttackDelay));
            return Math.Clamp(delay, 0, MaxDelayCentiseconds);
        }

        public bool HasPending(int characterId)
        {
            lock (_gate)
                return _pending.ContainsKey(characterId);
        }

        public ItemUseStart TryBegin(Player player, Identity slot, Item item)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(item);

            if (!ReferenceEquals(player.Playfield, _playfield)
                || HasPending(player.Identity.Instance)
                || _moves.HasPending(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (item.IsBackpackUse)
                return item.Use(player, slot, _inventoryRepository, _items) ? ItemUseStart.Executed : ItemUseStart.Rejected;

            if (!item.CanBeginUse(player))
                return ItemUseStart.Rejected;

            int delay = ResolveDelayCentiseconds(item);
            if (delay <= 0)
                return item.ExecuteUse(player, slot, _inventoryRepository, _items) ? ItemUseStart.Executed : ItemUseStart.Rejected;

            var pending = new PendingItemUse(player, slot, item, delay * 0.01);
            item.Locked = true;
            lock (_gate)
            {
                if (_pending.TryAdd(player.Identity.Instance, pending))
                    return ItemUseStart.Started;
            }

            item.Locked = false;
            return ItemUseStart.Rejected;
        }

        public void CancelPending(int characterId)
        {
            PendingItemUse? pending;
            lock (_gate)
            {
                if (!_pending.Remove(characterId, out pending))
                    return;
            }

            pending.Item.Locked = false;
        }

        public void Tick(double deltaTime)
        {
            if (deltaTime <= 0)
                return;

            List<PendingItemUse> due = [];
            List<PendingItemUse> stale = [];
            lock (_gate)
            {
                if (_pending.Count == 0)
                    return;

                List<int> remove = [];
                foreach (KeyValuePair<int, PendingItemUse> pair in _pending)
                {
                    PendingItemUse pending = pair.Value;
                    if (!ReferenceEquals(pending.Player.Playfield, _playfield))
                    {
                        stale.Add(pending);
                        remove.Add(pair.Key);
                        continue;
                    }

                    pending.RemainingSeconds -= deltaTime;
                    if (pending.RemainingSeconds > 0)
                        continue;

                    due.Add(pending);
                    remove.Add(pair.Key);
                }

                foreach (int id in remove)
                    _pending.Remove(id);
            }

            foreach (PendingItemUse pending in stale)
                pending.Item.Locked = false;

            foreach (PendingItemUse pending in due)
                Complete(pending);
        }

        void Complete(PendingItemUse pending)
        {
            // DestroyOne/ConsumeCharge refuse locked items.
            pending.Item.Locked = false;

            Player player = pending.Player;
            string? failure = Revalidate(player, pending);
            if (failure == null
                && pending.Item.ExecuteUse(player, pending.Slot, _inventoryRepository, _items))
                return;

            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Delayed item use aborted char={0} slot={1}:{2} low={3} instanceId={4}: {5}",
                    player.Identity.Instance,
                    pending.Slot.Type,
                    pending.Slot.Instance,
                    pending.Item.LowId,
                    pending.InstanceId,
                    failure ?? "OnUse spells returned false"));
            Tell(player, FailedText);
        }

        string? Revalidate(Player player, PendingItemUse pending)
        {
            if (player.Session == null || player.Session.State != SessionState.InPlay)
                return "session not InPlay";
            if (player.IsDead || player.IsPersistenceQuarantined)
                return "player dead or quarantined";
            if (!ReferenceEquals(player.Playfield, _playfield))
                return "playfield changed";
            if (!player.Inventory.IsHydrated)
                return "inventory not hydrated";
            if (!player.Inventory.TryGetItem(pending.Slot.Type, pending.Slot.Instance, out Item current)
                || !ReferenceEquals(current, pending.Item)
                || current.InstanceId != pending.InstanceId)
                return "slot changed";
            if (!current.CanBeginUse(player))
                return "use gates failed";
            return null;
        }

        static void Tell(Player player, string text)
        {
            player.Session?.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }

        sealed class PendingItemUse
        {
            public PendingItemUse(Player player, Identity slot, Item item, double remainingSeconds)
            {
                Player = player;
                Slot = slot;
                Item = item;
                InstanceId = item.InstanceId;
                RemainingSeconds = remainingSeconds;
            }

            public Player Player { get; }

            public Identity Slot { get; }

            public Item Item { get; }

            public int InstanceId { get; }

            public double RemainingSeconds { get; set; }
        }
    }
}
