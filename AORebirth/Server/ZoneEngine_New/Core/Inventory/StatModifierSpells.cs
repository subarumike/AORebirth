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
                FunctionType function = (FunctionType)spell.FunctionType;
                if (function != FunctionType.Modify && function != FunctionType.ScalingModify)
                    continue;
                if (!MeetsRequirements(spell, stats))
                    continue;
                if (!TryReadModify(spell, out CharacterStat stat, out int delta))
                    continue;
                if (stat == CharacterStat.Cash)
                    continue;

                stats.AddBonus(stat, delta, dirty: true);
            }
        }

        public static bool MeetsRequirements(ItemSpell spell, StatCollection stats)
        {
            bool result = true;
            bool hasReal = false;
            for (int i = 0; i < spell.Requirements.Count; i++)
            {
                ItemRequirement requirement = spell.Requirements[i];

                // Dynels.dat Criteria use Stat=0 rows as structural And/Or/Not markers.
                if (requirement.StatNumber == 0)
                {
                    if (hasReal && (Operator)requirement.Operator == Operator.Not)
                        result = !result;
                    continue;
                }

                bool pass = ItemTemplate.EvaluateRequirement(
                    stats.Get((CharacterStat)requirement.StatNumber),
                    requirement);

                if (!hasReal)
                {
                    result = pass;
                    hasReal = true;
                    continue;
                }

                if ((Operator)requirement.ChildOperator == Operator.Or)
                    result |= pass;
                else
                    result &= pass;
            }

            return !hasReal || result;
        }

        static bool TryReadModify(ItemSpell spell, out CharacterStat stat, out int delta)
        {
            stat = default;
            delta = 0;
            if (spell.Arguments.Count < 2)
                return false;
            if (!TryGetInt(spell.Arguments[0], out int statId) || !TryGetInt(spell.Arguments[1], out delta))
                return false;

            stat = (CharacterStat)statId;
            return true;
        }

        static bool TryGetInt(object? value, out int result)
        {
            switch (value)
            {
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = (int)l;
                    return true;
                case uint u:
                    result = (int)u;
                    return true;
                case short s:
                    result = s;
                    return true;
                case byte b:
                    result = b;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }
    }
}
