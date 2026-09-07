namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Shared item definition shape (catalog entry or builder-baked effective def).
    /// </summary>
    public sealed class ItemTemplate
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Quality { get; init; }

        public int Flags { get; init; }

        public int ItemType { get; init; }

        public int DynelType { get; init; }

        public int MultipleCount { get; init; }

        public Dictionary<CharacterStat, int> Stats { get; init; } = new();

        public Dictionary<CharacterStat, int> Attack { get; init; } = new();

        public Dictionary<CharacterStat, int> Defend { get; init; } = new();

        public Dictionary<EventType, List<ItemSpell>> SpellList { get; init; } = new();

        public List<ItemAction> Actions { get; init; } = new();

        public List<int> Relations { get; init; } = new();

        /// <summary>
        /// Combat style from InitiativeType; handedness from MultiMelee/MultiRanged presence.
        /// </summary>
        public WeaponFlags GetWeaponFlags()
        {
            if (!Stats.TryGetValue(CharacterStat.InitiativeType, out int initiativeType)
                || initiativeType <= 0)
                return WeaponFlags.None;

            WeaponFlags flags;
            if (initiativeType == (int)CharacterStat.MeleeInit)
                flags = WeaponFlags.Melee;
            else if (initiativeType == (int)CharacterStat.RangedInit)
                flags = WeaponFlags.Ranged;
            else if (initiativeType == (int)CharacterStat.PhysicalInit)
                flags = WeaponFlags.Unarmed;
            else
                return WeaponFlags.None;

            if (HasMultiHandStat())
                flags |= WeaponFlags.OneHanded;
            else
                flags |= WeaponFlags.TwoHanded;

            return flags;
        }

        bool HasMultiHandStat()
            => HasPresentStat(CharacterStat.MultiMelee) || HasPresentStat(CharacterStat.MultiRanged);

        bool HasPresentStat(CharacterStat stat)
        {
            if (Attack.TryGetValue(stat, out int attackValue) && attackValue != 0)
                return true;
            return Stats.TryGetValue(stat, out int statValue) && statValue != 0;
        }

        /// <summary>
        /// True when <paramref name="actionType"/> is missing, or every requirement on that action passes.
        /// </summary>
        public bool MeetsActionRequirements(Func<CharacterStat, int> getStat, ActionType actionType)
        {
            ArgumentNullException.ThrowIfNull(getStat);

            ItemAction? action = null;
            foreach (ItemAction candidate in Actions)
            {
                if (candidate.ActionType == (int)actionType)
                {
                    action = candidate;
                    break;
                }
            }

            if (action == null)
                return true;

            foreach (ItemRequirement requirement in action.Requirements)
            {
                if (!EvaluateRequirement(getStat((CharacterStat)requirement.StatNumber), requirement))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Runs every <see cref="EventType.OnUse"/> function on this template.
        /// Unimplemented functions are skipped and do not fail the use.
        /// </summary>
        public bool ExecuteOnUseSpells(
            Player player,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            if (!SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells.Count == 0)
                return false;

            foreach (ItemSpell spell in spells)
                ExecuteSpell(player, spell, inventoryRepository, items);

            return true;
        }

        bool ExecuteSpell(
            Player player,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
            => ItemUseFunctions.TryExecute(Id, player, spell, inventoryRepository, items);

        public static bool EvaluateRequirement(int statValue, ItemRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(requirement);

            int required = requirement.Value;
            return (Operator)requirement.Operator switch
            {
                Operator.EqualTo => statValue == required,
                Operator.GreaterThan => statValue > required,
                Operator.LessThan => statValue < required,
                Operator.BitAnd => (statValue & required) != 0,
                Operator.NotBitAnd => (statValue & required) == 0,
                _ => true
            };
        }
    }
}
