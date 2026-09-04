namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

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
    }
}
