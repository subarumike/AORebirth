namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// GameData/MonsterCombatRules.json: combat behaviour keyed by mob hash, e.g.
    /// <c>{ "ASSU": { "Pets": { "MaxPets": 2 } }, "NEBG": { "Features": [ "Guard" ] }, "MUSN": { "Hates": [ 42 ] } }</c>.
    /// Keys are NpcTemplates.json leaf hashes. Loaded once per GameData root (through the GameData override folder) and
    /// swapped whole by <see cref="Reload"/>: a lookup sees the old rules or the new, never a mix. NPCs already spawned
    /// keep what they read at spawn until they respawn.
    /// </summary>
    public sealed class MonsterCombatRulesCatalog
    {
        public const string FileName = "MonsterCombatRules.json";

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

        MonsterCombatRulesCatalog(Dictionary<string, MonsterCombatRules> rules)
        {
            _rules = rules;
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
        public static AORebirth.Modules.Content.GameDataReloadResult Reload(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            MonsterCombatRulesCatalog loaded;
            try
            {
                loaded = Load(gameDataRoot);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
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

        static MonsterCombatRulesCatalog Load(string root)
        {
            var rules = new Dictionary<string, MonsterCombatRules>(StringComparer.OrdinalIgnoreCase);
            string path = AORebirth.Core.GameData.GameDataPaths.ResolveFile(root, FileName);
            if (!File.Exists(path))
                return new MonsterCombatRulesCatalog(rules);

            Dictionary<string, MonsterCombatRules>? file =
                JsonSerializer.Deserialize<Dictionary<string, MonsterCombatRules>>(File.ReadAllText(path), JsonOptions);
            foreach (KeyValuePair<string, MonsterCombatRules> entry in file ?? new())
            {
                if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value != null)
                {
                    if (entry.Value.Pets?.MaxPetsPerType is < 1)
                        throw new JsonException($"{path}: {entry.Key}.Pets.MaxPetsPerType must be positive or null.");
                    if (entry.Value.TriggeredSelfNanos is { } triggers)
                    {
                        for (int i = 0; i < triggers.Count; i++)
                        {
                            MonsterTriggeredSelfNano? trigger = triggers[i];
                            if (trigger == null || trigger.NanoId <= 0
                                || trigger.HealthBelowPercent is < 1 or > 100
                                || trigger.ChancePercent is < 0 or > 100
                                || trigger.MaxCastsPerFight is < 1 || trigger.CooldownSeconds < 0)
                                throw new JsonException($"{path}: {entry.Key}.TriggeredSelfNanos[{i}] needs a positive NanoId, "
                                    + "HealthBelowPercent 1..100, ChancePercent 0..100, MaxCastsPerFight positive or null, "
                                    + "and CooldownSeconds >= 0.");
                        }
                    }
                    rules[entry.Key.Trim()] = entry.Value;
                }
            }

            return new MonsterCombatRulesCatalog(rules);
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
        public int? MaxCastsPerFight { get; set; } = 1;

        /// <summary>Additional delay after successful execution; normal casting cadence and recharge still apply.</summary>
        public int CooldownSeconds { get; set; }
    }

    /// <summary>
    /// Pets the NPC raises while it fights with the "NPC Pet Creation Item" in its equipment (the first, by slot); the
    /// item's OnUse names the pet's mob hash and level. Every NPC with that item uses these defaults unless its hash
    /// overrides them. Pets leave when the NPC resets, dies or despawns.
    /// </summary>
    public sealed class MonsterPetRule
    {
        /// <summary>Most pets alive at once.</summary>
        public int MaxPets { get; set; } = 1;

        /// <summary>Most living pets of each creation-item mob hash; null leaves the per-type count unlimited.</summary>
        public int? MaxPetsPerType { get; set; }

        /// <summary>Seconds from combat start to the first pet, and between each pet after it.</summary>
        public double SummonIntervalSeconds { get; set; } = 1;

        /// <summary>Seconds after a pet dies before its replacement, while the fight goes on.</summary>
        public double RespawnDelaySeconds { get; set; } = 10;
    }
}
