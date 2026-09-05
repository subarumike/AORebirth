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

        public bool IsOpen { get; set; }

        public bool IsHydrated { get; set; } = true;

        public int InventoryHandle { get; set; }

        public Item? LinkedItem { get; set; }

        public Identity ParentSlot { get; set; } = Identity.None;

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

        public int FindFreeSlot()
        {
            for (int slot = Offset; slot < Offset + Capacity; slot++)
            {
                if (!Content.ContainsKey(slot))
                    return slot;
            }

            return -1;
        }

        public ChestItemFullUpdateMessage BuildChestItemFullUpdate(
            Identity owner,
            int playfieldId,
            Identity sourceInventorySlot)
        {
            Item bag = LinkedItem
                ?? throw new InvalidOperationException("Container has no LinkedItem for ChestItemFullUpdate.");

            return new ChestItemFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                Unknown1 = 0x0b,
                Owner = owner,
                PlayfieldId = playfieldId,
                StateMachine = new Identity { Type = (IdentityType)1000015, Instance = 0 },
                Unknown5 = (short)(0x0100 | (sourceInventorySlot.Instance & 0xff)),
                Stats =
                [
                    StatTuple(CharacterStat.Flags, (uint)bag.Flags),
                    StatTuple(CharacterStat.StaticInstance, (uint)bag.HighId),
                    StatTuple(CharacterStat.ACGItemLevel, (uint)bag.Quality),
                    StatTuple(CharacterStat.ACGItemTemplateID, (uint)bag.LowId),
                    StatTuple(CharacterStat.ACGItemTemplateID2, (uint)bag.HighId),
                    StatTuple(
                        CharacterStat.MultipleCount,
                        (uint)Math.Max(1, bag.StackCount))
                ],
                Unknown6 = 0,
                Unknown7 = 2,
                Unknown8 = 50,
                UnknownArray = [],
                Unknown9 = 3
            };
        }

        /// <summary>
        /// Builds an open/update packet for this container's current contents.
        /// </summary>
        public InventoryUpdateMessage BuildInventoryUpdateMessage(
            Identity recipient,
            Identity bagIdentity,
            int slotNumberInMainInventory,
            int unknown1 = 2,
            int unknown2 = 1)
        {
            var entries = new InventoryEntry[Content.Count];
            int index = 0;
            foreach (KeyValuePair<int, Item> slot in Content.OrderBy(static pair => pair.Key))
            {
                Item item = slot.Value;
                short count = (short)Math.Clamp(Math.Max(1, item.StackCount), 1, short.MaxValue);

                entries[index++] = new InventoryEntry
                {
                    Slotnumber = slot.Key,
                    UnknownFlags = item.ToInventoryPacketFlags(),
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
                Unknown2 = unknown2
            };
        }

        static GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
            => new() { Value1 = stat, Value2 = value };
    }
}
