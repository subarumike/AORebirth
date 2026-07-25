namespace ZoneEngine.Core
{
    using AORebirth.Enums;

    /// <summary>
    /// Capture-backed player secondary specials.
    /// FlingShot/Burst: 20260719-fling-burst (lock 6s / 17s).
    /// Brawl/Dimach: 20260724-001643 (SpecialUsed lock 15s / 1800s; SpecialAvailable for Brawl).
    /// </summary>
    internal static class PlayerSpecialAttackRules
    {
        internal const int FlingShotLockSeconds = 6;

        internal const int BurstLockSeconds = 17;

        internal const int BrawlLockSeconds = 15;

        // Capture SpecialUsed Parameter2=1800 for Dimach (30 minutes).
        internal const int DimachLockSeconds = 1800;

        // Burst fires multiple pellets — capture total damage ~1.5x a strong single hit.
        internal const int BurstHitCount = 3;

        // Capture 20260724-001643: Dimach SpecialAttackInfo Amount=4074 vs nearby auto ~1120 (~3.6x).
        internal const int DimachDamageScale = 4;

        internal static bool IsSupportedSpecial(int specialStatId)
        {
            return specialStatId == (int)StatIds.flingshot
                   || specialStatId == (int)StatIds.burst
                   || specialStatId == (int)StatIds.brawl
                   || specialStatId == (int)StatIds.dimach;
        }

        internal static int ResolveLockSeconds(int specialStatId)
        {
            if (specialStatId == (int)StatIds.burst)
            {
                return BurstLockSeconds;
            }

            if (specialStatId == (int)StatIds.brawl)
            {
                return BrawlLockSeconds;
            }

            if (specialStatId == (int)StatIds.dimach)
            {
                return DimachLockSeconds;
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

        internal static int ResolveDamageScale(int specialStatId)
        {
            if (specialStatId == (int)StatIds.dimach)
            {
                return DimachDamageScale;
            }

            return 1;
        }
    }
}
