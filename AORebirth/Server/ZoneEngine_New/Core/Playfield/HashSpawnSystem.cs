namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;

    using Utility;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Playfield.Locality;

    internal enum HashSpawnState
    {
        Alive = 0,
        Dead = 1
    }

    /// <summary>
    /// Runtime HashSpawnPoint: district spawn entry plus Alive/Dead state and Character link.
    /// </summary>
    internal sealed class HashSpawnPoint
    {
        internal HashSpawnPoint(
            string hashText,
            Vector3 position,
            Quaternion heading,
            int respawnTimeSeconds,
            int cellId)
        {
            HashText = hashText;
            Position = position;
            Heading = heading;
            RespawnTimeSeconds = respawnTimeSeconds;
            CellId = cellId;
            State = HashSpawnState.Dead;
            NextSpawnTime = DateTime.MinValue;
        }

        internal string HashText { get; }

        internal Vector3 Position { get; }

        internal Quaternion Heading { get; }

        internal int RespawnTimeSeconds { get; }

        internal int CellId { get; }

        internal HashSpawnState State { get; set; }

        internal DateTime NextSpawnTime { get; set; }

        internal Character? Spawned { get; set; }
    }

    /// <summary>
    /// Loads Spawns.json, assigns points to cells, and drives spawn/despawn from cell heat.
    /// </summary>
    public sealed class HashSpawnSystem
    {
        private const string FallbackMobHash = "AAAA";

        private readonly Playfield _playfield;
        private readonly SpawnService _spawnService;
        private readonly IMobTemplateCatalog _mobTemplates;
        private readonly IZoneLogger _logger;
        private readonly Dictionary<int, List<HashSpawnPoint>> _pointsByCell = new();
        private readonly List<HashSpawnPoint> _allPoints = new();
        private int _spawnRate = 1;
        private bool _initialized;

        public HashSpawnSystem(
            Playfield playfield,
            SpawnService spawnService,
            IMobTemplateCatalog mobTemplates,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(spawnService);
            ArgumentNullException.ThrowIfNull(mobTemplates);
            ArgumentNullException.ThrowIfNull(logger);

            _playfield = playfield;
            _spawnService = spawnService;
            _mobTemplates = mobTemplates;
            _logger = logger;
        }

        internal int PointCount => _allPoints.Count;

        /// <summary>Called once after playfield DI is ready.</summary>
        public void Initialize(PlayfieldLocality locality)
        {
            ArgumentNullException.ThrowIfNull(locality);
            if (_initialized)
                return;

            _initialized = true;
            _spawnRate = locality.Policy.SpawnRate;
            LoadSpawns(locality.Grid);
            locality.AttachHashSpawns(
                _pointsByCell.Keys,
                OnCellSleep,
                OnCellTick,
                OnIndoorSpawnTick);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "HashSpawnSystem ready playfield={0} points={1} spawnCells={2} spawnRate={3}",
                    _playfield.Identity.Instance,
                    _allPoints.Count,
                    _pointsByCell.Count,
                    _spawnRate));
        }

        private void LoadSpawns(CellGrid grid)
        {
            PlayfieldSpawnsData data = GameDataLoader.LoadPlayfieldSpawns(_playfield.Identity.Instance);
            PlayfieldSpawnEntry[] entries = data.Spawns ?? [];
            int skipped = 0;

            foreach (PlayfieldSpawnEntry entry in entries)
            {
                if (entry == null)
                {
                    skipped++;
                    continue;
                }

                string? hashText = entry.HashText;
                if (string.IsNullOrEmpty(hashText))
                {
                    skipped++;
                    continue;
                }

                string spawnHash = hashText;
                if (!_mobTemplates.TryGet(hashText, out _))
                {
                    if (!_mobTemplates.TryGet(FallbackMobHash, out _))
                    {
                        _logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Hash spawn skipped missing mob template hash={0} and fallback={1} playfield={2}",
                                hashText,
                                FallbackMobHash,
                                _playfield.Identity.Instance));
                        skipped++;
                        continue;
                    }

                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Hash spawn missing mob template hash={0}; using fallback={1} playfield={2}",
                            hashText,
                            FallbackMobHash,
                            _playfield.Identity.Instance));
                    spawnHash = FallbackMobHash;
                }

                if (entry.Position == null || entry.Position.Length < 3)
                {
                    skipped++;
                    continue;
                }

                Vector3 position = new Vector3(entry.Position[0], entry.Position[1], entry.Position[2]);
                if (!grid.TryResolveCell(position, out Cell cell))
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Hash spawn skipped OOB hash={0} pos=({1},{2},{3}) playfield={4}",
                            hashText,
                            position.xf,
                            position.yf,
                            position.zf,
                            _playfield.Identity.Instance));
                    skipped++;
                    continue;
                }

                HashSpawnPoint point = new HashSpawnPoint(
                    spawnHash,
                    position,
                    HeadingFromAngles(entry.Angle, entry.AngleW),
                    Math.Max(0, entry.RespawnTime),
                    cell.Id);

                if (!_pointsByCell.TryGetValue(cell.Id, out List<HashSpawnPoint>? list))
                {
                    list = new List<HashSpawnPoint>();
                    _pointsByCell[cell.Id] = list;
                }

                list.Add(point);
                _allPoints.Add(point);
            }

            if (skipped > 0)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "HashSpawnSystem skipped {0} spawn entries playfield={1}",
                        skipped,
                        _playfield.Identity.Instance));
            }
        }

        private void OnCellSleep(int cellId)
        {
            if (!_pointsByCell.TryGetValue(cellId, out List<HashSpawnPoint>? points))
                return;

            foreach (HashSpawnPoint point in points)
                DespawnForSleep(point);
        }

        private void OnCellTick(int cellId)
        {
            if (!_pointsByCell.TryGetValue(cellId, out List<HashSpawnPoint>? points))
                return;

            EvaluateCell(points);
        }

        private void OnIndoorSpawnTick()
        {
            foreach (KeyValuePair<int, List<HashSpawnPoint>> pair in _pointsByCell)
                EvaluateCell(pair.Value);
        }

        private void EvaluateCell(List<HashSpawnPoint> points)
        {
            int budget = _spawnRate;
            if (budget <= 0)
                return;

            DateTime now = DateTime.UtcNow;
            foreach (HashSpawnPoint point in points)
            {
                if (budget <= 0)
                    return;

                if (!ShouldSpawn(point, now))
                    continue;

                if (!TrySpawn(point))
                    continue;

                budget--;
            }
        }

        private static bool ShouldSpawn(HashSpawnPoint point, DateTime now)
        {
            if (point.Spawned != null)
                return false;

            if (point.State == HashSpawnState.Alive)
                return true;

            return now >= point.NextSpawnTime;
        }

        private bool TrySpawn(HashSpawnPoint point)
        {
            try
            {
                Character character = _spawnService.Spawn(point.HashText, point.Position, point.Heading);
                point.Spawned = character;
                point.State = HashSpawnState.Alive;
                character.Died += OnSpawnedDied;
                LogSpawn(
                    "spawn",
                    point,
                    character.Identity.Instance,
                    character.Name);
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Hash spawn failed hash={0} cell={1} playfield={2}",
                        point.HashText,
                        point.CellId,
                        _playfield.Identity.Instance));
                return false;
            }
        }

        private void DespawnForSleep(HashSpawnPoint point)
        {
            Character? character = point.Spawned;
            if (character == null)
                return;

            character.Died -= OnSpawnedDied;
            point.Spawned = null;
            // Stay Alive — sleep-despawned; respawn on next awake tick.
            point.State = HashSpawnState.Alive;
            int instance = character.Identity.Instance;
            string? name = character.Name;
            _spawnService.DespawnNpc(character);
            LogSpawn("sleep-despawn", point, instance, name);
        }

        private void OnSpawnedDied(Character character)
        {
            HashSpawnPoint? point = FindPointForCharacter(character);
            if (point == null)
                return;

            character.Died -= OnSpawnedDied;
            point.Spawned = null;
            point.State = HashSpawnState.Dead;
            point.NextSpawnTime = DateTime.UtcNow.AddSeconds(point.RespawnTimeSeconds);
            int instance = character.Identity.Instance;
            string? name = character.Name;

            // Death removes the live dynel from the world; sleep path uses DespawnNpc explicitly.
            if (character.Playfield != null)
                _spawnService.DespawnNpc(character);

            LogSpawnDeath(point, instance, name);
        }

        private void LogSpawn(string action, HashSpawnPoint point, int identityInstance, string? name)
        {
            if (!LogUtil.HasDetail(DebugInfoDetail.Locality))
                return;

            LogUtil.Debug(
                DebugInfoDetail.Locality,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} hash-spawn {1} hash={2} cell={3} id={4} name={5} state={6} pos=({7:F1},{8:F1},{9:F1})",
                    _playfield.Identity.Instance,
                    action,
                    point.HashText,
                    point.CellId,
                    identityInstance,
                    name ?? string.Empty,
                    point.State,
                    point.Position.xf,
                    point.Position.yf,
                    point.Position.zf));
        }

        private void LogSpawnDeath(HashSpawnPoint point, int identityInstance, string? name)
        {
            if (!LogUtil.HasDetail(DebugInfoDetail.Locality))
                return;

            LogUtil.Debug(
                DebugInfoDetail.Locality,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} hash-spawn death hash={1} cell={2} id={3} name={4} nextSpawn={5:o} respawnSec={6}",
                    _playfield.Identity.Instance,
                    point.HashText,
                    point.CellId,
                    identityInstance,
                    name ?? string.Empty,
                    point.NextSpawnTime,
                    point.RespawnTimeSeconds));
        }

        private HashSpawnPoint? FindPointForCharacter(Character character)
        {
            foreach (HashSpawnPoint point in _allPoints)
            {
                if (ReferenceEquals(point.Spawned, character))
                    return point;
            }

            return null;
        }

        /// <summary>
        /// AO yaw-only headings commonly store quaternion Y/W in Angle/AngleW.
        /// </summary>
        private static Quaternion HeadingFromAngles(int angle, int angleW)
        {
            if (angle == 0 && angleW == 0)
                return new Quaternion(0, 0, 0, 1);

            double y = angle;
            double w = angleW;
            double length = Math.Sqrt((y * y) + (w * w));
            if (length <= 0.0)
                return new Quaternion(0, 0, 0, 1);

            return new Quaternion(0, y / length, 0, w / length);
        }
    }
}
