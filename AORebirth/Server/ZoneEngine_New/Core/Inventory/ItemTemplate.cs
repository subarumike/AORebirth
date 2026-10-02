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
        /// The client's weapon type bitmask (Gamecode.dll 0x1009c8e1, weapon object +0x1F0), which feeds the
        /// EquippedWeapons / EquippedRHWeapon criteria stats: 0x02 when the weapon takes no ammo (melee), then one
        /// pair of bits per Attack skill carrying at least half the weight (ranged skills at 50%, melee and gun
        /// skills above 50%), e.g. 2H Blunt 0x102, Pistol 0x404.
        /// </summary>
        public int GetWeaponTypeFlags()
        {
            int flags = Stats.TryGetValue(CharacterStat.AmmoType, out int ammoType) && ammoType >= 1 ? 0 : 0x02;
            foreach ((CharacterStat skill, int percent) in Attack)
            {
                flags |= (int)skill switch
                {
                    111 when percent >= 50 => 0x0C,     // Bow
                    114 when percent >= 50 => 0x14,     // MG / SMG
                    133 when percent >= 50 => 0x4004,
                    134 when percent >= 50 => 0x04,
                    110 when percent >= 50 => 0x10004,  // Heavy weapons
                    109 when percent >= 50 => 0x8004,   // Grenade
                    103 when percent > 50 => 0x22,      // 1H Edged
                    102 when percent > 50 => 0x42,      // 1H Blunt
                    105 when percent > 50 => 0x82,      // 2H Edged
                    107 when percent > 50 => 0x102,     // 2H Blunt
                    104 when percent > 50 => 0x4002,    // Melee energy
                    106 when percent > 50 => 0x202,     // Piercing
                    112 when percent > 50 => 0x404,     // Pistol
                    116 when percent > 50 => 0x804,     // Assault rifle
                    113 when percent > 50 => 0x1004,    // Rifle
                    115 when percent > 50 => 0x2004,    // Shotgun
                    _ => 0
                };
            }

            return flags;
        }

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
        /// <param name="resolve">
        /// Optional character context for leaves a stat value cannot answer (HasPerk, IsPerkLocked,
        /// HasNotRunningNano): returns the leaf's result, or null to compare the stat as usual.
        /// </param>
        public bool MeetsActionRequirements(Func<CharacterStat, int> getStat, ActionType actionType,
            Func<ItemRequirement, bool?>? resolve = null)
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

            return action == null || MeetsRequirements(action.Requirements, getStat, resolve);
        }

        /// <summary>
        /// Stat checks of <paramref name="actionType"/> that fail. Empty when the action passes or is
        /// missing; also empty when the expression fails without any single failing check.
        /// </summary>
        public IReadOnlyList<ItemRequirement> UnmetActionRequirements(Func<CharacterStat, int> getStat, ActionType actionType)
        {
            ArgumentNullException.ThrowIfNull(getStat);

            ItemAction? action = Actions.Find(candidate => candidate.ActionType == (int)actionType);
            if (action == null || MeetsRequirements(action.Requirements, getStat))
                return [];

            var unmet = new List<ItemRequirement>();
            foreach (ItemRequirement requirement in action.Requirements)
            {
                if (!IsRequirementLinkOperator(requirement) && !EvaluateLeaf(requirement, getStat))
                    unmet.Add(requirement);
            }

            return unmet;
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
            Func<CharacterStat, int> getStat,
            Func<ItemRequirement, bool?>? resolve = null)
        {
            ArgumentNullException.ThrowIfNull(requirements);
            ArgumentNullException.ThrowIfNull(getStat);

            int count = requirements.Count;
            if (count == 0)
                return true;

            if (TryEvaluatePostfix(requirements, getStat, resolve, out bool expression))
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

                bool pass = EvaluateLeaf(requirement, getStat, resolve);

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
            Func<CharacterStat, int> getStat, Func<ItemRequirement, bool?>? resolve, out bool result)
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
                    values.Push(EvaluateLeaf(requirement, getStat, resolve));
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

            if (eventType is EventType.OnUse or EventType.OnUseItemOn
                && TryFindActiveSkillLock(eventType, target, source, DateTime.UtcNow, out int lockedStat, out TimeSpan remaining))
            {
                (source ?? target).SendSkillLocked(lockedStat, remaining);
                return false;
            }

            criteria ??= new SpellCriteria();
            bool executed = false;
            foreach (ItemSpell spell in spells)
                executed |= ExecuteSpell(target, source, spell, inventoryRepository, items, skipPassiveModifiers, criteria);

            return executed;
        }

        /// <summary>
        /// A LockSkill in <paramref name="eventType"/> whose stat is still locked on the character it
        /// would lock. Such a use is refused outright: none of its functions run.
        /// </summary>
        public bool TryFindActiveSkillLock(
            EventType eventType,
            Character target,
            Character? source,
            DateTime nowUtc,
            out int statId,
            out TimeSpan remaining)
        {
            ArgumentNullException.ThrowIfNull(target);

            statId = 0;
            remaining = TimeSpan.Zero;
            if (!SpellList.TryGetValue(eventType, out List<ItemSpell>? spells))
                return false;

            foreach (ItemSpell spell in spells)
            {
                if (!spell.Is(FunctionType.LockSkill) || !ItemUseFunctions.TryReadSkillLock(spell, out int stat, out _))
                    continue;

                TimeSpan left = ItemUseFunctions.ResolveApplyOn(target, source, spell).SkillLocks.Remaining(stat, nowUtc);
                if (left <= TimeSpan.Zero)
                    continue;

                statId = stat;
                remaining = left;
                return true;
            }

            return false;
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
            // Each function names who it applies to (User / Wearer / Self: whoever used the item or cast the
            // nano; Target: the event target). A nano cast on someone else can still act on its caster, e.g. a
            // pet summon. Its requirements are checked against that same character.
            target = ItemUseFunctions.ResolveApplyOn(target, source, spell);
            if (!spell.MeetsRequirements(stat => criteria.Resolve(stat, id => target.Stats.Get(id))))
                return false;

            if (spell.Is(FunctionType.Modify) || spell.Is(FunctionType.ScalingModify)
                || spell.Is(FunctionType.ModifyPercentage) || spell.Is(FunctionType.MonsterShape))
            {
                if (skipPassiveModifiers)
                    return true;

                StatModifierSpells.Apply([spell], target.Stats);
                return true;
            }

            if ((spell.Is(FunctionType.SetFlag) || spell.Is(FunctionType.ChangeActionRestriction)
                    || spell.Is(FunctionType.ChangeVariable))
                && skipPassiveModifiers)
                return true;

            return ItemUseFunctions.TryExecute(Id, target, source, spell, inventoryRepository, items, criteria,
                templateDamageType: Stats.TryGetValue(CharacterStat.DamageType, out int damageType) ? damageType : 0);
        }

        /// <summary>
        /// One requirement leaf against the character. A VisualProfession requirement also passes on the
        /// real Profession, so a disguise (False Profession) never locks a character out of its own nanos.
        /// </summary>
        public static bool EvaluateLeaf(ItemRequirement requirement, Func<CharacterStat, int> getStat,
            Func<ItemRequirement, bool?>? resolve = null)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            ArgumentNullException.ThrowIfNull(getStat);

            if (resolve?.Invoke(requirement) is bool resolved)
                return resolved;

            var stat = (CharacterStat)requirement.StatNumber;
            if (EvaluateRequirement(getStat(stat), requirement))
                return true;

            return stat == CharacterStat.VisualProfession
                && EvaluateRequirement(getStat(CharacterStat.Profession), requirement);
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
                Operator.TestNumPets => Pets.PetTypes.TestNumPets(statValue, required),
                Operator.True => true,
                Operator.False => false,
                // And/Or/Not and other non-comparison ops are requirement links, not checks.
                _ => true
            };
        }
    }
}
