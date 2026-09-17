namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// ItemTemplates.json: a hash with Templates is a leaf item family; a hash with Children is a category.
    /// Parents never have template ids. Leaves never have children.
    /// Lookup is category-first: a hash with children picks a random child Hash, then repeats.
    /// </summary>
    public sealed class HashItemCatalog
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        readonly Dictionary<string, string[]> _categories;
        readonly Dictionary<string, HashInstance> _instances;
        readonly Random _random;

        public HashItemCatalog(
            Dictionary<string, string[]> categories,
            Dictionary<string, HashInstance> instances,
            Random? random = null)
        {
            ArgumentNullException.ThrowIfNull(categories);
            ArgumentNullException.ThrowIfNull(instances);
            _categories = categories;
            _instances = instances;
            _random = random ?? Random.Shared;
        }

        public int CategoryCount => _categories.Count;

        public int InstanceCount => _instances.Count;

        public static HashItemCatalog Parse(string? json, Random? random = null)
        {
            var categories = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var instances = new Dictionary<string, HashInstance>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return new HashItemCatalog(categories, instances, random);

            Dictionary<string, HashItemRow>? records =
                JsonSerializer.Deserialize<Dictionary<string, HashItemRow>>(json, JsonOptions);
            if (records == null)
                return new HashItemCatalog(categories, instances, random);

            foreach (KeyValuePair<string, HashItemRow> pair in records)
            {
                string hash = pair.Key;
                if (string.IsNullOrEmpty(hash) || pair.Value == null)
                    continue;

                string[]? children = pair.Value.Children;
                if (children != null && children.Length > 0)
                {
                    categories[hash] = children;
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

                instances.TryAdd(hash, new HashInstance(hash, ids.ToArray()));
            }

            return new HashItemCatalog(categories, instances, random);
        }

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
        }
    }
}
