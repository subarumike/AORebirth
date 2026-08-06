namespace ZoneEngine.Core.Navigation
{
    internal static class NpcCombatLeashPolicy
    {
        internal const int SubwayPlayfieldResource = 127;

        // PF127 keeps its accepted private-server default, while captured
        // encounters may provide a narrower NPC travel limit. Target distance
        // remains a separate safety boundary so a fleeing target does not make
        // an encounter reset before the NPC reaches its captured travel limit.
        internal const double SubwayDefaultMaximumNpcDistanceFromHome = 100.0;

        internal const double SubwayMaximumTargetDistanceFromHome = 100.0;

        internal const double ReturnCompletionDistance = 0.75;

        internal const double ReturnNavigationStopDistance = 0.25;

        internal static bool Applies(int playfieldResource, bool isPlayerOwnedPet)
        {
            return playfieldResource == SubwayPlayfieldResource && !isPlayerOwnedPet;
        }

        internal static bool ShouldResetCombat(
            int playfieldResource,
            bool isPlayerOwnedPet,
            ChaseNavigationPoint home,
            ChaseNavigationPoint npc,
            ChaseNavigationPoint target)
        {
            return ShouldResetCombat(
                playfieldResource,
                isPlayerOwnedPet,
                home,
                npc,
                target,
                SubwayDefaultMaximumNpcDistanceFromHome);
        }

        internal static bool ShouldResetCombat(
            int playfieldResource,
            bool isPlayerOwnedPet,
            ChaseNavigationPoint home,
            ChaseNavigationPoint npc,
            ChaseNavigationPoint target,
            double maximumNpcDistanceFromHome)
        {
            if (isPlayerOwnedPet)
            {
                return false;
            }

            // PF127 always leashes. Explicit short custom leashes (e.g. Arete
            // Robotic Guard Dog) also enforce on any playfield.
            bool subwayApplies = playfieldResource == SubwayPlayfieldResource;
            bool customShortLeash =
                maximumNpcDistanceFromHome > 0.0
                && maximumNpcDistanceFromHome
                   < SubwayDefaultMaximumNpcDistanceFromHome
                && !double.IsNaN(maximumNpcDistanceFromHome)
                && !double.IsInfinity(maximumNpcDistanceFromHome);
            if (!subwayApplies && !customShortLeash)
            {
                return false;
            }

            if (!home.IsFinite
                || !npc.IsFinite
                || !target.IsFinite
                || double.IsNaN(maximumNpcDistanceFromHome)
                || double.IsInfinity(maximumNpcDistanceFromHome)
                || maximumNpcDistanceFromHome <= 0.0)
            {
                return true;
            }

            // Subway keeps the separate 100m target boundary (captured short
            // NPC travel limits still use that target safety).
            // Short custom leashes use the same home radius for target so a
            // fleeing player cannot keep a guard dog in combat across the zone.
            double maximumTargetDistanceFromHome = subwayApplies
                                                       ? SubwayMaximumTargetDistanceFromHome
                                                       : maximumNpcDistanceFromHome;

            return home.Distance2D(npc) > maximumNpcDistanceFromHome
                   || home.Distance2D(target) > maximumTargetDistanceFromHome;
        }

        internal static bool HasReturnedHome(
            ChaseNavigationPoint home,
            ChaseNavigationPoint npc)
        {
            return home.IsFinite
                   && npc.IsFinite
                   && home.Distance2D(npc) <= ReturnCompletionDistance;
        }
    }
}
