namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;
    using AORebirth.Modules.Content;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// GameData/Quests.json templates keyed by hash. Start items may be named by ItemTemplates.json hash and must resolve
    /// to template ids before publication. Reward items come from QuestRewards.json. Loaded whole and swapped on reload;
    /// a file that fails to load keeps the old templates. A quest already held keeps its accepted template.
    /// </summary>
    public sealed class QuestCatalog
    {
        public const string FileName = "Quests.json";
        public const string ReloadName = "Quests";

        volatile IReadOnlyDictionary<string, QuestTemplate> _templates = new Dictionary<string, QuestTemplate>(StringComparer.Ordinal);

        readonly IGameData _gameData;
        readonly IZoneLogger _logger;
        readonly HashItemMinter? _minter;

        public QuestCatalog(IGameData gameData, IZoneLogger logger, HashItemMinter? minter = null)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            _gameData = gameData;
            _logger = logger;
            _minter = minter;
            Reload();
        }

        public int Count => _templates.Count;

        public bool TryGet(string hash, out QuestTemplate template)
            => _templates.TryGetValue(hash ?? string.Empty, out template!);

        /// <summary>Every loaded template (read-only after load).</summary>
        public IEnumerable<QuestTemplate> Templates => _templates.Values;

        // The quest loader freezes hash-named start item endpoints into its published templates.
        internal void RequireCompatibleItemTemplates(HashItemCatalog current, HashItemCatalog proposed)
        {
            var before = new List<HashInstance>();
            var after = new List<HashInstance>();
            foreach (QuestTemplate template in Templates)
            {
                foreach (QuestRewardItem startItem in template.StartItems ?? [])
                {
                    if (startItem.Hash is not { Length: > 0 } hash)
                        continue;
                    before.Clear();
                    after.Clear();
                    current.CollectLeafInstances(hash, before);
                    proposed.CollectLeafInstances(hash, after);
                    if (before.Count != after.Count || (before.Count == 1
                        && !before[0].TemplateIds.AsSpan().SequenceEqual(after[0].TemplateIds)))
                        throw new InvalidDataException($"Item hash {hash} changes the loaded start item binding for quest {template.Hash}.");
                }
            }
        }

        /// <summary>Reads the file (through the GameData override folder) and swaps it in; on any error the old templates stay.</summary>
        public GameDataReloadResult Reload()
        {
            string path = GameDataPaths.ResolveFile(_gameData.RootPath, FileName);
            if (!File.Exists(path))
            {
                _templates = new Dictionary<string, QuestTemplate>(StringComparer.Ordinal);
                _logger.Warn(string.Format(CultureInfo.InvariantCulture, "{0} not found at {1}; no quest templates", FileName, path));
                return new GameDataReloadResult(ReloadName, true, 0, []);
            }

            try
            {
                Dictionary<string, QuestTemplate> templates = LoadTemplates(path, out List<string> warnings);
                _templates = templates;
                return new GameDataReloadResult(ReloadName, true, templates.Count, warnings);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, string.Format(CultureInfo.InvariantCulture,
                    "Failed to load {0} from {1}; templates unchanged", FileName, path));
                return new GameDataReloadResult(ReloadName, false, _templates.Count, [exception.Message]);
            }
        }

        Dictionary<string, QuestTemplate> LoadTemplates(string path, out List<string> warnings)
        {
            var templates = new Dictionary<string, QuestTemplate>(StringComparer.Ordinal);
            warnings = new List<string>();
            IZoneLogger logger = _logger;
            List<QuestTemplate>? loaded = JsonSerializer.Deserialize<List<QuestTemplate>>(
                File.ReadAllText(path), QuestTemplate.JsonOptions);
            int skipped = 0;
            var recordsIgnored = new List<string>();
            foreach (QuestTemplate template in loaded ?? [])
            {
                if (string.IsNullOrEmpty(template.Hash) || !templates.TryAdd(template.Hash, template))
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

                if (template.ExpiresAfterHours is int hours and < 0)
                {
                    string warning = string.Format(CultureInfo.InvariantCulture, "Quest {0} ExpiresAfterHours {1} is negative; ignored",
                        template.Hash, hours);
                    warnings.Add(warning);
                    logger.Warn(warning);
                    template.ExpiresAfterHours = null;
                }

                // Live's dumped compass point stands in for a waypoint nobody wrote by hand.
                if (template.Waypoint == null && template.Waypoints is { Count: > 0 } && template.Waypoints[0] is { Playfield: > 0 } dumped)
                    template.Waypoint = new QuestWaypoint { Playfield = dumped.Playfield, Position = [dumped.X, dumped.Y, dumped.Z] };

                // Items come only from QuestRewards.json, by Reward.Hash; a record left in Quests.json is dropped so it cannot pay.
                if (template.Reward?.Records is { Count: > 0 })
                {
                    recordsIgnored.Add(template.Hash);
                    template.Reward.Records = null;
                }

                BindRequiredItems(template);
            }

            if (recordsIgnored.Count > 0)
                logger.Warn(string.Format(CultureInfo.InvariantCulture,
                    "{0} quests carry Reward.Records, which are not read; move them to QuestRewards.json: {1}{2}",
                    recordsIgnored.Count, string.Join(", ", recordsIgnored.GetRange(0, Math.Min(8, recordsIgnored.Count))),
                    recordsIgnored.Count > 8 ? ", ..." : string.Empty));
            logger.Info(string.Format(CultureInfo.InvariantCulture, "GameData quests={0} skipped={1} from {2}", templates.Count, skipped, path));

            return templates;
        }

        /// <summary>
        /// Resolves every hash-named start item of <paramref name="template"/> to its template ids for the item's quality.
        /// A start item hash must name one item family so the quest always hands over the same item.
        /// </summary>
        void BindRequiredItems(QuestTemplate template)
        {
            var items = new List<QuestRewardItem>(template.StartItems ?? []);

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
                    throw new InvalidDataException($"Quest {template.Hash} start item hash '{item.Hash}' must resolve to one item family.");

                item.Id = lowId;
                item.HighId = highId;
            }

            if (template.Action is not (QuestTemplate.AssembleAction or QuestTemplate.EquipAction))
                return;

            string? specItem = template.Objective.SpecItem;
            if (string.IsNullOrWhiteSpace(specItem))
                throw new InvalidDataException($"Quest {template.Hash} requires an objective SpecItem for {template.Action}.");
            if (!_gameData.CanResolveItemHash(specItem))
                throw new InvalidDataException($"Quest {template.Hash} objective SpecItem '{specItem}' does not resolve.");
        }
    }
}
