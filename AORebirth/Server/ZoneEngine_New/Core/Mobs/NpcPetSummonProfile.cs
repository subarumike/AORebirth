namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>Canonical item bindings and the default authored policy for NPC combat pets.</summary>
    public sealed class NpcPetSummonProfile
    {
        public const string FileName = "NpcPetSummons.json";
        public const int SupportedVersion = 1;

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            AllowDuplicateProperties = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        public required int Version { get; init; }
        public required int[] CreationItemTemplateIds { get; init; }
        public required NpcPetSummonPolicy Policy { get; init; }

        internal static NpcPetSummonProfile? LoadOptional(string root)
        {
            string path = GameDataPaths.ResolveFile(root, FileName);
            if (!File.Exists(path))
                return null;

            NpcPetSummonProfile? profile;
            try
            {
                profile = JsonSerializer.Deserialize<NpcPetSummonProfile>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"{FileName}: {exception.Message}", exception);
            }

            if (profile == null || profile.Version != SupportedVersion || profile.Policy == null
                || profile.CreationItemTemplateIds is not { Length: > 0 } ids
                || Array.Exists(ids, id => id <= 0) || new HashSet<int>(ids).Count != ids.Length)
                throw new InvalidDataException($"{FileName}: expected version {SupportedVersion}, unique positive item IDs and a pet policy.");

            profile.Policy.Validate(FileName);
            return profile;
        }

        internal void ValidateReferences(IItemTemplateCatalog items, IGameData gameData, NpcTemplateCatalog? templates = null)
        {
            templates ??= (gameData as GameDataStore)?.LoadedNpcTemplates;
            HashSet<int> equippedIds = templates == null ? [] : new HashSet<int>(templates.EquipmentTemplateIds());
            foreach (int id in CreationItemTemplateIds)
            {
                if (!items.TryGet(id, out ItemTemplate item))
                    throw new InvalidDataException($"{FileName}: missing creation item template {id}.");

                // Validate the authored operation for every item and require spawnable targets for equipped items.
                ReadSummons(item, templates == null || equippedIds.Contains(id) ? gameData : null, templates);
            }
        }

        internal static NpcPetSummonOperation[] ReadSummons(ItemTemplate creator, IGameData? gameData, NpcTemplateCatalog? templates = null)
        {
            if (!creator.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                throw new InvalidDataException($"NPC pet creation item {creator.Id} has no OnUse event.");

            var summons = new List<NpcPetSummonOperation>();
            foreach (ItemSpell spell in spells)
            {
                bool spawnsMonster = spell.Is(FunctionType.SpawnMonster2);
                if (!spawnsMonster && !spell.Is(FunctionType.SummonPet))
                    continue;
                if (!spell.TryReadString(0, out string petHash) || string.IsNullOrWhiteSpace(petHash))
                    throw new InvalidDataException($"NPC pet creation item {creator.Id} has a summon with no target hash.");

                petHash = petHash.Trim();
                bool hasLevel = spell.TryReadInt(1, out int level);
                bool hasDuration = spell.TryReadInt(2, out int duration);
                if ((spawnsMonster && level <= 0) || (!hasLevel && spell.ArgumentCount > 1)
                    || (!hasDuration && spell.ArgumentCount > 2))
                    throw new InvalidDataException($"NPC pet creation item {creator.Id} has invalid summon arguments for '{petHash}'.");
                if (gameData != null && !CanSpawnPet(gameData, templates, petHash, level > 0 ? level : null))
                    throw new InvalidDataException($"NPC pet creation item {creator.Id} refers to unspawnable pet '{petHash}'.");

                // SpawnMonster2 does not apply a time-to-live in the normal item function.
                summons.Add(new NpcPetSummonOperation(spell, petHash, level, spawnsMonster ? 0 : duration));
            }

            if (summons.Count == 0)
                throw new InvalidDataException($"NPC pet creation item {creator.Id} has no OnUse summon operation.");
            return summons.ToArray();
        }

        static bool CanSpawnPet(IGameData gameData, NpcTemplateCatalog? templates, string hash, int? level)
        {
            if (!(templates?.HasTemplate(hash) ?? gameData.HasMobTemplate(hash)))
                return false;

            MobTemplate template;
            bool found = templates == null
                ? gameData.TryResolveMobTemplate(hash, level, out template)
                : templates.TryResolve(hash, level, out template);
            return found && NpcTemplateValidation.CanSpawn(template);
        }
    }

    internal readonly record struct NpcPetSummonOperation(ItemSpell Spell, string Hash, int Level, int Duration);

    /// <summary>Explicit limits and timing for an equipped pet creation template.</summary>
    public sealed class NpcPetSummonPolicy
    {
        public required int MaxPets { get; init; }
        public required int? MaxPetsPerType { get; init; }
        public required double SummonIntervalSeconds { get; init; }
        public required double RespawnDelaySeconds { get; init; }
        public required double RetryDelaySeconds { get; init; }

        internal void Validate(string context)
        {
            if (MaxPets < 0 || MaxPetsPerType is < 1
                || !double.IsFinite(SummonIntervalSeconds) || SummonIntervalSeconds < 0
                || !double.IsFinite(RespawnDelaySeconds) || RespawnDelaySeconds < 0
                || !double.IsFinite(RetryDelaySeconds) || RetryDelaySeconds <= 0)
                throw new InvalidDataException($"{context}: invalid pet cap or timing.");
        }
    }
}
