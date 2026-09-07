namespace ZoneEngine_New.Core.Helpers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Player MaxNanoEnergy. Same <c>beforeModifiers</c> table as the official nano formula.
    /// NPCs (<c>NPCFamily</c> &gt; 0) keep stored max nano. Result is the full max, not trickle.
    /// Profession 14+ is shifted down one column (legacy Shade gap).
    /// </summary>
    public static class MaxNanoCalculator
    {
        static readonly int[,] ProfessionNanoPoints =
        {
            { 4, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
            { 4, 4, 5, 4, 5, 5, 5, 5, 4, 5, 5, 5, 4, 4 },
            { 4, 4, 6, 4, 6, 5, 5, 5, 4, 6, 6, 6, 4, 4 },
            { 4, 4, 7, 4, 6, 6, 5, 5, 4, 7, 7, 7, 4, 4 },
            { 4, 4, 8, 4, 7, 6, 6, 6, 4, 8, 8, 8, 4, 4 },
            { 4, 4, 9, 4, 7, 7, 7, 7, 4, 10, 10, 10, 4, 5 },
            { 5, 5, 10, 5, 8, 8, 8, 8, 5, 11, 11, 11, 5, 7 },
        };

        static readonly int[] BreedBaseNanoPoints = { 10, 10, 15, 8, 10, 10, 10 };
        static readonly int[] BreedMultiplicatorNanoPoints = { 3, 3, 4, 2, 3, 3, 3 };
        static readonly int[] BreedModificatorNanoPoints = { 0, -1, 1, -2, 0, 0, 0 };

        public static bool TryCompute(StatCollection stats, out int maxNano)
        {
            if (stats.GetOrZero(CharacterStat.NPCFamily) > 0)
            {
                maxNano = 0;
                return false;
            }

            maxNano = Compute(
                Math.Max(1, stats.GetOrZero(CharacterStat.Breed)),
                Math.Max(1, stats.GetOrZero(CharacterStat.Profession)),
                Math.Max(1, stats.GetOrZero(CharacterStat.TitleLevel)),
                Math.Max(1, stats.GetOrZero(CharacterStat.Level)),
                Math.Max(1, stats.GetOrZero(CharacterStat.NanoPool)));
            return maxNano > 0;
        }

        public static int Compute(int breed, int profession, int titleLevel, int level, int nanoEnergyPool)
        {
            int tableProfession = profession;
            if (tableProfession > 13)
                tableProfession--;

            int safeBreed = ClampIndex(breed, BreedBaseNanoPoints.Length);
            int safeProfession = ClampIndex(tableProfession, ProfessionNanoPoints.GetLength(1));
            int safeTitle = ClampIndex(titleLevel, ProfessionNanoPoints.GetLength(0));
            int safeLevel = Math.Max(1, level);
            int safePool = Math.Max(1, nanoEnergyPool);

            return BreedBaseNanoPoints[safeBreed - 1]
                   + (safeLevel
                      * (ProfessionNanoPoints[safeTitle - 1, safeProfession - 1]
                         + BreedModificatorNanoPoints[safeBreed - 1]))
                   + (safePool * BreedMultiplicatorNanoPoints[safeBreed - 1]);
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
