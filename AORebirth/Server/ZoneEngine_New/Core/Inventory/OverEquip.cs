namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Over-equipped (OE) items, as the client computes them (Gamecode.dll):
    /// <list type="bullet">
    /// <item>Only armor page items (ToWear) and the right / left hand weapons (ToWield) can be OE
    /// (FUN_10047a90 / FUN_1004d003); implants, social, HUD, utility and deck slots never are.</item>
    /// <item>Level (FUN_10081e10): for every GreaterThan row of that requirement, r = (float)skill / value with the
    /// full current skill; when r &lt; 1, level = trunc(5 * (1 - r)). The worst row wins, clamped to 0-4.</item>
    /// <item>Penalty (FUN_10047a73 / FUN_1009aa10): value - trunc(value * level * 0.25), i.e. 25% per level.</item>
    /// <item>Armor: the penalty hits its OnWear Modify functions on the client's penalised-stat table (FUN_10062c0c),
    /// re-judged for armor slots 0x10-0x1F only (FUN_1004d003 -> FUN_1004b624).</item>
    /// <item>Hand weapons: the penalty hits only MinDamage, MaxDamage and DamageBonus (FUN_1009b337, from the same
    /// re-judge). Their modifiers keep full strength: the client's modifier re-apply is never run for slots 6 / 8
    /// (FUN_1004d1ce, the only path that would, has no caller).</item>
    /// </list>
    /// </summary>
    internal static class OverEquip
    {
        /// <summary>The client's penalised-stat table, filled by FUN_10062c0c (byte flags at 0x102e40d0).</summary>
        static readonly HashSet<int> PenalisedStats =
        [
            1, // MaxHealth
            90, 91, 92, 93, 94, 95, 96, 97, // ACs
            118, 119, 120, 149, // Melee / Ranged / Physical / Nano init
            168, // NanoAC (Nano Resist)
            205, 206, 207, 208, 216, 217, 219, 225, // Reflect ACs
            226, 227, 228, 229, 230, 231, 233, 234, // Shield ACs
            276, 277, // AMS / DMS modifier (Add All Offense / Defense)
            278, 279, 280, 281, 282, 311, 316, 317, // Damage modifiers
            318, // NPCostModifier
            341, // XPBonus
            379, // CriticalIncrease
            380, // WeaponRange
            382, // SkillLockModifier
            383, // InterruptModifier
            393, // ResistModifier
        ];

        /// <summary>
        /// OE level 0-4 for <paramref name="item"/> in <paramref name="placement"/> of <paramref name="page"/>, judged on
        /// <paramref name="stats"/>.
        /// </summary>
        public static int ComputeLevel(Item item, IdentityType page, int placement, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(stats);

            ActionType action;
            if (page == IdentityType.ArmorPage)
                action = ActionType.ToWear;
            else if (page == IdentityType.WeaponPage
                && placement is (int)WeaponSlots.Righthand or (int)WeaponSlots.LeftHand)
                action = ActionType.ToWield;
            else
                return 0;

            ItemAction? requirement = item.Definition?.Actions.Find(candidate => candidate.ActionType == (int)action);
            return requirement == null ? 0 : ComputeLevel(requirement.Requirements, stats);
        }

        /// <summary>
        /// OE level 0-4 of a requirement against <paramref name="stats"/>: the worst GreaterThan row's
        /// trunc(5 * (1 - skill / value)) while skill / value &lt; 1. Pets use their summoning nano's or item's ToUse
        /// rows against their owner (OE from level 1: a skill under 80% of the requirement).
        /// </summary>
        public static int ComputeLevel(IReadOnlyList<ItemRequirement> rows, StatCollection stats)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(stats);

            int level = 0;
            foreach (ItemRequirement row in rows)
            {
                if ((Operator)row.Operator != Operator.GreaterThan || row.Value == 0)
                    continue;

                float ratio = (float)StatCollection.Normalize(stats.Get((CharacterStat)row.StatNumber)) / row.Value;
                if (!(ratio < 1.0f))
                    continue;

                level = Math.Max(level, (int)((10.0 - 10.0 * ratio) * 0.5));
            }

            return Math.Clamp(level, 0, 4);
        }

        /// <summary>
        /// The OE level <paramref name="item"/>'s modifiers are penalised at: <paramref name="level"/> for armor, 0 for a
        /// wielded weapon (a ToWield requirement), whose OE only cuts its damage (<see cref="ScaleDamage"/>).
        /// </summary>
        public static int ModifierLevel(Item item, int level)
            => level > 0 && item.Definition?.Actions.Exists(action => action.ActionType == (int)ActionType.ToWield) == true
                ? 0
                : level;

        /// <summary>An OnWear Modify of an OE armor item: penalised stats lose 25% per level, truncated toward zero.</summary>
        public static int ScaleModifier(CharacterStat stat, int delta, int level)
            => level <= 0 || !PenalisedStats.Contains((int)stat) ? delta : Scale(delta, level);

        /// <summary>A hand weapon's MinDamage / MaxDamage / DamageBonus at OE <paramref name="level"/>.</summary>
        public static int ScaleDamage(int value, int level) => level <= 0 ? value : Scale(value, level);

        static int Scale(int value, int level) => value - (int)((double)value * Math.Min(level, 4) * 0.25);
    }
}
