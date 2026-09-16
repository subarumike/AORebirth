namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Validates the complete supported OnUse graph before mutation. Deliberately does not guess
    /// hostile resistance, periodic scheduling, area recipients, pet ownership or visual morphs.
    /// </summary>
    internal sealed class NanoEffectPlan
    {
        public Dictionary<Player, Dictionary<CharacterStat, int>> BaseWrites { get; } = new();
        public Dictionary<CharacterStat, int> Modifiers { get; } = new();

        internal static bool Requirements(Player caster, Player target, IEnumerable<ItemRequirement> requirements)
            => TryRequirements(caster, target, requirements, out bool met) && met;

        private static bool TryRequirements(Player caster, Player target, IEnumerable<ItemRequirement> requirements, out bool met)
        {
            met = true;
            foreach (ItemRequirement requirement in requirements)
            {
                if (requirement.ChildOperator != 0 || (Operator)requirement.Operator is not
                    (Operator.EqualTo or Operator.GreaterThan or Operator.LessThan or Operator.BitAnd
                        or Operator.NotBitAnd or Operator.Unequal or Operator.True or Operator.False)) return false;
                Player? subject = Resolve(caster, target, requirement.Target);
                if (subject == null) return false;
                met &= ItemTemplate.EvaluateRequirement(subject.Stats.Get((CharacterStat)requirement.StatNumber), requirement);
            }
            return true;
        }

        internal static bool ActionRequirements(Player caster, Player target, NanoDefinition nano)
        {
            foreach (ItemAction action in nano.Template.Actions)
                if (action.ActionType == (int)ActionType.ToUse && !NanoRequirements.Action(caster, target, action.Requirements))
                    return false;
            return true;
        }

        internal static bool TryBuild(Player caster, Player target, NanoDefinition nano,
            Func<int, int, int> next, bool restoring, out NanoEffectPlan plan,
            IReadOnlyDictionary<CharacterStat, int>? previousSameNanoModifiers = null)
        {
            plan = new NanoEffectPlan();
            if (!restoring) return false; // New casts require a separate clean implementation.
            // Saved hostile effects require their own durable-state contract.
            if (nano.Template.Defend.Count != 0 || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells)
                || spells.Count == 0) return false;
            try
            {
                foreach (ItemSpell spell in spells)
                {
                    if (spell.TickCount < 0 || spell.TickCount > 1 || spell.TickInterval != 0) return false;
                    Player? subject = Resolve(caster, target, spell.Target);
                    if (subject == null) return false;
                    // Persisted rows have no historical conditional-branch or caster identity columns.
                    // Do not reconstruct a conditional buff by reevaluating unrelated login stats.
                    if (nano.DurationCentiseconds > 0 && spell.Requirements.Count != 0) return false;
                    if (!TryRequirements(caster, target, spell.Requirements, out bool met)) return false;
                    if (!met) continue;
                    if (!ItemUseFunctions.TryReadInt(spell.Arguments, 0, out int rawStat)
                        || !ItemUseFunctions.TryReadInt(spell.Arguments, 1, out int amount)) return false;
                    CharacterStat stat = (CharacterStat)rawStat;
                    switch ((FunctionType)spell.FunctionType)
                    {
                        case FunctionType.Modify:
                        case FunctionType.ScalingModify:
                            if (nano.DurationCentiseconds <= 0 || !ReferenceEquals(subject, target)
                                || stat == CharacterStat.Cash || amount == int.MinValue) return false;
                            plan.Modifiers[stat] = checked(plan.Modifiers.GetValueOrDefault(stat) + amount);
                            break;
                        case FunctionType.Hit:
                            if (stat is not (CharacterStat.Health or CharacterStat.CurrentNano)) return false;
                            int maximum = amount;
                            if (spell.Arguments.Count >= 3 && !ItemUseFunctions.TryReadInt(spell.Arguments, 2, out maximum))
                                return false;
                            if (amount < 0 || maximum < 0 || amount == int.MaxValue || maximum == int.MaxValue)
                                return false; // damaging Hit belongs to the damage/resist authority
                            // Saved-state hydration never replays an instantaneous effect.
                            break;
                        default: return false;
                    }
                }
            }
            catch (OverflowException) { return false; }
            return true;
        }

        private static Player? Resolve(Player caster, Player target, int rawTarget) => (ItemTarget)rawTarget switch
        {
            ItemTarget.User or ItemTarget.Self or ItemTarget.Wearer => caster,
            ItemTarget.Target or ItemTarget.Selectedtarget => target,
            _ => null
        };
    }
}
