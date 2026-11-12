namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// GameData/MonsterCombatRules.json: combat behaviour keyed by mob hash.
    /// Keys are NpcTemplates.json leaf hashes. Loaded once per GameData root (through the GameData override folder) and
    /// swapped whole by <see cref="Reload"/>: a lookup sees the old rules or the new, never a mix. NPCs already spawned
    /// keep what they read at spawn until they respawn.
    /// </summary>
    public sealed class MonsterCombatRulesCatalog
    {
        public const string FileName = "MonsterCombatRules.json";
        public const string PetSummonsFileName = "NpcPetSummons.json";

        /// <summary>The name <see cref="Reload"/> is registered under.</summary>
        public const string ReloadName = "MonsterCombatRules";

        static readonly ConcurrentDictionary<string, Holder> ByRoot = new(StringComparer.OrdinalIgnoreCase);

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        readonly Dictionary<string, MonsterCombatRules> _rules;
        readonly bool _hasRules;
        readonly NpcPetSummonProfile? _petSummons;
        readonly HashSet<int> _creationItemIds;

        MonsterCombatRulesCatalog(Dictionary<string, MonsterCombatRules> rules, bool hasRules, NpcPetSummonProfile? petSummons)
        {
            _rules = rules;
            _hasRules = hasRules;
            _petSummons = petSummons;
            _creationItemIds = petSummons == null ? [] : new HashSet<int>(petSummons.CreationItemTemplateIds);
        }

        public int Count => _rules.Count;

        /// <summary>The current rules for <paramref name="gameDataRoot"/>; take it once per lookup.</summary>
        public static MonsterCombatRulesCatalog For(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            return ByRoot.GetOrAdd(gameDataRoot, root => new Holder(Load(root))).Current;
        }

        /// <summary>
        /// Reads the file again and swaps it in for <paramref name="gameDataRoot"/>. A file that fails to read or parse
        /// keeps the current rules and reports why. Call off the playfield tick: it reads the file.
        /// </summary>
        public static AORebirth.Modules.Content.GameDataReloadResult Reload(string gameDataRoot,
            IItemTemplateCatalog? items = null, IGameData? gameData = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            MonsterCombatRulesCatalog loaded;
            try
            {
                loaded = Load(gameDataRoot);
                if (items != null && gameData != null)
                    loaded.ValidatePetSummons(items, gameData);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
                or NotSupportedException)
            {
                int kept = ByRoot.TryGetValue(gameDataRoot, out Holder? old) ? old.Current.Count : 0;
                return new AORebirth.Modules.Content.GameDataReloadResult(ReloadName, false, kept,
                    [FileName + ": " + exception.Message]);
            }

            Holder holder = ByRoot.GetOrAdd(gameDataRoot, _ => new Holder(loaded));
            holder.Current = loaded;
            return new AORebirth.Modules.Content.GameDataReloadResult(ReloadName, true, loaded.Count, []);
        }

        public bool TryGet(string? hash, out MonsterCombatRules rules)
        {
            if (!string.IsNullOrWhiteSpace(hash) && _rules.TryGetValue(hash.Trim(), out MonsterCombatRules? found))
            {
                rules = found;
                return true;
            }

            rules = null!;
            return false;
        }

        internal bool IsPetCreationItem(int templateId) => _creationItemIds.Contains(templateId);

        internal MonsterPetRule PetRule(string? hash)
            => TryGet(hash, out MonsterCombatRules rules) && rules.Pets != null
                ? rules.Pets
                : _petSummons?.Policy ?? throw new InvalidDataException($"{PetSummonsFileName}: missing pet policy.");

        public void ValidatePetSummons(IItemTemplateCatalog items, IGameData gameData)
        {
            if (_petSummons == null)
                throw new FileNotFoundException($"Mandatory NPC pet policy {PetSummonsFileName} is missing.");
            if (!_hasRules)
                throw new FileNotFoundException($"Mandatory NPC combat catalog {FileName} is missing.");

            HashSet<int> equippedIds = gameData is GameDataStore store
                ? new HashSet<int>(store.NpcEquipmentTemplateIds())
                : [];
            foreach (int id in _petSummons.CreationItemTemplateIds)
            {
                if (!items.TryGet(id, out ItemTemplate item) || !NpcPetSummons.TryReadSummon(item, out string target, out int level))
                    throw new InvalidDataException($"{PetSummonsFileName}: item {id} has no valid summon action.");

                if (equippedIds.Contains(id) && (!gameData.HasMobTemplate(target)
                    || !gameData.TryResolveMobTemplate(target, level > 0 ? level : null, out MobTemplate template)
                    || !NpcTemplateValidation.CanSpawn(template)))
                    throw new InvalidDataException($"{PetSummonsFileName}: equipped item {id} refers to unspawnable pet '{target}'.");
            }

            foreach ((string hash, MonsterCombatRules rule) in _rules)
            {
                if (rule.Pets != null && !gameData.HasMobTemplate(hash))
                    throw new InvalidDataException($"{FileName}: pet rule refers to unknown NPC template '{hash}'.");
            }
        }

        static MonsterCombatRulesCatalog Load(string root)
        {
            var rules = new Dictionary<string, MonsterCombatRules>(StringComparer.OrdinalIgnoreCase);
            string path = AORebirth.Core.GameData.GameDataPaths.ResolveFile(root, FileName);
            bool hasRules = File.Exists(path);
            if (hasRules)
            {
                var file = JsonSerializer.Deserialize<Dictionary<string, MonsterCombatRules>>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException($"{FileName}: expected a combat rule object.");
                foreach ((string hash, MonsterCombatRules rule) in file)
                {
                    if (string.IsNullOrWhiteSpace(hash) || rule == null || !rules.TryAdd(hash.Trim(), rule))
                        throw new InvalidDataException($"{FileName}: empty, invalid or duplicate NPC rule '{hash}'.");
                    rule.Pets?.Validate($"{FileName}: NPC '{hash}'");
                }
            }

            string petPath = AORebirth.Core.GameData.GameDataPaths.ResolveFile(root, PetSummonsFileName);
            NpcPetSummonProfile? petSummons = null;
            if (File.Exists(petPath))
            {
                petSummons = JsonSerializer.Deserialize<NpcPetSummonProfile>(File.ReadAllText(petPath), JsonOptions);
                if (petSummons?.Version != 1 || petSummons.Policy == null || petSummons.CreationItemTemplateIds == null
                    || petSummons.CreationItemTemplateIds.Length == 0
                    || Array.Exists(petSummons.CreationItemTemplateIds, id => id <= 0)
                    || new HashSet<int>(petSummons.CreationItemTemplateIds).Count != petSummons.CreationItemTemplateIds.Length)
                    throw new InvalidDataException($"{PetSummonsFileName}: expected version 1, unique positive item IDs and a pet policy.");
                petSummons.Policy.Validate(PetSummonsFileName);
            }

            return new MonsterCombatRulesCatalog(rules, hasRules, petSummons);
        }

        /// <summary>One root's current rules; the reference is swapped whole.</summary>
        sealed class Holder(MonsterCombatRulesCatalog current)
        {
            volatile MonsterCombatRulesCatalog _current = current;

            public MonsterCombatRulesCatalog Current
            {
                get => _current;
                set => _current = value;
            }
        }
    }

    /// <summary>One mob hash's combat rules.</summary>
    public sealed class MonsterCombatRules
    {
        HashSet<int>? _hatedFamilies;

        public MonsterPetRule? Pets { get; set; }

        /// <summary>Behaviour switches: NoCombat, NoMove, Wander, Guard, Boss (<see cref="NpcFeature"/>).</summary>
        public List<NpcFeature>? Features { get; set; }

        /// <summary>
        /// NPCFamily (stat 455) values this NPC attacks on sight: an NPC of one of these families that comes within
        /// its aggro range (ProximityRangeOutdoors/Indoors) is attacked as a player would be. Players' pets never are.
        /// </summary>
        public int[]? Hates { get; set; }

        /// <summary><see cref="Hates"/> as a set; null when the NPC hates no family. Not part of the file.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public IReadOnlySet<int>? HatedFamilies
            => Hates is not { Length: > 0 } ? null : _hatedFamilies ??= new HashSet<int>(Hates);
    }

    /// <summary>
    /// Explicit policy for pets raised by an NPC while fighting. Pets leave when the NPC resets, dies or despawns.
    /// </summary>
    public sealed class MonsterPetRule
    {
        /// <summary>Most pets alive at once.</summary>
        public required int MaxPets { get; init; }

        /// <summary>Seconds from combat start to the first pet, and between each pet after it.</summary>
        public required double SummonIntervalSeconds { get; init; }

        /// <summary>Seconds after a pet dies before its replacement, while the fight goes on.</summary>
        public required double RespawnDelaySeconds { get; init; }

        /// <summary>Seconds before retrying a summon that could not be placed.</summary>
        public required double RetryDelaySeconds { get; init; }

        internal void Validate(string context)
        {
            if (MaxPets < 0 || !double.IsFinite(SummonIntervalSeconds) || SummonIntervalSeconds < 0
                || !double.IsFinite(RespawnDelaySeconds) || RespawnDelaySeconds < 0
                || !double.IsFinite(RetryDelaySeconds) || RetryDelaySeconds <= 0)
                throw new InvalidDataException($"{context}: invalid pet cap or timing.");
        }
    }

    public sealed class NpcPetSummonProfile
    {
        public required int Version { get; init; }
        public required int[] CreationItemTemplateIds { get; init; }
        public required MonsterPetRule Policy { get; init; }
    }
}
