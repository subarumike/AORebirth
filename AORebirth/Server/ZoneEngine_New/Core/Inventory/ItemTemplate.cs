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
        /// <item><see cref="IsRequirementLinkOperator"/> rows (And/Or/Not) are structural markers;
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

            if (TryEvaluatePostfix(requirements, getStat, out bool expression))
                return expression;

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

        // AODB exports Criteria as postfix leaves and link operators. Older data can instead
        // carry ChildOperator on leaves; retain that representation's existing fold above.
        static bool TryEvaluatePostfix(IReadOnlyList<ItemRequirement> requirements,
            Func<CharacterStat, int> getStat, out bool result)
        {
            result = false;
            bool hasLeaf = false;
            bool hasLink = false;
            foreach (ItemRequirement requirement in requirements)
            {
                if (IsRequirementLinkOperator(requirement))
                    hasLink = true;
                else
                {
                    if (requirement.ChildOperator != 0)
                        return false;
                    hasLeaf = true;
                }
            }
            // A link-only placeholder and the older leaf fold are not postfix expressions.
            if (!hasLeaf || !hasLink)
                return false;

            var values = new Stack<bool>();
            foreach (ItemRequirement requirement in requirements)
            {
                if (!IsRequirementLinkOperator(requirement))
                {
                    values.Push(EvaluateRequirement(getStat((CharacterStat)requirement.StatNumber), requirement));
                    continue;
                }

                if (values.Count == 0)
                    return true;
                bool right = values.Pop();
                if ((Operator)requirement.Operator == Operator.Not)
                    values.Push(!right);
                else
                {
                    if (values.Count == 0)
                        return true;
                    bool left = values.Pop();
                    values.Push((Operator)requirement.Operator == Operator.Or ? left || right : left && right);
                }
            }

            if (values.Count == 1)
                result = values.Pop();
            return true;
        }

        /// <summary>
        /// And/Or/Not rows are expression-tree link operators, not stat checks.
        /// Authored criteria sometimes leave a compared stat on the link (item 222955's
        /// OnUseItemOn Or nodes carry stat 273); the operator is what makes it a link.
        /// </summary>
        public static bool IsRequirementLinkOperator(ItemRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(requirement);

            return (Operator)requirement.Operator is Operator.And or Operator.Or or Operator.Not;
        }

        /// <summary>
        /// Runs every <see cref="EventType.OnUse"/> function on this template.
        /// An empty OnUse list is a successful no-op; only present but unhandled functions fail.
        /// </summary>
        /// <param name="skipPassiveModifiers">
        /// When true, Modify, ScalingModify, MonsterShape, and SetFlag are skipped because NCU
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
            Character? source = null,
            SpellCriteria? criteria = null)
            => ExecuteSpells(
                EventType.OnUse,
                target,
                inventoryRepository,
                items,
                criteria,
                skipPassiveModifiers,
                source);

        /// <summary>
        /// Runs every function on <paramref name="eventType"/>. An empty OnUse list is a successful
        /// no-op; any other empty list does nothing. Criteria see <paramref name="criteria"/>
        /// (random roll, item used on the target) and otherwise the character's stats.
        /// </summary>
        public bool ExecuteSpells(
            EventType eventType,
            Character target,
            IInventoryRepository inventoryRepository,
            IItemBuilder items,
            SpellCriteria? criteria = null,
            bool skipPassiveModifiers = false,
            Character? source = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(items);

            if (!SpellList.TryGetValue(eventType, out List<ItemSpell>? spells) || spells.Count == 0)
                return eventType == EventType.OnUse;

            criteria ??= new SpellCriteria();
            bool executed = false;
            foreach (ItemSpell spell in spells)
                executed |= ExecuteSpell(target, source, spell, inventoryRepository, items, skipPassiveModifiers, criteria);

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

            return ExecuteSpells(
                EventType.OnTerminate,
                target,
                inventoryRepository,
                items);
        }

        bool ExecuteSpell(
            Character target,
            Character? source,
            ItemSpell spell,
            IInventoryRepository inventoryRepository,
            IItemBuilder items,
            bool skipPassiveModifiers,
            SpellCriteria criteria)
        {
            if (!spell.MeetsRequirements(stat => criteria.Resolve(stat, id => target.Stats.Get(id))))
                return false;

            if (spell.Is(FunctionType.Modify) || spell.Is(FunctionType.ScalingModify)
                || spell.Is(FunctionType.MonsterShape))
            {
                if (skipPassiveModifiers)
                    return true;

                StatModifierSpells.Apply([spell], target.Stats);
                return true;
            }

            if ((spell.Is(FunctionType.SetFlag) || spell.Is(FunctionType.ChangeActionRestriction))
                && skipPassiveModifiers)
                return true;

            return ItemUseFunctions.TryExecute(Id, target, source, spell, inventoryRepository, items, criteria);
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
