namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;

    using Utility;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.NpcEditor;
    using ZoneEngine_New.Core.WorldSimulation;

    /// <summary>
    /// Loads and caches the GameData tree from the runtime GameData root.
    /// <see cref="GameDataPaths.EnvironmentVariableName"/> overrides the directory beside the process.
    /// There is no path search. Missing root files log and degrade gracefully.
    /// </summary>
    public sealed partial class GameDataStore : IGameData
    {
        private static readonly JsonSerializerOptions CatalogJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions MetaDataJsonOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions SpawnsJsonOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly Lock _playfieldSync = new();
        private readonly Lock _lootCatalogSync = new();
        private readonly IZoneLogger _logger;
        private readonly NpcEditorLootJournalFile _editorLootFile;
        private bool _loadedNpcTemplates;
        // NpcTemplates.json and ItemTemplates.json are swapped whole by a reload (ReloadNpcTemplates /
        // ReloadHashItems); every lookup reads the field once.
        private volatile NpcTemplateCatalog _npcTemplates = new(
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            new Dictionary<string, NpcLeaf>(StringComparer.Ordinal));
        private readonly Dictionary<string, int[]> _monsterWeapons =
            new(StringComparer.Ordinal);
        private volatile HashItemCatalog _hashItems = new(
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            new Dictionary<string, HashInstance>(StringComparer.Ordinal));
        private readonly Dictionary<int, VendingMachineDefinition> _vendingMachines = new();
        private readonly Dictionary<int, int> _catMeshByMonsterData = new();
        private readonly Dictionary<int, (int? Mass, int? CharRadius)> _bodyStatsByMonsterData = new();
        private readonly Dictionary<int, int> _featuresByMonsterData = new();
        private readonly Dictionary<int, XpLevelEntry> _xpLevels = new();
        private readonly Dictionary<int, AlienXpLevelEntry> _alienXpLevels = new();
        private readonly Dictionary<int, PlayfieldMetaData?> _playfieldMetaData = new();
        private readonly Dictionary<int, PlayfieldSpawnsData> _playfieldSpawns = new();
        private readonly Dictionary<int, PlayfieldNpcContentCatalog> _playfieldNpcs = new();
        private readonly Dictionary<int, PlayfieldGeometryData> _playfieldGeometry = new();
        private readonly Dictionary<int, uint?> _playfieldCharacterAppearanceOverrides = new();
        private readonly Dictionary<int, int[]?> _exitProxyDoorAllowLists = new();
        private readonly Dictionary<int, int[]?> _dungeonExitDoors = new();
        private readonly Lock _exitProxySync = new();
        private Dictionary<int, int[]>? _exitProxyDoorsByPlayfield;
        private readonly TeleportDestinationCatalog? _teleportDestinations;

        public GameDataStore(IZoneLogger logger, TeleportDestinationCatalog? teleportDestinations = null)
            : this(logger, teleportDestinations, GameDataPaths.ResolveRuntimeRoot(), GameDataPaths.ResolveOverrideRoot())
        {
        }

        internal GameDataStore(IZoneLogger logger, TeleportDestinationCatalog? teleportDestinations, string gameDataRoot,
            string? editorLootOverrideRoot = null)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _teleportDestinations = teleportDestinations;

            RootPath = Path.GetFullPath(gameDataRoot);
            PlayfieldsPath = Path.Combine(RootPath, GameDataPaths.PlayfieldsFolderName);
            _editorLootFile = new NpcEditorLootJournalFile(RootPath, editorLootOverrideRoot);
            _editorEquipmentFile = new NpcEditorEquipmentJournalFile(RootPath, editorLootOverrideRoot);
            _editorStatsFile = new NpcEditorStatsJournalFile(RootPath, editorLootOverrideRoot);

            EnsureRootExists();
            RespawnContent = RespawnContentCatalog.Load(RootPath);
            // Startup is the one place a broken start location may stop the engine: never on a tick.
            if (RespawnContent.StartLocationError is string startLocationError)
                throw new InvalidDataException("Invalid NewCharacter.json: " + startLocationError);
            ReloadHashItems();
            ReloadNpcTemplates();
            LoadMonsterWeapons();
            ComboNames = ComboNameCatalog.Load(RootPath, _logger);
            GoverningForms = GoverningFormCatalog.Load(RootPath, _logger);
            SpawnExclusions = SpawnExclusionCatalog.Load(RootPath, _logger);
            LoadVendingMachines();
            LoadMonsterData();
            LoadXpLevels();
            LoadAlienXpLevels();
            SpawnEditor = new NpcEditorSpawnStore(this, editorLootOverrideRoot, _logger);
        }

        internal NpcEditorSpawnStore SpawnEditor { get; }

        internal PlayfieldSpawnsData ReadAuthoredEditorSpawns(int playfieldId) => ReadPlayfieldSpawns(playfieldId);

        public string RootPath { get; }

        public RespawnContentCatalog RespawnContent { get; }

        public ComboNameCatalog ComboNames { get; }

        public GoverningFormCatalog GoverningForms { get; }

        public SpawnExclusionCatalog SpawnExclusions { get; }

        public string PlayfieldsPath { get; }

        public int MobTemplateCount => _npcTemplates.LeafCount;

        public int HashTemplateCount => _hashItems.CategoryCount;

        public int HashInstanceCount => _hashItems.InstanceCount;

        public int VendingMachineCount => _vendingMachines.Count;

        public int MonsterDataCount => _catMeshByMonsterData.Count;

        public int XpLevelCount => _xpLevels.Count;

        public bool TryGetXpLevel(int level, out XpLevelEntry entry)
        {
            if (level <= 0)
            {
                entry = null!;
                return false;
            }

            return _xpLevels.TryGetValue(level, out entry!);
        }

        public int AlienXpLevelCount => _alienXpLevels.Count;

        public bool TryGetAlienXpLevel(int level, out AlienXpLevelEntry entry)
        {
            if (level <= 0)
            {
                entry = null!;
                return false;
            }

            return _alienXpLevels.TryGetValue(level, out entry!);
        }

        public bool CanResolveMobHash(string hash)
            => _npcTemplates.CanResolve(hash);

        public bool HasMobTemplate(string hash)
            => _npcTemplates.HasTemplate(hash);

        public bool TryGetMobTemplate(string hash, out MobTemplate template)
        {
            template = null!;
            NpcTemplateCatalog templates = _npcTemplates;
            if (string.IsNullOrEmpty(hash) || !templates.TryGetLeaf(hash, out NpcLeaf leaf))
                return false;

            template = templates.MaterializeWithEditorOverrides(leaf, leaf.MinLevel);
            return true;
        }

        public bool TryResolveMobTemplate(string hash, int? level, out MobTemplate template)
        {
            NpcTemplateCatalog templates = _npcTemplates;
            if (templates.CanResolve(hash))
                return templates.TryResolve(hash, level, out template);

            template = null!;
            return false;
        }

        public Dictionary<int, int> ComposeNpcStats(MobTemplate template, int? level)
        {
            ArgumentNullException.ThrowIfNull(template);
            return new Dictionary<int, int>(template.Stats);
        }

        public MobTemplate RequireMobTemplate(string hash)
        {
            if (TryResolveMobTemplate(hash, null, out MobTemplate template))
                return template;

            throw new KeyNotFoundException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mob template hash '{0}' not found",
                    hash));
        }

        public bool TryGetMonsterWeapon(string hash, out int[] ids)
        {
            if (string.IsNullOrEmpty(hash) || !_monsterWeapons.TryGetValue(hash, out int[]? found))
            {
                ids = [];
                return false;
            }

            ids = found;
            return ids.Length > 0;
        }

        public bool TryGetHashTemplate(string hash, out IReadOnlyList<string> childHashes)
            => _hashItems.TryGetCategory(hash, out childHashes);

        public bool TryGetHashInstance(string hash, out HashInstance instance)
            => _hashItems.TryGetInstance(hash, out instance);

        public IReadOnlyCollection<HashInstance> ItemFamilies => _hashItems.Instances;

        public bool TryGetAssignedItemHash(int lowId, int highId, out string hash)
            => _hashItems.TryGetAssignedHash(lowId, highId, out hash);

        public bool TryResolveHashInstance(string hash, out HashInstance instance)
            => _hashItems.TryResolveInstance(hash, out instance);

        public bool CanResolveItemHash(string hash)
            => _hashItems.CanResolveItem(hash);

        public void CollectHashSpawns(string hash, List<HashInstance> into)
            => _hashItems.CollectSpawns(hash, into);

        public void CollectHashSpawns(string hash, List<HashInstance> into, Func<string, string, bool> canPick)
            => _hashItems.CollectSpawns(hash, into, canPick);

        public void CollectHashSpawns(string hash, List<HashInstance> into, Func<string, string, bool> canPick, Random random)
            => _hashItems.CollectSpawns(hash, into, canPick, random);

        public bool IsSpawnAllItemHash(string hash)
            => _hashItems.IsSpawnAll(hash);

        public void CollectMobSpawns(string hash, int? level, List<MobTemplate> into)
            => _npcTemplates.CollectSpawns(hash, level, into);

        public void CollectHashLeafInstances(string hash, List<HashInstance> into)
            => _hashItems.CollectLeafInstances(hash, into);

        public bool TryGetVendingMachine(int templateId, out VendingMachineDefinition definition)
        {
            if (templateId <= 0)
            {
                definition = null!;
                return false;
            }

            return _vendingMachines.TryGetValue(templateId, out definition!);
        }

        public bool TryGetCatMesh(int monsterData, out int catMesh)
        {
            if (monsterData <= 0)
            {
                catMesh = 0;
                return false;
            }

            return _catMeshByMonsterData.TryGetValue(monsterData, out catMesh);
        }

        public bool TryGetMonsterBodyStats(int monsterData, out int? mass, out int? charRadius)
        {
            mass = null;
            charRadius = null;
            if (monsterData <= 0 || !_bodyStatsByMonsterData.TryGetValue(monsterData, out (int? Mass, int? CharRadius) body))
                return false;

            mass = body.Mass;
            charRadius = body.CharRadius;
            return true;
        }

        public bool TryGetMonsterFeatures(int monsterData, out int features)
        {
            features = 0;
            return monsterData > 0 && _featuresByMonsterData.TryGetValue(monsterData, out features);
        }

        public bool TryGetPlayfieldCharacterAppearanceOverride(int playfieldId, out uint monsterData)
        {
            monsterData = 0;
            if (playfieldId <= 0)
                return false;

            lock (_playfieldSync)
            {
                if (!_playfieldCharacterAppearanceOverrides.TryGetValue(playfieldId, out uint? cached))
                {
                    cached = ReadPlayfieldCharacterAppearanceOverride(playfieldId);
                    _playfieldCharacterAppearanceOverrides[playfieldId] = cached;
                }

                if (!cached.HasValue)
                    return false;

                monsterData = cached.Value;
                return true;
            }
        }

        public PlayfieldMetaData? GetPlayfieldMetaData(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (_playfieldSync)
            {
                if (_playfieldMetaData.TryGetValue(playfieldId, out PlayfieldMetaData? cached))
                    return cached;

                PlayfieldMetaData? loaded = ReadPlayfieldMetaData(playfieldId);
                _playfieldMetaData[playfieldId] = loaded;
                return loaded;
            }
        }

        public PlayfieldSpawnsData GetPlayfieldSpawns(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (_playfieldSync)
            {
                if (_playfieldSpawns.TryGetValue(playfieldId, out PlayfieldSpawnsData? cached))
                    return cached;

                PlayfieldSpawnsData loaded = ReadPlayfieldSpawns(playfieldId);
                _playfieldSpawns[playfieldId] = loaded;
                return loaded;
            }
        }


        public PlayfieldNpcContentCatalog GetPlayfieldNpcs(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (_playfieldSync)
            {
                if (_playfieldNpcs.TryGetValue(playfieldId, out PlayfieldNpcContentCatalog? cached))
                    return cached;

                PlayfieldNpcContentCatalog loaded = PlayfieldNpcContentCatalog.Load(RootPath, playfieldId);
                _playfieldNpcs[playfieldId] = loaded;
                return loaded;
            }
        }

        public PlayfieldGeometryData GetPlayfieldGeometry(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (_playfieldSync)
            {
                if (_playfieldGeometry.TryGetValue(playfieldId, out PlayfieldGeometryData? cached))
                    return cached;

                PlayfieldGeometryData loaded = ReadPlayfieldGeometry(playfieldId);
                _playfieldGeometry[playfieldId] = loaded;
                return loaded;
            }
        }

        /// <summary>Drops reloadable data for a fully retired ordinary world, preserving global catalogs.</summary>
        internal void ReleasePlayfieldCache(int playfieldId)
        {
            lock (_playfieldSync)
            {
                _playfieldMetaData.Remove(playfieldId);
                _playfieldSpawns.Remove(playfieldId);
                _playfieldNpcs.Remove(playfieldId);
                _playfieldGeometry.Remove(playfieldId);
                _playfieldCharacterAppearanceOverrides.Remove(playfieldId);
                _exitProxyDoorAllowLists.Remove(playfieldId);
                _dungeonExitDoors.Remove(playfieldId);
            }
        }

        public IReadOnlyList<int> GetExitProxyDoorInstances(int playfieldId)
        {
            if (playfieldId <= 0)
                return [];

            EnsureExitProxyIndex();
            return _exitProxyDoorsByPlayfield!.TryGetValue(playfieldId, out int[]? doors)
                ? doors
                : [];
        }

        public bool TryGetTeleportLanding(
            int playfield,
            int statelType,
            uint statelInstance,
            out int destinationPlayfield,
            out AORebirth.Core.Vector.Vector3 landing,
            out bool setsSavePoint)
        {
            if (_teleportDestinations != null
                && _teleportDestinations.TryGetLanding(playfield, statelType, statelInstance, out destinationPlayfield, out landing, out setsSavePoint))
                return true;

            destinationPlayfield = 0;
            landing = default!;
            setsSavePoint = false;
            return false;
        }

        public bool TryGetTeleportRoute(
            int playfield,
            int statelType,
            uint statelInstance,
            out int destinationPlayfield,
            out int destinationType,
            out uint destinationInstance)
        {
            if (_teleportDestinations != null
                && _teleportDestinations.TryGet(
                    playfield,
                    statelType,
                    statelInstance,
                    out destinationPlayfield,
                    out destinationType,
                    out destinationInstance))
                return true;

            destinationPlayfield = 0;
            destinationType = 0;
            destinationInstance = 0;
            return false;
        }

        public IReadOnlyCollection<int>? GetConfiguredExitProxyDoorInstances(int playfieldId)
        {
            if (playfieldId <= 0)
                return null;

            lock (_playfieldSync)
            {
                if (_exitProxyDoorAllowLists.TryGetValue(playfieldId, out int[]? cached))
                    return cached;

                int[]? loaded = ReadPlayfieldExitProxyDoorInstances(playfieldId);
                _exitProxyDoorAllowLists[playfieldId] = loaded;
                return loaded;
            }
        }

        private void EnsureExitProxyIndex()
        {
            lock (_exitProxySync)
            {
                if (_exitProxyDoorsByPlayfield != null)
                    return;

                Dictionary<int, HashSet<int>> collected = new();
                if (!Directory.Exists(PlayfieldsPath))
                {
                    _exitProxyDoorsByPlayfield = new Dictionary<int, int[]>();
                    return;
                }

                foreach (string directory in Directory.EnumerateDirectories(PlayfieldsPath))
                {
                    string name = Path.GetFileName(directory);
                    if (!int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourcePlayfieldId)
                        || sourcePlayfieldId <= 0)
                        continue;

                    PlayfieldDynels? dynels = ReadPlayfieldDynels(sourcePlayfieldId);
                    ExitProxyDoorCatalog.CollectFromDynels(dynels, collected, GetConfiguredExitProxyDoorInstances);
                }

                Dictionary<int, int[]> index = new(collected.Count);
                int doorTotal = 0;
                foreach (KeyValuePair<int, HashSet<int>> pair in collected)
                {
                    int[] doors = new int[pair.Value.Count];
                    pair.Value.CopyTo(doors);
                    Array.Sort(doors);
                    index[pair.Key] = doors;
                    doorTotal += doors.Length;
                }

                _exitProxyDoorsByPlayfield = index;
                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData exit-proxy destinations playfields={0} doors={1}",
                        index.Count,
                        doorTotal));
            }
        }

        private void EnsureRootExists()
        {
            if (Directory.Exists(RootPath) && Directory.Exists(PlayfieldsPath))
                return;

            string message =
                "GameData playfield metadata is unavailable; locality will use the safe indoor fallback. Root="
                + RootPath;
            _logger.Warn(message);
            LogUtil.Debug(DebugInfoDetail.Engine, message);
        }

        #region Catalog loads

        /// <summary>
        /// Reads NpcTemplates.json (through the GameData override folder) and swaps it in. On a missing file or a parse
        /// failure the current templates stay. Startup and module reloads; never on a playfield tick.
        /// </summary>
        internal AORebirth.Modules.Content.GameDataReloadResult ReloadNpcTemplates()
        {
            lock (_lootCatalogSync)
                return ReloadNpcTemplatesCore();
        }

        private AORebirth.Modules.Content.GameDataReloadResult ReloadNpcTemplatesCore()
        {
            const string name = "NpcTemplates";
            string path = GameDataPaths.ResolveFile(RootPath, GameDataPaths.NpcTemplatesFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NpcTemplates.json not found at {0}; catalog unchanged",
                        path));
                return new(name, false, _npcTemplates.LeafCount, ["NpcTemplates.json not found at " + path]);
            }

            try
            {
                NpcTemplateCatalog loaded = NpcTemplateCatalog.Parse(File.ReadAllText(path));
                // Behaviour switches (NoCombat, Guard, ...) live in MonsterCombatRules.json; read at materialize time,
                // so a rules reload reaches the next spawn.
                string rulesRoot = RootPath;
                loaded.RuleFeatures = hash =>
                    Mobs.MonsterCombatRulesCatalog.For(rulesRoot).TryGet(hash, out Mobs.MonsterCombatRules rules) ? rules.Features : null;
                try
                {
                    _npcTemplates = WithLoadedEditorStats(
                        WithLoadedEditorEquipment(loaded.WithEditorLoot(_editorLootFile.Read(loaded, IsEditorLootItem))));
                    _loadedNpcTemplates = true;
                    if (_npcTemplates.EditorEquipment.LoadError is string equipmentError)
                        return new(name, false, _npcTemplates.LeafCount, [equipmentError]);
                    if (_npcTemplates.EditorStats.LoadError is string statError)
                        return new(name, false, _npcTemplates.LeafCount, [statError]);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
                {
                    NpcTemplateCatalog keep = _loadedNpcTemplates ? _npcTemplates : WithLoadedEditorStats(WithLoadedEditorEquipment(loaded));
                    const string error = "The NPC loot overlay failed validation. See the ZoneEngine log and reload NpcTemplates after repair.";
                    _npcTemplates = keep.WithEditorLoot(keep.EditorLoot with { LoadError = error });
                    _loadedNpcTemplates = true;
                    _logger.Error(exception, error);
                    return new(name, false, keep.LeafCount, [error]);
                }
                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData npc templates leaves={0} families={1} from {2}",
                        loaded.LeafCount,
                        loaded.FamilyCount,
                        path));
                return new(name, true, loaded.LeafCount, []);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load NpcTemplates.json from {0}; catalog unchanged",
                        path));
                return new(name, false, _npcTemplates.LeafCount, ["NpcTemplates.json: " + exception.Message]);
            }
        }

        internal NpcEditorLootState GetEditorLoot(NpcEditorLootTarget target)
            => GetEditorLoot(_npcTemplates, target);

        internal (NpcEditorLootState State, List<MobItemTableEntry> Rows) DescribeEditorLoot(NpcEditorLootTarget target)
        {
            NpcTemplateCatalog catalog = _npcTemplates;
            NpcEditorLootState state = GetEditorLoot(catalog, target);
            if (!MatchesLootBand(catalog, target, out NpcLeaf leaf))
                return (state, []);
            NpcEditorLootRevision[] history = catalog.EditorLoot.HistoryFor(target.TemplateHash);
            return (state, NpcEditorLootJournal.MaterializeBand(leaf.Bands[target.BandIndex], target.BandIndex,
                history.LastOrDefault()));
        }

        private NpcEditorLootState GetEditorLoot(NpcTemplateCatalog catalog, NpcEditorLootTarget target)
        {
            string? unavailable = _editorLootFile.ConfigurationProblem ?? catalog.EditorLoot.LoadError;
            if (!catalog.TryGetEditableLootLeaf(target.TemplateHash, out NpcLeaf leaf))
                unavailable = "Select an NPC with an authored, resolved leaf template.";
            else if (unavailable == null && !MatchesLootBand(catalog, target, out _))
                unavailable = "The authored NPC template changed. Select a new spawn before editing its loot.";
            NpcEditorLootRevision[] history = catalog.EditorLoot.HistoryFor(target.TemplateHash);
            NpcEditorLootRevision? latest = history.Length > 0 ? history[^1] : null;
            string? definition = target.BandIndex >= 0 && leaf != null && target.BandIndex < leaf.Bands.Length
                ? $"NpcTemplates.json / {target.TemplateHash} / level {leaf.Bands[target.BandIndex].Level} (band {target.BandIndex + 1})"
                : null;
            return new(target.TemplateHash, unavailable == null, unavailable, latest?.Revision ?? 0,
                latest == null ? [] : (NpcEditorLootEntry[])latest.Entries.Clone(),
                history.Select(revision => new NpcEditorLootRevisionSummary(revision.Revision, revision.Actor,
                    revision.AtUtc, revision.Action, revision.Overrides.Count(),
                    revision.Audit?.BandIndex == null ? "template" : "templateBand", revision.Audit?.BandIndex,
                    revision.Audit?.RowIndex, revision.Audit?.Before, revision.Audit?.After,
                    revision.Audit?.RestoredRevision)).ToArray(), definition, target.BandIndex);
        }

        static bool MatchesLootBand(NpcTemplateCatalog catalog, NpcEditorLootTarget target, out NpcLeaf leaf)
            => catalog.TryGetEditableLootLeaf(target.TemplateHash, out leaf)
                && target.BandIndex >= 0 && target.BandIndex < leaf.Bands.Length
                && target.TemplateSignature == leaf.EditorLootTemplateSignature;

        internal NpcEditorLootState ChangeEditorLoot(NpcEditorLootTarget target, int expectedRevision, string actor, string action,
            Func<NpcLeaf, NpcEditorLootRevision[], NpcEditorLootRevisionEdit> edit,
            Func<string, bool> hasRdbTemplates)
        {
            if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
                throw new ArgumentException("A valid GM actor is required.", nameof(actor));

            lock (_lootCatalogSync)
            {
                NpcTemplateCatalog catalog = _npcTemplates;
                string hash = target.TemplateHash;
                if (!MatchesLootBand(catalog, target, out NpcLeaf leaf))
                {
                    throw new NpcEditorLootConflictException("The authored NPC template changed. Select a new spawn before editing its loot.");
                }

                NpcEditorLootState current = GetEditorLoot(catalog, target);
                if (!current.CanSave)
                    throw new InvalidOperationException(current.UnavailableReason);
                if (current.Revision != expectedRevision)
                    throw new NpcEditorLootConflictException("The template's loot revision changed. Refresh and retry.");
                NpcEditorLootRevision[] history = catalog.EditorLoot.HistoryFor(hash);
                if (history.Length >= NpcEditorLootJournalFile.MaxRevisionsPerTemplate)
                    throw new InvalidOperationException("The template's loot history has reached its revision limit.");

                NpcEditorLootRevisionEdit edited = edit(leaf, history);
                var revision = new NpcEditorLootRevision(current.Revision + 1, actor, DateTimeOffset.UtcNow,
                    action, edited.Entries, edited.Bands, edited.Audit);
                if (edited.Entries.Length > NpcEditorLootJournalFile.MaxRowsPerTemplate
                    || !NpcEditorLootJournalFile.ValidBands(leaf, edited.Bands)
                    || revision.Overrides.Any(entry => !NpcEditorLootJournalFile.ValidEntry(entry)
                        || !IsEditorLootItem(entry.ItemHash) || !hasRdbTemplates(entry.ItemHash)))
                {
                    throw new ArgumentException("Loot rows require a current item family and valid chance, repeats and level variance.");
                }

                var templates = new Dictionary<string, NpcEditorLootHistory>(catalog.EditorLoot.Templates, StringComparer.Ordinal)
                {
                    [hash] = new(target.TemplateSignature, [.. history, revision])
                };
                NpcEditorLootJournal updated = _editorLootFile.Write(catalog.EditorLoot, catalog.EditorLoot with { Templates = templates });
                NpcTemplateCatalog revised = catalog.WithEditorLoot(updated);
                _npcTemplates = revised;
                return GetEditorLoot(revised, target);
            }
        }

        private bool IsEditorLootItem(string hash) => _hashItems.CanResolveItem(hash);

        private void LoadMonsterWeapons()
        {
            string path = Path.Combine(RootPath, GameDataPaths.MonsterWeaponsFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "MonsterWeapons.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                Dictionary<string, int[]>? loaded =
                    JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(path), CatalogJsonOptions);
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "MonsterWeapons.json was empty: {0}",
                            path));
                    return;
                }

                foreach (KeyValuePair<string, int[]> pair in loaded)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || pair.Value.Length == 0)
                        continue;
                    _monsterWeapons[pair.Key] = pair.Value;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData monster weapons={0} from {1}",
                        _monsterWeapons.Count,
                        path));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load MonsterWeapons.json from {0}; catalog empty",
                        path));
            }
        }

        /// <summary>
        /// Reads ItemTemplates.json (through the GameData override folder) and swaps the hash catalog in. On a missing
        /// file or a parse failure the current catalog stays. Items already minted or spawned keep their template.
        /// Startup and module reloads; never on a playfield tick.
        /// </summary>
        internal AORebirth.Modules.Content.GameDataReloadResult ReloadHashItems()
        {
            lock (_lootCatalogSync)
                return ReloadHashItemsCore();
        }

        private AORebirth.Modules.Content.GameDataReloadResult ReloadHashItemsCore()
        {
            const string name = "ItemTemplates";
            string path = GameDataPaths.ResolveFile(RootPath, GameDataPaths.ItemTemplatesFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ItemTemplates.json not found at {0}; hash catalog unchanged",
                        path));
                return new(name, false, _hashItems.InstanceCount, ["ItemTemplates.json not found at " + path]);
            }

            try
            {
                HashItemCatalog loaded = HashItemCatalog.Parse(File.ReadAllText(path));
                if (_npcTemplates.EditorLoot.Templates.Values.Any(history => history.Revisions[^1].Overrides.Any(
                    entry => !loaded.CanResolveItem(entry.ItemHash))))
                {
                    throw new InvalidDataException("ItemTemplates would invalidate a saved NPC loot item family.");
                }
                _hashItems = loaded;
                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData hash templates={0} hash instances={1} from {2}",
                        loaded.CategoryCount,
                        loaded.InstanceCount,
                        path));
                return new(name, true, loaded.InstanceCount, []);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load ItemTemplates.json from {0}; hash catalog unchanged",
                        path));
                return new(name, false, _hashItems.InstanceCount, ["ItemTemplates.json: " + exception.Message]);
            }
        }

        private void LoadVendingMachines()
        {
            string path = Path.Combine(RootPath, GameDataPaths.VendingMachinesFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "VendingMachines.json not found at {0}; shops will stock nothing",
                        path));
                return;
            }

            try
            {
                Dictionary<string, VendingMachineDefinition>? loaded =
                    JsonSerializer.Deserialize<Dictionary<string, VendingMachineDefinition>>(
                        File.ReadAllText(path),
                        CatalogJsonOptions);
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "VendingMachines.json was empty: {0}",
                            path));
                    return;
                }

                int skipped = 0;
                foreach (KeyValuePair<string, VendingMachineDefinition> pair in loaded)
                {
                    if (!int.TryParse(
                            pair.Key,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int templateId)
                        || templateId <= 0
                        || pair.Value == null)
                    {
                        skipped++;
                        continue;
                    }

                    if (!_vendingMachines.TryAdd(templateId, pair.Value))
                        skipped++;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData vending machines={0} skipped={1}",
                        _vendingMachines.Count,
                        skipped));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load VendingMachines.json from {0}; shops will stock nothing",
                        path));
            }
        }

        private void LoadMonsterData()
        {
            string path = Path.Combine(RootPath, GameDataPaths.MonsterDataFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "MonsterData.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                List<MonsterDataCatMeshPairing>? loaded =
                    JsonSerializer.Deserialize<List<MonsterDataCatMeshPairing>>(
                        File.ReadAllText(path),
                        CatalogJsonOptions);
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "MonsterData.json was empty: {0}",
                            path));
                    return;
                }

                int skipped = 0;
                foreach (MonsterDataCatMeshPairing pairing in loaded)
                {
                    if (pairing.MonsterData > 0 && (pairing.Mass != null || pairing.CharRadius != null))
                        _bodyStatsByMonsterData.TryAdd(pairing.MonsterData, (pairing.Mass, pairing.CharRadius));
                    if (pairing.MonsterData > 0 && pairing.Features is int features)
                        _featuresByMonsterData.TryAdd(pairing.MonsterData, features);

                    // A record without a mesh has body stats only.
                    if (pairing.MonsterData > 0 && pairing.CatMesh <= 0)
                        continue;

                    if (pairing.MonsterData <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    if (!_catMeshByMonsterData.TryAdd(pairing.MonsterData, pairing.CatMesh))
                    {
                        _logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Duplicate MonsterData {0} skipped",
                                pairing.MonsterData));
                        skipped++;
                    }
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData MonsterData pairings={0} from {1}",
                        _catMeshByMonsterData.Count,
                        path));

                if (skipped > 0)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "GameData skipped {0} MonsterData entries (invalid or duplicate)",
                            skipped));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load MonsterData.json from {0}; catalog empty",
                        path));
            }
        }

        private void LoadXpLevels()
        {
            string path = Path.Combine(RootPath, GameDataPaths.XpFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Xp.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                // Level rows are keyed by level; other keys (NoKillXpFamilies) belong to other readers.
                Dictionary<string, JsonElement>? file =
                    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        File.ReadAllText(path),
                        CatalogJsonOptions);
                Dictionary<string, XpLevelRow?>? loaded = null;
                if (file != null)
                {
                    loaded = new Dictionary<string, XpLevelRow?>(file.Count);
                    foreach (KeyValuePair<string, JsonElement> pair in file)
                    {
                        if (string.Equals(pair.Key, NoKillXpFamilyCatalog.PropertyName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        loaded[pair.Key] = pair.Value.ValueKind == JsonValueKind.Object
                            ? pair.Value.Deserialize<XpLevelRow>(CatalogJsonOptions)
                            : null;
                    }
                }
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Xp.json was empty: {0}",
                            path));
                    return;
                }

                var parsed = new List<(int Level, XpLevelRow Row)>();
                int skipped = 0;
                foreach (KeyValuePair<string, XpLevelRow?> pair in loaded)
                {
                    if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                        || level <= 0
                        || pair.Value is not XpLevelRow row)
                    {
                        skipped++;
                        continue;
                    }

                    parsed.Add((level, row));
                }

                parsed.Sort((left, right) => left.Level.CompareTo(right.Level));
                int floorXp = 0;
                foreach ((int level, XpLevelRow row) in parsed)
                {
                    if (!_xpLevels.TryAdd(
                            level,
                            new XpLevelEntry
                            {
                                Level = level,
                                KillAward = row.KillAward,
                                NextLevelXp = row.NextLevelXp,
                                XpKillRange = row.XPKillRange,
                                FloorXp = floorXp
                            }))
                    {
                        skipped++;
                        continue;
                    }

                    if (row.NextLevelXp > 0)
                        floorXp += row.NextLevelXp;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData xp levels={0} from {1}",
                        _xpLevels.Count,
                        path));

                if (skipped > 0)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "GameData skipped {0} xp level rows (invalid or duplicate)",
                            skipped));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load Xp.json from {0}; catalog empty",
                        path));
            }
        }

        private void LoadAlienXpLevels()
        {
            string path = Path.Combine(RootPath, GameDataPaths.AlienXpFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "AlienXp.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                Dictionary<string, AlienXpLevelRow>? loaded =
                    JsonSerializer.Deserialize<Dictionary<string, AlienXpLevelRow>>(
                        File.ReadAllText(path),
                        CatalogJsonOptions);
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "AlienXp.json was empty: {0}",
                            path));
                    return;
                }

                var parsed = new List<(int Level, AlienXpLevelRow Row)>();
                int skipped = 0;
                foreach (KeyValuePair<string, AlienXpLevelRow> pair in loaded)
                {
                    if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                        || level <= 0
                        || pair.Value == null)
                    {
                        skipped++;
                        continue;
                    }

                    parsed.Add((level, pair.Value));
                }

                parsed.Sort((left, right) => left.Level.CompareTo(right.Level));
                int floorXp = 0;
                foreach ((int level, AlienXpLevelRow row) in parsed)
                {
                    if (!_alienXpLevels.TryAdd(
                            level,
                            new AlienXpLevelEntry
                            {
                                Level = level,
                                NextLevelXp = row.NextLevelXp,
                                FloorXp = floorXp,
                                MinLevel = row.MinLevel
                            }))
                    {
                        skipped++;
                        continue;
                    }

                    if (row.NextLevelXp > 0)
                        floorXp += row.NextLevelXp;
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData alien xp levels={0} from {1}",
                        _alienXpLevels.Count,
                        path));

                if (skipped > 0)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "GameData skipped {0} alien xp level rows (invalid or duplicate)",
                            skipped));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load AlienXp.json from {0}; catalog empty",
                        path));
            }
        }

        #endregion

        #region Playfield loads

        private PlayfieldSpawnsData ReadPlayfieldSpawns(int playfieldId)
        {
            string spawnsPath = Path.Combine(
                RootPath,
                GameDataPaths.PlayfieldSpawnsRelativePath(playfieldId));

            if (!File.Exists(spawnsPath))
            {
                return new PlayfieldSpawnsData
                {
                    SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
                    PlayfieldId = playfieldId,
                    Spawns = []
                };
            }

            PlayfieldSpawnsData? data;
            try
            {
                data = JsonSerializer.Deserialize<PlayfieldSpawnsData>(
                    File.ReadAllText(spawnsPath),
                    SpawnsJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield spawns could not be read: "
                    + spawnsPath
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (data == null)
                throw new InvalidDataException("Playfield spawns was empty: " + spawnsPath);

            if (data.SchemaVersion != PlayfieldSpawnsData.SupportedSchemaVersion
                || (data.PlayfieldId != 0 && data.PlayfieldId != playfieldId))
                throw new InvalidDataException("Playfield spawn schema or playfield identity is invalid: " + spawnsPath);

            data.Spawns ??= [];
            if (data.PlayfieldId == 0)
                data.PlayfieldId = playfieldId;

            return data;
        }



        private uint? ReadPlayfieldCharacterAppearanceOverride(int playfieldId)
        {
            string path = Path.Combine(
                RootPath,
                GameDataPaths.PlayfieldCharacterAppearanceRelativePath(playfieldId));

            if (!File.Exists(path))
                return null;

            PlayfieldCharacterAppearanceData? data;
            try
            {
                data = JsonSerializer.Deserialize<PlayfieldCharacterAppearanceData>(
                    File.ReadAllText(path),
                    CatalogJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield character appearance could not be read: "
                    + path
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (data == null
                || data.SchemaVersion != PlayfieldCharacterAppearanceData.SupportedSchemaVersion
                || data.PlayfieldId != playfieldId
                || data.MonsterData == 0)
            {
                throw new InvalidDataException("Invalid playfield character appearance: " + path);
            }

            return data.MonsterData;
        }

        private int[]? ReadPlayfieldExitProxyDoorInstances(int playfieldId)
        {
            string path = Path.Combine(
                RootPath,
                GameDataPaths.PlayfieldExitProxyDoorsRelativePath(playfieldId));

            if (!File.Exists(path))
                return (int[]?)GetDungeonExitDoors(playfieldId);

            PlayfieldExitProxyDoorsData? data;
            try
            {
                data = JsonSerializer.Deserialize<PlayfieldExitProxyDoorsData>(
                    File.ReadAllText(path),
                    CatalogJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield exit-proxy doors could not be read: "
                    + path
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (data == null
                || data.SchemaVersion != PlayfieldExitProxyDoorsData.SupportedSchemaVersion
                || data.PlayfieldId != playfieldId
                || data.DoorInstances == null)
            {
                throw new InvalidDataException("Invalid playfield exit-proxy doors: " + path);
            }

            int[] doors = new int[data.DoorInstances.Length];
            Array.Copy(data.DoorInstances, doors, data.DoorInstances.Length);
            Array.Sort(doors);
            return doors;
        }

        readonly object _dynelCollisionSync = new();
        DynelCollisionData? _dynelCollision;
        bool _dynelCollisionLoaded;

        public bool TryGetDynelCollisionSphere(int templateId, out float radius, out float centerY)
        {
            radius = 0f;
            centerY = 0f;
            DynelCollisionData? data = LoadDynelCollision();
            if (data?.Templates == null
                || !data.Templates.TryGetValue(templateId.ToString(CultureInfo.InvariantCulture), out CollisionSphereData? sphere)
                || sphere == null || sphere.Radius <= 0f)
                return false;

            radius = sphere.Radius;
            centerY = sphere.CenterY;
            return true;
        }

        public bool TryGetDynelCollisionSphere(int templateId, int placedMesh, int placedScale, out float radius, out float centerY)
        {
            radius = 0f;
            centerY = 0f;
            DynelCollisionData? data = LoadDynelCollision();
            if (data == null)
                return false;

            CollisionSphereData? template = null;
            data.Templates?.TryGetValue(templateId.ToString(CultureInfo.InvariantCulture), out template);
            int templateScale = template?.Scale is int s && s > 0 ? s : 100;
            int scale = placedScale > 0 ? placedScale : templateScale;

            // The client builds the sphere from the dynel's own mesh, which its placement can override.
            if (placedMesh > 0
                && data.Meshes != null
                && data.Meshes.TryGetValue(placedMesh.ToString(CultureInfo.InvariantCulture), out CollisionSphereData? mesh)
                && mesh != null && mesh.Radius > 0f)
            {
                radius = mesh.Radius * scale / 100f;
                centerY = mesh.CenterY * scale / 100f;
                return true;
            }

            if (template == null || template.Radius <= 0f)
                return false;

            float rescale = scale / (float)templateScale;
            radius = template.Radius * rescale;
            centerY = template.CenterY * rescale;
            return true;
        }

        public CollisionSphereData? PlayerCollisionSphere => LoadDynelCollision()?.PlayerSphere;

        DynelCollisionData? LoadDynelCollision()
        {
            lock (_dynelCollisionSync)
            {
                if (_dynelCollisionLoaded)
                    return _dynelCollision;

                _dynelCollisionLoaded = true;
                string path = Path.Combine(RootPath, GameDataPaths.DynelCollisionFileName);
                if (!File.Exists(path))
                {
                    _logger.Warn("DynelCollision.json not found at " + path + "; doors keep the default trigger cylinder");
                    return null;
                }

                try
                {
                    DynelCollisionData? data = JsonSerializer.Deserialize<DynelCollisionData>(File.ReadAllText(path), CatalogJsonOptions);
                    if (data == null || data.SchemaVersion != DynelCollisionData.SupportedSchemaVersion)
                    {
                        _logger.Warn("DynelCollision.json has an unsupported schema: " + path);
                        return null;
                    }

                    _dynelCollision = data;
                    _logger.Info(
                        "GameData dynel collision templates=" + (data.Templates?.Count ?? 0).ToString(CultureInfo.InvariantCulture)
                        + " player=" + (data.PlayerSphere != null ? "yes" : "no"));
                    return data;
                }
                catch (Exception exception)
                {
                    _logger.Warn("DynelCollision.json could not be read: " + path + " (" + exception.Message + ")");
                    return null;
                }
            }
        }

        public IReadOnlyList<int>? GetDungeonExitDoors(int playfieldId)
        {
            if (playfieldId <= 0)
                return null;

            lock (_playfieldSync)
            {
                if (!_dungeonExitDoors.TryGetValue(playfieldId, out int[]? exits))
                {
                    exits = ReadDungeonExitDoors(playfieldId);
                    _dungeonExitDoors[playfieldId] = exits;
                }

                return exits;
            }
        }

        /// <summary>
        /// A dungeon's exits are its doors that open onto no room, as the client registers them
        /// (<see cref="DungeonExitDoors"/>); without an ExitProxyDoors.json they are its exit allow-list. Null outside
        /// dungeons or when the rooms name none, which keeps every return-recording landing door as an exit.
        /// </summary>
        private int[]? ReadDungeonExitDoors(int playfieldId)
        {
            string path = Path.Combine(RootPath, GameDataPaths.PlayfieldRoomsRelativePath(playfieldId));
            if (!File.Exists(path))
                return null;

            PlayfieldRoomsData? rooms;
            try
            {
                rooms = JsonSerializer.Deserialize<PlayfieldRoomsData>(File.ReadAllText(path), CatalogJsonOptions);
            }
            catch (Exception exception)
            {
                _logger.Warn("Playfield rooms could not be read for exits: " + path + " (" + exception.Message + ")");
                return null;
            }

            float tileSize = GetPlayfieldMetaData(playfieldId)?.TileSize ?? 0f;
            int[]? exits = DungeonExitDoors.Find(rooms, ReadPlayfieldDynels(playfieldId), tileSize);
            if (exits != null)
            {
                _logger.Info(
                    "Dungeon exit doors playfield=" + playfieldId.ToString(CultureInfo.InvariantCulture) + " doors="
                    + string.Join(",", Array.ConvertAll(exits, door => door.ToString("X8", CultureInfo.InvariantCulture))));
            }

            return exits;
        }

        private PlayfieldMetaData? ReadPlayfieldMetaData(int playfieldId)
        {
            string metadataPath = Path.Combine(
                RootPath,
                GameDataPaths.PlayfieldMetadataRelativePath(playfieldId));

            if (!File.Exists(metadataPath))
                return null;

            PlayfieldMetaData? metaData;
            try
            {
                metaData = JsonSerializer.Deserialize<PlayfieldMetaData>(
                    File.ReadAllText(metadataPath),
                    MetaDataJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield metadata could not be read: "
                    + metadataPath
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (metaData == null)
                throw new InvalidDataException("Playfield metadata was empty: " + metadataPath);

            if (!metaData.IsValid(out string error))
            {
                throw new InvalidDataException(
                    "Playfield metadata is invalid: " + metadataPath + " (" + error + ")");
            }

            if (metaData.TilemapResource != playfieldId)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData tilemapResource {0} does not match playfield {1} path={2}",
                        metaData.TilemapResource,
                        playfieldId,
                        metadataPath));
            }

            return metaData;
        }

        private PlayfieldGeometryData ReadPlayfieldGeometry(int playfieldId)
        {
            PlayfieldWalls? walls = TryDeserializeRdbObject<PlayfieldWalls>(
                Path.Combine(RootPath, GameDataPaths.PlayfieldWallsRelativePath(playfieldId)));
            PlayfieldDynels? dynels = ReadPlayfieldDynels(playfieldId);
            PlayfieldDoors? doors = TryDeserializeRdbObject<PlayfieldDoors>(
                Path.Combine(RootPath, GameDataPaths.PlayfieldDoorsRelativePath(playfieldId)));

            PlayfieldCollisionSet collision = PlayfieldCollisionLoader.Load(RootPath, playfieldId);

            return new PlayfieldGeometryData
            {
                Walls = walls,
                Dynels = dynels,
                Doors = doors,
                Collision = collision.HasCollision ? collision : null
            };
        }

        private PlayfieldDynels? ReadPlayfieldDynels(int playfieldId)
        {
            PlayfieldDynels? dynels = TryDeserializeRdbObject<PlayfieldDynels>(
                Path.Combine(RootPath, GameDataPaths.PlayfieldDynelsRelativePath(playfieldId)));
            _teleportDestinations?.Apply(playfieldId, dynels);
            return dynels;
        }

        private static T? TryDeserializeRdbObject<T>(string path)
            where T : RDBObject, new()
        {
            if (!File.Exists(path))
                return null;

            byte[] payload;
            try
            {
                payload = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield geometry could not be read: "
                    + path
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (payload.Length == 0)
                return null;

            try
            {
                return DeserializeRdbObject<T>(payload, path);
            }
            catch (Exception exception)
            {
                // Optional geometry: prefer null over aborting playfield load.
                // Callers that need hard failure use DeserializeRdbObject directly (Collision.dat).
                System.Diagnostics.Debug.WriteLine(
                    "GameData skipped unreadable geometry file "
                    + path
                    + ": "
                    + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// RDBDataExtractor writes GetRaw payloads that begin with type+id+version (12 bytes).
        /// Prefer a parse that consumes the stream; partial "success" at wrong offsets is common.
        /// </summary>
        private static T DeserializeRdbObject<T>(byte[] payload, string sourcePath)
            where T : RDBObject, new()
        {
            Exception? lastFailure = null;
            T? best = null;
            long bestRemaining = long.MaxValue;
            int[] offsets = ResolveDeserializeOffsets(payload);
            for (int i = 0; i < offsets.Length; i++)
            {
                int offset = offsets[i];
                if (offset < 0 || offset >= payload.Length)
                    continue;

                try
                {
                    T record = new();
                    // Surfaces.dat / Collision.dat strip the RDB type+id+version header.
                    // AODB SurfaceResource.Deserialize requires RecordVersion 5 on the instance.
                    if (record is SurfaceResource surface)
                        surface.RecordVersion = 5;

                    using MemoryStream stream = new(payload, offset, payload.Length - offset, writable: false);
                    using BinaryReader reader = new(stream);
                    record.Deserialize(reader);
                    long remaining = stream.Length - stream.Position;
                    if (remaining < bestRemaining)
                    {
                        best = record;
                        bestRemaining = remaining;
                        if (remaining == 0)
                            return record;
                    }
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                }
            }

            if (best != null)
                return best;

            throw new InvalidDataException(
                "Playfield geometry could not be deserialized: "
                + sourcePath
                + " ("
                + (lastFailure?.GetType().Name ?? "Error")
                + ": "
                + (lastFailure?.Message ?? "no viable offset")
                + ")",
                lastFailure);
        }

        private static int[] ResolveDeserializeOffsets(byte[] payload)
        {
            if (payload.Length < 8)
                return new[] { 0 };

            uint typeId = unchecked((uint)BitConverter.ToInt32(payload, 0));
            // AODB ResourceTypeId values used for playfield geometry (e.g. PlayfieldWall = 0xF4255).
            bool looksLikeRdbHeader =
                typeId is >= 0x000F4200 and <= 0x000F42FF
                or >= 0x000F6900 and <= 0x000F69FF
                or 0x000FDE97;

            if (!looksLikeRdbHeader)
                return new[] { 0 };

            // type(4)+id(4)+version(4) is the common GetRaw prefix for these records.
            return new[] { 12, 8, 0 };
        }

        #endregion

        private sealed class MonsterDataCatMeshPairing
        {
            public int MonsterData { get; set; }

            public int CatMesh { get; set; }

            /// <summary>MonsterData stat 2.</summary>
            public int? Mass { get; set; }

            /// <summary>MonsterData stat 421.</summary>
            public int? CharRadius { get; set; }

            /// <summary>MonsterData stat 224 (Features).</summary>
            public int? Features { get; set; }
        }

        private sealed class XpLevelRow
        {
            public int KillAward { get; set; }

            public int NextLevelXp { get; set; }

            public int XPKillRange { get; set; }
        }

        private sealed class AlienXpLevelRow
        {
            public int NextLevelXp { get; set; }

            public int MinLevel { get; set; }
        }
    }
}

