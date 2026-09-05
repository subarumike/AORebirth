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

    using Utility;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;

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
        private readonly Dictionary<string, MobTemplate> _mobTemplates =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, LootItemPair[]> _lootTables =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, int> _catMeshByMonsterData = new();
        private readonly Dictionary<int, PlayfieldMetaData?> _playfieldMetaData = new();
        private readonly Dictionary<int, PlayfieldSpawnsData> _playfieldSpawns = new();
        private readonly Dictionary<int, PlayfieldGeometryData> _playfieldGeometry = new();

        public GameDataStore(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;

            RootPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                GameDataPaths.RootFolderName);
            PlayfieldsPath = Path.Combine(RootPath, GameDataPaths.PlayfieldsFolderName);

            EnsureRootExists();
            LoadMobTemplates();
            LoadLootTables();
            LoadMonsterData();
        }

        public string RootPath { get; }

        public string PlayfieldsPath { get; }

        public int MobTemplateCount => _mobTemplates.Count;

        public int LootTableCount => _lootTables.Count;

        public int MonsterDataCount => _catMeshByMonsterData.Count;

        public bool TryGetMobTemplate(string hash, out MobTemplate template)
        {
            if (string.IsNullOrEmpty(hash))
            {
                template = null!;
                return false;
            }

            return _mobTemplates.TryGetValue(hash, out template!);
        }

        public MobTemplate RequireMobTemplate(string hash)
        {
            if (TryGetMobTemplate(hash, out MobTemplate template))
                return template;

            throw new KeyNotFoundException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mob template hash '{0}' not found",
                    hash));
        }

        public bool TryGetLootTable(string hash, out IReadOnlyList<LootItemPair> pairs)
        {
            if (string.IsNullOrEmpty(hash) || !_lootTables.TryGetValue(hash, out LootItemPair[]? entries))
            {
                pairs = Array.Empty<LootItemPair>();
                return false;
            }

            pairs = entries;
            return true;
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

        private void LoadMobTemplates()
        {
            string path = Path.Combine(RootPath, GameDataPaths.MobTemplatesFileName);
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
                List<MobTemplate>? loaded =
                    JsonSerializer.Deserialize<List<MobTemplate>>(File.ReadAllText(path), CatalogJsonOptions);
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

                    if (!_mobTemplates.TryAdd(template.Hash, template))
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
                        "GameData mob templates={0} from {1}",
                        _mobTemplates.Count,
                        path));

                if (skipped > 0)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "GameData skipped {0} mob templates (empty or duplicate hash)",
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

        private void LoadLootTables()
        {
            string path = Path.Combine(RootPath, GameDataPaths.LootTableFileName);
            if (!File.Exists(path))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "LootTable.json not found at {0}; catalog empty",
                        path));
                return;
            }

            try
            {
                Dictionary<string, List<List<int>>>? loaded =
                    JsonSerializer.Deserialize<Dictionary<string, List<List<int>>>>(
                        File.ReadAllText(path),
                        CatalogJsonOptions);
                if (loaded == null)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "LootTable.json was empty: {0}",
                            path));
                    return;
                }

                int skipped = 0;
                foreach (KeyValuePair<string, List<List<int>>> entry in loaded)
                {
                    if (string.IsNullOrEmpty(entry.Key) || entry.Value == null)
                    {
                        skipped++;
                        continue;
                    }

                    List<LootItemPair> pairs = new();
                    foreach (List<int> pair in entry.Value)
                    {
                        if (pair == null || pair.Count < 2 || pair[0] <= 0)
                        {
                            skipped++;
                            continue;
                        }

                        pairs.Add(new LootItemPair(pair[0], pair[1]));
                    }

                    if (pairs.Count == 0)
                    {
                        skipped++;
                        continue;
                    }

                    if (!_lootTables.TryAdd(entry.Key, pairs.ToArray()))
                    {
                        _logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Duplicate loot table hash '{0}' skipped",
                                entry.Key));
                        skipped++;
                    }
                }

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData loot tables={0} from {1}",
                        _lootTables.Count,
                        path));

                if (skipped > 0)
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "GameData skipped {0} loot entries (empty, invalid, or duplicate)",
                            skipped));
                }
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to load LootTable.json from {0}; catalog empty",
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

            data.Spawns ??= [];
            if (data.PlayfieldId == 0)
                data.PlayfieldId = playfieldId;

            return data;
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

            Tilemap? tilemap = null;
            SurfaceResource? surface = null;
            string collisionPath = Path.Combine(
                RootPath,
                GameDataPaths.PlayfieldCollisionRelativePath(playfieldId));
            if (File.Exists(collisionPath))
            {
                byte[] framed;
                try
                {
                    framed = File.ReadAllBytes(collisionPath);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        "Playfield Collision.dat could not be read: "
                        + collisionPath
                        + " ("
                        + exception.GetType().Name
                        + ": "
                        + exception.Message
                        + ")",
                        exception);
                }

                byte[] tilemapPayload;
                byte[] surfacePayload;
                try
                {
                    PlayfieldCollisionDat.Parse(framed, out tilemapPayload, out surfacePayload);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        "Playfield Collision.dat framing is invalid: "
                        + collisionPath
                        + " ("
                        + exception.GetType().Name
                        + ": "
                        + exception.Message
                        + ")",
                        exception);
                }

                if (tilemapPayload.Length > 0)
                    tilemap = DeserializeRdbObject<Tilemap>(tilemapPayload, collisionPath + "#tilemap");

                if (surfacePayload.Length > 0)
                {
                    surface = DeserializeRdbObject<SurfaceResource>(
                        surfacePayload,
                        collisionPath + "#surface");
                }
            }

            return new PlayfieldGeometryData
            {
                Walls = walls,
                Dynels = dynels,
                Tilemap = tilemap,
                Surface = surface
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

            return DeserializeRdbObject<T>(payload, path);
        }

        private static T DeserializeRdbObject<T>(byte[] payload, string sourcePath)
            where T : RDBObject, new()
        {
            T record = new();
            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                record.Deserialize(reader);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield geometry could not be deserialized: "
                    + sourcePath
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            return record;
        }

        #endregion

        private sealed class MonsterDataCatMeshPairing
        {
            public int MonsterData { get; set; }

            public int CatMesh { get; set; }
        }
    }
}
