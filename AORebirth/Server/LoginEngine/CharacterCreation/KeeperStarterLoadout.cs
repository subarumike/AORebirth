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
    /// Level-1 Keeper starter inventory from capture 20260711-keeper-starter-kit (FullCharacter slots 64-71).
    /// </summary>
    internal static class KeeperStarterLoadout
    {
        private struct StarterItemDefinition
        {
            public int Placement;
            public int LowId;
            public int HighId;
            public int Quality;
            public int Count;
        }

        private static readonly StarterItemDefinition[] Items =
        {
            new StarterItemDefinition { Placement = 64, LowId = 291082, HighId = 291082, Quality = 1, Count = 50 },
            new StarterItemDefinition { Placement = 65, LowId = 291043, HighId = 291043, Quality = 1, Count = 25 },
            new StarterItemDefinition { Placement = 66, LowId = 252158, HighId = 252158, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 67, LowId = 292235, HighId = 292235, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 68, LowId = 296977, HighId = 296977, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 69, LowId = 287047, HighId = 287047, Quality = 25, Count = 1 },
            new StarterItemDefinition { Placement = 70, LowId = 218403, HighId = 218403, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 71, LowId = 210529, HighId = 210529, Quality = 1, Count = 1 },
        };

        public static void Apply(int characterId)
        {
            ApplyStarterItems(characterId);
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
