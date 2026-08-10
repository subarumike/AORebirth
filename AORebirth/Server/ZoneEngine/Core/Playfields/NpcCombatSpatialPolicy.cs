namespace ZoneEngine.Core.Playfields
{
    using System;

    internal static class NpcCombatSpatialPolicy
    {
        // NpcChaseNavigationRuntimeService treats a direct destination within
        // 0.3 metres of its requested stop distance as arrived.  Keep the
        // requested destination inside the attack envelope so that arrival can
        // never strand a melee actor just outside its certified reach.
        internal const double NavigationArrivalTolerance = 0.3;

        private const double LegacyMeleeFollowHoldDistance = 3.0;

        internal static bool IsWithinAttackEnvelope(double distance, double attackRange)
        {
            return IsFiniteNonNegative(distance)
                   && IsFinitePositive(attackRange)
                   && distance <= attackRange;
        }

        internal static double BuildPursuitStopDistance(double attackRange)
        {
            if (!IsFinitePositive(attackRange))
            {
                return 0.0;
            }

            double desiredStopDistance =
                attackRange > NpcCombatAttackRules.MaxMeleeCombatDistance
                    ? attackRange
                    : Math.Min(attackRange, LegacyMeleeFollowHoldDistance);
            return Math.Max(0.0, desiredStopDistance - NavigationArrivalTolerance);
        }

        internal static bool ShouldHoldMeleeFollow(double distance, double attackRange)
        {
            double holdDistance = IsFinitePositive(attackRange)
                                      ? Math.Min(
                                          attackRange,
                                          LegacyMeleeFollowHoldDistance)
                                      : 0.0;
            return holdDistance > 0.0
                   && IsFiniteNonNegative(distance)
                   && distance <= holdDistance;
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0.0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return value >= 0.0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
