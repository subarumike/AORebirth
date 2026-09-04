namespace ZoneEngine_New.Core.Inventory
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    /// <summary>
    /// Shared item definition shape (catalog entry or builder-baked effective def).
    /// </summary>
    public sealed class ItemTemplate
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Quality { get; init; }

        public int Flags { get; init; }

        public int ItemType { get; init; }

        public int MultipleCount { get; init; }

        public Dictionary<int, int> Stats { get; init; } = new();

        public Dictionary<int, int> Attack { get; init; } = new();

        public Dictionary<int, int> Defend { get; init; } = new();

        public Dictionary<EventType, List<ItemSpell>> SpellList { get; init; } = new();

        public List<ItemAction> Actions { get; init; } = new();

        public List<int> Relations { get; init; } = new();
    }
}
