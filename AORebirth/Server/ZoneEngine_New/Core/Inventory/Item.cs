namespace ZoneEngine_New.Core.Inventory
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Dumb runtime item: occupancy ids + builder-baked effective <see cref="ItemTemplate"/>.
    /// </summary>
    public sealed class Item
    {
        public Identity Identity { get; init; }

        public int LowId { get; init; }

        public int HighId { get; init; }

        public int Quality { get; init; }

        public int StackCount { get; set; } = 1;

        public ItemTemplate Definition { get; init; } = null!;

        public string Name => Definition.Name;

        public int Flags => Definition.Flags;

        public Dictionary<EventType, List<ItemSpell>> SpellList => Definition.SpellList;

        public int GetStat(int attrId)
            => Definition.Stats.TryGetValue(attrId, out int value) ? value : 0;
    }
}
