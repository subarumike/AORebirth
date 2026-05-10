namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using CellAO.Core.Entities;
    using CellAO.Core.NPCHandler;
    using CellAO.Core.Vector;
    using CellAO.Enums;

    using ZoneEngine.Core.Controllers;

    public static class CombatTestMobArchetype
    {
        public const string TemplateHash = "A004";

        public const string DisplayName = "Codex Test Beach Leet";

        public const int MonsterData = 17655;

        public const int CorpseCatMesh = 15222;

        public static readonly Entry BeachLeet = new Entry(
            "beachleet",
            new[] { "beachleet", "leet", "codexleet" },
            TemplateHash,
            DisplayName,
            1,
            12,
            MonsterData,
            CorpseCatMesh,
            90,
            36,
            6,
            5);

        public static readonly Entry IslandReet = new Entry(
            "islandreet",
            new[] { "islandreet", "reet" },
            "A001",
            "Codex Test Island Reet",
            1,
            12,
            30365,
            25733,
            90,
            53,
            6,
            5);

        public static readonly Entry ShoreSnake = new Entry(
            "shoresnake",
            new[] { "shoresnake", "snake" },
            "A003",
            "Codex Test Shore Snake",
            1,
            25,
            30252,
            23353,
            36,
            27,
            6,
            5);

        public static readonly Entry StowawayRollerrat = new Entry(
            "rollerrat",
            new[] { "rollerrat", "stowawayrollerrat", "rat" },
            "A012",
            "Codex Test Stowaway Rollerrat",
            4,
            58,
            17687,
            15272,
            65,
            55,
            6,
            5);

        public static readonly Entry[] All =
        {
            BeachLeet,
            IslandReet,
            ShoreSnake,
            StowawayRollerrat
        };

        private const int LiveObservedDeathActionKey = 0x1F7;

        public static Entry Default
        {
            get
            {
                return BeachLeet;
            }
        }

        public static bool TryGetByAlias(string alias, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            foreach (Entry candidate in All)
            {
                if (candidate.MatchesAlias(alias))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetByName(string name, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            foreach (Entry candidate in All)
            {
                if (string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool IsCombatTestMob(ICharacter character)
        {
            Entry ignored;
            return character != null
                   && character.Controller is NPCController
                   && TryGetByName(character.Name, out ignored);
        }

        public static IEnumerable<KeyValuePair<int, int>> CorpseVisualMappings()
        {
            foreach (Entry entry in All)
            {
                yield return new KeyValuePair<int, int>(entry.MonsterData, entry.CorpseCatMesh);
            }
        }

        public static Character SpawnNear(ICharacter character, float zOffset)
        {
            return SpawnNear(character, Default, zOffset);
        }

        public static Character SpawnNear(ICharacter character, Entry entry, float zOffset)
        {
            Coordinate spawnCoordinate = new Coordinate(character.Coordinates());
            spawnCoordinate.z += zOffset;

            var npcController = new NPCController();
            Character mobCharacter = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                entry.TemplateHash,
                character.Playfield.Identity,
                spawnCoordinate,
                character.Heading,
                npcController,
                entry.Level);

            if (mobCharacter == null)
            {
                return null;
            }

            mobCharacter.Name = entry.DisplayName;
            mobCharacter.Playfield = character.Playfield;
            Prepare(mobCharacter, entry);
            mobCharacter.DoNotDoTimers = false;
            return mobCharacter;
        }

        public static void Prepare(ICharacter mobCharacter)
        {
            Entry entry;
            if (!TryGetByName(mobCharacter.Name, out entry))
            {
                entry = Default;
            }

            Prepare(mobCharacter, entry);
        }

        public static void Prepare(ICharacter mobCharacter, Entry entry)
        {
            SetMobStat(mobCharacter, StatIds.monsterdata, entry.MonsterData);
            SetMobStat(mobCharacter, StatIds.catmesh, entry.CorpseCatMesh);
            SetMobStat(mobCharacter, StatIds.displaycatmesh, entry.CorpseCatMesh);
            SetMobStat(mobCharacter, StatIds.monsterscale, entry.MonsterScale);
            SetMobStat(mobCharacter, StatIds.visualflags, 0x1F);
            SetMobStat(mobCharacter, StatIds.side, 3);
            SetMobStat(mobCharacter, StatIds.fatness, 1);
            SetMobStat(mobCharacter, StatIds.breed, entry.Breed);
            SetMobStat(mobCharacter, StatIds.sex, entry.Sex);
            SetMobStat(mobCharacter, StatIds.race, 1);
            SetMobStat(mobCharacter, StatIds.npcfamily, entry.NpcFamily);
            SetMobStat(mobCharacter, StatIds.itemanim, LiveObservedDeathActionKey);
            SetMobStat(mobCharacter, StatIds.corpseanimkey, LiveObservedDeathActionKey);
            SetMobStat(mobCharacter, StatIds.dieanim, LiveObservedDeathActionKey);
            SetMobStat(mobCharacter, StatIds.life, entry.Health);
            SetMobStat(mobCharacter, StatIds.health, entry.Health);
            SetMobStat(mobCharacter, StatIds.healdelta, 0);
            SetMobStat(mobCharacter, StatIds.healinterval, 600);
        }

        private static void SetMobStat(ICharacter mobCharacter, StatIds stat, int value)
        {
            mobCharacter.Stats[stat].Value = value;
            mobCharacter.Stats[stat].BaseValue = (uint)value;
        }

        public class Entry
        {
            public Entry(
                string key,
                string[] aliases,
                string templateHash,
                string displayName,
                int level,
                int health,
                int monsterData,
                int corpseCatMesh,
                int monsterScale,
                int npcFamily,
                int breed,
                int sex)
            {
                this.Key = key;
                this.Aliases = aliases;
                this.TemplateHash = templateHash;
                this.DisplayName = displayName;
                this.Level = level;
                this.Health = health;
                this.MonsterData = monsterData;
                this.CorpseCatMesh = corpseCatMesh;
                this.MonsterScale = monsterScale;
                this.NpcFamily = npcFamily;
                this.Breed = breed;
                this.Sex = sex;
            }

            public string Key { get; private set; }

            public string[] Aliases { get; private set; }

            public string TemplateHash { get; private set; }

            public string DisplayName { get; private set; }

            public int Level { get; private set; }

            public int Health { get; private set; }

            public int MonsterData { get; private set; }

            public int CorpseCatMesh { get; private set; }

            public int MonsterScale { get; private set; }

            public int NpcFamily { get; private set; }

            public int Breed { get; private set; }

            public int Sex { get; private set; }

            public bool MatchesAlias(string alias)
            {
                if (string.Equals(this.Key, alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                foreach (string candidate in this.Aliases)
                {
                    if (string.Equals(candidate, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
