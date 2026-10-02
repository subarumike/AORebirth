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

            foreach (KeyValuePair<int, Item> slot in page.EnumerateSlots())
            {
                if (slot.Value.Definition == null)
                    continue;

                ApplyItem(slot.Value, includeWield, stats);
            }
        }

        public static void ApplyItem(Item item, bool includeWield, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(stats);

            // The client re-applies only OnWear functions at the over-equipped level (Gamecode.dll FUN_1004b624).
            Dictionary<EventType, List<ItemSpell>> spells = item.SpellList;
            if (spells.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear))
                StatModifierSpells.Apply(wear, stats, item.OverEquipLevel);

            if (includeWield && spells.TryGetValue(EventType.OnWield, out List<ItemSpell>? wield))
                StatModifierSpells.Apply(wield, stats);
        }
    }
}
