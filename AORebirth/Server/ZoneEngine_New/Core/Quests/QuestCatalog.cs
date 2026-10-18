namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// GameData/Quests.json templates keyed by hash. Items are named by ItemTemplates.json hash: reward and start
    /// items are resolved to template ids here, and a quest naming an item hash that does not resolve is left out.
    /// </summary>
    public sealed class QuestCatalog
    {
        public const string FileName = "Quests.json";

        readonly Dictionary<string, QuestTemplate> _templates = new(StringComparer.Ordinal);

        readonly IGameData _gameData;
        readonly HashItemMinter? _minter;

        public QuestCatalog(IGameData gameData, IZoneLogger logger, HashItemMinter? minter = null)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            _gameData = gameData;
            _minter = minter;
            LoadTemplates(Path.Combine(gameData.RootPath, FileName), logger);
        }

        public int Count => _templates.Count;

        public bool TryGet(string hash, out QuestTemplate template)
            => _templates.TryGetValue(hash ?? string.Empty, out template!);

        /// <summary>
        /// The template of <paramref name="hash"/>, or the one listing it in <see cref="QuestTemplate.Aliases"/>: the
        /// live quest hash an item names (SpawnQuest KY6D on the Bank of Rubi-Ka Credit Card is HWXS).
        /// </summary>
        public bool TryGetByHashOrAlias(string hash, out QuestTemplate template)
        {
            if (TryGet(hash, out template))
                return true;
            foreach (QuestTemplate candidate in _templates.Values)
            {
                if (Array.IndexOf(candidate.Aliases ?? [], hash) >= 0)
                {
                    template = candidate;
                    return true;
                }
            }

            return false;
        }

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
                    {
                        skipped++;
                        continue;
                    }

                    if (template.MissionBit is int bit && !MissionBits.IsValid(bit))
                    {
                        logger.Warn(string.Format(CultureInfo.InvariantCulture, "Quest {0} MissionBit {1} is outside 0-{2}; ignored",
                            template.Hash, bit, MissionBits.Count - 1));
                        template.MissionBit = null;
                    }

                    // A broken cooldown or level band would let the quest repeat freely: the quest is left out instead.
                    if (template.Recurring is { } recurring && (recurring.CooldownSeconds <= 0
                            || !(recurring.IsPerAccount || string.Equals(recurring.Per, QuestRecurring.PerCharacter, StringComparison.OrdinalIgnoreCase)))
                        || template.MinLevel < 1 || template.MaxLevel < (template.MinLevel ?? 1))
                    {
                        logger.Warn(string.Format(CultureInfo.InvariantCulture,
                            "Quest {0} has an invalid Recurring cooldown or level band; quest skipped", template.Hash));
                        _templates.Remove(template.Hash);
                        skipped++;
                        continue;
                    }

                    if (UnresolvedItem(template) is string unresolved)
                    {
                        logger.Warn(string.Format(CultureInfo.InvariantCulture,
                            "Quest {0} names item hash {1}, which does not resolve to one item; quest skipped", template.Hash, unresolved));
                        _templates.Remove(template.Hash);
                        skipped++;
                    }
                }

                logger.Info(string.Format(CultureInfo.InvariantCulture, "GameData quests={0} skipped={1} from {2}", _templates.Count, skipped, path));
            }
            catch (Exception exception)
            {
                logger.Error(exception, string.Format(CultureInfo.InvariantCulture, "Failed to load {0} from {1}", FileName, path));
            }
        }

        /// <summary>
        /// Resolves every hash-named item of <paramref name="template"/> to its template ids for the item's quality.
        /// Returns the first hash that does not resolve, or null. A hash must name one item family (an ItemTemplates.json
        /// leaf) so the quest always hands over the same item.
        /// </summary>
        string? UnresolvedItem(QuestTemplate template)
        {
            var items = new List<QuestRewardItem>();
            foreach (List<QuestRewardItem> record in template.Reward?.Records ?? [])
                items.AddRange(record);
            items.AddRange(template.StartItems ?? []);

            var leaves = new List<HashInstance>();
            foreach (QuestRewardItem item in items)
            {
                if (string.IsNullOrEmpty(item.Hash))
                    continue;

                leaves.Clear();
                _gameData.CollectHashLeafInstances(item.Hash, leaves);
                // Only the ids come from the hash: the item keeps the quest's Quality, as it did when quests named ids.
                if (leaves.Count != 1 || _minter == null
                    || !_minter.TryRollIds(item.Hash, item.Quality, out int lowId, out int highId, out _))
                    return item.Hash;

                item.Id = lowId;
                item.HighId = highId;
            }

            if ((template.Action is QuestTemplate.AssembleAction or QuestTemplate.EquipAction)
                && (template.Objective.SpecItem is not { Length: > 0 } specItem || !_gameData.CanResolveItemHash(specItem)))
                return template.Objective.SpecItem ?? "(no SpecItem)";

            return null;
        }
    }
}
