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
            return ClampDelay(item.GetStat(CharacterStat.AttackDelay));
        }

        public static int ResolveDelayCentiseconds(StaticDynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);
            return ClampDelay(dynel.Stats.GetOrZero(CharacterStat.AttackDelay));
        }

        static int ClampDelay(int attackDelay)
            => Math.Clamp(StatCollection.Normalize(attackDelay), 0, MaxDelayCentiseconds);

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

            int instanceId = item.InstanceId;
            var pending = new PendingItemUse(
                player,
                string.Format(CultureInfo.InvariantCulture, "slot={0}:{1} low={2} instanceId={3}",
                    slot.Type, slot.Instance, item.LowId, instanceId),
                () => RevalidateInventory(player, slot, item, instanceId),
                () => item.ExecuteUse(player, slot, _inventoryRepository, _items),
                lockTarget: locked => item.Locked = locked);
            return Begin(pending, ResolveDelayCentiseconds(item));
        }

        /// <summary>
        /// World item / static dynel Use. The template's AttackDelay runs like an inventory use; range,
        /// playfield and use requirements are checked again before OnUse runs.
        /// </summary>
        public ItemUseStart TryBegin(Player player, StaticDynel dynel)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(dynel);

            if (!ReferenceEquals(player.Playfield, _playfield)
                || HasPending(player.Identity.Instance)
                || _moves.HasPending(player.Identity.Instance))
                return ItemUseStart.Rejected;

            if (!dynel.CanBeginUse(player))
                return ItemUseStart.Rejected;

            var pending = new PendingItemUse(
                player,
                string.Format(CultureInfo.InvariantCulture, "dynel={0} template={1}", dynel.Identity, dynel.Template.Id),
                () => RevalidateDynel(player, dynel),
                () => dynel.ExecuteUse(player),
                lockTarget: null);
            return Begin(pending, ResolveDelayCentiseconds(dynel));
        }

        ItemUseStart Begin(PendingItemUse pending, int delayCentiseconds)
        {
            if (delayCentiseconds <= 0)
                return pending.Execute() ? ItemUseStart.Executed : ItemUseStart.Rejected;

            pending.RemainingSeconds = delayCentiseconds * 0.01;
            pending.SetLocked(true);
            lock (_gate)
            {
                if (_pending.TryAdd(pending.Player.Identity.Instance, pending))
                    return ItemUseStart.Started;
            }

            pending.SetLocked(false);
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

            pending.SetLocked(false);
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
                pending.SetLocked(false);

            foreach (PendingItemUse pending in due)
                Complete(pending);
        }

        void Complete(PendingItemUse pending)
        {
            // DestroyOne/ConsumeCharge refuse locked items.
            pending.SetLocked(false);

            Player player = pending.Player;
            string? failure = RevalidatePlayer(player) ?? pending.Revalidate();
            if (failure == null && pending.Execute())
                return;

            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Delayed item use aborted char={0} {1}: {2}",
                    player.Identity.Instance,
                    pending.Description,
                    failure ?? "OnUse spells returned false"));
            Tell(player, FailedText);
        }

        string? RevalidatePlayer(Player player)
        {
            if (player.Session == null || player.Session.State != SessionState.InPlay)
                return "session not InPlay";
            if (player.IsDead || player.IsPersistenceQuarantined)
                return "player dead or quarantined";
            if (!ReferenceEquals(player.Playfield, _playfield))
                return "playfield changed";
            return null;
        }

        static string? RevalidateInventory(Player player, Identity slot, Item item, int instanceId)
        {
            if (!player.Inventory.IsHydrated)
                return "inventory not hydrated";
            if (!player.Inventory.TryGetItem(slot.Type, slot.Instance, out Item current)
                || !ReferenceEquals(current, item)
                || current.InstanceId != instanceId)
                return "slot changed";
            if (!current.CanBeginUse(player))
                return "use gates failed";
            return null;
        }

        string? RevalidateDynel(Player player, StaticDynel dynel)
        {
            if (!_playfield.GetRequiredService<DynelRegistry>().TryGet(dynel.Identity, out Dynel? current)
                || !ReferenceEquals(current, dynel))
                return "dynel gone";
            if (!dynel.CanBeginUse(player))
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

        /// <summary>
        /// One delayed use. <see cref="Revalidate"/> re-checks the used thing's own gates (null = still valid);
        /// the player gates are shared. Only inventory items lock while the delay runs.
        /// </summary>
        sealed class PendingItemUse(
            Player player,
            string description,
            Func<string?> revalidate,
            Func<bool> execute,
            Action<bool>? lockTarget)
        {
            public Player Player { get; } = player;

            public string Description { get; } = description;

            public double RemainingSeconds { get; set; }

            public string? Revalidate() => revalidate();

            public bool Execute() => execute();

            public void SetLocked(bool locked) => lockTarget?.Invoke(locked);
        }
    }
}
