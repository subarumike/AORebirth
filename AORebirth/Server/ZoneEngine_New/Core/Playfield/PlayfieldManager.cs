namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility.Config;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Trade;
    using ZoneEngine_New.Core.Teams;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.WorldSimulation;
    using ZoneEngine.Core.Missions;
    using AORebirth.Interfaces.Persistence.Missions;
    using AORebirth.Interfaces.Persistence.Shops;

    public sealed class PlayfieldManager : IDisposable
    {
        public const int DefaultLinkDeadTimeoutSeconds = 60;

        private readonly Lock _sync = new();
        private readonly Dictionary<int, Playfield> _playfields = new();
        private readonly Dictionary<int, Player> _playersByCharacterId = new();

        /// <summary>Playfields being built off every tick thread; one build per id, shared by every waiter.</summary>
        private readonly ConcurrentDictionary<int, Lazy<Task<Playfield>>> _building = new();

        /// <summary>Players waiting on a background build to move (character id to playfield id): one move at a time.</summary>
        private readonly ConcurrentDictionary<int, int> _pendingMoveByCharacter = new();

        /// <summary>When each quest dungeon was first seen empty (UTC ticks); absent while occupied or just requested.</summary>
        private readonly Dictionary<int, long> _dungeonEmptySince = new();
        private readonly IZoneLogger _logger;
        private readonly IMessageRouter _router;
        private readonly PlayerHydrator _playerHydrator;
        private readonly IGameData _gameData;
        private readonly IItemBuilder _items;
        private readonly HashItemMinter _hashItems;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IItemInstanceIdAllocator _instanceIds;
        private readonly InventoryMoveService _inventoryMoves;
        private readonly InventoryFlushService _inventoryFlush;
        private readonly TradeService _trades;
        private readonly CharacterSnapshotService _characterSnapshot;
        private readonly IPlayfieldMetricsRegistry _metricsRegistry;
        private readonly IShopDao _shopDao;
        private bool _disposed;

        public PlayfieldManager(
            IZoneLogger logger,
            IMessageRouter router,
            PlayerHydrator playerHydrator,
            IGameData gameData,
            IItemBuilder items,
            HashItemMinter hashItems,
            IInventoryRepository inventoryRepository,
            IItemInstanceIdAllocator instanceIds,
            InventoryMoveService inventoryMoves,
            InventoryFlushService inventoryFlush,
            TradeService trades,
            CharacterSnapshotService characterSnapshot,
            IPlayfieldMetricsRegistry metricsRegistry,
            TeamService teams,
            AuthoredQuestService authoredQuests,
            IItemTemplateCatalog itemTemplates,
            IShopDao shopDao,
            Quests.QuestService? quests = null,
            ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonService? questDungeons = null,
            ZoneEngine_New.Core.Knubot.KnubotService? knubot = null)
        {
            QuestDungeons = questDungeons;
            Knubot = knubot;
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(router);
            ArgumentNullException.ThrowIfNull(playerHydrator);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(hashItems);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(instanceIds);
            ArgumentNullException.ThrowIfNull(inventoryMoves);
            ArgumentNullException.ThrowIfNull(inventoryFlush);
            ArgumentNullException.ThrowIfNull(trades);
            ArgumentNullException.ThrowIfNull(characterSnapshot);
            ArgumentNullException.ThrowIfNull(metricsRegistry);
            ArgumentNullException.ThrowIfNull(shopDao);

            _logger = logger;
            _router = router;
            _playerHydrator = playerHydrator;
            _gameData = gameData;
            _items = items;
            _hashItems = hashItems;
            _inventoryRepository = inventoryRepository;
            _instanceIds = instanceIds;
            _inventoryMoves = inventoryMoves;
            _inventoryFlush = inventoryFlush;
            _trades = trades;
            _characterSnapshot = characterSnapshot;
            _metricsRegistry = metricsRegistry;
            _shopDao = shopDao;
            Teams = teams ?? throw new ArgumentNullException(nameof(teams));
            AuthoredQuests = authoredQuests ?? throw new ArgumentNullException(nameof(authoredQuests));
            ItemTemplates = itemTemplates ?? throw new ArgumentNullException(nameof(itemTemplates));
            Quests = quests;
        }

        public TeamService Teams { get; }
        public AuthoredQuestService AuthoredQuests { get; }

        /// <summary>Knubot NPC conversations, ticked on every playfield owner.</summary>
        public ZoneEngine_New.Core.Knubot.KnubotService? Knubot { get; }

        public IItemTemplateCatalog ItemTemplates { get; }

        /// <summary>Root quest service, forwarded into every playfield container.</summary>
        public Quests.QuestService? Quests { get; }

        /// <summary>Root quest dungeon service, forwarded into every playfield container.</summary>
        public ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonService? QuestDungeons { get; }

        public static TimeSpan ResolveLinkDeadTimeout()
        {
            Config? config = ConfigReadWrite.Instance.CurrentConfig;
            int seconds = config == null ? 0 : config.LinkDeadTimeoutSeconds;
            if (seconds <= 0)
                seconds = DefaultLinkDeadTimeoutSeconds;

            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// The playfield, building it on the calling thread when it is not loaded. A tick thread should not build: use
        /// <see cref="WithPlayfield"/>. Joins a background build already running for the same id instead of building
        /// it twice.
        /// </summary>
        public Playfield GetOrCreate(int playfieldId)
        {
            if (_building.TryGetValue(playfieldId, out Lazy<Task<Playfield>>? pending))
                return pending.Value.GetAwaiter().GetResult();

            return GetOrCreateCore(playfieldId);
        }

        /// <summary>
        /// Runs <paramref name="then"/> with the destination on <paramref name="player"/>'s own playfield tick. When the
        /// destination is loaded that is right now, on the caller's thread, exactly as a direct call. Otherwise the
        /// destination is built on a background thread (no tick waits on it) and <paramref name="then"/> is queued to
        /// the player's playfield once it is ready; it must re-check anything that may have changed meanwhile (session,
        /// playfield, death). False when the player already has a move waiting on a build; the request is dropped.
        /// </summary>
        public bool WithPlayfield(int playfieldId, Player player, Action<Playfield> then)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(then);

            if (TryGet(playfieldId, out Playfield? loaded) && loaded != null)
            {
                then(loaded);
                return true;
            }

            int characterId = player.Identity.Instance;
            if (!_pendingMoveByCharacter.TryAdd(characterId, playfieldId))
                return false;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} building in background for character {1}",
                    playfieldId,
                    characterId));

            BuildInBackground(playfieldId).ContinueWith(
                build =>
                {
                    if (!build.IsCompletedSuccessfully)
                    {
                        _pendingMoveByCharacter.TryRemove(characterId, out _);
                        _logger.Error(
                            build.Exception?.GetBaseException() ?? new InvalidOperationException("Playfield build was cancelled."),
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Background build of playfield {0} failed; character {1} stays put",
                                playfieldId,
                                characterId));
                        return;
                    }

                    Playfield ready = build.Result;
                    Playfield? owner = player.Playfield;
                    if (owner == null)
                    {
                        _pendingMoveByCharacter.TryRemove(characterId, out _);
                        return;
                    }

                    owner.DispatchPlayerProjection(player, () =>
                    {
                        _pendingMoveByCharacter.TryRemove(characterId, out _);
                        then(ready);
                    });
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            return true;
        }

        Task<Playfield> BuildInBackground(int playfieldId)
        {
            Lazy<Task<Playfield>> build = _building.GetOrAdd(
                playfieldId,
                id => new Lazy<Task<Playfield>>(
                    () => Task.Run(() =>
                    {
                        try
                        {
                            return GetOrCreateCore(id);
                        }
                        finally
                        {
                            _building.TryRemove(id, out _);
                        }
                    }),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            return build.Value;
        }

        private Playfield GetOrCreateCore(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            // Retired mission world ids (the old SQL-leased instances) must never become ordinary empty RDB playfields.
            if (playfieldId >= GeneratedMissionIdentitySpace.MinimumLivePlayfield2
                && playfieldId <= GeneratedMissionIdentitySpace.MaximumLivePlayfield2)
                throw new InvalidOperationException("A retired mission world id cannot become an ordinary playfield.");

            // A quest dungeon id must never become an ordinary empty playfield (login, teleport or a crafted route).
            if (ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonIds.IsDungeonPlayfield(playfieldId))
                throw new InvalidOperationException("A quest dungeon playfield requires its quest's generated layout.");

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_playfields.TryGetValue(playfieldId, out Playfield? existing))
                    return existing;
            }

            // Construct outside the manager lock. Playfield starts a heartbeat thread that may
            // call back into Register/Unregister/FindPlayer; holding _sync here deadlocks login.
            ZoneEngine_New.Core.Metrics.TickStallWatch.Stage("pf.construct", playfieldId);
            IZoneLogger playfieldLogger = _logger.CreateForPlayfield(playfieldId);
            Identity identity = new Identity
            {
                Type = IdentityType.Playfield,
                Instance = playfieldId
            };

            Playfield created;
            if (RequiresWorldSimulation(_gameData, playfieldId))
            {
                created = new ACGPlayfield(
                    identity,
                    playfieldLogger,
                    _router,
                    this,
                    _playerHydrator,
                    _gameData,
                    _items,
                    _hashItems,
                    _inventoryRepository,
                    _instanceIds,
                    _inventoryMoves,
                    _inventoryFlush,
                    _trades,
                    _characterSnapshot,
                    _metricsRegistry,
                    _shopDao);
            }
            else
            {
                created = new Playfield(
                    identity,
                    playfieldLogger,
                    _router,
                    this,
                    _playerHydrator,
                    _gameData,
                    _items,
                    _hashItems,
                    _inventoryRepository,
                    _instanceIds,
                    _inventoryMoves,
                    _inventoryFlush,
                    _trades,
                    _characterSnapshot,
                    _metricsRegistry,
                    _shopDao);
            }

            ZoneEngine_New.Core.Metrics.TickStallWatch.Stage("pf.build", playfieldId);
            created.Build();

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_playfields.TryGetValue(playfieldId, out Playfield? raced))
                {
                    created.Dispose();
                    return raced;
                }

                _playfields[playfieldId] = created;

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PlayfieldManager created playfield {0}",
                        playfieldId));
            }

            ZoneEngine_New.Core.Metrics.TickStallWatch.Stage("pf.start", playfieldId);
            created.StartHeartbeat();
            return created;
        }

        /// <summary>
        /// Outdoor playfields carry metadata, while many retail interiors only carry Dynels.dat.
        /// An interior still needs the world simulation when it contains a portal or is the
        /// destination of a return-recording TeleportProxy; otherwise its exit boundary can never
        /// initiate the server-side area change.
        /// </summary>
        internal static bool RequiresWorldSimulation(IGameData gameData, int playfieldId)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            if (gameData.GetPlayfieldMetaData(playfieldId) != null
                || gameData.GetExitProxyDoorInstances(playfieldId).Count > 0)
            {
                return true;
            }

            var dynels = gameData.GetPlayfieldGeometry(playfieldId).Dynels?.Dynels;
            return dynels != null && dynels.Any(d => PortalDoorLandingResolver.TryReadPortal(d, out _));
        }

        public bool TryGet(int playfieldId, out Playfield? playfield)
        {
            lock (_sync)
            {
                return _playfields.TryGetValue(playfieldId, out playfield);
            }
        }

        /// <summary>
        /// The live dungeon for <paramref name="questId"/>, built from <paramref name="layout"/> when none exists.
        /// Requesting it restarts its empty timer, so a dungeon cannot be released under a player who is entering.
        /// </summary>
        public QuestDungeonPlayfield GetOrCreateQuestDungeon(int playfieldId, string questId,
            ZoneEngine_New.Core.Quests.Dungeons.DungeonLayout layout, ZoneEngine_New.Core.Quests.Dungeons.MissionEntrance entrance,
            string? targetHash = null, int quality = 0)
        {
            ArgumentNullException.ThrowIfNull(questId);
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(entrance);
            if (!ZoneEngine_New.Core.Quests.Dungeons.QuestDungeonIds.IsDungeonPlayfield(playfieldId))
                throw new ArgumentOutOfRangeException(nameof(playfieldId), "Not a quest dungeon id.");

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_playfields.TryGetValue(playfieldId, out var existing))
                {
                    _dungeonEmptySince.Remove(playfieldId);
                    return RequireQuestDungeon(existing, questId);
                }
            }

            QuestDungeonPlayfield created = new(playfieldId, questId, layout, entrance, targetHash, quality, _logger.CreateForPlayfield(playfieldId),
                _router, this, _playerHydrator, _gameData, _items, _hashItems, _inventoryRepository, _instanceIds,
                _inventoryMoves, _inventoryFlush, _trades, _characterSnapshot, _metricsRegistry, _shopDao);
            try
            {
                created.Build();
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (_playfields.TryGetValue(playfieldId, out var raced))
                    {
                        QuestDungeonPlayfield winner = RequireQuestDungeon(raced, questId);
                        _dungeonEmptySince.Remove(playfieldId);
                        created.Dispose();
                        return winner;
                    }

                    _playfields.Add(playfieldId, created);
                    _dungeonEmptySince.Remove(playfieldId);
                }
            }
            catch
            {
                created.Dispose();
                throw;
            }

            created.StartHeartbeat();
            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Quest dungeon created playfield={0} quest={1}", playfieldId, questId));
            return created;
        }

        public bool TryGetQuestDungeon(int playfieldId, out QuestDungeonPlayfield dungeon)
        {
            lock (_sync)
            {
                dungeon = (_playfields.TryGetValue(playfieldId, out var existing) ? existing as QuestDungeonPlayfield : null)!;
                return dungeon != null;
            }
        }

        /// <summary>
        /// Releases quest dungeons that have had nobody in them for <paramref name="emptyLifetime"/>. A player still
        /// linked to the dungeon (in it, transferring, or link-dead) keeps it alive. Run off every playfield tick.
        /// </summary>
        public void SweepQuestDungeons(DateTime nowUtc, TimeSpan emptyLifetime)
        {
            var released = new List<QuestDungeonPlayfield>();
            lock (_sync)
            {
                if (_disposed) return;
                // One pass over players, not one per dungeon.
                var withPlayers = new HashSet<Playfield>(ReferenceEqualityComparer.Instance);
                foreach (Player player in _playersByCharacterId.Values)
                    if (player.Playfield is Playfield current) withPlayers.Add(current);

                foreach (Playfield playfield in _playfields.Values)
                {
                    if (playfield is not QuestDungeonPlayfield dungeon) continue;
                    int id = dungeon.Identity.Instance;
                    if (withPlayers.Contains(dungeon) || dungeon.GetRequiredService<DynelRegistry>().PlayerEntities().Any())
                    {
                        _dungeonEmptySince.Remove(id);
                        continue;
                    }

                    if (!_dungeonEmptySince.TryGetValue(id, out long since))
                    {
                        _dungeonEmptySince[id] = nowUtc.Ticks;
                        continue;
                    }

                    if (nowUtc.Ticks - since >= emptyLifetime.Ticks)
                        released.Add(dungeon);
                }

                foreach (QuestDungeonPlayfield dungeon in released)
                {
                    _playfields.Remove(dungeon.Identity.Instance);
                    _dungeonEmptySince.Remove(dungeon.Identity.Instance);
                }
            }

            DisposeReleased(released);
        }

        /// <summary>Releases a quest's dungeon now if it is empty (its quest ended). False while occupied.</summary>
        public bool TryReleaseQuestDungeon(int playfieldId, string questId)
        {
            QuestDungeonPlayfield? released;
            lock (_sync)
            {
                if (_disposed || !_playfields.TryGetValue(playfieldId, out var existing)) return true;
                if (existing is not QuestDungeonPlayfield dungeon || !string.Equals(dungeon.QuestId, questId, StringComparison.Ordinal)
                    || IsOccupied(dungeon))
                    return false;

                _playfields.Remove(playfieldId);
                _dungeonEmptySince.Remove(playfieldId);
                released = dungeon;
            }

            DisposeReleased([released]);
            return true;
        }

        // Caller holds _sync.
        bool IsOccupied(Playfield playfield)
            => playfield.GetRequiredService<DynelRegistry>().PlayerEntities().Any()
                || _playersByCharacterId.Values.Any(player => ReferenceEquals(player.Playfield, playfield));

        void DisposeReleased(List<QuestDungeonPlayfield> released)
        {
            // Never dispose (join a heartbeat) while holding the manager lock.
            foreach (QuestDungeonPlayfield dungeon in released)
            {
                try
                {
                    dungeon.Dispose();
                    _logger.Info(string.Format(CultureInfo.InvariantCulture, "Quest dungeon released playfield={0} quest={1}",
                        dungeon.Identity.Instance, dungeon.QuestId));
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Quest dungeon disposal failed playfield=" + dungeon.Identity.Instance);
                }
            }
        }

        static QuestDungeonPlayfield RequireQuestDungeon(Playfield candidate, string questId)
        {
            if (candidate is not QuestDungeonPlayfield dungeon || !string.Equals(dungeon.QuestId, questId, StringComparison.Ordinal))
                throw new InvalidOperationException("Quest dungeon id is held by a different playfield.");
            return dungeon;
        }

        public bool FindPlayer(int characterId, out Player player)
        {
            lock (_sync)
            {
                return _playersByCharacterId.TryGetValue(characterId, out player!);
            }
        }

        public IReadOnlyList<Player> SnapshotPlayers()
        {
            lock (_sync)
            {
                return _playersByCharacterId.Values.ToList();
            }
        }

        public IReadOnlyList<Playfield> SnapshotPlayfields()
        {
            lock (_sync)
            {
                return _playfields.Values.ToList();
            }
        }

        public void RegisterPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            int characterId = player.Identity.Instance;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_playersByCharacterId.TryGetValue(characterId, out Player? existing)
                    && !ReferenceEquals(existing, player))
                    throw new InvalidOperationException("A character already has an authoritative player instance.");
                _playersByCharacterId[characterId] = player;
            }

            ZoneEngine_New.Core.Metrics.WatchdogFeed.Sync(player);
        }

        public void UnregisterPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            int characterId = player.Identity.Instance;
            lock (_sync)
            {
                if (_playersByCharacterId.TryGetValue(characterId, out Player? existing)
                    && ReferenceEquals(existing, player))
                {
                    _playersByCharacterId.Remove(characterId);
                }
            }

            // A move queued behind a background build is dropped with its player; free the slot.
            _pendingMoveByCharacter.TryRemove(characterId, out _);

            ZoneEngine_New.Core.Metrics.WatchdogFeed.Remove(player);
        }

        public void Dispose()
        {
            List<Playfield> playfields;
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                playfields = new List<Playfield>(_playfields.Values);
                _playfields.Clear();
                _playersByCharacterId.Clear();
            }

            foreach (Playfield playfield in playfields)
                playfield.Dispose();

            _metricsRegistry.Clear();
        }
    }
}
