namespace ZoneEngine.Core.Navigation
{
    internal static class NpcCombatLeashPolicy
    {
        internal const int SubwayPlayfieldResource = 127;

        // This is the first bounded private-server PF127 leash. It keeps the
        // observed open-door combat case inside the leash while preventing a
        // hostile NPC from being dragged across the full Subway playfield.
        internal const double SubwayMaximumDistanceFromHome = 100.0;

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
            if (!Applies(playfieldResource, isPlayerOwnedPet))
            {
                return false;
            }

            if (!home.IsFinite || !npc.IsFinite || !target.IsFinite)
            {
                return true;
            }

            return home.Distance2D(npc) > SubwayMaximumDistanceFromHome
                   || home.Distance2D(target) > SubwayMaximumDistanceFromHome;
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
