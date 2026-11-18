namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;

    /// <summary>
    /// GameData/MonsterCombatRules.json: combat behaviour keyed by mob hash.
    /// Keys are NpcTemplates.json leaf hashes. Loaded once per GameData root (through the GameData override folder) and
    /// swapped whole by <see cref="Reload"/>: a lookup sees the old rules or the new, never a mix. NPCs already spawned
    /// keep what they read at spawn until they respawn.
    /// </summary>
    public sealed class MonsterCombatRulesCatalog
    {
        public const string FileName = "MonsterCombatRules.json";
        public const string PetSummonsFileName = NpcPetSummonProfile.FileName;

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
        readonly NpcPetSummonProfile? _petSummons;
        readonly HashSet<int> _creationItemIds;

        MonsterCombatRulesCatalog(Dictionary<string, MonsterCombatRules> rules, NpcPetSummonProfile? petSummons)
        {
            _rules = rules;
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
                    loaded.ValidateEncounterReferences(items, gameData);
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

        NpcPetSummonPolicy? PetOverride(string? spawnHash, string? templateHash)
        {
            if (TryGet(spawnHash, out MonsterCombatRules spawnRule) && spawnRule.Pets != null)
                return spawnRule.Pets;
            return TryGet(templateHash, out MonsterCombatRules templateRule) ? templateRule.Pets : null;
        }

        internal NpcPetSummonPolicy? PetRule(string? spawnHash, string? templateHash)
            => PetOverride(spawnHash, templateHash) ?? _petSummons?.Policy;

        internal bool RequiresPetCreator(string? spawnHash, string? templateHash)
            => PetOverride(spawnHash, templateHash) is { MaxPets: > 0 };

        public void ValidateEncounterReferences(IItemTemplateCatalog items, IGameData gameData)
        {
            if (_petSummons == null)
                throw new FileNotFoundException($"Mandatory NPC pet policy {PetSummonsFileName} is missing.");
            if (!File.Exists(AORebirth.Core.GameData.GameDataPaths.ResolveFile(gameData.RootPath, FileName)))
                throw new FileNotFoundException($"Mandatory NPC combat catalog {FileName} is missing.");

            _petSummons.ValidateReferences(items, gameData);

            GameDataStore? store = gameData as GameDataStore;
            foreach ((string hash, MonsterCombatRules rule) in _rules)
            {
                if (rule.Pets == null && rule.TriggeredSelfNanos is not { Count: > 0 })
                    continue;
                if (!gameData.HasMobTemplate(hash))
                    throw new InvalidDataException($"{FileName}: encounter rule refers to unknown NPC template '{hash}'.");
                NpcLevelBand[]? bands = store?.NpcEquipmentBands(hash).ToArray();
                if (bands is { Length: 0 })
                    throw new InvalidDataException($"{FileName}: encounter rule requires an NPC leaf template for '{hash}'.");
                if (rule.Pets is { MaxPets: > 0 } && bands != null
                    && bands.Any(band => band.Equipment.Count(pair => HasEquipmentBinding(pair, IsPetCreationItem)) != 1))
                    throw new InvalidDataException($"{FileName}: NPC '{hash}' needs one bound pet creation item in each level band.");
                foreach (MonsterTriggeredSelfNano trigger in rule.TriggeredSelfNanos ?? [])
                {
                    if (!items.TryGet(trigger.NanoId, out ItemTemplate item) || NanoSpell.From(item).IsHostile)
                        throw new InvalidDataException($"{FileName}: NPC '{hash}' references invalid self nano {trigger.NanoId}.");
                    bool UploadsNano(int id) => items.TryGet(id, out ItemTemplate equipment)
                        && equipment.UploadedNanoIds().Contains(trigger.NanoId);
                    if (bands != null && bands.Any(band => !band.Equipment.Any(pair => HasEquipmentBinding(pair, UploadsNano))))
                        throw new InvalidDataException($"{FileName}: NPC '{hash}' has no equipment uploading nano {trigger.NanoId} in one or more level bands.");
                }
            }
        }

        static bool HasEquipmentBinding(List<int> pair, Func<int, bool> hasBinding)
            => pair is { Count: > 0 } && pair[0] > 0 && hasBinding(pair[0])
                && (pair.Count < 2 || pair[1] <= 0 || pair[1] == pair[0] || hasBinding(pair[1]));

        static MonsterCombatRulesCatalog Load(string root)
        {
            var rules = new Dictionary<string, MonsterCombatRules>(StringComparer.OrdinalIgnoreCase);
            string path = AORebirth.Core.GameData.GameDataPaths.ResolveFile(root, FileName);
            Dictionary<string, MonsterCombatRules>? file = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, MonsterCombatRules>>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException($"{FileName}: expected a combat rule object.")
                : null;
            foreach (KeyValuePair<string, MonsterCombatRules> entry in file ?? new())
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || !rules.TryAdd(entry.Key.Trim(), entry.Value))
                    throw new InvalidDataException($"{FileName}: empty, invalid or duplicate NPC rule '{entry.Key}'.");

                entry.Value.Pets?.Validate($"{FileName}: NPC '{entry.Key}'");
                if (entry.Value.TriggeredSelfNanos is { } triggers)
                {
                    var seen = new HashSet<int>();
                    for (int i = 0; i < triggers.Count; i++)
                    {
                        MonsterTriggeredSelfNano? trigger = triggers[i];
                        if (trigger == null || trigger.NanoId <= 0 || !seen.Add(trigger.NanoId)
                            || trigger.HealthBelowPercent is < 1 or > 100
                            || trigger.ChancePercent is < 0 or > 100
                            || trigger.MaxCastsPerFight is < 1 || trigger.CooldownSeconds < 0)
                            throw new JsonException($"{path}: {entry.Key}.TriggeredSelfNanos[{i}] needs a unique positive NanoId, "
                                + "HealthBelowPercent 1..100, ChancePercent 0..100, MaxCastsPerFight positive or null, "
                                + "and CooldownSeconds >= 0.");
                    }
                }
            }

            return new MonsterCombatRulesCatalog(rules, NpcPetSummonProfile.LoadOptional(root));
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

        public NpcPetSummonPolicy? Pets { get; set; }

        /// <summary>Health-triggered self casts with per-fight limits; excluded from ordinary casting.</summary>
        public List<MonsterTriggeredSelfNano>? TriggeredSelfNanos { get; set; }

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

    /// <summary>A health threshold rolls once per fight; selected casts use their own count and cooldown.</summary>
    public sealed class MonsterTriggeredSelfNano
    {
        public required int NanoId { get; set; }

        public required int HealthBelowPercent { get; set; }

        public required int ChancePercent { get; set; }

        /// <summary>Successful executions allowed after the roll; null means unlimited. Reset restores the allowance.</summary>
        public required int? MaxCastsPerFight { get; set; }

        /// <summary>Additional delay after successful execution; normal casting cadence and recharge still apply.</summary>
        public required int CooldownSeconds { get; set; }
    }
}
