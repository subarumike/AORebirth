namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// GameData/Quests.json templates keyed by hash.
    /// </summary>
    public sealed class QuestCatalog
    {
        public const string FileName = "Quests.json";

        /// <summary>Feedback category 110 id 204307477: "You have to kill %d more %s to fulfill one of your missions!"</summary>
        public const int KillsRemainingTextId = 204307477;

        readonly Dictionary<string, QuestTemplate> _templates = new(StringComparer.Ordinal);

        public QuestCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            LoadTemplates(Path.Combine(gameData.RootPath, FileName), logger);
        }

        public int Count => _templates.Count;

        public bool TryGet(string hash, out QuestTemplate template)
            => _templates.TryGetValue(hash ?? string.Empty, out template!);

        void LoadTemplates(string path, IZoneLogger logger)
        {
            if (!File.Exists(path))
            {
                logger.Warn(string.Format(CultureInfo.InvariantCulture, "{0} not found at {1}; no quest templates", FileName, path));
                return;
            }

            try
            {
                List<QuestTemplate>? loaded = JsonSerializer.Deserialize<List<QuestTemplate>>(
                    File.ReadAllText(path), QuestTemplate.JsonOptions);
                int skipped = 0;
                foreach (QuestTemplate template in loaded ?? [])
                {
                    if (string.IsNullOrEmpty(template.Hash) || !_templates.TryAdd(template.Hash, template))
                        skipped++;
                }

                logger.Info(string.Format(CultureInfo.InvariantCulture, "GameData quests={0} skipped={1} from {2}", _templates.Count, skipped, path));
            }
            catch (Exception exception)
            {
                logger.Error(exception, string.Format(CultureInfo.InvariantCulture, "Failed to load {0} from {1}", FileName, path));
            }
        }
    }
}
