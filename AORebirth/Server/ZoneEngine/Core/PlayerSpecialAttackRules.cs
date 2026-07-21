namespace ZoneEngine.Core
{
    using AORebirth.Enums;

    /// <summary>
    /// Capture-backed player secondary specials (20260719-fling-burst).
    /// FlingShot SpecialUsed lock=6s; Burst SpecialUsed lock=17s; then SpecialAvailable.
    /// </summary>
    internal static class PlayerSpecialAttackRules
    {
        internal const int FlingShotLockSeconds = 6;

        internal const int BurstLockSeconds = 17;

        // Burst fires multiple pellets — capture total damage ~1.5x a strong single hit.
        internal const int BurstHitCount = 3;

        internal static bool IsSupportedSpecial(int specialStatId)
        {
            return specialStatId == (int)StatIds.flingshot || specialStatId == (int)StatIds.burst;
        }

        internal static int ResolveLockSeconds(int specialStatId)
        {
            if (specialStatId == (int)StatIds.burst)
            {
                return BurstLockSeconds;
            }

            return FlingShotLockSeconds;
        }

        internal static int ResolveHitCount(int specialStatId)
        {
            if (specialStatId == (int)StatIds.burst)
            {
                return BurstHitCount;
            }

            return 1;
        }
    }
}
