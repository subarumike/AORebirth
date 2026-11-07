namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Dynel with a loot container that players can open (corpses, chests).
    /// </summary>
    public abstract class LootableDynel : Dynel, IUsableDynel
    {
        public const float OpenRange = 5f;

        static readonly TimeSpan EmptyDespawnDelay = TimeSpan.FromSeconds(1);

        const int CloseActionIdentity = 0x66;

        static readonly Random LootRandom = new();

        int _inventoryHandle;
        bool _wasOpened;
        DateTime? _emptyDespawnAtUtc;
        // Everyone given a client handle for this loot, so despawn can hand the handles back to their pools.
        readonly HashSet<Identity> _handleHolders = new();

        protected LootableDynel(Identity identity, IdentityType lootContainerType, int lootCapacity)
            : base(identity)
        {
            if (lootCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(lootCapacity));

            Loot = new Container(lootContainerType, offset: 0, capacity: lootCapacity, instanceId: identity.Instance)
            {
                Flags = ContainerFlags.CanRemove
            };
        }

        /// <summary>Loot slots populated by <see cref="ResolveLoot"/>.</summary>
        public Container Loot { get; }

        public Identity OpenerIdentity { get; private set; } = Identity.None;

        public bool IsOpen => OpenerIdentity != Identity.None;

        /// <summary>True after the first successful open (survives close toggle).</summary>
        public bool WasOpened => _wasOpened;

        public int InventoryHandle => _inventoryHandle;

        /// <summary>
        /// Opened at least once, loot empty, and the 1s empty-despawn delay has elapsed.
        /// </summary>
        public bool ShouldDespawnEmpty
            => _wasOpened
                && Loot.Content.Count == 0
                && _emptyDespawnAtUtc.HasValue
                && DateTime.UtcNow >= _emptyDespawnAtUtc.Value;

        protected int LootLevel { get; set; } = 1;

        protected List<MobItemTableEntry>? ItemTable { get; set; }

        /// <summary>Items the owner already carried (SpawnItem on an NPC); placed ahead of the item table roll.</summary>
        protected IReadOnlyList<Item>? CarriedItems { get; set; }

        /// <summary>The owner's equipment, whose OnWear SpawnItem functions drop (<see cref="EquipmentLoot"/>).</summary>
        protected IReadOnlyList<Item>? LootEquipment { get; set; }

        protected bool ShareScalingEquipmentQuality { get; set; }

        /// <summary>
        /// Places <see cref="CarriedItems"/>, then rolls what <see cref="LootEquipment"/> spawns and <see cref="ItemTable"/>
        /// against the loot catalog into the remaining <see cref="Loot"/> slots. Each rolled item gets a unique in-memory
        /// <see cref="Item.InstanceId"/> (no DB write). Persistence happens on loot via MarkDirty / flush.
        /// </summary>
        public void ResolveLoot(HashItemMinter minter, int qualityVariancePercent)
        {
            ArgumentNullException.ThrowIfNull(minter);

            int slot = 0;
            if (CarriedItems != null)
            {
                foreach (Item carried in CarriedItems)
                {
                    if (slot >= Loot.Capacity || !Loot.Add(slot, carried))
                        return;
                    slot++;
                }
            }

            var rolled = new List<(int LowId, int HighId, int Quality)>();
            if (LootEquipment != null)
                EquipmentLoot.Roll(LootEquipment, minter, Loot.Capacity - slot, qualityVariancePercent, rolled,
                    ShareScalingEquipmentQuality);
            RollItemTable(ItemTable, LootLevel, minter, Loot.Capacity - slot - rolled.Count, qualityVariancePercent, rolled);
            for (int i = 0; i < rolled.Count; i++, slot++)
            {
                (int lowId, int highId, int quality) = rolled[i];
                if (!Loot.Add(slot, minter.CreateWithNewInstance(lowId, highId, quality, ItemSource.Loot)))
                    return;
            }
        }

        /// <summary>
        /// One corpse's loot roll: every item table row is tried Repeats times at its Chance, each hit rolls a quality
        /// within the row's own LevelMod percent of <paramref name="level"/> (or <paramref name="qualityVariancePercent"/>,
        /// Gameplay.json loot, when the row has none) and spawns its hash's items, until <paramref name="capacity"/> items.
        /// Appends template ids and quality to <paramref name="into"/>; builds no items (GM loot simulation uses it too).
        /// </summary>
        public static void RollItemTable(
            IReadOnlyList<MobItemTableEntry>? itemTable,
            int level,
            HashItemMinter minter,
            int capacity,
            int qualityVariancePercent,
            List<(int LowId, int HighId, int Quality)> into)
        {
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(into);
            if (itemTable == null)
                return;

            int start = into.Count;
            var spawned = new List<(int LowId, int HighId, int Quality)>();
            foreach (MobItemTableEntry entry in itemTable)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Hash) || entry.Repeats <= 0)
                    continue;

                for (int repeat = 0; repeat < entry.Repeats; repeat++)
                {
                    if (into.Count - start >= capacity)
                        return;

                    if (!RollChance(entry.Chance))
                        continue;

                    spawned.Clear();
                    (int minQuality, int maxQuality) = QualityWindow(
                        level, entry.LevelMod > 0 ? entry.LevelMod : qualityVariancePercent);
                    minter.RollSpawnIds(
                        entry.Hash, minQuality, maxQuality, LootRandom.Next(minQuality, maxQuality + 1), spawned);
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        if (into.Count - start >= capacity)
                            return;
                        into.Add(spawned[i]);
                    }
                }
            }
        }

        public virtual bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (player.Session == null)
                return false;

            if (IsOpen)
            {
                if (OpenerIdentity == player.Identity)
                {
                    Close();
                    return true;
                }

                return false;
            }

            if (!CanOpenLoot(player))
                return false;

            // The handle comes from the opener's own pool, shared with their bags; another opener gets one from theirs.
            _inventoryHandle = player.Inventory.GetOrAllocateLootHandle(Identity);
            if (_inventoryHandle == 0)
                return false;
            _handleHolders.Add(player.Identity);

            OpenerIdentity = player.Identity;
            _wasOpened = true;
            OnOpened(player);
            player.Session.Send(Loot.BuildInventoryUpdateMessage(player.Identity, Identity, _inventoryHandle));
            NotifyLootChanged();
            return true;
        }

        /// <summary>Called when a player newly opens this lootable (not on close toggle).</summary>
        protected virtual void OnOpened(Player player)
        {
        }

        protected virtual bool CanOpenLoot(Player player)
            => true;

        /// <summary>
        /// Call after loot contents change (item taken/deleted). Schedules despawn 1s after empty
        /// once the lootable has been opened.
        /// </summary>
        public void NotifyLootChanged()
        {
            if (!_wasOpened)
                return;

            if (Loot.Content.Count > 0)
            {
                _emptyDespawnAtUtc = null;
                return;
            }

            _emptyDespawnAtUtc = DateTime.UtcNow.Add(EmptyDespawnDelay);
        }

        /// <summary>
        /// Hands every client handle for this loot back to its holder's pool. Called on despawn, while the playfield is
        /// still attached. A holder who already left the playfield had their pool reset on the way out.
        /// </summary>
        public void ReleaseClientHandles()
        {
            if (_handleHolders.Count == 0)
                return;

            DynelRegistry? registry = Playfield?.GetService<DynelRegistry>();
            if (registry != null)
            {
                foreach (Identity holder in _handleHolders)
                {
                    if (registry.TryGet(holder, out Dynel? dynel) && dynel is Player player)
                        player.Inventory.ReleaseLootHandle(Identity);
                }
            }

            _handleHolders.Clear();
            _inventoryHandle = 0;
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            Identity opener = OpenerIdentity;
            OpenerIdentity = Identity.None;

            if (Playfield == null)
                return;

            if (!Playfield.GetRequiredService<DynelRegistry>().TryGet(opener, out Dynel? dynel)
                || dynel is not Player player
                || player.Session == null)
            {
                return;
            }

            player.Session.Send(
                new ActionMessage
                {
                    Identity = Identity,
                    Unknown = 1,
                    ActionCode = 1,
                    ActionIdentity = CloseActionIdentity,
                    Target = opener
                });
        }

        public override void Tick(double deltaTime)
        {
            base.Tick(deltaTime);

            if (!IsOpen || Playfield == null)
                return;

            // Closes past the client's use range: 5 m between the bodies (edge distance, Gamecode.dll 0x10059fc7).
            if (!Playfield.GetRequiredService<DynelRegistry>().TryGet(OpenerIdentity, out Dynel? dynel)
                || dynel is not Player opener
                || GetEdgeDistanceTo(opener) > OpenRange)
            {
                Close();
            }
        }

        static bool RollChance(int chance)
        {
            if (chance >= 100)
                return true;
            if (chance <= 0)
                return false;

            return LootRandom.Next(1, 101) <= chance;
        }

        /// <summary>The QL window a drop centred on <paramref name="level"/> rolls in: ± <paramref name="percent"/>.</summary>
        internal static (int Min, int Max) QualityWindow(int level, int percent)
        {
            int mod = Math.Max(0, percent);
            int min = Math.Max(1, level * (100 - mod) / 100);
            int max = Math.Max(min, level * (100 + mod) / 100);
            return (min, max);
        }
    }
}
