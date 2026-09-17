namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Mobs;

    /// <summary>
    /// NpcTemplates.json: a hash with Templates is a leaf NPC; a hash with Children is a family.
    /// Parents never have stats. Leaves never have children.
    /// </summary>
    public sealed class NpcTemplateCatalog
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        readonly Dictionary<string, string[]> _families;
        readonly Dictionary<string, NpcLeaf> _leaves;
        readonly Random _random;

        public NpcTemplateCatalog(
            Dictionary<string, string[]> families,
            Dictionary<string, NpcLeaf> leaves,
            Random? random = null)
        {
            ArgumentNullException.ThrowIfNull(families);
            ArgumentNullException.ThrowIfNull(leaves);
            _families = families;
            _leaves = leaves;
            _random = random ?? Random.Shared;
        }

        public int FamilyCount => _families.Count;

        public int LeafCount => _leaves.Count;

        public static NpcTemplateCatalog Parse(string? json, Random? random = null)
        {
            var families = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var leaves = new Dictionary<string, NpcLeaf>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return new NpcTemplateCatalog(families, leaves, random);

            Dictionary<string, NpcHashDto>? records =
                JsonSerializer.Deserialize<Dictionary<string, NpcHashDto>>(json, JsonOptions);
            if (records == null)
                return new NpcTemplateCatalog(families, leaves, random);

            foreach (KeyValuePair<string, NpcHashDto> pair in records)
            {
                string hash = pair.Key;
                if (string.IsNullOrEmpty(hash) || pair.Value == null)
                    continue;

                string[]? children = pair.Value.Children;
                if (children != null && children.Length > 0)
                {
                    families[hash] = children;
                    continue;
                }

                NpcLevelBand[]? bands = pair.Value.Templates;
                if (bands == null || bands.Length == 0)
                    continue;

                Array.Sort(bands, static (left, right) => left.Level.CompareTo(right.Level));
                leaves[hash] = new NpcLeaf(hash, bands);
            }

            return new NpcTemplateCatalog(families, leaves, random);
        }

        public bool CanResolve(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return false;
            return CanResolveCore(hash, new HashSet<string>(StringComparer.Ordinal));
        }

        public bool TryGetLeaf(string hash, out NpcLeaf leaf)
        {
            if (string.IsNullOrEmpty(hash) || !_leaves.TryGetValue(hash, out NpcLeaf? found))
            {
                leaf = null!;
                return false;
            }

            leaf = found;
            return true;
        }

        public bool TryResolve(string hash, int? level, out MobTemplate template)
        {
            template = null!;
            if (string.IsNullOrEmpty(hash))
                return false;
            if (TryResolveLeaf(hash, out NpcLeaf leaf))
            {
                template = Materialize(leaf, level);
                return true;
            }

            return false;
        }

        bool CanResolveCore(string hash, HashSet<string> seen)
        {
            if (!seen.Add(hash))
                return false;
            if (_leaves.ContainsKey(hash))
                return true;
            if (!_families.TryGetValue(hash, out string[]? children) || children.Length == 0)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                string child = children[i];
                if (string.IsNullOrEmpty(child))
                    continue;
                if (CanResolveCore(child, seen))
                    return true;
            }

            return false;
        }

        bool TryResolveLeaf(string hash, out NpcLeaf leaf)
        {
            leaf = null!;
            if (string.IsNullOrEmpty(hash))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            string current = hash;
            while (_families.TryGetValue(current, out string[]? children) && children.Length > 0)
            {
                if (!seen.Add(current))
                    return false;

                string? pick = PickChild(children);
                if (string.IsNullOrEmpty(pick))
                    return false;

                current = pick;
            }

            return _leaves.TryGetValue(current, out leaf!);
        }

        string? PickChild(string[] children)
        {
            int viable = 0;
            for (int i = 0; i < children.Length; i++)
            {
                if (!string.IsNullOrEmpty(children[i]))
                    viable++;
            }

            if (viable == 0)
                return null;

            int index = _random.Next(viable);
            for (int i = 0; i < children.Length; i++)
            {
                if (string.IsNullOrEmpty(children[i]))
                    continue;
                if (index == 0)
                    return children[i];
                index--;
            }

            return null;
        }

        public static MobTemplate Materialize(NpcLeaf leaf, int? desiredLevel)
        {
            ArgumentNullException.ThrowIfNull(leaf);

            NpcLevelBand[] bands = leaf.Bands;
            int min = bands[0].Level;
            int max = bands[^1].Level;
            if (max < min)
                (min, max) = (max, min);

            int level = desiredLevel is > 0 ? desiredLevel.Value : min;
            if (level < min)
                level = min;
            else if (level > max)
                level = max;

            SelectBands(bands, level, out NpcLevelBand low, out NpcLevelBand high, out double t);
            NpcLevelBand nearest = t <= 0.5 ? low : high;
            Dictionary<int, int> stats = InterpolateStats(low, high, t);
            stats[(int)CharacterStat.Level] = level;
            bool placeholder = string.Equals(leaf.Hash, MobTemplate.FallbackHash, StringComparison.Ordinal);

            return new MobTemplate
            {
                Hash = leaf.Hash,
                Name = nearest.Name ?? string.Empty,
                TemplateId = nearest.TemplateId,
                HasHeadMesh = nearest.HasHeadMesh,
                Stats = stats,
                Attackable = !placeholder && nearest.Attackable,
                UnresolvedPlaceholder = placeholder,
                MinLevel = min,
                MaxLevel = max,
                Equipment = CopyPairs(nearest.Equipment),
                KnuBotId = nearest.KnuBotId,
                ItemTable = CopyLoot(nearest.LootTable)
            };
        }

        static void SelectBands(
            NpcLevelBand[] bands,
            int level,
            out NpcLevelBand low,
            out NpcLevelBand high,
            out double t)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].Level != level)
                    continue;

                low = bands[i];
                high = bands[i];
                t = 0;
                return;
            }

            int upper = bands.Length - 1;
            for (int i = 0; i < bands.Length; i++)
            {
                if (bands[i].Level >= level)
                {
                    upper = i;
                    break;
                }
            }

            if (upper <= 0)
            {
                low = bands[0];
                high = bands[0];
                t = 0;
                return;
            }

            low = bands[upper - 1];
            high = bands[upper];
            int span = high.Level - low.Level;
            t = span <= 0 ? 0 : (level - low.Level) / (double)span;
        }

        static Dictionary<int, int> InterpolateStats(NpcLevelBand low, NpcLevelBand high, double t)
        {
            if (ReferenceEquals(low, high) || t <= 0)
                return CopyStats(low.Stats);
            if (t >= 1)
                return CopyStats(high.Stats);

            var stats = new Dictionary<int, int>();
            Dictionary<int, int> lowStats = low.Stats ?? new Dictionary<int, int>();
            Dictionary<int, int> highStats = high.Stats ?? new Dictionary<int, int>();

            foreach (KeyValuePair<int, int> pair in lowStats)
            {
                int highValue = highStats.TryGetValue(pair.Key, out int found) ? found : pair.Value;
                stats[pair.Key] = Lerp(pair.Value, highValue, t);
            }

            foreach (KeyValuePair<int, int> pair in highStats)
            {
                if (stats.ContainsKey(pair.Key))
                    continue;
                stats[pair.Key] = pair.Value;
            }

            return stats;
        }

        static int Lerp(int a, int b, double t)
            => (int)Math.Round(a + ((b - a) * t), MidpointRounding.AwayFromZero);

        static Dictionary<int, int> CopyStats(Dictionary<int, int>? source)
        {
            var copy = new Dictionary<int, int>();
            if (source == null)
                return copy;

            foreach (KeyValuePair<int, int> pair in source)
                copy[pair.Key] = pair.Value;

            return copy;
        }

        static List<List<int>> CopyPairs(List<List<int>>? source)
        {
            var copy = new List<List<int>>();
            if (source == null)
                return copy;

            for (int i = 0; i < source.Count; i++)
            {
                List<int>? pair = source[i];
                copy.Add(pair == null ? new List<int>() : new List<int>(pair));
            }

            return copy;
        }

        static List<MobItemTableEntry> CopyLoot(List<MobItemTableEntry>? source)
        {
            var copy = new List<MobItemTableEntry>();
            if (source == null)
                return copy;

            for (int i = 0; i < source.Count; i++)
            {
                MobItemTableEntry? entry = source[i];
                if (entry == null)
                    continue;

                copy.Add(
                    new MobItemTableEntry
                    {
                        Hash = entry.Hash,
                        Repeats = entry.Repeats,
                        Chance = entry.Chance,
                        LevelMod = entry.LevelMod
                    });
            }

            return copy;
        }

        sealed class NpcHashDto
        {
            public string[]? Children { get; set; }

            public NpcLevelBand[]? Templates { get; set; }
        }
    }

    public sealed class NpcLevelBand
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<int, int> Stats { get; set; } = new();

        public int Level { get; set; }

        public int TemplateId { get; set; }

        public bool HasHeadMesh { get; set; }

        public bool Attackable { get; set; } = true;

        public int KnuBotId { get; set; }

        public List<List<int>> Equipment { get; set; } = new();

        [JsonPropertyName("LootTable")]
        public List<MobItemTableEntry> LootTable { get; set; } = new();
    }

    public sealed class NpcLeaf
    {
        public NpcLeaf(string hash, NpcLevelBand[] bands)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(bands);
            if (bands.Length == 0)
                throw new ArgumentException("A leaf requires at least one template band.", nameof(bands));

            Hash = hash;
            Bands = bands;
        }

        public string Hash { get; }

        public NpcLevelBand[] Bands { get; }

        public int MinLevel => Bands[0].Level;

        public int MaxLevel => Bands[^1].Level;
    }
}
