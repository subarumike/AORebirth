namespace ZoneEngine_New.Core.Inventory
{
    using System;
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

        /// <summary>Every leaf item family reachable from <paramref name="hash"/>.</summary>
        public void CollectLeafInstances(string hash, List<HashInstance> into)
            => _gameData.CollectHashLeafInstances(hash, into);

        public Item CreateWithNewInstance(int lowId, int highId, int quality, ItemSource source)
            => _items.CreateWithNewInstance(lowId, highId, quality, source);

        int CatalogQuality(int aoid)
        {
            if (_catalog.TryGet(aoid, out ItemTemplate template) && template.Quality > 0)
                return template.Quality;

            return 1;
        }
    }
}
