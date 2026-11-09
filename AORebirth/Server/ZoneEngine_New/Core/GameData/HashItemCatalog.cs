namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public readonly record struct EquipmentLootMember(HashInstance Family, int Weight, int MinCenter, int MaxCenter);

    /// <summary>
    /// ItemTemplates.json: a hash with Templates is a leaf item family; a hash with Children is a category.
    /// Parents never have template ids. Leaves never have children.
    /// Lookup is category-first: a hash with children picks a random child Hash, then repeats.
    /// Optional <c>SpawnAll</c> on a parent takes every child branch instead of one random child.
    /// </summary>
    public sealed class HashItemCatalog
    {
        // Gameplay traverses category paths recursively, so a new graph must fit a bounded stack.
        const int MaxCategoryDepth = 256;

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        static readonly JsonSerializerOptions VerifiedJsonOptions = new(JsonOptions)
        {
            AllowDuplicateProperties = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        readonly Dictionary<string, string[]> _categories;
        readonly Dictionary<string, HashInstance> _instances;
        readonly Dictionary<(int LowId, int HighId), string?> _assignedHashes;
        readonly HashSet<string> _spawnAll;
        readonly Dictionary<string, IReadOnlyList<EquipmentLootMember>> _equipmentLoot = new(StringComparer.Ordinal);
        readonly Random _random;
        Dictionary<string, HashInstance> _equipmentOnly = new(StringComparer.Ordinal);
        readonly Dictionary<string, string> _invalidRows = new(StringComparer.Ordinal);

        internal string? Sha256 { get; set; }

        public HashItemCatalog(
            Dictionary<string, string[]> categories,
            Dictionary<string, HashInstance> instances,
            Random? random = null,
            HashSet<string>? spawnAll = null)
        {
            ArgumentNullException.ThrowIfNull(categories);
            ArgumentNullException.ThrowIfNull(instances);
            _categories = categories;
            _instances = instances;
            _assignedHashes = BuildAssignedHashes(instances);
            _spawnAll = spawnAll ?? new HashSet<string>(StringComparer.Ordinal);
            _random = random ?? Random.Shared;
        }

        public int CategoryCount => _categories.Count;

        public int InstanceCount => _instances.Count;

        /// <summary>Every leaf family, independent of category, NPC loot, or mission reward selection.</summary>
        public IReadOnlyCollection<HashInstance> Instances => _instances.Values;

        public static HashItemCatalog Parse(string? json, Random? random = null)
            => ParseRows(string.IsNullOrWhiteSpace(json) ? null
                : JsonSerializer.Deserialize<Dictionary<string, HashItemRow>>(json, JsonOptions), random);

        static HashItemCatalog ParseRows(Dictionary<string, HashItemRow>? records, Random? random)
        {
            var categories = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var instances = new Dictionary<string, HashInstance>(StringComparer.Ordinal);
            var equipmentOnly = new Dictionary<string, HashInstance>(StringComparer.Ordinal);
            var spawnAll = new HashSet<string>(StringComparer.Ordinal);
            if (records == null)
                return new HashItemCatalog(categories, instances, random, spawnAll);

            foreach (KeyValuePair<string, HashItemRow> pair in records)
            {
                string hash = pair.Key;
                if (string.IsNullOrEmpty(hash) || pair.Value == null)
                    continue;

                if (pair.Value.EquipmentLoot != null)
                    continue;

                string[]? children = pair.Value.Children;
                if (pair.Value.EquipmentLootOnly && (children is { Length: > 0 } || pair.Value.SpawnAll
                    || pair.Value.Templates is not { Length: > 0 } || Array.Exists(pair.Value.Templates, id => id <= 0)))
                    throw new FormatException("Invalid equipment-only item family: " + hash);

                if (children != null && children.Length > 0)
                {
                    categories[hash] = children;
                    if (pair.Value.SpawnAll)
                        spawnAll.Add(hash);
                    continue;
                }

                int[]? templates = pair.Value.Templates;
                if (templates == null || templates.Length == 0)
                    continue;

                List<int> ids = new(templates.Length);
                foreach (int id in templates)
                {
                    if (id > 0)
                        ids.Add(id);
                }

                if (ids.Count == 0)
                    continue;

                (pair.Value.EquipmentLootOnly ? equipmentOnly : instances).Add(hash, new HashInstance(hash, ids.ToArray()));
            }

            var catalog = new HashItemCatalog(categories, instances, random, spawnAll) { _equipmentOnly = equipmentOnly };
            foreach ((string hash, HashItemRow row) in records)
            {
                if (string.IsNullOrWhiteSpace(hash) || row is null
                    || (row.Children is { Length: > 0 } childrenIds
                        && (row.Templates is { Length: > 0 } || childrenIds.Distinct(StringComparer.Ordinal).Count() != childrenIds.Length))
                    || (row.Children is not { Length: > 0 } && (row.Templates is not { Length: > 0 } || row.SpawnAll))
                    || (row.Templates is { } templateIds && Array.Exists(templateIds, id => id <= 0)))
                    catalog._invalidRows[hash] = JsonSerializer.Serialize(row, JsonOptions);

                if (row?.EquipmentLoot is not { } policy)
                    continue;
                if (string.IsNullOrEmpty(hash) || row.EquipmentLootOnly || row.SpawnAll || row.Templates is { Length: > 0 }
                    || row.Children is not { Length: > 0 } children || children.Length != policy.Count)
                    throw new FormatException("Invalid equipment loot pool: " + hash);

                var members = new List<EquipmentLootMember>(children.Length);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string child in children)
                {
                    if (string.IsNullOrEmpty(child) || !seen.Add(child) || !policy.TryGetValue(child, out EquipmentLootSettings? settings)
                        || settings == null || settings.Weight <= 0 || settings.MinCenter <= 0 || settings.MaxCenter < settings.MinCenter
                        || (!instances.TryGetValue(child, out HashInstance? family) && !equipmentOnly.TryGetValue(child, out family)))
                        throw new FormatException($"Invalid equipment loot member {child} in {hash}");

                    members.Add(new EquipmentLootMember(family, settings.Weight, settings.MinCenter, settings.MaxCenter));
                }

                catalog._equipmentLoot.Add(hash, members.AsReadOnly());
            }

            return catalog;
        }

        internal static HashItemCatalog ParseVerified(string json, HashItemCatalog previous, Func<int, bool> templateExists)
        {
            Dictionary<string, HashItemRow>? records = JsonSerializer.Deserialize<Dictionary<string, HashItemRow>>(json, VerifiedJsonOptions);
            if (records is not { Count: > 0 })
                throw new FormatException("ItemTemplates must contain hash definitions.");

            HashItemCatalog next = ParseRows(records, null);
            foreach ((string hash, string row) in next._invalidRows)
            {
                if (!previous._invalidRows.TryGetValue(hash, out string? prior) || row != prior)
                    throw new FormatException("Invalid hash definition: " + hash);
            }

            // Other GameData can reference existing hashes independently of this catalog.
            RequireExistingHashes(previous._categories, next._categories);
            RequireExistingHashes(previous._instances, next._instances);
            RequireExistingHashes(previous._equipmentLoot, next._equipmentLoot);
            foreach ((string parent, string[] children) in next._categories)
            {
                foreach (string child in children)
                {
                    if (!string.IsNullOrWhiteSpace(child) && next.HasOrdinaryHash(child))
                        continue;
                    // Shipped content contains dangling edges. Only keep an unchanged existing finding.
                    if (previous._categories.TryGetValue(parent, out string[]? oldChildren)
                        && children.SequenceEqual(oldChildren) && !previous.HasOrdinaryHash(child)
                        && previous._spawnAll.Contains(parent) == next._spawnAll.Contains(parent))
                        continue;
                    throw new FormatException($"Missing item hash {child} referenced by {parent}.");
                }
            }
            next.ValidateAcyclic();
            foreach ((string hash, string[] children) in next._categories)
            {
                if ((!previous._categories.TryGetValue(hash, out string[]? prior) || !children.SequenceEqual(prior))
                    && !next.CanResolveItem(hash))
                    throw new FormatException("Item category does not resolve to an item family: " + hash);
            }
            ValidateIds(next._instances, previous._instances, templateExists);
            ValidateIds(next._equipmentOnly, previous._equipmentOnly, templateExists);
            return next;
        }

        static void RequireExistingHashes<T>(Dictionary<string, T> previous, Dictionary<string, T> next)
        {
            foreach (string hash in previous.Keys)
                if (!next.ContainsKey(hash))
                    throw new FormatException("Cannot remove or change the type of an existing hash: " + hash);
        }

        bool HasOrdinaryHash(string? hash)
            => !string.IsNullOrWhiteSpace(hash) && (_categories.ContainsKey(hash) || _instances.ContainsKey(hash));

        static void ValidateIds(Dictionary<string, HashInstance> next, Dictionary<string, HashInstance> previous,
            Func<int, bool> templateExists)
        {
            foreach ((string hash, HashInstance family) in next)
            {
                foreach (int id in family.TemplateIds)
                {
                    if (!templateExists(id) && !(previous.TryGetValue(hash, out HashInstance? prior)
                        && prior.TemplateIds.AsSpan().SequenceEqual(family.TemplateIds)))
                        throw new FormatException($"Missing item template {id} in {hash}.");
                }
            }
        }

        void ValidateAcyclic()
        {
            var incoming = _categories.Keys.ToDictionary(hash => hash, _ => 0, StringComparer.Ordinal);
            var depths = _categories.Keys.ToDictionary(hash => hash, _ => 1, StringComparer.Ordinal);
            foreach (string[] children in _categories.Values)
                foreach (string child in children)
                    if (child != null && incoming.ContainsKey(child))
                        incoming[child]++;
            var pending = new Queue<string>(incoming.Where(entry => entry.Value == 0).Select(entry => entry.Key));
            int visited = 0;
            while (pending.TryDequeue(out string? parent))
            {
                visited++;
                foreach (string child in _categories[parent])
                {
                    if (child is null || !incoming.ContainsKey(child))
                        continue;
                    depths[child] = Math.Max(depths[child], depths[parent] + 1);
                    if (depths[child] > MaxCategoryDepth)
                        throw new FormatException($"Item hash categories exceed {MaxCategoryDepth} levels.");
                    if (--incoming[child] == 0)
                        pending.Enqueue(child);
                }
            }
            if (visited != _categories.Count)
                throw new FormatException("Item hash categories contain a cycle.");
        }

        public IReadOnlyList<EquipmentLootMember>? GetEquipmentLootMembers(string hash)
            => _equipmentLoot.GetValueOrDefault(hash);

        public bool TryGetCategory(string hash, out IReadOnlyList<string> childHashes)
        {
            if (string.IsNullOrEmpty(hash) || !_categories.TryGetValue(hash, out string[]? children))
            {
                childHashes = Array.Empty<string>();
                return false;
            }

            childHashes = children;
            return true;
        }

        /// <summary>True when <paramref name="hash"/> is a category flagged <c>SpawnAll</c>.</summary>
        public bool IsSpawnAll(string hash)
            => !string.IsNullOrEmpty(hash) && _spawnAll.Contains(hash);

        public bool TryGetInstance(string hash, out HashInstance instance)
        {
            if (string.IsNullOrEmpty(hash) || !_instances.TryGetValue(hash, out HashInstance? found))
            {
                instance = null!;
                return false;
            }

            instance = found;
            return true;
        }

        /// <summary>
        /// Resolves only an exact correlation assignment. A one-template leaf represents LowId=HighId;
        /// a two-template leaf represents that exact ordered pair. Category hashes and multi-band item
        /// families are not treated as stock-row correlations.
        /// </summary>
        public bool TryGetAssignedHash(int lowId, int highId, out string hash)
        {
            if (_assignedHashes.TryGetValue((lowId, highId), out string? assigned)
                && !string.IsNullOrEmpty(assigned))
            {
                hash = assigned;
                return true;
            }

            hash = string.Empty;
            return false;
        }

        public bool TryResolveInstance(string hash, out HashInstance instance)
        {
            instance = null!;
            if (string.IsNullOrEmpty(hash))
                return false;

            HashSet<string> seen = new(StringComparer.Ordinal);
            string current = hash;
            while (_categories.TryGetValue(current, out string[]? children) && children.Length > 0)
            {
                if (!seen.Add(current))
                    return false;

                current = children[_random.Next(children.Length)];
                if (string.IsNullOrEmpty(current))
                    return false;
            }

            return _instances.TryGetValue(current, out instance!);
        }

        /// <summary>
        /// True when a leaf item family is reachable from <paramref name="hash"/>, ignoring random selection.
        /// </summary>
        public bool CanResolveItem(string hash)
            => CanResolveItem(hash, new HashSet<string>(StringComparer.Ordinal));

        /// <summary>
        /// Appends the families one loot roll or world-item spawn should create.
        /// A parent with <c>SpawnAll</c> contributes every child branch. Any other parent contributes one random child.
        /// </summary>
        public void CollectSpawns(string hash, List<HashInstance> into)
            => CollectSpawns(hash, into, canPick: null);

        /// <summary>
        /// Same as <see cref="CollectSpawns(string, List{HashInstance})"/>, but a random category pick only
        /// considers children accepted by <paramref name="canPick"/> (parent category, child hash). A category with no
        /// accepted child contributes nothing. SpawnAll branches are not filtered; random picks beneath them still are.
        /// </summary>
        public void CollectSpawns(string hash, List<HashInstance> into, Func<string, string, bool>? canPick)
        {
            ArgumentNullException.ThrowIfNull(into);
            CollectSpawns(hash, into, new HashSet<string>(StringComparer.Ordinal), canPick);
        }

        /// <summary>
        /// Appends every leaf <see cref="HashInstance"/> reachable from <paramref name="hash"/> to
        /// <paramref name="into"/>. A hash that is itself a leaf contributes just that instance.
        /// Category children with no leaf Templates entry are skipped, and each hash is visited once
        /// so a cyclic or diamond-shaped category graph terminates without duplicates.
        /// </summary>
        public void CollectLeafInstances(string hash, List<HashInstance> into)
        {
            ArgumentNullException.ThrowIfNull(into);
            if (string.IsNullOrEmpty(hash))
                return;

            HashSet<string> seen = new(StringComparer.Ordinal);
            Stack<string> pending = new();
            pending.Push(hash);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (string.IsNullOrEmpty(current) || !seen.Add(current))
                    continue;

                if (_categories.TryGetValue(current, out string[]? children) && children.Length > 0)
                {
                    for (int i = children.Length - 1; i >= 0; i--)
                        pending.Push(children[i]);

                    continue;
                }

                if (_instances.TryGetValue(current, out HashInstance? instance))
                    into.Add(instance);
            }
        }

        bool CanResolveItem(string hash, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(hash) || !seen.Add(hash))
                return false;
            if (_instances.ContainsKey(hash))
                return true;
            if (!_categories.TryGetValue(hash, out string[]? children) || children.Length == 0)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                if (CanResolveItem(children[i], seen))
                    return true;
            }

            return false;
        }

        void CollectSpawns(string hash, List<HashInstance> into, HashSet<string> trail, Func<string, string, bool>? canPick)
        {
            if (string.IsNullOrEmpty(hash) || !trail.Add(hash))
                return;

            try
            {
                if (_categories.TryGetValue(hash, out string[]? children) && children.Length > 0)
                {
                    if (_spawnAll.Contains(hash))
                    {
                        for (int i = 0; i < children.Length; i++)
                            CollectSpawns(children[i], into, trail, canPick);
                        return;
                    }

                    string? child = PickChild(hash, children, canPick);
                    if (!string.IsNullOrEmpty(child))
                        CollectSpawns(child, into, trail, canPick);
                    return;
                }

                if (_instances.TryGetValue(hash, out HashInstance? instance))
                    into.Add(instance);
            }
            finally
            {
                trail.Remove(hash);
            }
        }

        string? PickChild(string parent, string[] children, Func<string, string, bool>? canPick)
        {
            if (canPick == null)
                return children[_random.Next(children.Length)];

            int eligible = 0;
            for (int i = 0; i < children.Length; i++)
            {
                if (!string.IsNullOrEmpty(children[i]) && canPick(parent, children[i]))
                    eligible++;
            }

            if (eligible == 0)
                return null;

            int pick = _random.Next(eligible);
            for (int i = 0; i < children.Length; i++)
            {
                if (string.IsNullOrEmpty(children[i]) || !canPick(parent, children[i]))
                    continue;
                if (pick == 0)
                    return children[i];
                pick--;
            }

            return null;
        }

        public static int ClampQuality(HashInstance instance, int desiredQuality)
        {
            ArgumentNullException.ThrowIfNull(instance);
            return desiredQuality < 1 ? 1 : desiredQuality;
        }

        public static bool TrySelectIds(
            HashInstance instance,
            int quality,
            Func<int, int> catalogQuality,
            out int lowId,
            out int highId)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ArgumentNullException.ThrowIfNull(catalogQuality);

            int[] ids = instance.TemplateIds;
            if (ids.Length == 0)
            {
                lowId = 0;
                highId = 0;
                return false;
            }

            if (ids.Length == 1)
            {
                lowId = ids[0];
                highId = ids[0];
                return ids[0] > 0;
            }

            Band[] bands = new Band[ids.Length];
            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                if (id <= 0)
                    continue;

                bands[count] = new Band(id, catalogQuality(id));
                count++;
            }

            if (count == 0)
            {
                lowId = 0;
                highId = 0;
                return false;
            }

            if (count == 1)
            {
                lowId = bands[0].Id;
                highId = bands[0].Id;
                return true;
            }

            Array.Sort(bands, 0, count, BandComparer.Instance);

            for (int i = 0; i < count - 1; i++)
            {
                if (quality >= bands[i].Quality && quality <= bands[i + 1].Quality)
                {
                    lowId = bands[i].Id;
                    highId = bands[i + 1].Id;
                    return true;
                }
            }

            if (quality < bands[0].Quality)
            {
                lowId = bands[0].Id;
                highId = bands[1].Id;
                return true;
            }

            lowId = bands[count - 2].Id;
            highId = bands[count - 1].Id;
            return true;
        }

        static Dictionary<(int LowId, int HighId), string?> BuildAssignedHashes(
            Dictionary<string, HashInstance> instances)
        {
            var result = new Dictionary<(int LowId, int HighId), string?>();
            foreach (KeyValuePair<string, HashInstance> pair in instances)
            {
                int[] ids = pair.Value.TemplateIds;
                (int LowId, int HighId) itemPair;
                if (ids.Length == 1)
                    itemPair = (ids[0], ids[0]);
                else if (ids.Length == 2)
                    itemPair = (ids[0], ids[1]);
                else
                    continue;

                if (result.TryGetValue(itemPair, out string? existing)
                    && !string.Equals(existing, pair.Key, StringComparison.Ordinal))
                    result[itemPair] = null;
                else
                    result[itemPair] = pair.Key;
            }

            return result;
        }

        readonly struct Band
        {
            public Band(int id, int quality)
            {
                Id = id;
                Quality = quality;
            }

            public int Id { get; }

            public int Quality { get; }
        }

        sealed class BandComparer : IComparer<Band>
        {
            public static readonly BandComparer Instance = new();

            public int Compare(Band x, Band y)
                => x.Quality.CompareTo(y.Quality);
        }

        sealed class HashItemRow
        {
            public string[]? Children { get; set; }

            public int[]? Templates { get; set; }

            public bool SpawnAll { get; set; }

            public bool EquipmentLootOnly { get; set; }

            public Dictionary<string, EquipmentLootSettings>? EquipmentLoot { get; set; }
        }

        sealed class EquipmentLootSettings
        {
            public int Weight { get; set; }

            public int MinCenter { get; set; }

            public int MaxCenter { get; set; }
        }
    }
}
