namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Applies OnWear / OnWield Modify and ScalingModify bonuses from equipped items.
    /// </summary>
    public static class WearBonusApplier
    {
        public static void ApplyContainer(Container page, bool includeWield, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(stats);

            int last = page.Offset + page.Capacity;
            for (int slot = page.Offset; slot < last; slot++)
            {
                if (!page.Content.TryGetValue(slot, out Item? item) || item?.Definition == null)
                    continue;

                ApplyItem(item, includeWield, stats);
            }
        }

        public static void ApplyItem(Item item, bool includeWield, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(stats);

            Dictionary<EventType, List<ItemSpell>> spells = item.SpellList;
            if (spells.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear))
                StatModifierSpells.Apply(wear, stats);

            if (includeWield && spells.TryGetValue(EventType.OnWield, out List<ItemSpell>? wield))
                StatModifierSpells.Apply(wield, stats);
        }
    }
}
