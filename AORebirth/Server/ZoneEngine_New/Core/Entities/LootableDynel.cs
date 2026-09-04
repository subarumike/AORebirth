namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Dynel with a loot container that players can open (corpses, chests).
    /// </summary>
    public abstract class LootableDynel : Dynel
    {
        public const float OpenRange = 5f;

        const int CloseActionIdentity = 0x66;

        static readonly Random LootRandom = new();

        int _inventoryHandle;

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

        public int InventoryHandle => _inventoryHandle;

        protected int LootLevel { get; set; } = 1;

        protected List<MobItemTableEntry>? ItemTable { get; set; }

        /// <summary>
        /// Rolls <see cref="ItemTable"/> against the loot catalog into <see cref="Loot"/>.
        /// Must run before the dynel is added to a cell (spawn packet).
        /// </summary>
        public void ResolveLoot(IGameData gameData, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);

            if (ItemTable == null || ItemTable.Count == 0)
                return;

            int nextSlot = 0;
            foreach (MobItemTableEntry entry in ItemTable)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Hash) || entry.Repeats <= 0)
                    continue;

                for (int repeat = 0; repeat < entry.Repeats; repeat++)
                {
                    if (nextSlot >= Loot.Capacity)
                        return;

                    if (!RollChance(entry.Chance))
                        continue;

                    if (!gameData.TryGetLootTable(entry.Hash, out IReadOnlyList<LootItemPair> pairs) || pairs.Count == 0)
                        continue;

                    LootItemPair pair = pairs[LootRandom.Next(pairs.Count)];
                    int quality = RollQuality(LootLevel, entry.LevelMod);
                    Item item = items.Create(pair.LowId, pair.HighId, quality);
                    if (!Loot.Add(nextSlot, item))
                        return;

                    nextSlot++;
                }
            }
        }

        public bool TryOpen(Player player)
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

            if (_inventoryHandle == 0 && Playfield != null)
                _inventoryHandle = Playfield.AllocateLootInventoryHandle();

            OpenerIdentity = player.Identity;
            player.Session.Send(Loot.BuildInventoryUpdateMessage(player.Identity, Identity, _inventoryHandle));
            return true;
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

            // TODO: properly implement open-range / distance close
            if (!Playfield.GetRequiredService<DynelRegistry>().TryGet(OpenerIdentity, out Dynel? dynel)
                || dynel is not Player opener
                || Distance3D(opener) > OpenRange)
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

        static int RollQuality(int level, int levelMod)
        {
            int mod = Math.Max(0, levelMod);
            int min = Math.Max(1, level * (100 - mod) / 100);
            int max = Math.Max(min, level * (100 + mod) / 100);
            return LootRandom.Next(min, max + 1);
        }

        protected static int NormalizeLevel(int level)
        {
            if (StatCollection.IsUnset(level) || level < 1)
                return 1;
            return level;
        }
    }
}
