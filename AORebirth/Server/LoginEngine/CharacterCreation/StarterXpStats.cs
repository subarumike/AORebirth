#region License



// Copyright (c) 2005-2014, CellAO Team

// All rights reserved.



#endregion



namespace LoginEngine.CharacterCreation

{

    #region Usings ...



    using AORebirth.Database.Dao;

    using AORebirth.Database.Entities;



    #endregion



    /// <summary>

    /// Seeds RK XP bar stats for new characters so login and combat persistence start from a known baseline.

    /// </summary>

    internal static class StarterXpStats

    {

        private const int CharacterStatType = 50000;

        private const int Level1NextXp = 1450;



        public static void Apply(int characterId)

        {

            UpsertStat(characterId, 52, 0);

            UpsertStat(characterId, 350, Level1NextXp);

            UpsertStat(characterId, 372, 0);

            UpsertStat(characterId, 592, 0);

            UpsertStat(characterId, 334, 0);

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


