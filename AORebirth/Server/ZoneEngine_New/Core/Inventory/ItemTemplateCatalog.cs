namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory.Dat;
    using ZoneEngine_New.Core.Logging;

    public sealed class ItemTemplateCatalog : IItemTemplateCatalog
    {
        private readonly Dictionary<int, ItemTemplate> _templates;
        private readonly IZoneLogger _logger;
        private readonly string _itemsDatPath;
        private readonly string _itemEventsDatPath;

        public ItemTemplateCatalog(IItemNameRepository names, IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(names);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _itemsDatPath = Path.Combine(gameData.RootPath, GameDataPaths.ItemsFileName);
            _itemEventsDatPath = Path.Combine(gameData.RootPath, GameDataPaths.ItemEventsFileName);
            _templates = new Dictionary<int, ItemTemplate>(capacity: 130000);

            IReadOnlyDictionary<int, string> nameMap = names.GetAllNames();
            TryLoadItemsDat(nameMap);

            foreach (KeyValuePair<int, string> pair in nameMap)
            {
                if (_templates.ContainsKey(pair.Key))
                    continue;

                _templates[pair.Key] = new ItemTemplate
                {
                    Id = pair.Key,
                    Name = pair.Value ?? string.Empty,
                    Quality = 1
                };
            }

            TryLoadItemEventsDat();

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

        private void TryLoadItemsDat(IReadOnlyDictionary<int, string> nameMap)
        {
            if (!File.Exists(_itemsDatPath))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData items.dat not found at {0}; catalog will use name stubs only",
                        _itemsDatPath));
                return;
            }

            try
            {
                List<DatItemTemplate> loaded = ItemsDatReader.Read(_itemsDatPath);
                int merged = 0;
                foreach (DatItemTemplate dat in loaded)
                {
                    nameMap.TryGetValue(dat.ID, out string? name);
                    _templates[dat.ID] = DatItemMapper.ToTemplate(dat, name ?? string.Empty);
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
                        "Failed to load GameData items.dat from {0}; continuing with name stubs",
                        _itemsDatPath));
            }
        }

        /// <summary>
        /// Merges OnUse/OnWear events from the legacy companion overlay onto RDB templates.
        /// RDB-exported items.dat already embeds events; IDs absent from this overlay keep those.
        /// </summary>
        private void TryLoadItemEventsDat()
        {
            if (!File.Exists(_itemEventsDatPath))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData {0} not found at {1}; templates rely on events embedded in items.dat",
                        GameDataPaths.ItemEventsFileName,
                        _itemEventsDatPath));
                return;
            }

            try
            {
                Dictionary<int, ItemEventsDatTemplate> events = ItemEventsDatReader.Read(_itemEventsDatPath);
                int merged = 0;
                foreach (KeyValuePair<int, ItemEventsDatTemplate> pair in events)
                {
                    if (!_templates.TryGetValue(pair.Key, out ItemTemplate? template))
                        continue;

                    _templates[pair.Key] = DatItemMapper.WithEvents(template, pair.Value);
                    merged++;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Merged item events into {0} of {1} templates from {2}",
                        merged,
                        events.Count,
                        _itemEventsDatPath));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load {0} from {1}; templates rely on events embedded in items.dat",
                        GameDataPaths.ItemEventsFileName,
                        _itemEventsDatPath));
            }
        }
    }
}
