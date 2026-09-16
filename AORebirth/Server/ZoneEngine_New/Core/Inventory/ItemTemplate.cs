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
        /// True when <paramref name="actionType"/> is missing, or the action's requirement
        /// expression passes (legacy Events fold: leaf compares + And/Or/Not links).
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

            return action == null || MeetsRequirements(action.Requirements, getStat);
        }

        /// <summary>
        /// Legacy requirement expression fold (Events / Criteria):
        /// <list type="bullet">
        /// <item><see cref="IsRequirementLinkOperator"/> rows are structural And/Or/Not markers;
        /// <see cref="Operator.Not"/> inverts the accumulated result.</item>
        /// <item>Leaf rows are compared via <see cref="EvaluateRequirement"/> and combined with
        /// <see cref="ItemRequirement.ChildOperator"/> (<see cref="Operator.Or"/> or And).</item>
        /// </list>
        /// </summary>
        public static bool MeetsRequirements(
            IReadOnlyList<ItemRequirement> requirements,
            Func<CharacterStat, int> getStat)
        {
            ArgumentNullException.ThrowIfNull(requirements);
            ArgumentNullException.ThrowIfNull(getStat);

            int count = requirements.Count;
            if (count == 0)
                return true;

            bool result = true;
            bool hasReal = false;
            for (int i = 0; i < count; i++)
            {
                ItemRequirement requirement = requirements[i];

                if (IsRequirementLinkOperator(requirement))
                {
                    if (hasReal && (Operator)requirement.Operator == Operator.Not)
                        result = !result;
                    continue;
                }

                bool pass = EvaluateRequirement(
                    getStat((CharacterStat)requirement.StatNumber),
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

        /// <summary>
        /// Stat=0 And/Or/Not rows are expression-tree link operators, not Flags checks.
        /// </summary>
        public static bool IsRequirementLinkOperator(ItemRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(requirement);

            if (requirement.StatNumber != 0)
                return false;

            return (Operator)requirement.Operator is Operator.And or Operator.Or or Operator.Not;
        }

        /// <summary>
        /// Runs every <see cref="EventType.OnUse"/> function on this template.
        /// Reject unsupported functions before applying any part of a compound use.
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
                    return true;

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
                Operator.Unequal => statValue != required,
                Operator.True => true,
                Operator.False => false,
                // And/Or/Not and other non-comparison ops are requirement links, not checks.
                _ => true
            };
        }
    }
}
