#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace LoginEngine.CharacterCreation
{
    #region Usings ...

    using System;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Sets level-1 current HP/NP from the same breed/profession formulas used by ZoneEngine StatLife and StatMaxNanoEnergy.
    /// Only persists current vitals (stats 27 and 214); max vitals are computed at runtime from abilities.
    /// </summary>
    internal static class StarterVitalStats
    {
        private const int CharacterStatType = 50000;
        private const int BodyDevelopmentBase = 5;
        private const int NanoEnergyPoolBase = 5;

        public static void Apply(int characterId, int breed, int profession, int[] abis)
        {
            if (abis == null || abis.Length < 6)
            {
                return;
            }

            int strength = abis[0];
            int psychic = abis[1];
            int sense = abis[2];
            int intelligence = abis[3];
            int stamina = abis[4];
            int agility = abis[5];

            int bodyDevelopment = BodyDevelopmentBase + CalculateBodyDevelopmentTrickle(stamina);
            int nanoEnergyPool = NanoEnergyPoolBase + CalculateNanoEnergyPoolTrickle(sense, intelligence, agility, psychic);
            int maxHealth = CalculateMaxHealth(breed, profession, bodyDevelopment);
            int maxNano = CalculateMaxNano(breed, profession, nanoEnergyPool);

            UpsertStat(characterId, 27, maxHealth);
            UpsertStat(characterId, 214, maxNano);
        }

        private static int CalculateBodyDevelopmentTrickle(int stamina)
        {
            return (int)Math.Floor(stamina / 4.0);
        }

        private static int CalculateNanoEnergyPoolTrickle(int sense, int intelligence, int agility, int psychic)
        {
            double trickle =
                (0.1 * sense) + (0.1 * intelligence) + (0.1 * agility) + (0.7 * psychic);
            return (int)Math.Floor(trickle / 4.0);
        }

        private static int CalculateMaxHealth(int breed, int profession, int bodyDevelopment)
        {
            int[,] tableProfessionHitPoints =
            {
                { 6, 6, 6, 6, 6, 6, 6, 6, 7, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5 },
                { 7, 7, 6, 7, 7, 7, 6, 7, 8, 6, 6, 6, 7, 7, 5, 5, 5, 5, 5 },
                { 8, 7, 6, 7, 7, 8, 7, 7, 9, 6, 6, 6, 8, 7, 5, 5, 5, 5, 5 },
                { 9, 8, 6, 8, 8, 8, 7, 7, 10, 6, 6, 6, 9, 8, 5, 5, 5, 5, 5 },
                { 10, 9, 6, 9, 8, 9, 8, 8, 11, 6, 6, 6, 10, 9, 5, 5, 5, 5, 5 },
                { 11, 12, 6, 10, 9, 9, 9, 9, 12, 6, 6, 6, 11, 10, 5, 5, 5, 5, 5 },
                { 12, 13, 7, 11, 10, 10, 10, 10, 13, 7, 7, 7, 12, 11, 5, 5, 5, 5, 5 },
            };

            int[] breedBaseHitPoints = { 10, 15, 10, 25, 30, 30, 30 };
            int[] breedMultiplicatorHitPoints = { 3, 3, 2, 4, 8, 8, 10 };
            int[] breedModificatorHitPoints = { 0, -1, -1, 0, 0, 0, 0 };

            int safeBreed = ClampIndex(breed, breedBaseHitPoints.Length);
            int safeProfession = ClampIndex(profession, tableProfessionHitPoints.GetLength(1));
            const int titleLevel = 1;
            const int level = 1;

            return breedBaseHitPoints[safeBreed - 1]
                   + (level
                      * (tableProfessionHitPoints[titleLevel - 1, safeProfession - 1]
                         + breedModificatorHitPoints[safeBreed - 1]))
                   + (bodyDevelopment * breedMultiplicatorHitPoints[safeBreed - 1]);
        }

        private static int CalculateMaxNano(int breed, int profession, int nanoEnergyPool)
        {
            int[,] tableProfessionNanoPoints =
            {
                { 4, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 4 },
                { 4, 4, 5, 4, 5, 5, 5, 5, 4, 5, 5, 5, 4, 4 },
                { 4, 4, 6, 4, 6, 5, 5, 5, 4, 6, 6, 6, 4, 4 },
                { 4, 4, 7, 4, 6, 6, 5, 5, 4, 7, 7, 7, 4, 4 },
                { 4, 4, 8, 4, 7, 6, 6, 6, 4, 8, 8, 8, 4, 4 },
                { 4, 4, 9, 4, 7, 7, 7, 7, 4, 10, 10, 10, 4, 5 },
                { 5, 5, 10, 5, 8, 8, 8, 8, 5, 11, 11, 11, 5, 7 },
            };

            int[] breedBaseNanoPoints = { 10, 10, 15, 8, 10, 10, 10 };
            int[] breedMultiplicatorNanoPoints = { 3, 3, 4, 2, 3, 3, 3 };
            int[] breedModificatorNanoPoints = { 0, -1, 1, -2, 0, 0, 0 };

            int safeBreed = ClampIndex(breed, breedBaseNanoPoints.Length);
            int tableProfession = profession;
            if (tableProfession > 13)
            {
                tableProfession--;
            }

            tableProfession = ClampIndex(tableProfession, tableProfessionNanoPoints.GetLength(1));
            const int titleLevel = 1;
            const int level = 1;

            return breedBaseNanoPoints[safeBreed - 1]
                   + (level
                      * (tableProfessionNanoPoints[titleLevel - 1, tableProfession - 1]
                         + breedModificatorNanoPoints[safeBreed - 1]))
                   + (nanoEnergyPool * breedMultiplicatorNanoPoints[safeBreed - 1]);
        }

        private static int ClampIndex(int value, int max)
        {
            if (value < 1)
            {
                return 1;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static void UpsertStat(int characterId, int statId, int statValue)
        {
            var stat = new DBStats
                       {
                           Type = CharacterStatType,
                           Instance = characterId,
                           StatId = statId,
                           StatValue = statValue
                       };

            DBStats existing = StatDao.Instance.GetById(stat.Type, stat.Instance, stat.StatId);
            if (existing.Id != 0)
            {
                if (existing.StatValue == stat.StatValue)
                {
                    return;
                }

                existing.StatValue = stat.StatValue;
                StatDao.Instance.Save(existing);
                return;
            }

            StatDao.Instance.Add(stat);
        }
    }
}
