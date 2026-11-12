#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace AORebirth.Database.Dao
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    using AORebirth.Database.Entities;

    /// <summary>Authored compatibility templates for summon hashes without a database row.</summary>
    public static class PetSummonMobTemplateCatalog
    {
        public const string FileName = "NpcSummonFallbacks.json";

        private static readonly ConcurrentDictionary<string, Dictionary<string, TemplateRow>> ByPath =
            new ConcurrentDictionary<string, Dictionary<string, TemplateRow>>(StringComparer.Ordinal);

        public static DBMobTemplate TryGet(string path, string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return null;
            }

            TemplateRow template;
            return ByPath.GetOrAdd(Path.GetFullPath(path), Load).TryGetValue(hash, out template)
                ? template.ToMobTemplate()
                : null;
        }

        /// <summary>Reads and validates the same catalog used by compatibility spawns.</summary>
        public static int Validate(string path)
        {
            return Load(path).Count;
        }

        private static Dictionary<string, TemplateRow> Load(string path)
        {
            CatalogDocument document;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    document = (CatalogDocument)new DataContractJsonSerializer(typeof(CatalogDocument)).ReadObject(stream);
                }
            }
            catch (SerializationException exception)
            {
                throw new InvalidDataException(path + ": invalid NPC summon fallback catalog.", exception);
            }

            if (document == null || document.Version != 1 || document.Templates == null || document.Templates.Length == 0)
            {
                throw new InvalidDataException(path + ": expected version 1 with at least one template.");
            }

            var templates = new Dictionary<string, TemplateRow>(StringComparer.OrdinalIgnoreCase);
            foreach (TemplateRow row in document.Templates)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Hash) || row.Hash.Length != 4
                    || row.Hash != row.Hash.Trim() || string.IsNullOrWhiteSpace(row.Name)
                    || row.MinLvl <= 0 || row.MaxLvl < row.MinLvl || row.Health <= 0 || row.MonsterScale <= 0
                    || templates.ContainsKey(row.Hash))
                {
                    throw new InvalidDataException(path + ": invalid or duplicate NPC template '" + (row == null ? null : row.Hash) + "'.");
                }

                templates.Add(row.Hash, row);
            }

            return templates;
        }

        [DataContract]
        private sealed class CatalogDocument
        {
            [DataMember(IsRequired = true)]
            public int Version { get; set; }

            [DataMember(IsRequired = true)]
            public TemplateRow[] Templates { get; set; }
        }

        [DataContract]
        private sealed class TemplateRow
        {
            [DataMember(IsRequired = true)]
            public string Hash { get; set; }

            [DataMember(IsRequired = true)]
            public int MinLvl { get; set; }

            [DataMember(IsRequired = true)]
            public int MaxLvl { get; set; }

            [DataMember(IsRequired = true)]
            public int Side { get; set; }

            [DataMember(IsRequired = true)]
            public int Fatness { get; set; }

            [DataMember(IsRequired = true)]
            public int Breed { get; set; }

            [DataMember(IsRequired = true)]
            public int Sex { get; set; }

            [DataMember(IsRequired = true)]
            public int Race { get; set; }

            [DataMember(IsRequired = true)]
            public string Name { get; set; }

            [DataMember(IsRequired = true)]
            public int Flags { get; set; }

            [DataMember(IsRequired = true)]
            public int NPCFamily { get; set; }

            [DataMember(IsRequired = true)]
            public int Health { get; set; }

            [DataMember(IsRequired = true)]
            public int MonsterData { get; set; }

            [DataMember(IsRequired = true)]
            public int MonsterScale { get; set; }

            [DataMember]
            public int TextureHands { get; set; }

            [DataMember]
            public int TextureBody { get; set; }

            [DataMember]
            public int TextureFeet { get; set; }

            [DataMember]
            public int TextureArms { get; set; }

            [DataMember]
            public int TextureLegs { get; set; }

            [DataMember]
            public int HeadMesh { get; set; }

            public DBMobTemplate ToMobTemplate()
            {
                return new DBMobTemplate
                {
                    Hash = Hash,
                    MinLvl = MinLvl,
                    MaxLvl = MaxLvl,
                    Side = Side,
                    Fatness = Fatness,
                    Breed = Breed,
                    Sex = Sex,
                    Race = Race,
                    Name = Name,
                    Flags = Flags,
                    NPCFamily = NPCFamily,
                    Health = Health,
                    MonsterData = MonsterData,
                    MonsterScale = MonsterScale,
                    TextureHands = TextureHands,
                    TextureBody = TextureBody,
                    TextureFeet = TextureFeet,
                    TextureArms = TextureArms,
                    TextureLegs = TextureLegs,
                    HeadMesh = HeadMesh,
                };
            }
        }
    }
}
