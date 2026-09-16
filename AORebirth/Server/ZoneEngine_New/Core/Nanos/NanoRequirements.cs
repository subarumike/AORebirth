namespace ZoneEngine_New.Core.Nanos
{
    using System.Collections.Generic;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Scalar event predicates used to decode saved effects. Unsupported operations reject.
    /// New player action admission remains unavailable.
    /// </summary>
    internal static class NanoRequirements
    {
        // Player cast admission is unavailable until its independent rule contract is implemented.
        public static bool Action(Player caster, Player target, IEnumerable<ItemRequirement> requirements) => false;

        public static bool TryEvent(Character caster, Character target, IReadOnlyList<ItemRequirement> requirements, out bool met)
        {
            met = requirements.Count == 0 || requirements[0].ChildOperator != (int)Operator.Or;
            foreach (ItemRequirement requirement in requirements)
            {
                if (requirement.ChildOperator is not (0 or (int)Operator.And or (int)Operator.Or
                    or (int)Operator.Unknown) || !TryOne(caster, target, requirement, out bool value)) return false;
                if (requirement.ChildOperator == (int)Operator.Or) met |= value;
                else met &= value;
            }
            return true;
        }

        private static bool TryOne(Character caster, Character target, ItemRequirement requirement, out bool met)
        {
            met = false;
            Operator op = (Operator)requirement.Operator;
            if (requirement.StatNumber == 0 && op is Operator.And or Operator.Or or Operator.Not)
            { met = true; return true; } // Requirement.IsRequirementLinkOperator
            Character? subject = (ItemTarget)requirement.Target switch
            {
                ItemTarget.User or ItemTarget.Self or ItemTarget.Wearer => caster,
                ItemTarget.Target or ItemTarget.Selectedtarget => target,
                // Existing scalar nano requirements also encode caster as 100. Legacy
                // RequirementLambdaCreator.GetTarget resolves that encoding to self.
                (ItemTarget)100 => caster,
                _ => null
            };
            if (subject == null) return false;
            int actual = subject.Stats.Get((CharacterStat)requirement.StatNumber);
            int expected = requirement.Value;
            switch (op)
            {
                case Operator.EqualTo: met = actual == expected; break;
                case Operator.GreaterThan: met = actual > expected; break;
                case Operator.LessThan: met = actual < expected; break;
                case Operator.BitAnd: met = (actual & expected) > 0; break;
                case Operator.BitOr: met = (actual | expected) != 0; break;
                case Operator.NotBitAnd: met = (actual & expected) == 0; break;
                case Operator.Unequal:
                case Operator.Not: met = actual != expected; break;
                case Operator.True: met = true; break;
                case Operator.False: met = false; break;
                default: return false;
            }
            return true;
        }
    }
}
