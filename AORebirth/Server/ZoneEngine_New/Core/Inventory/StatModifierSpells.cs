namespace ZoneEngine_New.Core.Inventory
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Applies <see cref="FunctionType.Modify"/> spell functions as stat bonuses.
    /// Shared by worn equipment and active nano buffs: both are declarative bonuses that
    /// a rebase recomputes from scratch, never incremental edits.
    /// </summary>
    public static class StatModifierSpells
    {
        public static void Apply(IReadOnlyList<ItemSpell> spells, StatCollection stats)
        {
            if (spells == null || stats == null)
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                if (!spell.Is(FunctionType.Modify) && !spell.Is(FunctionType.ScalingModify))
                    continue;
                if (!spell.MeetsRequirements(stats))
                    continue;
                if (!TryReadModify(spell, out CharacterStat stat, out int delta))
                    continue;
                if (stat == CharacterStat.Cash)
                    continue;

                stats.AddBonus(stat, delta, dirty: true);
            }
        }

        static bool TryReadModify(ItemSpell spell, out CharacterStat stat, out int delta)
        {
            stat = default;
            delta = 0;
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out delta))
                return false;

            stat = (CharacterStat)statId;
            return true;
        }
    }
}
