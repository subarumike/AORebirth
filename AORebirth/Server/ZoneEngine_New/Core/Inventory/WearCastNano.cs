namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// One-shot OnWear / OnWield CastNano-family functions. Not part of rebase.
    /// </summary>
    internal static class WearCastNano
    {
        public static void ApplyContainer(
            Character wearer,
            Container page,
            bool includeWield,
            IItemBuilder items,
            IInventoryRepository inventory)
        {
            ArgumentNullException.ThrowIfNull(wearer);
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(inventory);

            foreach (KeyValuePair<int, Item> slot in page.EnumerateSlots())
                ApplyItem(wearer, slot.Value, includeWield, items, inventory);
        }

        public static void ApplyItem(
            Character wearer,
            Item item,
            bool includeWield,
            IItemBuilder items,
            IInventoryRepository inventory)
        {
            ArgumentNullException.ThrowIfNull(wearer);
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(inventory);

            ApplyEvent(wearer, item, EventType.OnWear, items, inventory);
            if (includeWield)
                ApplyEvent(wearer, item, EventType.OnWield, items, inventory);
        }

        static void ApplyEvent(
            Character wearer,
            Item item,
            EventType eventType,
            IItemBuilder items,
            IInventoryRepository inventory)
        {
            if (!item.SpellList.TryGetValue(eventType, out List<ItemSpell>? spells) || spells == null)
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                if (!IsCastFunction(spell) || !spell.MeetsRequirements(wearer.Stats))
                    continue;

                NanoCastFunctions.TryExecute(wearer, wearer, spell, items, inventory);
            }
        }

        static bool IsCastFunction(ItemSpell spell)
            => spell.Is(FunctionType.CastNano)
                || spell.Is(FunctionType.AreaCastNano)
                || spell.Is(FunctionType.TeamCastNano)
                || spell.Is(FunctionType.PlayfieldNano);
    }
}
