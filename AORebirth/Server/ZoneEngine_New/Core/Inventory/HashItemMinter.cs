namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using ZoneEngine_New.Core.GameData;

    /// <summary>
    /// Shared hash-to-item mint for loot, vendors, and other hash catalogs.
    /// </summary>
    public sealed class HashItemMinter
    {
        readonly IGameData _gameData;
        readonly IItemTemplateCatalog _catalog;
        readonly IItemBuilder _items;
        readonly ConcurrentDictionary<string, QualitySpan[]> _qualitySpans = new(StringComparer.Ordinal);

        public HashItemMinter(IGameData gameData, IItemTemplateCatalog catalog, IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(items);
            _gameData = gameData;
            _catalog = catalog;
            _items = items;
        }

        /// <summary>
        /// Rolls <paramref name="hash"/> into an item carrying a freshly allocated instance id.
        /// </summary>
        public bool TryMint(string hash, int desiredQuality, ItemSource source, out Item item)
        {
            item = null!;
            if (!TryRollIds(hash, desiredQuality, out int lowId, out int highId, out int quality))
                return false;

            item = _items.CreateWithNewInstance(lowId, highId, quality, source);
            return true;
        }

        /// <summary>
        /// Resolves a hash to a low/high template pair and the clamped quality without creating an
        /// <see cref="Item"/>. Shops stock unminted ids and only build items at trade close.
        /// </summary>
        public bool TryRollIds(string hash, int desiredQuality, out int lowId, out int highId, out int quality)
        {
            lowId = 0;
            highId = 0;
            quality = 0;
            if (!_gameData.TryResolveHashInstance(hash, out HashInstance instance))
                return false;

            return TryRollIdsFor(instance, desiredQuality, out lowId, out highId, out quality);
        }

        /// <summary>
        /// Same as <see cref="TryRollIds"/> for an already-resolved leaf, skipping the category descent.
        /// </summary>
        public bool TryRollIdsFor(
            HashInstance instance,
            int desiredQuality,
            out int lowId,
            out int highId,
            out int quality)
        {
            ArgumentNullException.ThrowIfNull(instance);

            quality = HashItemCatalog.ClampQuality(instance, desiredQuality);
            return HashItemCatalog.TrySelectIds(instance, quality, CatalogQuality, out lowId, out highId);
        }

        /// <summary>
        /// Mints every item one loot roll or world spawn of <paramref name="hash"/> should create.
        /// Appends to <paramref name="into"/>. A SpawnAll parent mints every branch; any other hash mints one.
        /// </summary>
        public void MintSpawns(string hash, int desiredQuality, ItemSource source, List<Item> into)
        {
            ArgumentNullException.ThrowIfNull(into);
            if (string.IsNullOrEmpty(hash))
                return;

            // A random category pick must land on a family whose QL range covers the roll; otherwise the
            // builder clamps the roll up to the family's minimum (a level 1 mob dropping a QL 15 item).
            int rollQuality = desiredQuality < 1 ? 1 : desiredQuality;
            var instances = new List<HashInstance>();
            _gameData.CollectHashSpawns(hash, instances, child => CoversQuality(child, rollQuality));
            for (int i = 0; i < instances.Count; i++)
            {
                if (!TryRollIdsFor(instances[i], desiredQuality, out int lowId, out int highId, out int quality))
                    continue;

                into.Add(_items.CreateWithNewInstance(lowId, highId, quality, source));
            }
        }

        /// <summary>Every leaf item family reachable from <paramref name="hash"/>.</summary>
        public void CollectLeafInstances(string hash, List<HashInstance> into)
            => _gameData.CollectHashLeafInstances(hash, into);

        public Item CreateWithNewInstance(int lowId, int highId, int quality, ItemSource source)
            => _items.CreateWithNewInstance(lowId, highId, quality, source);

        public Item Create(int lowId, int highId, int quality, ItemSource source)
            => _items.Create(lowId, highId, quality, source);

        /// <summary>
        /// True when some leaf family reachable from <paramref name="hash"/> can be minted at
        /// <paramref name="quality"/> without clamping: a QL-range family must span it, and a
        /// single fixed-QL template must not exceed it.
        /// </summary>
        bool CoversQuality(string hash, int quality)
        {
            if (!_qualitySpans.TryGetValue(hash, out QualitySpan[]? spans))
                spans = GetQualitySpans(hash, new HashSet<string>(StringComparer.Ordinal));

            for (int i = 0; i < spans.Length; i++)
            {
                if (quality >= spans[i].Min && quality <= spans[i].Max)
                    return true;
            }

            return false;
        }

        QualitySpan[] GetQualitySpans(string hash, HashSet<string> visiting)
        {
            if (_qualitySpans.TryGetValue(hash, out QualitySpan[]? cached))
                return cached;
            if (!visiting.Add(hash))
                return Array.Empty<QualitySpan>();

            QualitySpan[] spans;
            if (_gameData.TryGetHashInstance(hash, out HashInstance instance))
            {
                spans = new[] { LeafSpan(instance) };
            }
            else if (_gameData.TryGetHashTemplate(hash, out IReadOnlyList<string> children))
            {
                var all = new List<QualitySpan>();
                for (int i = 0; i < children.Count; i++)
                {
                    if (!string.IsNullOrEmpty(children[i]))
                        all.AddRange(GetQualitySpans(children[i], visiting));
                }

                spans = Merge(all);
            }
            else
            {
                spans = Array.Empty<QualitySpan>();
            }

            visiting.Remove(hash);
            _qualitySpans.TryAdd(hash, spans);
            return spans;
        }

        QualitySpan LeafSpan(HashInstance instance)
        {
            int min = int.MaxValue;
            int max = int.MinValue;
            int count = 0;
            int[] ids = instance.TemplateIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] <= 0)
                    continue;

                int ql = CatalogQuality(ids[i]);
                min = Math.Min(min, ql);
                max = Math.Max(max, ql);
                count++;
            }

            if (count == 0)
                return new QualitySpan(int.MaxValue, int.MinValue);

            // A fixed-QL item has no range to violate from above.
            return min == max ? new QualitySpan(min, int.MaxValue) : new QualitySpan(min, max);
        }

        static QualitySpan[] Merge(List<QualitySpan> spans)
        {
            if (spans.Count == 0)
                return Array.Empty<QualitySpan>();

            spans.Sort((a, b) => a.Min.CompareTo(b.Min));
            var merged = new List<QualitySpan> { spans[0] };
            for (int i = 1; i < spans.Count; i++)
            {
                QualitySpan last = merged[^1];
                if (spans[i].Min <= last.Max || (last.Max != int.MaxValue && spans[i].Min == last.Max + 1))
                    merged[^1] = new QualitySpan(last.Min, Math.Max(last.Max, spans[i].Max));
                else
                    merged.Add(spans[i]);
            }

            return merged.ToArray();
        }

        readonly record struct QualitySpan(int Min, int Max);

        int CatalogQuality(int aoid)
        {
            if (_catalog.TryGet(aoid, out ItemTemplate template) && template.Quality > 0)
                return template.Quality;

            return 1;
        }
    }
}
