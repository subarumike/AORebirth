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

    /// <summary>One candidate centre/heading/radius for a hash spawn point.</summary>
    internal readonly struct SpawnSite
    {
        internal SpawnSite(Vector3 centre, Quaternion heading, float radius)
        {
            Centre = centre;
            Heading = heading;
            Radius = Math.Max(0f, radius);
        }

        internal Vector3 Centre { get; }

        internal Quaternion Heading { get; }

        internal float Radius { get; }
    }

    /// <summary>
    /// Runtime HashSpawnPoint: district spawn entry plus Alive/Dead state and NpcCharacter link.
    /// </summary>
    internal sealed class HashSpawnPoint
    {
        internal HashSpawnPoint(
            string hashText,
            SpawnSite[] sites,
            int respawnTimeSeconds,
            int respawnChance,
            int minLevel,
            int maxLevel,
            int cellId)
        {
            ArgumentNullException.ThrowIfNull(sites);
            if (sites.Length == 0)
                throw new ArgumentException("At least one spawn site is required.", nameof(sites));

            HashText = hashText;
            Sites = sites;
            RespawnTimeSeconds = respawnTimeSeconds;
            RespawnChance = Math.Clamp(respawnChance, 0, 100);
            MinLevel = minLevel;
            MaxLevel = maxLevel;
            CellId = cellId;
            State = HashSpawnState.Dead;
            NextSpawnTime = DateTime.UtcNow;
        }

        internal string HashText { get; }

        internal SpawnSite[] Sites { get; }

        internal int RespawnTimeSeconds { get; }

        /// <summary>Percent chance (0-100) to spawn when state is Dead and the timer has elapsed.</summary>
        internal int RespawnChance { get; }

        internal int MinLevel { get; }

        internal int MaxLevel { get; }

        internal int CellId { get; }

        internal HashSpawnState State { get; set; }

        internal DateTime NextSpawnTime { get; set; }

        internal NpcCharacter? Spawned { get; set; }
    }

    /// <summary>
    /// Loads Spawns.json, assigns points to cells, and drives spawn/despawn from cell heat.
    /// </summary>
    public sealed class HashSpawnSystem
    {
        private const string FallbackMobHash = "AAAA";

        /// <summary>Cap catch-up chance rolls so long sleep cannot explode roll count.</summary>
        private const int MaxCatchUpRolls = 64;

        private readonly Playfield _playfield;
        private readonly SpawnService _spawnService;
        private readonly IGameData _gameData;
        private readonly IZoneLogger _logger;
        private readonly Dictionary<int, List<HashSpawnPoint>> _pointsByCell = new();
        private readonly List<HashSpawnPoint> _allPoints = new();
        private readonly Dictionary<NpcCharacter, HashSpawnPoint> _pointBySpawned = new();
        private int _spawnRate = 1;
        private bool _initialized;

        public HashSpawnSystem(
            Playfield playfield,
            SpawnService spawnService,
            IGameData gameData,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(spawnService);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);

            _playfield = playfield;
            _spawnService = spawnService;
            _gameData = gameData;
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
            PlayfieldSpawnsData data = _gameData.GetPlayfieldSpawns(_playfield.Identity.Instance);
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
                if (!_gameData.TryGetMobTemplate(hashText, out _))
                {
                    if (!_gameData.TryGetMobTemplate(FallbackMobHash, out _))
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

                Vector3 primaryPosition = new Vector3(entry.Position[0], entry.Position[1], entry.Position[2]);
                if (!grid.TryResolveCell(primaryPosition, out Cell cell))
                {
                    _logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Hash spawn skipped OOB hash={0} pos=({1},{2},{3}) playfield={4}",
                            hashText,
                            primaryPosition.xf,
                            primaryPosition.yf,
                            primaryPosition.zf,
                            _playfield.Identity.Instance));
                    skipped++;
                    continue;
                }

                SpawnSite[] sites = BuildSites(entry, primaryPosition);
                if (sites.Length == 0)
                {
                    skipped++;
                    continue;
                }

                HashSpawnPoint point = new HashSpawnPoint(
                    spawnHash,
                    sites,
                    Math.Max(0, entry.RespawnTime),
                    entry.RespawnChance,
                    entry.MinLevel,
                    entry.MaxLevel,
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

        private static SpawnSite[] BuildSites(PlayfieldSpawnEntry entry, Vector3 primaryPosition)
        {
            List<SpawnSite> sites = new()
            {
                new SpawnSite(
                    primaryPosition,
                    HeadingFromAngles(entry.Angle, entry.AngleW),
                    entry.Radius)
            };

            PlayfieldRotationSpawnPoint[]? additional = entry.AdditionalPoints;
            if (additional == null)
                return sites.ToArray();

            foreach (PlayfieldRotationSpawnPoint extra in additional)
            {
                if (extra?.Position == null || extra.Position.Length < 3)
                    continue;

                sites.Add(
                    new SpawnSite(
                        new Vector3(extra.Position[0], extra.Position[1], extra.Position[2]),
                        HeadingFromAngles(extra.Angle, extra.AngleW),
                        extra.Radius));
            }

            return sites.ToArray();
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

        private bool ShouldSpawn(HashSpawnPoint point, DateTime now)
        {
            RecoverOrphanedSpawn(point);

            if (point.Spawned != null)
                return false;

            if (point.State == HashSpawnState.Alive)
                return true;

            return now >= point.NextSpawnTime;
        }

        private bool TrySpawn(HashSpawnPoint point)
        {
            RecoverOrphanedSpawn(point);

            if (point.State == HashSpawnState.Dead && !TryConsumeRespawnChance(point))
                return false;

            try
            {
                PickSpawnTransform(point, out Vector3 position, out Quaternion heading);
                NpcCharacter character = _spawnService.Spawn(
                    point.HashText,
                    position,
                    heading,
                    RollLevel(point));
                point.Spawned = character;
                point.State = HashSpawnState.Alive;
                _pointBySpawned[character] = point;
                character.Died += OnSpawnedDied;
                LogSpawn(
                    "spawn",
                    point,
                    character.Identity.Instance,
                    character.Name,
                    position);
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

        /// <summary>
        /// Dead points roll once per elapsed respawn window (catch-up while asleep).
        /// Stops at the first success so at most one spawn is created.
        /// </summary>
        private static bool TryConsumeRespawnChance(HashSpawnPoint point)
        {
            DateTime now = DateTime.UtcNow;
            int windowSeconds = point.RespawnTimeSeconds;
            int rolls = 1;
            if (windowSeconds > 0 && now > point.NextSpawnTime)
            {
                double elapsedSeconds = (now - point.NextSpawnTime).TotalSeconds;
                rolls = 1 + (int)(elapsedSeconds / windowSeconds);
                if (rolls > MaxCatchUpRolls)
                    rolls = MaxCatchUpRolls;
                else if (rolls < 1)
                    rolls = 1;
            }

            for (int i = 0; i < rolls; i++)
            {
                if (PassesRespawnChance(point))
                    return true;
            }

            point.NextSpawnTime = windowSeconds > 0
                ? now.AddSeconds(windowSeconds)
                : now;
            return false;
        }

        private static bool PassesRespawnChance(HashSpawnPoint point)
        {
            if (point.RespawnChance >= 100)
                return true;
            if (point.RespawnChance <= 0)
                return false;

            return Random.Shared.Next(100) < point.RespawnChance;
        }

        /// <summary>
        /// Inclusive random level from the spawn entry range. Returns null when the range is unset/invalid
        /// so <see cref="SpawnService"/> keeps the template level.
        /// </summary>
        private static int? RollLevel(HashSpawnPoint point)
        {
            int min = point.MinLevel;
            int max = point.MaxLevel;
            if (max < min)
                (min, max) = (max, min);
            if (max < 1)
                return null;
            if (min < 1)
                min = 1;

            if (min == max)
                return min;

            return Random.Shared.Next(min, max + 1);
        }

        private static void PickSpawnTransform(
            HashSpawnPoint point,
            out Vector3 position,
            out Quaternion heading)
        {
            SpawnSite site = point.Sites[Random.Shared.Next(point.Sites.Length)];
            heading = site.Heading;
            position = site.Centre;
            if (site.Radius <= 0f)
                return;

            // Uniform disk on XZ; Y stays at the site centre.
            double angle = Random.Shared.NextDouble() * (Math.PI * 2.0);
            double distance = Math.Sqrt(Random.Shared.NextDouble()) * site.Radius;
            position = new Vector3(
                site.Centre.xf + (float)(Math.Cos(angle) * distance),
                site.Centre.yf,
                site.Centre.zf + (float)(Math.Sin(angle) * distance));
        }

        private void DespawnForSleep(HashSpawnPoint point)
        {
            NpcCharacter? character = point.Spawned;
            if (character == null)
                return;

            character.Died -= OnSpawnedDied;
            _pointBySpawned.Remove(character);
            point.Spawned = null;
            // Stay Alive — sleep-despawned; respawn on next awake tick.
            point.State = HashSpawnState.Alive;
            int instance = character.Identity.Instance;
            string? name = character.Name;
            Vector3 position = character.Position;
            _spawnService.DespawnNpc(character);
            LogSpawn("sleep-despawn", point, instance, name, position);
        }

        private void OnSpawnedDied(Character character)
        {
            if (character is not NpcCharacter npc)
                return;
            if (!_pointBySpawned.TryGetValue(npc, out HashSpawnPoint? point))
                return;

            npc.Died -= OnSpawnedDied;
            _pointBySpawned.Remove(npc);
            point.Spawned = null;
            point.State = HashSpawnState.Dead;
            point.NextSpawnTime = DateTime.UtcNow.AddSeconds(point.RespawnTimeSeconds);
            int instance = npc.Identity.Instance;
            string? name = npc.Name;

            // Death removes the live dynel from the world; sleep path uses DespawnNpc explicitly.
            if (npc.Playfield != null)
                _spawnService.DespawnNpc(npc);

            LogSpawnDeath(point, instance, name);
        }

        /// <summary>
        /// Clears ownership when an NPC was removed without raising <see cref="Character.Died"/>.
        /// </summary>
        private void RecoverOrphanedSpawn(HashSpawnPoint point)
        {
            NpcCharacter? character = point.Spawned;
            if (character == null || character.Playfield != null)
                return;

            character.Died -= OnSpawnedDied;
            _pointBySpawned.Remove(character);
            point.Spawned = null;
            point.State = HashSpawnState.Dead;
            point.NextSpawnTime = DateTime.UtcNow.AddSeconds(point.RespawnTimeSeconds);
        }

        private void LogSpawn(
            string action,
            HashSpawnPoint point,
            int identityInstance,
            string? name,
            Vector3 position)
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
                    position.xf,
                    position.yf,
                    position.zf));
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
