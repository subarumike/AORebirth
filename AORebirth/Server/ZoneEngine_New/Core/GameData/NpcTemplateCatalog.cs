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
    /// Optional <c>SpawnAll</c> on a parent spawns every child branch instead of one random child.
    /// </summary>
    public sealed class NpcTemplateCatalog
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        readonly Dictionary<string, string[]> _families;
        readonly Dictionary<string, NpcLeaf> _leaves;
        readonly HashSet<string> _spawnAll;
        readonly Random _random;

        public NpcTemplateCatalog(
            Dictionary<string, string[]> families,
            Dictionary<string, NpcLeaf> leaves,
            Random? random = null,
            HashSet<string>? spawnAll = null)
        {
            ArgumentNullException.ThrowIfNull(families);
            ArgumentNullException.ThrowIfNull(leaves);
            _families = families;
            _leaves = leaves;
            _spawnAll = spawnAll ?? new HashSet<string>(StringComparer.Ordinal);
            _random = random ?? Random.Shared;
        }

        public int FamilyCount => _families.Count;

        public int LeafCount => _leaves.Count;

        public static NpcTemplateCatalog Parse(string? json, Random? random = null)
        {
            var families = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var leaves = new Dictionary<string, NpcLeaf>(StringComparer.Ordinal);
            var spawnAll = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return new NpcTemplateCatalog(families, leaves, random, spawnAll);

            Dictionary<string, NpcHashDto>? records =
                JsonSerializer.Deserialize<Dictionary<string, NpcHashDto>>(json, JsonOptions);
            if (records == null)
                return new NpcTemplateCatalog(families, leaves, random, spawnAll);

            foreach (KeyValuePair<string, NpcHashDto> pair in records)
            {
                string hash = pair.Key;
                if (string.IsNullOrEmpty(hash) || pair.Value == null)
                    continue;

                string[]? children = pair.Value.Children;
                if (children != null && children.Length > 0)
                {
                    families[hash] = children;
                    if (pair.Value.SpawnAll)
                        spawnAll.Add(hash);
                    continue;
                }

                NpcLevelBand[]? bands = pair.Value.Templates;
                if (bands == null || bands.Length == 0)
                    continue;

                Array.Sort(bands, static (left, right) => left.Level.CompareTo(right.Level));
                leaves[hash] = new NpcLeaf(hash, bands);
            }

            return new NpcTemplateCatalog(families, leaves, random, spawnAll);
        }

        public bool CanResolve(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return false;
            if (CanResolveCore(hash, new HashSet<string>(StringComparer.Ordinal)))
                return true;

            return CanUseFallback(hash);
        }

        /// <summary>True when <paramref name="hash"/> resolves to an authored template, ignoring the placeholder fallback.</summary>
        public bool HasTemplate(string hash)
            => !string.IsNullOrEmpty(hash) && CanResolveCore(hash, new HashSet<string>(StringComparer.Ordinal));

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
            if (TryResolveLeaf(hash, level, out NpcLeaf leaf))
            {
                template = Materialize(leaf, level);
                return true;
            }

            if (!CanUseFallback(hash) || !_leaves.TryGetValue(MobTemplate.FallbackHash, out NpcLeaf fallback))
                return false;

            template = Materialize(fallback, level);
            return true;
        }

        bool CanUseFallback(string hash)
        {
            if (string.Equals(hash, MobTemplate.FallbackHash, StringComparison.Ordinal))
                return false;
            if (_leaves.ContainsKey(hash) || _families.ContainsKey(hash))
                return false;

            return _leaves.ContainsKey(MobTemplate.FallbackHash);
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

        bool TryResolveLeaf(string hash, int? level, out NpcLeaf leaf)
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

                string? pick = PickChild(children, level);
                if (string.IsNullOrEmpty(pick))
                    return false;

                current = pick;
            }

            return _leaves.TryGetValue(current, out leaf!);
        }

        /// <summary>
        /// Appends the NPCs one spawn of <paramref name="hash"/> should create.
        /// A parent with <c>SpawnAll</c> contributes every child branch. Any other parent contributes one random child.
        /// An unknown hash contributes the placeholder fallback when that leaf exists.
        /// </summary>
        public void CollectSpawns(string hash, int? level, List<MobTemplate> into)
        {
            ArgumentNullException.ThrowIfNull(into);
            if (string.IsNullOrEmpty(hash))
                return;

            int start = into.Count;
            CollectSpawns(hash, level, into, new HashSet<string>(StringComparer.Ordinal));
            if (into.Count != start)
                return;

            if (CanUseFallback(hash) && _leaves.TryGetValue(MobTemplate.FallbackHash, out NpcLeaf fallback))
                into.Add(Materialize(fallback, level));
        }

        void CollectSpawns(string hash, int? level, List<MobTemplate> into, HashSet<string> trail)
        {
            if (string.IsNullOrEmpty(hash) || !trail.Add(hash))
                return;

            try
            {
                if (_families.TryGetValue(hash, out string[]? children) && children.Length > 0)
                {
                    if (_spawnAll.Contains(hash))
                    {
                        for (int i = 0; i < children.Length; i++)
                            CollectSpawns(children[i], level, into, trail);
                        return;
                    }

                    string? pick = PickChild(children, level);
                    if (!string.IsNullOrEmpty(pick))
                        CollectSpawns(pick, level, into, trail);
                    return;
                }

                if (_leaves.TryGetValue(hash, out NpcLeaf? leaf))
                    into.Add(Materialize(leaf, level));
            }
            finally
            {
                trail.Remove(hash);
            }
        }

        /// <summary>
        /// Random child. With a requested level, only children whose template range covers it are
        /// eligible; when none do, the children whose range lies closest to the level are used instead.
        /// </summary>
        string? PickChild(string[] children, int? level)
        {
            if (level is not > 0)
                return PickAny(children);

            var eligible = new List<string>(children.Length);
            int bestDistance = int.MaxValue;
            for (int i = 0; i < children.Length; i++)
            {
                string child = children[i];
                if (string.IsNullOrEmpty(child))
                    continue;
                if (!TryGetLevelRange(child, new HashSet<string>(StringComparer.Ordinal), out int min, out int max))
                    continue;

                int distance = level.Value < min ? min - level.Value
                    : level.Value > max ? level.Value - max
                    : 0;
                if (distance > bestDistance)
                    continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    eligible.Clear();
                }

                eligible.Add(child);
            }

            if (eligible.Count == 0)
                return PickAny(children);

            return eligible[_random.Next(eligible.Count)];
        }

        string? PickAny(string[] children)
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

        /// <summary>Union of the template level ranges of every leaf reachable from <paramref name="hash"/>.</summary>
        bool TryGetLevelRange(string hash, HashSet<string> seen, out int min, out int max)
        {
            min = int.MaxValue;
            max = int.MinValue;
            if (string.IsNullOrEmpty(hash) || !seen.Add(hash))
                return false;

            if (_leaves.TryGetValue(hash, out NpcLeaf? leaf))
            {
                seen.Remove(hash);
                min = Math.Min(leaf.MinLevel, leaf.MaxLevel);
                max = Math.Max(leaf.MinLevel, leaf.MaxLevel);
                return true;
            }

            if (!_families.TryGetValue(hash, out string[]? children))
            {
                seen.Remove(hash);
                return false;
            }

            bool found = false;
            for (int i = 0; i < children.Length; i++)
            {
                if (!TryGetLevelRange(children[i], seen, out int childMin, out int childMax))
                    continue;

                min = Math.Min(min, childMin);
                max = Math.Max(max, childMax);
                found = true;
            }

            seen.Remove(hash);
            return found;
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
            if (nearest.CharacterFlags.HasValue)
                stats[(int)CharacterStat.Flags] = nearest.CharacterFlags.Value;

            stats[(int)CharacterStat.Level] = level;
            bool isFallback = string.Equals(leaf.Hash, MobTemplate.FallbackHash, StringComparison.Ordinal);

            return new MobTemplate
            {
                Hash = leaf.Hash,
                Name = nearest.Name ?? string.Empty,
                TemplateId = nearest.TemplateId,
                HasHeadMesh = nearest.HasHeadMesh,
                Stats = stats,
                Attackable = !isFallback && nearest.Attackable,
                MinLevel = min,
                MaxLevel = max,
                UnresolvedPlaceholder = isFallback,
                Equipment = CopyPairs(nearest.Equipment),
                ExtendedTextureOverrideData = CopyBytes(nearest.ExtendedTextureOverrideData),
                CorpseFullUpdateTemplate = CopyCorpseFullUpdateTemplate(nearest.CorpseFullUpdateTemplate),
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

        static byte[] CopyBytes(byte[]? source)
        {
            if (source == null || source.Length == 0)
                return [];

            var copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }

        static MobCorpseFullUpdateTemplate? CopyCorpseFullUpdateTemplate(MobCorpseFullUpdateTemplate? source)
        {
            if (source == null || source.PacketTemplate.Length == 0)
                return null;

            return new MobCorpseFullUpdateTemplate
            {
                PacketTemplate = CopyBytes(source.PacketTemplate),
                MessageId = source.MessageId,
                MessageIdOffset = source.MessageIdOffset,
                PacketLengthOffset = source.PacketLengthOffset,
                SenderInstanceOffset = source.SenderInstanceOffset,
                ReceiverInstanceOffset = source.ReceiverInstanceOffset,
                CorpseInstanceOffset = source.CorpseInstanceOffset,
                PositionXOffset = source.PositionXOffset,
                PositionYOffset = source.PositionYOffset,
                PositionZOffset = source.PositionZOffset,
                PlayfieldIdOffset = source.PlayfieldIdOffset,
                DeadNpcInstanceOffset = source.DeadNpcInstanceOffset,
                CatMeshOffset = source.CatMeshOffset,
                CashOffset = source.CashOffset,
                MonsterDataOffset = source.MonsterDataOffset,
                TailDeadNpcInstanceOffset = source.TailDeadNpcInstanceOffset
            };
        }

        sealed class NpcHashDto
        {
            public string[]? Children { get; set; }

            public NpcLevelBand[]? Templates { get; set; }

            public bool SpawnAll { get; set; }
        }
    }

    public sealed class NpcLevelBand
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<int, int> Stats { get; set; } = new();

        public int? CharacterFlags { get; set; }

        public int Level { get; set; }

        public int TemplateId { get; set; }

        public bool HasHeadMesh { get; set; }

        public bool Attackable { get; set; } = true;

        public int KnuBotId { get; set; }

        public List<List<int>> Equipment { get; set; } = new();

        public byte[] ExtendedTextureOverrideData { get; set; } = [];

        public MobCorpseFullUpdateTemplate? CorpseFullUpdateTemplate { get; set; }

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
