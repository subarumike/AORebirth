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
    /// Derived views (<see cref="Nanos.NanoSpell"/>) reinterpret the same stat map.
    /// </summary>
    public class ItemTemplate
    {
        public ItemTemplate()
        {
        }

        /// <summary>
        /// Reinterpretation copy for derived views. Collections are shared, not cloned:
        /// catalog templates are read-only after load.
        /// </summary>
        protected ItemTemplate(ItemTemplate other)
        {
            ArgumentNullException.ThrowIfNull(other);

            Id = other.Id;
            Name = other.Name;
            Quality = other.Quality;
            Flags = other.Flags;
            ItemType = other.ItemType;
            DynelType = other.DynelType;
            MultipleCount = other.MultipleCount;
            Stats = other.Stats;
            Attack = other.Attack;
            Defend = other.Defend;
            SpellList = other.SpellList;
            Actions = other.Actions;
            Relations = other.Relations;
            IsBuff = other.IsBuff;
            CanCancel = other.CanCancel;
        }

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
        /// True when this definition takes an NCU slot for a while instead of firing once.
        /// <see cref="ItemFlags"/> already uses all 32 bits, so buff-ness lives here.
        /// </summary>
        public bool IsBuff { get; init; }

        /// <summary>False when the owner may not dismiss the effect from NCU.</summary>
        public bool CanCancel { get; init; } = true;

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
        /// Runs every <see cref="EventType.OnUse"/> function on this template. Returns true when at
        /// least one function did something, which is what decides whether the use spends a charge:
        /// an item whose effect never happened must not be consumed.
        /// </summary>
        /// <param name="skipPassiveModifiers">
        /// When true, <see cref="FunctionType.Modify"/> and ScalingModify are skipped because NCU
        /// rebase already applies them from active buffs.
        /// </param>
        /// <param name="source">
        /// Optional source for damage attribution (nano caster). Defaults to <paramref name="target"/>.
        /// </param>
        public bool ExecuteOnUseSpells(
            Character target,
            IInventoryRepository inventoryRepository,
            IItemBuilder items,
            bool skipPassiveModifiers = false,
            Character? source = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            if (!SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells) || spells.Count == 0)
                return false;

            bool executed = false;
            foreach (ItemSpell spell in spells)
                executed |= ExecuteSpell(target, source, spell, inventoryRepository, items, skipPassiveModifiers);

            return executed;
        }

        /// <summary>Runs OnTerminate functions when a timed nano leaves NCU.</summary>
        public bool ExecuteTerminateSpells(
            Character target,
            IInventoryRepository inventoryRepository,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            if (!SpellList.TryGetValue(EventType.OnTerminate, out List<ItemSpell>? spells) || spells.Count == 0)
                return false;

            bool executed = false;
            foreach (ItemSpell spell in spells)
                executed |= ExecuteSpell(target, source: null, spell, inventoryRepository, items, skipPassiveModifiers: false);

            return executed;
        }

        /// <summary>
        /// Clears SetFlag bits applied by this template's OnUse handlers when the nano leaves NCU.
        /// </summary>
        public void ReverseOnUseSetFlags(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);

            if (!SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                if ((FunctionType)spell.FunctionType != FunctionType.SetFlag
                    || spell.Arguments.Count < 2
                    || !TryGetIntArgument(spell.Arguments[0], out int statId)
                    || !TryGetIntArgument(spell.Arguments[1], out int bitIndex)
                    || bitIndex < 0
                    || bitIndex > 31)
                    continue;

                var stat = (CharacterStat)statId;
                int current = target.Stats.GetOrZero(stat, StatDetail.Base);
                target.Stats.Set(stat, current & ~(1 << bitIndex), StatDetail.Base, dirty: true);
            }
        }

        bool ExecuteSpell(
            Character target,
            Character? source,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items,
            bool skipPassiveModifiers)
        {
            if (!StatModifierSpells.MeetsRequirements(spell, target.Stats))
                return false;

            FunctionType function = (FunctionType)spell.FunctionType;
            if (function is FunctionType.Modify or FunctionType.ScalingModify)
            {
                if (skipPassiveModifiers)
                    return false;

                StatModifierSpells.Apply([spell], target.Stats);
                return true;
            }

            return ItemUseFunctions.TryExecute(Id, target, source, spell, inventoryRepository, items);
        }

        static bool TryGetIntArgument(object? value, out int result)
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
