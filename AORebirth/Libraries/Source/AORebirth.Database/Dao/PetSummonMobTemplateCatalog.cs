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
    /// Capture-backed pet mob templates used when MySQL mobtemplate rows are missing.
    /// Source: SqlTables/mobtemplate.sql (BSLX, PT50-PT56, MT01-MT04, A020, A141-A142)
    /// and live captures
    /// 20260710-185528 (Belamorte), 20260711-181536 (attack pets PT50-PT54),
    /// 20260711-195926 (Soothing Spirits heal pets MT01-MT04),
    /// 20260713-103510 and 20260713-110254 (Bureaucrat shell/direct-summon SCFUs).
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
                    "A020",
                    new DBMobTemplate
                    {
                        Hash = "A020",
                        MinLvl = 5,
                        MaxLvl = 6,
                        Side = 1,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Bureaucrat Worker",
                        Flags = 403182081,
                        NPCFamily = 95,
                        Health = 110,
                        MonsterData = 96056,
                        MonsterScale = 93,
                    }
                },
                {
                    "A141",
                    new DBMobTemplate
                    {
                        Hash = "A141",
                        MinLvl = 193,
                        MaxLvl = 236,
                        Side = 0,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "CEO Guardian",
                        Flags = 403182081,
                        NPCFamily = 95,
                        Health = 34513,
                        MonsterData = 227701,
                        MonsterScale = 125,
                        TextureHands = 909,
                        TextureBody = 224853,
                        TextureFeet = 224854,
                        TextureArms = 224854,
                        TextureLegs = 224854,
                    }
                },
                {
                    "BCBG",
                    new DBMobTemplate
                    {
                        Hash = "BCBG",
                        MinLvl = 200,
                        MaxLvl = 200,
                        Side = 2,
                        Fatness = 1,
                        Breed = 7,
                        Sex = 5,
                        Race = 1,
                        Name = "Bureaucrat Bodyguard",
                        Flags = 403182081,
                        NPCFamily = 95,
                        Health = 29148,
                        MonsterData = 17627,
                        MonsterScale = 121,
                    }
                },
                {
                    "CRLT",
                    new DBMobTemplate
                    {
                        Hash = "CRLT",
                        MinLvl = 200,
                        MaxLvl = 215,
                        Side = 2,
                        Fatness = 1,
                        Breed = 4,
                        Sex = 5,
                        Race = 1,
                        Name = "Carlita Desposito",
                        Flags = 403182081,
                        NPCFamily = 97,
                        Health = 51768,
                        MonsterData = 293901,
                        MonsterScale = 100,
                        TextureHands = 284555,
                        TextureBody = 247933,
                        TextureFeet = 284553,
                        TextureArms = 247887,
                        TextureLegs = 284556,
                        HeadMesh = 223867,
                    }
                },
                {
                    "A142",
                    new DBMobTemplate
                    {
                        Hash = "A142",
                        MinLvl = 220,
                        MaxLvl = 220,
                        Side = 2,
                        Fatness = 1,
                        Breed = 4,
                        Sex = 5,
                        Race = 1,
                        Name = "Carlo Pinnetti",
                        Flags = 403182081,
                        NPCFamily = 97,
                        Health = 55687,
                        MonsterData = 258209,
                        MonsterScale = 130,
                        TextureBody = 284557,
                        TextureFeet = 247977,
                        TextureArms = 247887,
                        TextureLegs = 248016,
                        HeadMesh = 40121,
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
