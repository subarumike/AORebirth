namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    public sealed class MobStatEntry
    {
        public int Key { get; set; }

        public int Value { get; set; }
    }

    public sealed class MobItemTableEntry
    {
        public string Hash { get; set; } = string.Empty;

        public int Repeats { get; set; }

        public int Chance { get; set; }

        public int LevelMod { get; set; }
    }

    /// <summary>
    /// Full mob template as stored in GameData/MobTemplates.json.
    /// </summary>
    public sealed class MobTemplate
    {
        public bool HasHeadMesh { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int TemplateId { get; set; }

        public List<MobStatEntry> Stats { get; set; } = new();

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        /// <summary>Per-slot AOID lists from the template Equipment jagged array.</summary>
        public List<List<int>> Equipment { get; set; } = new();

        public int KnuBotId { get; set; }

        public string RawFeatures { get; set; } = string.Empty;

        public JsonElement? Features { get; set; }

        public List<MobItemTableEntry> ItemTable { get; set; } = new();

        public string BinaryListData { get; set; } = string.Empty;

        public JsonElement? BinaryList { get; set; }
    }

    public interface IMobTemplateCatalog
    {
        int Count { get; }

        bool TryGet(string hash, out MobTemplate template);

        MobTemplate Require(string hash);
    }

    public sealed class MobTemplateCatalog : IMobTemplateCatalog
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly Dictionary<string, MobTemplate> _templates;
        private readonly IZoneLogger _logger;

        public MobTemplateCatalog(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _templates = new Dictionary<string, MobTemplate>(StringComparer.Ordinal);

            Load();
        }

        public int Count => _templates.Count;

        public bool TryGet(string hash, out MobTemplate template)
        {
            if (string.IsNullOrEmpty(hash))
            {
                template = null!;
                return false;
            }

            return _templates.TryGetValue(hash, out template!);
        }

        public MobTemplate Require(string hash)
        {
            if (TryGet(hash, out MobTemplate template))
                return template;

            throw new KeyNotFoundException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mob template hash '{0}' not found",
                    hash));
        }

        private void Load()
        {
            string path = Path.Combine(GameDataLoader.RootPath, GameDataPaths.MobTemplatesFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "MobTemplates.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                List<MobTemplate>? loaded = JsonSerializer.Deserialize<List<MobTemplate>>(json, JsonOptions);
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "MobTemplates.json was empty: {0}",
                            path));
                    return;
                }

                int skipped = 0;
                foreach (MobTemplate template in loaded)
                {
                    if (string.IsNullOrEmpty(template.Hash))
                    {
                        skipped++;
                        continue;
                    }

                    if (!_templates.TryAdd(template.Hash, template))
                    {
                        _logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Duplicate mob template hash '{0}' skipped",
                                template.Hash));
                        skipped++;
                    }
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "MobTemplateCatalog ready with {0} templates from {1}",
                        _templates.Count,
                        path));

                if (skipped > 0)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "MobTemplateCatalog skipped {0} entries (empty or duplicate hash)",
                            skipped));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load MobTemplates.json from {0}; catalog empty",
                        path));
            }
        }
    }
}
