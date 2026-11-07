namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// What an NPC's equipment drops: every OnWear SpawnItem on an equipped item (a boss's "Lootgiver" and Boss
    /// Modifier items) spawns its hash. The function's QL, which the item's own QL (the NPC's level) scales, is the
    /// centre for Gameplay.json loot.qualityVariancePercent. Outdoor Rubi-Ka bosses with modifier equipment share
    /// the requested QL across scaling pools with the same centre. Each pool still resolves its own highest eligible QL.
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
            => Roll(equipment, minter, capacity, qualityVariancePercent, into, false);

        internal static void Roll(
            IEnumerable<Item> equipment,
            HashItemMinter minter,
            int capacity,
            int qualityVariancePercent,
            List<(int LowId, int HighId, int Quality)> into,
            bool shareScalingQuality)
            => Roll(equipment, minter, capacity, qualityVariancePercent, into, shareScalingQuality, Random.Shared);

        internal static void Roll(
            IEnumerable<Item> equipment,
            HashItemMinter minter,
            int capacity,
            int qualityVariancePercent,
            List<(int LowId, int HighId, int Quality)> into,
            bool shareScalingQuality,
            Random random)
        {
            ArgumentNullException.ThrowIfNull(equipment);
            ArgumentNullException.ThrowIfNull(minter);
            ArgumentNullException.ThrowIfNull(into);

            int start = into.Count;
            Dictionary<int, int>? sharedByCentre = shareScalingQuality ? new() : null;
            foreach (Item item in equipment)
            {
                foreach (ItemSpell spell in SpawnFunctions(item))
                {
                    if (into.Count - start >= capacity)
                        return;

                    if (!TryReadSpawn(spell, out string hash, out int quality))
                        continue;

                    IReadOnlyList<EquipmentLootMember>? reconstructed = sharedByCentre != null
                        ? minter.GetEquipmentLootMembers(hash) : null;
                    int requestedQuality;
                    if (sharedByCentre != null && (reconstructed == null
                        ? minter.HasScalingFamily(hash) : minter.HasScalingFamily(reconstructed)))
                    {
                        if (!sharedByCentre.TryGetValue(quality, out requestedQuality))
                        {
                            requestedQuality = RollQuality(quality, qualityVariancePercent, random);
                            sharedByCentre.Add(quality, requestedQuality);
                        }
                    }
                    else
                    {
                        requestedQuality = RollQuality(quality, qualityVariancePercent, random);
                    }

                    bool rolled = reconstructed == null
                        ? minter.TryRollHighestUpTo(hash, requestedQuality, out int lowId, out int highId, out int rolledQuality)
                        : minter.TryRollEquipmentLoot(reconstructed, quality, requestedQuality, random, out lowId, out highId, out rolledQuality);
                    if (rolled)
                        into.Add((lowId, highId, rolledQuality));
                }
            }
        }

        static int RollQuality(int centre, int variance, Random random)
        {
            (int min, int max) = LootableDynel.QualityWindow(centre, variance);
            return random.Next(min, max + 1);
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
