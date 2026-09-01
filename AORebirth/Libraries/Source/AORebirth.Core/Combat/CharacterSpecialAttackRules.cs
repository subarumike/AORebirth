namespace AORebirth.Core.Combat
{
    using AORebirth.Enums;

    public static class CharacterSpecialAttackRules
    {
        public const int FlingShotLockSeconds = 6;

        public const int BurstLockSeconds = 17;

        public const int BrawlLockSeconds = 15;

        public const int DimachLockSeconds = 1800;

        public const int BurstHitCount = 3;

        public const int DimachDamageScale = 4;

        public static bool IsSupportedSpecial(int specialStatId)
        {
            return specialStatId == (int)StatIds.flingshot
                   || specialStatId == (int)StatIds.burst
                   || specialStatId == (int)StatIds.brawl
                   || specialStatId == (int)StatIds.dimach;
        }

        public static int ResolveLockSeconds(int specialStatId)
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

        public static int ResolveHitCount(int specialStatId)
        {
            if (specialStatId == (int)StatIds.burst)
            {
                return BurstHitCount;
            }

            return 1;
        }

        public static int ResolveDamageScale(int specialStatId)
        {
            if (specialStatId == (int)StatIds.dimach)
            {
                return DimachDamageScale;
            }

            return 1;
        }
    }
}
