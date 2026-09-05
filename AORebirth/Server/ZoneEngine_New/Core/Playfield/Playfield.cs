namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Globalization;
    using System.Threading;

    using AORebirth.Core.GameData;

    using Microsoft.Extensions.DependencyInjection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Playfield instance: GameData metadata, child DI (DynelRegistry, SpawnService), heartbeat.
    /// </summary>
    public sealed class Playfield : IDisposable
    {
        private readonly IZoneLogger _logger;
        private readonly IMessageRouter _router;
        private readonly PlayfieldManager _playfieldManager;
        private readonly PlayerHydrator _playerHydrator;
        private readonly IGameData _gameData;
        private readonly IItemBuilder _items;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IItemInstanceIdAllocator _instanceIds;
        private readonly InventoryMoveService _inventoryMoves;
        private readonly InventoryFlushService _inventoryFlush;
        private readonly ServiceProvider _serviceProvider;
        private readonly DynelRegistry _dynelRegistry;
        private readonly PlayfieldInboundQueue _inbound = new();
        private PlayfieldHeartbeat? _heartBeat;
        private readonly Lock _tickSync = new();
        private int _nextContainerInventoryHandle = 1;
        private bool _disposed;

        public Playfield(
            Identity playfieldIdentity,
            IZoneLogger playfieldLogger,
            IMessageRouter router,
            PlayfieldManager playfieldManager,
            PlayerHydrator playerHydrator,
            IGameData gameData,
            IItemBuilder items,
            IInventoryRepository inventoryRepository,
            IItemInstanceIdAllocator instanceIds,
            InventoryMoveService inventoryMoves,
            InventoryFlushService inventoryFlush)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                playfieldIdentity.Instance,
                nameof(playfieldIdentity));
            ArgumentNullException.ThrowIfNull(playfieldLogger);
            ArgumentNullException.ThrowIfNull(router);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(playerHydrator);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ArgumentNullException.ThrowIfNull(instanceIds);
            ArgumentNullException.ThrowIfNull(inventoryMoves);
            ArgumentNullException.ThrowIfNull(inventoryFlush);

            Identity = playfieldIdentity;
            _logger = playfieldLogger;
            _router = router;
            _playfieldManager = playfieldManager;
            _playerHydrator = playerHydrator;
            _gameData = gameData;
            _items = items;
            _inventoryRepository = inventoryRepository;
            _instanceIds = instanceIds;
            _inventoryMoves = inventoryMoves;
            _inventoryFlush = inventoryFlush;
            MetaData = _gameData.GetPlayfieldMetaData(playfieldIdentity.Instance);

            _serviceProvider = BuildServices().BuildServiceProvider();
            _dynelRegistry = _serviceProvider.GetRequiredService<DynelRegistry>();
            _serviceProvider.GetRequiredService<HashSpawnSystem>().Initialize(
                _serviceProvider.GetRequiredService<PlayfieldLocality>());

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield created metadata={0}",
                    MetaData == null ? "null(indoor)" : "loaded"));
        }

        /// <summary>Starts the tick thread after the playfield is registered with <see cref="PlayfieldManager"/>.</summary>
        public void StartHeartbeat()
        {
            if (_heartBeat != null || _disposed)
                return;

            _heartBeat = new PlayfieldHeartbeat(Identity, Tick);
        }

        public Identity Identity { get; }

        /// <summary>Null for playfields with no extracted GameData; those resolve to an indoor layout.</summary>
        public PlayfieldMetaData? MetaData { get; }

        /// <summary>
        /// Builds a PlayfieldAnarchyF login packet for this playfield.
        /// TEMP: PlayfieldX/Z hardcoded from Playfields.xml 4310; special playfield types not wired yet.
        /// </summary>
        public PlayfieldAnarchyFMessage CreatePlayfieldAnarchyFMessage(Vector3 characterCoordinates)
        {
            // TEMP: Playfields.xml 4310 (Nascense Frontier) until GameData / Playfields.xml lookup is wired.
            int playfieldX = 32321;
            int playfieldZ = 26244;

            return new PlayfieldAnarchyFMessage
            {
                Identity = new Identity
                {
                    Type = IdentityType.Playfield2,
                    Instance = Identity.Instance
                },
                CharacterCoordinates = characterCoordinates,
                PlayfieldId1 = new Identity
                {
                    Type = IdentityType.Playfield1,
                    Instance = Identity.Instance
                },
                // ACG / mission / apartment / private-city PlayfieldId1 overrides not wired yet.
                // Unknown3 / Unknown4 org-building overrides not wired yet.
                PlayfieldId2 = new Identity
                {
                    Type = IdentityType.Playfield2,
                    Instance = Identity.Instance
                },
                // PlayfieldVendorInfo — vendors not wired yet.
                // GeneratorPayload — ACG generator layouts not wired yet.
                PlayfieldX = playfieldX,
                PlayfieldZ = playfieldZ
            };

            
        }

        public T GetRequiredService<T>()
            where T : class
            => _serviceProvider.GetRequiredService<T>();

        /// <summary>Client inventory handle for an opened container (bags, corpses, chests). Range 1..ushort.MaxValue.</summary>
        public int AllocateContainerInventoryHandle()
        {
            int handle = _nextContainerInventoryHandle;
            if (_nextContainerInventoryHandle == ushort.MaxValue)
                _nextContainerInventoryHandle = 1;
            else
                _nextContainerInventoryHandle++;

            return handle;
        }

        /// <summary>Called from async I/O tasks. Handlers run on the playfield tick thread.</summary>
        public bool TryEnqueue(PlayfieldInboundItem item) => _inbound.TryEnqueue(item);

        /// <summary>
        /// Registers a transferred player on this playfield under the tick lock (safe from another PF tick).
        /// </summary>
        public void ArriveTransferredPlayer(Player player, AORebirth.Core.Vector.Vector3 position)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(position);

            lock (_tickSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                GetRequiredService<SpawnService>().ArriveFromTransfer(player, position);
            }
        }

        /// <summary>Soft-leave for playfield transfer. Must run on this playfield's tick thread.</summary>
        public void LeaveTransferredPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            GetRequiredService<SpawnService>().LeaveForTransfer(player);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _heartBeat?.Dispose();
            _heartBeat = null;

            lock (_tickSync)
            {
                foreach (Player player in _dynelRegistry.PlayerEntities())
                {
                    _playfieldManager.UnregisterPlayer(player);
                }

                _dynelRegistry.Clear();
            }

            _serviceProvider.Dispose();
        }

        public void Tick(double deltaTime)
        {
            lock (_tickSync)
            {
                if (_disposed)
                {
                    return;
                }

                SpawnService spawn = _serviceProvider.GetRequiredService<SpawnService>();
                _inbound.Drain(_router, spawn);
                spawn.Tick();
                _inventoryMoves.Tick(this, deltaTime);

                _serviceProvider.GetRequiredService<PlayfieldLocality>().Tick(deltaTime);
            }
        }

        private IServiceCollection BuildServices()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddSingleton(this);
            services.AddSingleton(_logger);
            services.AddSingleton(_playfieldManager);
            services.AddSingleton(_playerHydrator);
            services.AddSingleton(_gameData);
            services.AddSingleton(_items);
            services.AddSingleton(_inventoryRepository);
            services.AddSingleton(_instanceIds);
            services.AddSingleton(_inventoryMoves);
            services.AddSingleton(_inventoryFlush);
            services.Add(new ServiceDescriptor(typeof(Identity), Identity));
            if (MetaData != null)
            {
                services.AddSingleton(MetaData);
            }

            services.AddSingleton<DynelRegistry>();
            services.AddSingleton<PlayfieldLocality>(_ => new PlayfieldLocality(Identity.Instance, MetaData));
            services.AddSingleton<SpawnService>();
            services.AddSingleton<HashSpawnSystem>();
            return services;
        }
    }
}
