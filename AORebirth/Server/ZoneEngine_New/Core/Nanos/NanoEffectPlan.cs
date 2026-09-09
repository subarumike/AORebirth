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
            if (nano.Id == 300439) return BucketheadNanoSpecialization.ActionRequirements(caster, target, nano);
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
            // No player NanoResist resolution is proven by the existing generic Legacy path.
            if (nano.Template.Defend.Count != 0 || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells)
                || spells.Count == 0) return false;
            try
            {
                bool previousRemoved = false;
                if (!restoring)
                    plan.BaseWrites[caster] = new() { [CharacterStat.CurrentNano] =
                        checked(caster.Stats.GetOrZero(CharacterStat.CurrentNano, StatDetail.Base) - nano.NanoCost) };
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
                            // Legacy RecordModifier reverses this nano's previous contributions on
                            // the first Modify, after the new delta; subsequent Hits see that state.
                            previousRemoved = true;
                            break;
                        case FunctionType.Hit:
                            if (stat is not (CharacterStat.Health or CharacterStat.CurrentNano)) return false;
                            int maximum = amount;
                            if (spell.Arguments.Count >= 3 && !ItemUseFunctions.TryReadInt(spell.Arguments, 2, out maximum))
                                return false;
                            if (amount < 0 || maximum < 0 || amount == int.MaxValue || maximum == int.MaxValue)
                                return false; // damaging Hit belongs to the damage/resist authority
                            // Restore the contribution, never replay an instant heal on login.
                            if (restoring) break;
                            if (maximum < amount) (amount, maximum) = (maximum, amount);
                            int delta = amount == maximum ? amount : next(amount, maximum + 1);
                            if (delta < amount || delta > maximum) throw new InvalidOperationException("Nano random result outside declared range.");
                            if (!plan.BaseWrites.TryGetValue(subject, out var writes))
                                plan.BaseWrites[subject] = writes = new();
                            int current = writes.GetValueOrDefault(stat, subject.Stats.GetOrZero(stat, StatDetail.Base));
                            CharacterStat maximumStat = stat == CharacterStat.Health ? CharacterStat.MaxHealth : CharacterStat.MaxNanoEnergy;
                            int projectedMaximum = subject.Stats.GetOrZero(maximumStat);
                            int projectedBonus = subject.Stats.GetOrZero(stat, StatDetail.Bonus);
                            if (ReferenceEquals(subject, target))
                            {
                                projectedMaximum = checked(projectedMaximum + plan.Modifiers.GetValueOrDefault(maximumStat)
                                    - (previousRemoved ? previousSameNanoModifiers?.GetValueOrDefault(maximumStat) ?? 0 : 0));
                                CharacterStat sourceStat = stat == CharacterStat.Health ? CharacterStat.BodyDevelopment : CharacterStat.NanoPool;
                                int sourceDelta = checked(plan.Modifiers.GetValueOrDefault(sourceStat)
                                    - (previousRemoved ? previousSameNanoModifiers?.GetValueOrDefault(sourceStat) ?? 0 : 0));
                                sourceDelta = checked(sourceDelta + NanoDerivedStats.SkillDelta(subject.Stats, sourceStat,
                                    plan.Modifiers, previousRemoved ? previousSameNanoModifiers : null));
                                projectedMaximum = checked(projectedMaximum + (stat == CharacterStat.Health
                                    ? NanoDerivedStats.HealthDelta(subject, sourceDelta) : NanoDerivedStats.NanoDelta(subject, sourceDelta)));
                                projectedBonus = checked(projectedBonus + plan.Modifiers.GetValueOrDefault(stat)
                                    - (previousRemoved ? previousSameNanoModifiers?.GetValueOrDefault(stat) ?? 0 : 0));
                            }
                            int cap = checked(projectedMaximum - projectedBonus);
                            writes[stat] = (int)Math.Min(Math.Max(current, cap), (long)current + delta);
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
