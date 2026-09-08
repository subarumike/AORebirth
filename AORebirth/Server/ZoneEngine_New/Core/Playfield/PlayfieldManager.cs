namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

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

    public sealed class PlayfieldManager : IDisposable
    {
        public const int DefaultLinkDeadTimeoutSeconds = 60;

        private readonly Lock _sync = new();
        private readonly Dictionary<int, Playfield> _playfields = new();
        private readonly Dictionary<int, Player> _playersByCharacterId = new();
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
            IPlayfieldMetricsRegistry metricsRegistry)
        {
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
        }

        public static TimeSpan ResolveLinkDeadTimeout()
        {
            Config? config = ConfigReadWrite.Instance.CurrentConfig;
            int seconds = config == null ? 0 : config.LinkDeadTimeoutSeconds;
            if (seconds <= 0)
                seconds = DefaultLinkDeadTimeoutSeconds;

            return TimeSpan.FromSeconds(seconds);
        }

        public Playfield GetOrCreate(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_playfields.TryGetValue(playfieldId, out Playfield? existing))
                    return existing;
            }

            // Construct outside the manager lock. Playfield starts a heartbeat thread that may
            // call back into Register/Unregister/FindPlayer; holding _sync here deadlocks login.
            IZoneLogger playfieldLogger = _logger.CreateForPlayfield(playfieldId);
            Identity identity = new Identity
            {
                Type = IdentityType.Playfield,
                Instance = playfieldId
            };

            Playfield created;
            if (_gameData.GetPlayfieldMetaData(playfieldId) != null)
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
                    _metricsRegistry);
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
                    _metricsRegistry);
            }

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

            created.StartHeartbeat();
            return created;
        }

        public bool TryGet(int playfieldId, out Playfield? playfield)
        {
            lock (_sync)
            {
                return _playfields.TryGetValue(playfieldId, out playfield);
            }
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

        public void RegisterPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            int characterId = player.Identity.Instance;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _playersByCharacterId[characterId] = player;
            }
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
