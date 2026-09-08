namespace ZoneEngine_New.Core.Helpers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Player Life / MaxHealth. Same <c>beforeModifiers</c> table as the official Life formula.
    /// NPCs (<c>NPCFamily</c> &gt; 0) keep stored Life. Result is the full max, not trickle.
    /// </summary>
    public static class MaxHealthCalculator
    {
        static readonly int[,] ProfessionHitPoints =
        {
            { 6, 6, 6, 6, 6, 6, 6, 6, 7, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5 },
            { 7, 7, 6, 7, 7, 7, 6, 7, 8, 6, 6, 6, 7, 7, 5, 5, 5, 5, 5 },
            { 8, 7, 6, 7, 7, 8, 7, 7, 9, 6, 6, 6, 8, 7, 5, 5, 5, 5, 5 },
            { 9, 8, 6, 8, 8, 8, 7, 7, 10, 6, 6, 6, 9, 8, 5, 5, 5, 5, 5 },
            { 10, 9, 6, 9, 8, 9, 8, 8, 11, 6, 6, 6, 10, 9, 5, 5, 5, 5, 5 },
            { 11, 12, 6, 10, 9, 9, 9, 9, 12, 6, 6, 6, 11, 10, 5, 5, 5, 5, 5 },
            { 12, 13, 7, 11, 10, 10, 10, 10, 13, 7, 7, 7, 12, 11, 5, 5, 5, 5, 5 },
        };

        static readonly int[] BreedBaseHitPoints = { 10, 15, 10, 25, 30, 30, 30 };
        static readonly int[] BreedMultiplicatorHitPoints = { 3, 3, 2, 4, 8, 8, 10, 8, 8, 8, 8, 8, 8 };
        static readonly int[] BreedModificatorHitPoints = { 0, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public static bool TryCompute(StatCollection stats, out int maxHealth)
        {
            if (stats.GetOrZero(CharacterStat.NPCFamily) > 0)
            {
                maxHealth = 0;
                return false;
            }

            maxHealth = Compute(
                Math.Max(1, stats.GetOrZero(CharacterStat.Breed)),
                Math.Max(1, stats.GetOrZero(CharacterStat.Profession)),
                Math.Max(1, stats.GetOrZero(CharacterStat.TitleLevel)),
                Math.Max(1, stats.GetOrZero(CharacterStat.Level)),
                Math.Max(1, stats.GetOrZero(CharacterStat.BodyDevelopment)));
            return maxHealth > 0;
        }

        public static int Compute(int breed, int profession, int titleLevel, int level, int bodyDevelopment)
        {
            int safeBreed = ClampIndex(breed, BreedBaseHitPoints.Length);
            int safeProfession = ClampIndex(profession, ProfessionHitPoints.GetLength(1));
            int safeTitle = ClampIndex(titleLevel, ProfessionHitPoints.GetLength(0));
            int safeLevel = Math.Max(1, level);
            int safeBody = Math.Max(1, bodyDevelopment);
            int breedIndex = ClampIndex(safeBreed, BreedMultiplicatorHitPoints.Length) - 1;

            return BreedBaseHitPoints[safeBreed - 1]
                   + (safeLevel
                      * (ProfessionHitPoints[safeTitle - 1, safeProfession - 1]
                         + BreedModificatorHitPoints[breedIndex]))
                   + (safeBody * BreedMultiplicatorHitPoints[breedIndex]);
        }

        static int ClampIndex(int value, int max)
        {
            if (value < 1)
                return 1;
            if (value > max)
                return max;
            return value;
        }
    }
}
