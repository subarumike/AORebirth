namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Inventory.Dat;
    using ZoneEngine_New.Core.Logging;

    public sealed class ItemTemplateCatalog : IItemTemplateCatalog
    {
        private readonly Dictionary<int, ItemTemplate> _templates;
        private readonly IZoneLogger _logger;

        public ItemTemplateCatalog(IItemNameRepository names, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(names);
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
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
            string? path = ResolveItemsDatPath();
            if (path == null)
            {
                _logger.Warn("items.dat not found; catalog will use name stubs only");
                return;
            }

            try
            {
                List<DatItemTemplate> loaded = ItemsDatReader.Read(path);
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
                        path));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load items.dat from {0}; continuing with name stubs",
                        path));
            }
        }

        private static string? ResolveItemsDatPath()
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            [
                Path.Combine(baseDir, "items.dat"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "items.dat")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Datafiles", "items.dat")),
                Path.GetFullPath(
                    Path.Combine(baseDir, "..", "..", "..", "..", "..", "AORebirth", "Datafiles", "items.dat"))
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
