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
    /// Source: SqlTables/mobtemplate.sql (BSLX, PT50-PT56, MT01-MT04) and live captures
    /// 20260710-185528 (Belamorte), 20260711-181536 (attack pets PT50-PT54),
    /// 20260711-195926 (Soothing Spirits heal pets MT01-MT04).
    /// </summary>
    internal static class PetSummonMobTemplateCatalog
    {
        private static readonly Dictionary<string, DBMobTemplate> TemplatesByHash =
            new Dictionary<string, DBMobTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "PT50",
                    new DBMobTemplate
                    {
                        Hash = "PT50",
                        MinLvl = 1,
                        MaxLvl = 10,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Anger Manifestation",
                        Flags = 403182081,
                        NPCFamily = 97,
                        Health = 182,
                        MonsterData = 96195,
                        MonsterScale = 95,
                    }
                },
                {
                    "PT51",
                    new DBMobTemplate
                    {
                        Hash = "PT51",
                        MinLvl = 13,
                        MaxLvl = 32,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Fury Externalization",
                        Flags = 403182081,
                        NPCFamily = 97,
                        Health = 925,
                        MonsterData = 96195,
                        MonsterScale = 102,
                    }
                },
                {
                    "PT52",
                    new DBMobTemplate
                    {
                        Hash = "PT52",
                        MinLvl = 36,
                        MaxLvl = 62,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Rage Materialization",
                        Flags = 403182081,
                        NPCFamily = 97,
                        // PT52 from 20260711-192136: L62, health 2624, scale 107.
                        Health = 2624,
                        MonsterData = 96195,
                        MonsterScale = 107,
                    }
                },
                {
                    "PT53",
                    new DBMobTemplate
                    {
                        Hash = "PT53",
                        MinLvl = 67,
                        MaxLvl = 95,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Wrath Incarnation",
                        Flags = 403182081,
                        NPCFamily = 97,
                        Health = 4989,
                        MonsterData = 96195,
                        MonsterScale = 111,
                    }
                },
                {
                    "PT54",
                    new DBMobTemplate
                    {
                        Hash = "PT54",
                        MinLvl = 101,
                        MaxLvl = 137,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Frenzy Embodiment",
                        Flags = 403182081,
                        NPCFamily = 97,
                        Health = 9145,
                        MonsterData = 96195,
                        MonsterScale = 116,
                    }
                },
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
                    "MT01",
                    new DBMobTemplate
                    {
                        Hash = "MT01",
                        MinLvl = 1,
                        MaxLvl = 14,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Medinos",
                        Flags = 403182081,
                        NPCFamily = 96,
                        Health = 181,
                        MonsterData = 96193,
                        MonsterScale = 100,
                    }
                },
                {
                    "MT02",
                    new DBMobTemplate
                    {
                        Hash = "MT02",
                        MinLvl = 15,
                        MaxLvl = 33,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Salvinous",
                        Flags = 403182081,
                        NPCFamily = 96,
                        Health = 609,
                        MonsterData = 96193,
                        MonsterScale = 102,
                    }
                },
                {
                    "MT03",
                    new DBMobTemplate
                    {
                        Hash = "MT03",
                        MinLvl = 34,
                        MaxLvl = 55,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Valentyia",
                        Flags = 403182081,
                        NPCFamily = 96,
                        Health = 1345,
                        MonsterData = 96193,
                        MonsterScale = 106,
                    }
                },
                {
                    "MT04",
                    new DBMobTemplate
                    {
                        Hash = "MT04",
                        MinLvl = 56,
                        MaxLvl = 77,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Sanoo",
                        Flags = 403182081,
                        NPCFamily = 96,
                        Health = 2274,
                        MonsterData = 96193,
                        MonsterScale = 109,
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
