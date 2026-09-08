namespace ZoneEngine_New.Core.Nanos
{
    using System.Collections.Generic;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// The two existing AORebirth.Core evaluators are deliberately different: Actions enforce
    /// And-linked criteria; Events fold Or links, otherwise And. This is their supported scalar
    /// subset, not an invented general AO expression grammar. Unknown enforced operations fail.
    /// </summary>
    internal static class NanoRequirements
    {
        public static bool Action(Player caster, Player target, IEnumerable<ItemRequirement> requirements)
        {
            foreach (ItemRequirement requirement in requirements)
            {
                // Zero-link criteria were already enforced by New; retain that stronger boundary.
                if (requirement.ChildOperator is 0 or (int)Operator.And)
                {
                    if (!TryOne(caster, target, requirement, out bool met) || !met) return false;
                }
                else if (requirement.ChildOperator is not ((int)Operator.Or or (int)Operator.HasRunningNanoLine
                    or (int)Operator.Unknown)) return false;
                // The known non-And links above are ignored by the accepted Legacy Action path.
            }
            return true;
        }

        public static bool TryEvent(Player caster, Player target, IReadOnlyList<ItemRequirement> requirements, out bool met)
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

        private static bool TryOne(Player caster, Player target, ItemRequirement requirement, out bool met)
        {
            met = false;
            Operator op = (Operator)requirement.Operator;
            if (requirement.StatNumber == 0 && op is Operator.And or Operator.Or or Operator.Not)
            { met = true; return true; } // Requirement.IsRequirementLinkOperator
            Player? subject = (ItemTarget)requirement.Target switch
            {
                ItemTarget.User or ItemTarget.Self or ItemTarget.Wearer => caster,
                ItemTarget.Target or ItemTarget.Selectedtarget => target,
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
                // Exact existing RequirementLambdaCreator behavior. Flight-state admission is
                // separately fenced by explicit server IsVehicle and current playfield authority.
                case Operator.FlyingAllowed: met = true; break;
                default: return false;
            }
            return true;
        }
    }
}
