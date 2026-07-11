#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace LoginEngine.CharacterCreation
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Level-1 Shade starter inventory from capture 20260711-shade-starter-kit (FullCharacter slots 64-72).
    /// </summary>
    internal static class ShadeStarterLoadout
    {
        private struct StarterItemDefinition
        {
            public int Placement;
            public int LowId;
            public int HighId;
            public int Quality;
            public int Count;
        }

        private const int StarterLife = 38;
        private const int StarterHealth = 38;
        private const int StarterNano = 31;
        private const int StarterMaxNano = 31;
        private const int CharacterStatType = 50000;

        private static readonly StarterItemDefinition[] Items =
        {
            new StarterItemDefinition { Placement = 64, LowId = 291082, HighId = 291082, Quality = 1, Count = 50 },
            new StarterItemDefinition { Placement = 65, LowId = 291043, HighId = 291043, Quality = 1, Count = 25 },
            new StarterItemDefinition { Placement = 66, LowId = 252158, HighId = 252158, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 67, LowId = 292235, HighId = 292235, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 68, LowId = 296977, HighId = 296977, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 69, LowId = 287047, HighId = 287047, Quality = 25, Count = 1 },
            new StarterItemDefinition { Placement = 70, LowId = 218395, HighId = 218395, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 71, LowId = 211155, HighId = 211155, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 72, LowId = 218395, HighId = 218395, Quality = 1, Count = 1 },
        };

        public static void Apply(int characterId)
        {
            ApplyStarterStats(characterId);
            ApplyStarterItems(characterId);
        }

        private static void ApplyStarterStats(int characterId)
        {
            var stats = new List<DBStats>
                        {
                            new DBStats
                            {
                                Type = CharacterStatType,
                                Instance = characterId,
                                StatId = 1,
                                StatValue = StarterLife
                            },
                            new DBStats
                            {
                                Type = CharacterStatType,
                                Instance = characterId,
                                StatId = 27,
                                StatValue = StarterHealth
                            },
                            new DBStats
                            {
                                Type = CharacterStatType,
                                Instance = characterId,
                                StatId = 214,
                                StatValue = StarterNano
                            },
                            new DBStats
                            {
                                Type = CharacterStatType,
                                Instance = characterId,
                                StatId = 221,
                                StatValue = StarterMaxNano
                            },
                        };

            foreach (DBStats stat in stats)
            {
                DBStats existing = StatDao.Instance.GetById(stat.Type, stat.Instance, stat.StatId);
                if (existing.Id != 0)
                {
                    if (existing.StatValue == stat.StatValue)
                    {
                        continue;
                    }

                    existing.StatValue = stat.StatValue;
                    StatDao.Instance.Save(existing);
                    continue;
                }

                StatDao.Instance.Add(stat);
            }
        }

        private static void ApplyStarterItems(int characterId)
        {
            int containerType = characterId;
            int containerInstance = (int)IdentityType.Inventory;
            var items = new List<DBItem>(Items.Length);

            foreach (StarterItemDefinition entry in Items)
            {
                items.Add(
                    new DBItem
                    {
                        containertype = containerType,
                        containerinstance = containerInstance,
                        containerplacement = entry.Placement,
                        lowid = entry.LowId,
                        highid = entry.HighId,
                        quality = entry.Quality,
                        multiplecount = entry.Count
                    });
            }

            ItemDao.Instance.Save(items, null, null);
        }
    }
}
