namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    [Flags]
    public enum ContainerFlags
    {
        None = 0,
        CanAdd = 1 << 0,
        CanRemove = 1 << 1,
        Backpack = 1 << 2,
        Inventory = 1 << 3,
        Bank = 1 << 4,
        Contract = 1 << 5,
    }

    /// <summary>
    /// Item container with slot-indexed content (inventory page, backpack, bank, etc.).
    /// </summary>
    public class Container
    {
        const short DefaultEntryFlags = 0x00A1;

        public Container(IdentityType type, int offset, int capacity, int instanceId = 0)
        {
            Identity = new Identity { Type = type, Instance = instanceId };
            Offset = offset;
            Capacity = capacity;
        }

        public Identity Identity { get; }

        public ContainerFlags Flags { get; set; }

        public Dictionary<int, Item> Content { get; } = new();

        public int Offset { get; }

        public int Capacity { get; }

        public bool Add(int slot, Item item)
        {
            if (Content.ContainsKey(slot))
                return false;

            if (slot < Offset || slot >= Offset + Capacity)
                return false;

            Content.Add(slot, item);
            return true;
        }

        public Item? Remove(int slot)
        {
            if (!Content.TryGetValue(slot, out Item? item))
                return null;

            Content.Remove(slot);
            return item;
        }

        /// <summary>
        /// Builds an open/update packet for this container's current contents.
        /// </summary>
        /// <param name="recipient">Player identity receiving the update.</param>
        /// <param name="bagIdentity">Wire bag identity (e.g. corpse dynel identity).</param>
        /// <param name="slotNumberInMainInventory">Client inventory handle for this bag.</param>
        /// <param name="unknown1">Wire Unknown1 (corpse open uses 2).</param>
        public InventoryUpdateMessage BuildInventoryUpdateMessage(
            Identity recipient,
            Identity bagIdentity,
            int slotNumberInMainInventory,
            int unknown1 = 2)
        {
            var entries = new InventoryEntry[Content.Count];
            int index = 0;
            foreach (KeyValuePair<int, Item> slot in Content.OrderBy(static pair => pair.Key))
            {
                Item item = slot.Value;
                short count = (short)Math.Clamp(Math.Max(1, item.StackCount), 1, short.MaxValue);
                short flags = item.Flags != 0
                    ? (short)Math.Clamp(item.Flags, short.MinValue, short.MaxValue)
                    : DefaultEntryFlags;

                entries[index++] = new InventoryEntry
                {
                    Slotnumber = slot.Key,
                    UnknownFlags = flags,
                    Unknown1 = count,
                    Identity = item.Identity,
                    LowId = item.LowId,
                    HighId = item.HighId,
                    Quality = item.Quality,
                    Unknown2 = 0
                };
            }

            return new InventoryUpdateMessage
            {
                Identity = recipient,
                Unknown = 1,
                NumberOfSlots = Capacity,
                Unknown1 = unknown1,
                Entries = entries,
                BagIdentity = bagIdentity,
                SlotnumberInMainInventory = slotNumberInMainInventory,
                Unknown2 = 1
            };
        }
    }
}
