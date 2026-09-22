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
        public static void Apply(IReadOnlyList<ItemSpell> spells, StatCollection stats)
        {
            if (spells == null || stats == null)
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                bool shape = spell.Is(FunctionType.MonsterShape);
                bool setFlag = spell.Is(FunctionType.SetFlag);
                if (!shape && !setFlag && !spell.Is(FunctionType.Modify) && !spell.Is(FunctionType.ScalingModify))
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
                if (!TryReadModify(spell, out CharacterStat stat, out int delta))
                    continue;
                if (stat == CharacterStat.Cash)
                    continue;

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
