namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory.Dat;
    using ZoneEngine_New.Core.Logging;

    public sealed class ItemTemplateCatalog : IItemTemplateCatalog
    {
        private readonly Dictionary<int, ItemTemplate> _templates;
        private readonly IZoneLogger _logger;
        private readonly string _itemsDatPath;

        /// <summary>GameData items.dat is the only source of item templates, names included.</summary>
        public ItemTemplateCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _itemsDatPath = Path.Combine(gameData.RootPath, GameDataPaths.ItemsFileName);
            _templates = new Dictionary<int, ItemTemplate>(capacity: 130000);

            TryLoadItemsDat();

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ItemTemplateCatalog ready with {0} templates",
                    _templates.Count));
        }

        public bool TryGet(int aoid, out ItemTemplate template)
            => _templates.TryGetValue(aoid, out template!);

        public ItemTemplate Require(int aoid)
        {
            if (TryGet(aoid, out ItemTemplate template))
                return template;

            throw new KeyNotFoundException(
                string.Format(CultureInfo.InvariantCulture, "Item template {0} not found", aoid));
        }

        private void TryLoadItemsDat()
        {
            if (!File.Exists(_itemsDatPath))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData items.dat not found at {0}; item catalog is empty",
                        _itemsDatPath));
                return;
            }

            try
            {
                List<DatItemTemplate> loaded = ItemsDatReader.Read(_itemsDatPath);
                int merged = 0;
                foreach (DatItemTemplate dat in loaded)
                {
                    _templates[dat.ID] = DatItemMapper.ToTemplate(dat, dat.Name ?? string.Empty);
                    merged++;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Loaded {0} item templates from {1}",
                        merged,
                        _itemsDatPath));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load GameData items.dat from {0}; item catalog is empty",
                        _itemsDatPath));
            }
        }
    }
}
