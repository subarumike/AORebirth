namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;

    public enum BuffApplyDecision
    {
        /// <summary>Nothing in NCU conflicts; add the buff.</summary>
        Apply,

        /// <summary>An existing entry must leave first (recast or weaker same-strain nano).</summary>
        Replace,

        /// <summary>The nano has no duration, so it never enters NCU.</summary>
        RefusedNotABuff,

        /// <summary>A same-strain nano with a higher stacking order is already up.</summary>
        RefusedStrainStronger,

        /// <summary>The target does not have enough free NCU.</summary>
        RefusedNotEnoughNcu,
    }

    /// <summary>
    /// Strain, stacking and NCU decision for landing one nano on one target. Pure: it reads the
    /// current NCU list and answers what should happen, and never mutates anything.
    /// </summary>
    public static class BuffApplyRules
    {
        /// <summary>
        /// Decides whether <paramref name="spell"/> can land. <paramref name="maxNcu"/> of 0 or less
        /// means unlimited (NPCs carry no NCU stat). Hostile nanos never consume the target's NCU.
        /// </summary>
        public static BuffApplyDecision Evaluate(
            NanoSpell spell,
            IReadOnlyList<Buff> active,
            int maxNcu,
            out Buff? replaced)
        {
            ArgumentNullException.ThrowIfNull(spell);
            ArgumentNullException.ThrowIfNull(active);

            replaced = null;
            if (!spell.IsBuff)
                return BuffApplyDecision.RefusedNotABuff;

            Buff? sameNano = null;
            Buff? strainConflict = null;
            int usedNcu = 0;
            for (int i = 0; i < active.Count; i++)
            {
                Buff buff = active[i];
                if (!buff.IsHostile)
                    usedNcu += buff.NcuCost;

                // A recast refreshes itself and never loses the stacking comparison.
                if (buff.Id == spell.Id)
                {
                    sameNano = buff;
                    continue;
                }

                if (spell.NanoStrain > 0 && buff.NanoStrain == spell.NanoStrain)
                {
                    if (buff.StackingOrder > spell.StackingOrder)
                        return BuffApplyDecision.RefusedStrainStronger;

                    strainConflict ??= buff;
                }
            }

            replaced = sameNano ?? strainConflict;

            if (!spell.IsHostile && maxNcu > 0)
            {
                int freed = replaced != null && !replaced.IsHostile ? replaced.NcuCost : 0;
                if (usedNcu - freed + spell.NcuCost > maxNcu)
                    return BuffApplyDecision.RefusedNotEnoughNcu;
            }

            return replaced == null ? BuffApplyDecision.Apply : BuffApplyDecision.Replace;
        }
    }
}
