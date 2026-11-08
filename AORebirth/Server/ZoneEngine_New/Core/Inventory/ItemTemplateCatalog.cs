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

        /// <summary>GameData items.dat is the only source of item templates, names included.</summary>
        public ItemTemplateCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);
            string path = Path.Combine(gameData.RootPath, GameDataPaths.ItemsFileName);
            _templates = LoadItemsDat(path);

            logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Loaded {0} item templates from {1}", _templates.Count, path));
            logger.Info(string.Format(CultureInfo.InvariantCulture,
                "ItemTemplateCatalog ready with {0} templates", _templates.Count));
        }

        public IReadOnlyCollection<ItemTemplate> Templates => _templates.Values;

        public bool TryGet(int aoid, out ItemTemplate template)
            => _templates.TryGetValue(aoid, out template!);

        public ItemTemplate Require(int aoid)
        {
            if (TryGet(aoid, out ItemTemplate template))
                return template;

            throw new KeyNotFoundException(
                string.Format(CultureInfo.InvariantCulture, "Item template {0} not found", aoid));
        }

        private static Dictionary<int, ItemTemplate> LoadItemsDat(string path)
        {
            try
            {
                List<DatItemTemplate> loaded = ItemsDatReader.Read(path);
                if (loaded.Count == 0)
                    throw new InvalidDataException("The catalog contains no item templates.");

                var templates = new Dictionary<int, ItemTemplate>(loaded.Count);
                foreach (DatItemTemplate dat in loaded)
                {
                    if (dat == null)
                        throw new InvalidDataException("The catalog contains a null item template.");
                    ItemTemplate template = DatItemMapper.ToTemplate(dat, dat.Name ?? string.Empty);
                    if (!templates.TryAdd(dat.ID, template))
                        throw new InvalidDataException($"Duplicate item template ID {dat.ID}.");
                }

                return templates;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidDataException or ArgumentException)
            {
                throw new InvalidDataException($"Could not load required GameData item catalog '{path}': {exception.Message}", exception);
            }
        }
    }
}
