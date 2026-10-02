namespace ZoneEngine_New.Core.Inventory
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Applies passive stat and shape functions as stat bonuses.
    /// Shared by worn equipment and active nano buffs: both are declarative bonuses that
    /// a rebase recomputes from scratch, never incremental edits.
    /// </summary>
    public static class StatModifierSpells
    {
        /// <param name="overEquipLevel">The source item's over-equipped level (<see cref="Inventory.OverEquip"/>); it scales Modify only.</param>
        public static void Apply(IReadOnlyList<ItemSpell> spells, StatCollection stats, int overEquipLevel = 0)
        {
            if (spells == null || stats == null)
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                bool shape = spell.Is(FunctionType.MonsterShape);
                bool setFlag = spell.Is(FunctionType.SetFlag);
                bool changeVariable = spell.Is(FunctionType.ChangeVariable);
                bool percentage = spell.Is(FunctionType.ModifyPercentage);
                if (!shape && !setFlag && !changeVariable && !percentage
                    && !spell.Is(FunctionType.Modify) && !spell.Is(FunctionType.ScalingModify))
                    continue;
                if (!spell.MeetsRequirements(stats))
                    continue;
                if (shape)
                {
                    if (spell.TryReadInt(0, out int monsterData))
                        stats.Set(CharacterStat.MonsterData,
                            monsterData - stats.GetOrZero(CharacterStat.MonsterData, StatDetail.Base),
                            StatDetail.Bonus);
                    continue;
                }

                if (setFlag)
                {
                    ApplySetFlag(spell, stats);
                    continue;
                }

                if (changeVariable)
                {
                    ApplyChangeVariable(spell, stats);
                    continue;
                }

                if (percentage)
                {
                    ApplyModifyPercentage(spell, stats);
                    continue;
                }
                if (!TryReadModify(spell, out CharacterStat stat, out int delta))
                    continue;
                // Health is current hit points. Folding a worn Modify into the bonus
                // layer makes a hit write that bonus back as base, and the client adds it again.
                if (stat == CharacterStat.Cash || stat == CharacterStat.Health)
                    continue;

                if (spell.Is(FunctionType.Modify))
                    delta = Inventory.OverEquip.ScaleModifier(stat, delta, overEquipLevel);
                stats.AddBonus(stat, delta, dirty: true);
            }
        }

        static void ApplySetFlag(ItemSpell spell, StatCollection stats)
        {
            if (!spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int bitIndex)
                || bitIndex < 0 || bitIndex > 31)
                return;

            var stat = (CharacterStat)statId;
            int bit = 1 << bitIndex;
            int currentFull = stats.GetOrZero(stat);
            int @base = stats.GetOrZero(stat, StatDetail.Base);
            int desired = currentFull | bit;
            stats.Set(stat, desired - @base, StatDetail.Bonus);
        }

        /// <summary>
        /// ChangeVariable {stat, value}: the stat reads <c>value</c> while the source is active
        /// (e.g. False Profession sets VisualProfession). Held in the bonus layer so it drops with the buff.
        /// </summary>
        static void ApplyChangeVariable(ItemSpell spell, StatCollection stats)
        {
            if (!TryReadModify(spell, out CharacterStat stat, out int value))
                return;
            if (stat == CharacterStat.Cash || stat == CharacterStat.Health)
                return;

            stats.Set(stat, value - stats.GetOrZero(stat, StatDetail.Base), StatDetail.Bonus);
        }

        /// <summary>
        /// ModifyPercentage {stat, percent}: adds percent of the stat's base value as a bonus
        /// (e.g. False Profession: -25% to each nano skill). Based on base so the order of sources
        /// does not change the result; the fraction truncates toward zero.
        /// </summary>
        static void ApplyModifyPercentage(ItemSpell spell, StatCollection stats)
        {
            if (!TryReadModify(spell, out CharacterStat stat, out int percent) || percent == 0)
                return;
            if (stat == CharacterStat.Cash || stat == CharacterStat.Health)
                return;

            long delta = (long)stats.GetOrZero(stat, StatDetail.Base) * percent / 100;
            if (delta != 0)
                stats.AddBonus(stat, (int)delta, dirty: true);
        }

        /// <summary>Total flat Modify these functions add to <paramref name="stat"/>.</summary>
        public static int SumModify(IReadOnlyList<ItemSpell> spells, CharacterStat stat)
        {
            int total = 0;
            foreach (ItemSpell spell in spells)
            {
                if (TryReadModify(spell, out CharacterStat modified, out int delta) && modified == stat)
                    total += delta;
            }

            return total;
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
