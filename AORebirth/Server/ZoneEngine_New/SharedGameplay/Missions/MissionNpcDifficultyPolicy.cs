namespace ZoneEngine.Core.Missions
{
    #region Usings

    using System;

    #endregion

    /// <summary>
    /// Existing generated-mission QL scaling policy shared by legacy and bound ACG interiors.
    /// Captured NPC appearance remains immutable evidence; accepted mission QL owns live level
    /// and health so a low-QL mission cannot inherit the source capture's higher difficulty.
    /// </summary>
    internal static class MissionNpcDifficultyPolicy
    {
        internal static int ResolveLevel(int missionQuality, Random rng)
        {
            int baseQuality = missionQuality > 0 ? missionQuality : 1;
            int delta = rng != null ? rng.Next(-2, 3) : 0;
            int level = baseQuality + delta;
            if (level < 1)
            {
                level = 1;
            }

            if (level > 220)
            {
                level = 220;
            }

            return level;
        }

        internal static int ResolveHealth(int level, Random rng)
        {
            int boundedLevel = level > 0 ? level : 1;
            int health = boundedLevel * 25;
            if (rng != null)
            {
                int jitter = rng.Next(-10, 11);
                health = health + ((health * jitter) / 100);
            }

            if (health < 50)
            {
                health = 50;
            }

            if (health > 40000)
            {
                health = 40000;
            }

            return health;
        }
    }
}
