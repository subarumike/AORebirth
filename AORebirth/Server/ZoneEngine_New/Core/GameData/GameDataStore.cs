namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Threading;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;
    using AORebirth.World.Collision;

    using Utility;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.WorldSimulation;

    /// <summary>
    /// Loads and caches the GameData tree from {BaseDirectory}\GameData.
    /// There is no path search. Missing root files log and degrade gracefully.
    /// </summary>
    public sealed class GameDataStore : IGameData
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
        private readonly IZoneLogger _logger;
        private NpcTemplateCatalog _npcTemplates = new(
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            new Dictionary<string, NpcLeaf>(StringComparer.Ordinal));
        private readonly Dictionary<string, int[]> _monsterWeapons =
            new(StringComparer.Ordinal);
        private HashItemCatalog _hashItems = new(
            new Dictionary<string, string[]>(StringComparer.Ordinal),
            new Dictionary<string, HashInstance>(StringComparer.Ordinal));
        private readonly Dictionary<int, VendingMachineDefinition> _vendingMachines = new();
        private readonly Dictionary<int, int> _catMeshByMonsterData = new();
        private readonly Dictionary<int, XpLevelEntry> _xpLevels = new();
        private readonly Dictionary<int, PlayfieldMetaData?> _playfieldMetaData = new();
        private readonly Dictionary<int, PlayfieldSpawnsData> _playfieldSpawns = new();
        private readonly Dictionary<int, PlayfieldNpcContentCatalog> _playfieldNpcs = new();
        private readonly Dictionary<int, PlayfieldGeometryData> _playfieldGeometry = new();
        private readonly Dictionary<int, uint?> _playfieldCharacterAppearanceOverrides = new();
        private readonly Dictionary<int, int[]?> _exitProxyDoorAllowLists = new();
        private readonly Lock _exitProxySync = new();
        private Dictionary<int, int[]>? _exitProxyDoorsByPlayfield;
        private readonly TeleportDestinationCatalog? _teleportDestinations;

        public GameDataStore(IZoneLogger logger, TeleportDestinationCatalog? teleportDestinations = null)
            : this(logger, teleportDestinations, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GameDataPaths.RootFolderName))
        {
        }

        internal GameDataStore(IZoneLogger logger, TeleportDestinationCatalog? teleportDestinations, string gameDataRoot)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _teleportDestinations = teleportDestinations;

            RootPath = Path.GetFullPath(gameDataRoot);
            PlayfieldsPath = Path.Combine(RootPath, GameDataPaths.PlayfieldsFolderName);

            EnsureRootExists();
            WorldContent = WorldContentCatalog.Load(RootPath);
            CorpseContent = CorpseContentCatalog.Load(RootPath);
            RespawnContent = RespawnContentCatalog.Load(RootPath);
            LoadNpcTemplates();
            LoadMonsterWeapons();
            LoadHashItems();
            LoadVendingMachines();
            LoadMonsterData();
            LoadXpLevels();
        }

        public string RootPath { get; }

        public WorldContentCatalog WorldContent { get; }

        public CorpseContentCatalog CorpseContent { get; }

        public RespawnContentCatalog RespawnContent { get; }

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

        public bool CanResolveMobHash(string hash)
            => _npcTemplates.CanResolve(hash);

        public bool TryGetMobTemplate(string hash, out MobTemplate template)
        {
            template = null!;
            if (string.IsNullOrEmpty(hash) || !_npcTemplates.TryGetLeaf(hash, out NpcLeaf leaf))
                return false;

            template = NpcTemplateCatalog.Materialize(leaf, leaf.MinLevel);
            return true;
        }

        public bool TryResolveMobTemplate(string hash, int? level, out MobTemplate template)
        {
            if (_npcTemplates.CanResolve(hash))
                return _npcTemplates.TryResolve(hash, level, out template);

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

        public bool TryGetAssignedItemHash(int lowId, int highId, out string hash)
            => _hashItems.TryGetAssignedHash(lowId, highId, out hash);

        public bool TryResolveHashInstance(string hash, out HashInstance instance)
            => _hashItems.TryResolveInstance(hash, out instance);

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

        public IReadOnlyList<int> GetExitProxyDoorInstances(int playfieldId)
        {
            if (playfieldId <= 0)
                return [];

            EnsureExitProxyIndex();
            return _exitProxyDoorsByPlayfield!.TryGetValue(playfieldId, out int[]? doors)
                ? doors
                : [];
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

                    string dynelsPath = Path.Combine(
                        RootPath,
                        GameDataPaths.PlayfieldDynelsRelativePath(sourcePlayfieldId));
                    PlayfieldDynels? dynels = TryDeserializeRdbObject<PlayfieldDynels>(dynelsPath);
                    _teleportDestinations?.Apply(sourcePlayfieldId, dynels);
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

        private void LoadNpcTemplates()
        {
            string path = Path.Combine(RootPath, GameDataPaths.NpcTemplatesFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "NpcTemplates.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                _npcTemplates = NpcTemplateCatalog.Parse(File.ReadAllText(path));
                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData npc templates leaves={0} families={1} from {2}",
                        _npcTemplates.LeafCount,
                        _npcTemplates.FamilyCount,
                        path));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load NpcTemplates.json from {0}; catalog empty",
                        path));
            }
        }

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

        private void LoadHashItems()
        {
            string path = Path.Combine(RootPath, GameDataPaths.ItemTemplatesFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ItemTemplates.json not found at {0}; hash catalog empty",
                        path));
                return;
            }

            try
            {
                _hashItems = HashItemCatalog.Parse(File.ReadAllText(path));
                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData hash templates={0} hash instances={1} from {2}",
                        _hashItems.CategoryCount,
                        _hashItems.InstanceCount,
                        path));
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load ItemTemplates.json from {0}; hash catalog empty",
                        path));
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
                    if (pairing.MonsterData <= 0 || pairing.CatMesh <= 0)
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
                Dictionary<string, XpLevelRow>? loaded =
                    JsonSerializer.Deserialize<Dictionary<string, XpLevelRow>>(
                        File.ReadAllText(path),
                        CatalogJsonOptions);
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
                foreach (KeyValuePair<string, XpLevelRow> pair in loaded)
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
                foreach ((int level, XpLevelRow row) in parsed)
                {
                    if (!_xpLevels.TryAdd(
                            level,
                            new XpLevelEntry
                            {
                                Level = level,
                                KillAward = row.KillAward,
                                LevelDelta = row.LevelDelta,
                                NextLevelXp = row.NextLevelXp,
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
                return null;

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
            PlayfieldDynels? dynels = TryDeserializeRdbObject<PlayfieldDynels>(
                Path.Combine(RootPath, GameDataPaths.PlayfieldDynelsRelativePath(playfieldId)));
            _teleportDestinations?.Apply(playfieldId, dynels);
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
        }

        private sealed class XpLevelRow
        {
            public int KillAward { get; set; }

            public int LevelDelta { get; set; }

            public int NextLevelXp { get; set; }
        }
    }
}
