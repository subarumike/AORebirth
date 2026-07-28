namespace ZoneEngine.Core.Missions
{
    internal static class MissionRollFeeRules
    {
        internal static int FeeForLevel(int characterLevel)
        {
            return characterLevel < 1 ? 1 : characterLevel;
        }

        internal static bool TryCalculateCharge(
            int characterLevel,
            int normalizedCashBefore,
            out int fee,
            out int cashAfter)
        {
            fee = FeeForLevel(characterLevel);
            cashAfter = normalizedCashBefore;
            if (normalizedCashBefore < fee)
            {
                return false;
            }

            cashAfter = normalizedCashBefore - fee;
            return true;
        }
    }
}
