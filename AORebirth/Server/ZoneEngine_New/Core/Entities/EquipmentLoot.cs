namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// What an NPC's equipment drops: every OnWear SpawnItem on an equipped item (a boss's "Lootgiver" and Boss
    /// Modifier items) spawns its hash. The function's QL, which the item's own QL (the NPC's level) scales, is the
    /// centre: the drop rolls a QL within Gameplay.json loot.qualityVariancePercent of it and takes the hash's highest QL
    /// up to that. Rolled when the corpse is made, not when the NPC spawns, so the GM loot simulation rolls the same way.
    /// </summary>
    public static class EquipmentLoot
    {
        /// <summary>
        /// Appends the ids and QL of every item <paramref name="equipment"/> spawns, up to <paramref name="capacity"/>
        /// items. Builds no items.
        /// </summary>
        public static void Roll(
            IEnumerable<Item> equipment,
            HashItemMinter minter,
            int capacity,
            int qualityVariancePercent,
            List<(int LowId, int HighId, int Quality)> into)
        {
            ArgumentNullException.ThrowIfNull(equipment);
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(into);

            Roll(ReadInputs(equipment), minter, capacity, qualityVariancePercent, into, null);
        }

        internal static IEnumerable<(string Hash, int Quality)> ReadInputs(IEnumerable<Item> equipment)
        {
            foreach (Item item in equipment)
            {
                foreach (ItemSpell spell in SpawnFunctions(item))
                {
                    if (TryReadSpawn(spell, out string hash, out int quality))
                        yield return (hash, quality);
                }
            }
        }

        internal static void Roll(IEnumerable<(string Hash, int Quality)> inputs, HashItemMinter minter,
            int capacity, int variance, List<(int LowId, int HighId, int Quality)> into, Random? random)
        {
            int start = into.Count;
            foreach ((string hash, int quality) in inputs)
            {
                if (into.Count - start >= capacity)
                    return;
                RollSpawn(hash, quality, minter, variance, into, random);
            }
        }

        static void RollSpawn(string hash, int quality, HashItemMinter minter, int variance,
            List<(int LowId, int HighId, int Quality)> into, Random? random)
        {
            (int min, int max) = LootableDynel.QualityWindow(quality, variance);
            if (minter.TryRollHighestUpTo(hash, (random ?? Random.Shared).Next(min, max + 1),
                out int lowId, out int highId, out int rolledQuality, random))
            {
                into.Add((lowId, highId, rolledQuality));
            }
        }

        /// <summary>The OnWear SpawnItem functions of <paramref name="item"/>.</summary>
        public static IEnumerable<ItemSpell> SpawnFunctions(Item item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!item.SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? spells) || spells == null)
                yield break;

            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Is(FunctionType.SpawnItem))
                    yield return spells[i];
            }
        }

        /// <summary>Catalog shape: hash, max QL; the rest (min QL, TimeExist, Flags, Rel) does not change a drop.</summary>
        public static bool TryReadSpawn(ItemSpell spell, out string hash, out int quality)
        {
            ArgumentNullException.ThrowIfNull(spell);
            quality = 0;
            return spell.TryReadString(0, out hash) && hash.Length > 0
                && spell.TryReadInt(1, out quality) && quality > 0;
        }
    }
}
