#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Capture-backed MP pet mob templates used when MySQL mobtemplate rows are missing.
    /// Source: SqlTables/mobtemplate.sql (BSLX, PT56) and 20260710-185528 Belamorte capture.
    /// </summary>
    internal static class PetSummonMobTemplateCatalog
    {
        private static readonly Dictionary<string, DBMobTemplate> TemplatesByHash =
            new Dictionary<string, DBMobTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "BSLX",
                    new DBMobTemplate
                    {
                        Hash = "BSLX",
                        MinLvl = 192,
                        MaxLvl = 192,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Belamorte",
                        Flags = 403182081,
                        NPCFamily = 11,
                        Health = 9815,
                        MonsterData = 96193,
                        MonsterScale = 120,
                    }
                },
                {
                    "PT56",
                    new DBMobTemplate
                    {
                        Hash = "PT56",
                        MinLvl = 180,
                        MaxLvl = 200,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Metaphysical Demon",
                        Flags = 403182081,
                        NPCFamily = 10,
                        Health = 19481,
                        MonsterData = 40515,
                        MonsterScale = 120,
                    }
                },
            };

        public static DBMobTemplate TryGet(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return null;
            }

            DBMobTemplate template;
            return TemplatesByHash.TryGetValue(hash, out template) ? template : null;
        }
    }
}
