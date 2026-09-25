namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// Bits from <see cref="FunctionType.ChangeActionRestriction"/>.
    /// 214879 sets <see cref="PvPEnabled"/>; 202732 sets <see cref="PvPEnabled_Tower"/>.
    /// </summary>
    [Flags]
    public enum ActionRestrictionFlags
    {
        None = 0,

        PvPEnabled = 1,

        PvPEnabled_Tower = 67108864,
    }

    /// <summary>Whether one character may engage another.</summary>
    public static class CombatRules
    {
        const ActionRestrictionFlags PlayerAttackable =
            ActionRestrictionFlags.PvPEnabled | ActionRestrictionFlags.PvPEnabled_Tower;

        public static bool CanAttack(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (attacker.IsDead || target.IsDead || ReferenceEquals(attacker, target))
                return false;

            if (IsInRestrictedGas(target))
                return false;

            if (target is Player player)
            {
                // Mobs can still hit players. Only another player needs the PvP flags.
                if (!attacker.IsPlayer)
                    return true;

                return (player.ActionRestrictionFlags & PlayerAttackable) != 0;
            }

            return target is not NpcCharacter npc || npc.Attackable;
        }

        /// <summary>
        /// True when the only reason <see cref="CanAttack"/> fails is that the target player is not PvP-flagged.
        /// </summary>
        public static bool IsPvpAttackBlocked(Character attacker, Character target)
        {
            ArgumentNullException.ThrowIfNull(attacker);
            ArgumentNullException.ThrowIfNull(target);

            if (!attacker.IsPlayer || attacker.IsDead || target.IsDead || ReferenceEquals(attacker, target))
                return false;
            if (IsInRestrictedGas(target))
                return false;

            return target is Player player && (player.ActionRestrictionFlags & PlayerAttackable) == 0;
        }

        /// <summary>
        /// Restricted gas refuses fighting. Volumes are not wired yet, so no target is in gas.
        /// </summary>
        public static bool IsInRestrictedGas(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return false;
        }

        /// <summary>
        /// Flags granted by active buffs. Arg1 of 0 means the bits last as long as the buff.
        /// </summary>
        public static ActionRestrictionFlags CollectActionRestrictions(IReadOnlyList<Buff> buffs, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(buffs);
            ArgumentNullException.ThrowIfNull(stats);

            ActionRestrictionFlags flags = ActionRestrictionFlags.None;
            for (int i = 0; i < buffs.Count; i++)
            {
                IReadOnlyList<ItemSpell> spells = buffs[i].ModifierSpells;
                for (int spellIndex = 0; spellIndex < spells.Count; spellIndex++)
                {
                    ItemSpell spell = spells[spellIndex];
                    if (!spell.Is(FunctionType.ChangeActionRestriction))
                        continue;
                    if (!spell.MeetsRequirements(stats))
                        continue;
                    if (!spell.TryReadInt(0, out int bits))
                        continue;
                    if (spell.TryReadInt(1, out int mode) && mode != 0)
                        continue;

                    flags |= (ActionRestrictionFlags)bits;
                }
            }

            return flags;
        }
    }
}
