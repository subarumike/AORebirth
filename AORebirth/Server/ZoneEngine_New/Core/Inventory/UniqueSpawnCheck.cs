namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;

    /// <summary>
    /// Unique items an OnUse SpawnItem would hand a player who already holds one (Engineer pet nanos and the Warbot
    /// Shell, 150773). When every item the hash can make is a Unique the player holds, the use or cast is refused up
    /// front, so nothing is spent; when only some outcomes are, the spawn itself gives no item for those
    /// (<see cref="ItemUseFunctions"/>).
    /// </summary>
    internal static class UniqueSpawnCheck
    {
        /// <summary>
        /// True, with the item's name, when <paramref name="template"/> has an OnUse SpawnItem whose every possible
        /// item is a Unique <paramref name="player"/> already holds.
        /// </summary>
        public static bool TryFindHeldUniqueOnly(
            ItemTemplate template,
            Player player,
            IGameData gameData,
            IItemTemplateCatalog catalog,
            out string itemName)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(catalog);
            itemName = string.Empty;
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            var leaves = new List<HashInstance>();
            foreach (ItemSpell spell in spells)
            {
                if (!spell.Is(FunctionType.SpawnItem) || !spell.TryReadString(0, out string hash) || string.IsNullOrWhiteSpace(hash))
                    continue;
                // Only the branches this player would actually run: the Izgimmer cyberdecks (253195-253214) carry one
                // SpawnItem per breed/gender, and the branch for the deck's own breed/gender spawns the deck itself.
                if (!ItemTemplate.MeetsRequirements(spell.Requirements, stat => player.Stats.Get(stat), player.ResolveRequirement))
                    continue;

                leaves.Clear();
                gameData.CollectHashLeafInstances(hash.Trim(), leaves);
                if (leaves.Count == 0)
                    continue;

                string? held = null;
                bool allHeldUnique = true;
                foreach (HashInstance leaf in leaves)
                {
                    foreach (int id in leaf.TemplateIds)
                    {
                        if (id <= 0)
                            continue;
                        if (!IsHeldUnique(player, catalog, id, out string name))
                        {
                            allHeldUnique = false;
                            break;
                        }

                        held ??= name;
                    }

                    if (!allHeldUnique)
                        break;
                }

                if (allHeldUnique && held != null)
                {
                    itemName = held;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Template <paramref name="templateId"/> is Unique and <paramref name="player"/> holds one of its family.</summary>
        static bool IsHeldUnique(Player player, IItemTemplateCatalog catalog, int templateId, out string name)
        {
            name = string.Empty;
            if (!catalog.TryGet(templateId, out ItemTemplate template) || (template.Flags & (int)ItemFlags.Unique) == 0)
                return false;

            name = string.IsNullOrEmpty(template.Name) ? templateId.ToString(System.Globalization.CultureInfo.InvariantCulture) : template.Name;
            return Trade.TradeRules.WouldDuplicateUnique(player, templateId, templateId);
        }
    }
}
