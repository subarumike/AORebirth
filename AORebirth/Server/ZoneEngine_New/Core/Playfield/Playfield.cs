namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
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
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Trade;
    using ZoneEngine_New.Core.WorldSimulation;

    /// <summary>
    /// Playfield instance: GameData metadata, child DI (DynelRegistry, SpawnService), heartbeat.
    /// </summary>
    public class Playfield : IPlayfield, IDisposable
    {
        private readonly IZoneLogger _logger;
        private readonly IMessageRouter _router;
        private readonly PlayfieldManager _playfieldManager;
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
        private readonly PlayfieldMetrics _metrics;
        private ServiceProvider _serviceProvider;
        private readonly DynelRegistry _dynelRegistry;
        private readonly PlayfieldInboundQueue _inbound = new();
        private PlayfieldHeartbeat? _heartBeat;
        private readonly Lock _tickSync = new();
        private int _nextContainerInventoryHandle = 1;
        // TEMP: WIFU Identity.Instance until real weapon-instance identity allocation exists.
        private int _nextWeaponInstanceId = 1;
        private volatile bool _disposed;
        private readonly ConcurrentDictionary<PlayfieldTransfer, byte> _outgoingTransfers = new();
        private readonly ConcurrentDictionary<PlayfieldTransfer, byte> _incomingTransfers = new();
        private bool _built;

        public Playfield(
            Identity playfieldIdentity,
            IZoneLogger playfieldLogger,
            IMessageRouter router,
            PlayfieldManager playfieldManager,
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
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                playfieldIdentity.Instance,
                nameof(playfieldIdentity));
            ArgumentNullException.ThrowIfNull(playfieldLogger);
            ArgumentNullException.ThrowIfNull(router);
            ArgumentNullException.ThrowIfNull(playfieldManager);
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

            Identity = playfieldIdentity;
            _logger = playfieldLogger;
            _router = router;
            _playfieldManager = playfieldManager;
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
            _metrics = metricsRegistry.GetOrCreate(playfieldIdentity.Instance);
            MetaData = _gameData.GetPlayfieldMetaData(playfieldIdentity.Instance);
            Geometry = _gameData.GetPlayfieldGeometry(playfieldIdentity.Instance);

            _serviceProvider = BuildServices().BuildServiceProvider();
            _dynelRegistry = _serviceProvider.GetRequiredService<DynelRegistry>();
            _serviceProvider.GetRequiredService<HashSpawnSystem>().Initialize(
                _serviceProvider.GetRequiredService<PlayfieldLocality>());

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield created metadata={0} walls={1} dynels={2} doors={3} tilemap={4} surface={5}",
                    MetaData == null ? "null(indoor)" : "loaded",
                    Geometry.Walls != null,
                    Geometry.Dynels != null,
                    Geometry.Doors != null,
                    Geometry.Tilemap != null,
                    Geometry.Surface != null));
        }

        /// <summary>
        /// Default Build loads static dynels (indoor / non-ACG). Outdoor world construction lives on <see cref="ACGPlayfield"/>.
        /// </summary>
        public virtual void Build()
        {
            if (_built)
                return;

            _built = true;
            Stopwatch sw = Stopwatch.StartNew();
            int staticDynels = SpawnStaticDynels();
            sw.Stop();
            _metrics.RecordBuild(sw.Elapsed.TotalMilliseconds);
            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield Build complete id={0} elapsedMs={1} statics=0 wallTriggers=0 portalTriggers=0 doors={2} staticDynels={3}",
                    Identity.Instance,
                    sw.ElapsedMilliseconds,
                    Geometry.Doors?.Doors?.Count ?? 0,
                    staticDynels));
        }

        protected PlayfieldMetrics Metrics => _metrics;
        protected bool IsBuilt => _built;
        protected void MarkBuilt() => _built = true;

        protected int SpawnStaticDynels()
            => GetRequiredService<SpawnService>().LoadStaticDynels();

        /// <summary>Starts the tick thread after the playfield is registered with <see cref="PlayfieldManager"/>.</summary>
        public void StartHeartbeat()
        {
            if (_heartBeat != null || _disposed)
                return;

            GetRequiredService<AcceptedNpcActivationService>().Activate();
            GetRequiredService<ZoneEngine_New.Core.Missions.AcceptedQuestPropService>().Activate();
            _heartBeat = new PlayfieldHeartbeat(Identity, Tick);
        }

        public Identity Identity { get; }

        protected IZoneLogger Logger => _logger;

        /// <summary>Null for playfields with no extracted GameData; those resolve to an indoor layout.</summary>
        public PlayfieldMetaData? MetaData { get; }

        /// <summary>Parsed Walls.dat / Dynels.dat / Doors.dat / Collision.dat; members null when files are missing.</summary>
        public PlayfieldGeometryData Geometry { get; }

        /// <summary>Zoning needs the destination playfield's geometry, not just this one's.</summary>
        protected IGameData GameData => _gameData;

        /// <summary>Optional world simulation assigned by <see cref="ACGPlayfield.Build"/>.</summary>
        public WorldSimulationAccess WorldAccess =>
            _serviceProvider.GetRequiredService<WorldSimulationAccess>();

        protected void RegisterWorldServices(WorldSimulation.PlayfieldWorldSimulation world)
        {
            ArgumentNullException.ThrowIfNull(world);
            WorldAccess.Instance = world;
        }

        /// <summary>
        /// Builds a PlayfieldAnarchyF login packet for this playfield.
        /// TEMP: PlayfieldX/Z hardcoded from Playfields.xml 4310; special playfield types not wired yet.
        /// </summary>
        public virtual PlayfieldAnarchyFMessage CreatePlayfieldAnarchyFMessage(Vector3 characterCoordinates)
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

        /// <summary>
        /// TEMP: Playfield-scoped unique id for WeaponItemFullUpdate Identity.Instance.
        /// Increments on every WIFU build; not tied to inventory item.InstanceId.
        /// </summary>
        public int AllocateWeaponInstanceId()
        {
            int id = _nextWeaponInstanceId;
            if (_nextWeaponInstanceId == int.MaxValue)
                _nextWeaponInstanceId = 1;
            else
                _nextWeaponInstanceId++;

            return id;
        }

        /// <summary>Called from async I/O tasks. Handlers run on the playfield tick thread.</summary>
        public bool TryEnqueue(PlayfieldInboundItem item) => _inbound.TryEnqueue(item);

        // Team projections may originate on another playfield. Never enter the
        // recipient tick lock synchronously while the caller owns another tick.
        public void DispatchPlayerProjection(Player player, Action projection)
        {
            if (_tickSync.IsHeldByCurrentThread && ReferenceEquals(player.Playfield, this))
            {
                projection();
                return;
            }
            if (!_disposed)
                _inbound.TryEnqueue(new PlayerProjectionInboundItem { Player = player, Projection = projection });
        }

        internal bool IsDisposed => _disposed;
        internal void NotifyTransportDisconnected(Player player, IZoneSession session)
            => _playfieldManager.Teams.OnTransportDisconnected(player, session);
        internal void RequireTransferTick()
        {
            if (!_tickSync.IsHeldByCurrentThread)
                throw new InvalidOperationException("Transfer world changes require the owning playfield tick.");
        }

        internal bool IsAuthoritativePlayer(Player player) =>
            _playfieldManager.FindPlayer(player.Identity.Instance, out Player current) && ReferenceEquals(current, player);

        internal bool ScheduleTransfer(PlayfieldTransfer transfer)
        {
            if (_disposed) return false;
            _outgoingTransfers.TryAdd(transfer, 0);
            if (_disposed) { _outgoingTransfers.TryRemove(transfer, out _); return false; }
            // Even an owner-tick request is queued: the caller may hold its session lock,
            // but departure must acquire PersistenceGate before that lock for the hard flush.
            _inbound.TryEnqueue(new TransferDepartureInboundItem(transfer));
            return true;
        }

        internal bool QueueTransferArrival(PlayfieldTransfer transfer)
        {
            if (_disposed) return false;
            _incomingTransfers.TryAdd(transfer, 0);
            if (_disposed) { _incomingTransfers.TryRemove(transfer, out _); return false; }
            return _inbound.TryEnqueue(new TransferArrivalInboundItem(transfer));
        }

        internal void QueueTransferReturn(PlayfieldTransfer transfer) =>
            _inbound.TryEnqueue(new TransferReturnInboundItem(transfer));
        internal void ForgetOutgoingTransfer(PlayfieldTransfer transfer) => _outgoingTransfers.TryRemove(transfer, out _);
        internal void ForgetIncomingTransfer(PlayfieldTransfer transfer) => _incomingTransfers.TryRemove(transfer, out _);

        /// <summary>Destination-owner operation only; the caller must queue across playfields.</summary>
        public void ArriveTransferredPlayer(Player player, AORebirth.Core.Vector.Vector3 position)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(position);

            RequireTransferTick();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_dynelRegistry.TryRegister(player))
                throw new InvalidOperationException("Transfer destination identity is already occupied.");
            GetRequiredService<SpawnService>().ArriveFromTransfer(player, position);
        }

        /// <summary>Soft-leave for playfield transfer. Must run on this playfield's tick thread.</summary>
        public void LeaveTransferredPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            RequireTransferTick();
            GetRequiredService<SpawnService>().LeaveForTransfer(player);
        }

        internal void RemovePartialTransferArrival(Player player)
        {
            RequireTransferTick();
            if (_dynelRegistry.TryGet(player.Identity, out Dynel? current) && ReferenceEquals(current, player))
            {
                GetRequiredService<PlayfieldLocality>().UnregisterDynel(player);
                _dynelRegistry.UnregisterExact(player);
            }
            if (ReferenceEquals(player.Playfield, this)) player.Playfield = null;
        }

        internal bool CanRestoreTransfer(Player player) =>
            (_playfieldManager.FindPlayer(player.Identity.Instance, out Player current) ? ReferenceEquals(current, player) : _disposed)
            && (!_dynelRegistry.TryGet(player.Identity, out Dynel? resident) || ReferenceEquals(resident, player));

        internal void RestoreTransfer(Player player, AORebirth.Core.Vector.Vector3 origin, AORebirth.Core.Vector.Quaternion heading)
        {
            RequireTransferTick();
            // Shutdown also restores pending departures before its ordinary snapshot/logout pass.
            GetRequiredService<SpawnService>().ArriveFromTransfer(player, origin);
            player.Motor.Warp(origin, heading);
        }

        internal void RefreshReturnedTransfer(Player player) =>
            GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);

        internal void AbandonDetachedTransfer(Player player, IZoneSession oldSession)
        {
            RequireTransferTick();
            // A replacement identity must never receive an old aggregate's snapshot or unregister.
            player.QuarantinePersistence();
            _playfieldManager.Teams.DetachPlayer(player);
            _playfieldManager.Nanos.DetachPlayer(player);
            player.NanoRuntime = null;
            if (ReferenceEquals(player.Session, oldSession)) player.EnterLinkDead(PlayfieldManager.ResolveLinkDeadTimeout());
            if (ReferenceEquals(oldSession.Player, player))
            { oldSession.UnbindPlayer(); oldSession.Close(); }
            _playfieldManager.UnregisterPlayer(player);
            player.ReleaseOnlineOwnership();
        }

        internal void ReportTransferFailure(Player player, Exception exception) =>
            _logger.Error(exception, "Transfer failed for character " + player.Identity.Instance.ToString(CultureInfo.InvariantCulture));

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
                SpawnService spawn = _serviceProvider.GetRequiredService<SpawnService>();
                foreach (PlayfieldTransfer transfer in _incomingTransfers.Keys) transfer.RequestReturn();
                foreach (PlayfieldTransfer transfer in _outgoingTransfers.Keys) transfer.SourceShutdown();
                _playfieldManager.Dialogues.Shutdown(this);
                GetRequiredService<ZoneEngine_New.Core.Missions.AcceptedQuestPropService>().Shutdown();
                Player[] remaining = [.. _dynelRegistry.PlayerEntities()];
                foreach (Player player in remaining)
                {
                    try
                    {
                        spawn.LogoutPlayer(player);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(
                            exception,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Shutdown logout failed for character {0}",
                                player.Identity.Instance));
                    }
                }

                GetRequiredService<AcceptedNpcActivationService>().Shutdown();
                _dynelRegistry.Clear();
            }

            OnDispose();
            _serviceProvider.Dispose();
        }

        protected virtual void OnDispose()
        {
        }

        public void Tick(double deltaTime)
        {
            long tickStart = Stopwatch.GetTimestamp();
            lock (_tickSync)
            {
                if (_disposed)
                    return;

                SpawnService spawn = _serviceProvider.GetRequiredService<SpawnService>();
                _inbound.Drain(_router, spawn, this);
                spawn.Tick();
                GetRequiredService<AcceptedNpcActivationService>().Tick();
                foreach (Player player in new System.Collections.Generic.List<Player>(_dynelRegistry.PlayerEntities()))
                    if (ReferenceEquals(player.Playfield, this)) _playfieldManager.Nanos.Tick(player);
                _inventoryMoves.Tick(this, deltaTime);
                _trades.Tick(this, deltaTime);

                WorldSimulation.PlayfieldWorldSimulation? world = WorldAccess.Instance;
                if (world != null)
                {
                    long worldStart = Stopwatch.GetTimestamp();
                    world.TickSoftTriggers(this, deltaTime);
                    _metrics.WorldSimTick.Record(ElapsedMilliseconds(worldStart));
                }

                _serviceProvider.GetRequiredService<PlayfieldLocality>().Tick(deltaTime);
                _playfieldManager.Dialogues.Tick(this);
                foreach (Player player in new System.Collections.Generic.List<Player>(_dynelRegistry.PlayerEntities()))
                    if (ReferenceEquals(player.Playfield, this)) _playfieldManager.Missions.PollLifecycle(player);
            }

            _metrics.TickExecution.Record(ElapsedMilliseconds(tickStart));
        }

        private static double ElapsedMilliseconds(long startTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            return elapsed * 1000.0 / Stopwatch.Frequency;
        }

        private IServiceCollection BuildServices()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddSingleton(this);
            services.AddSingleton(_logger);
            services.AddSingleton(_playfieldManager);
            services.AddSingleton(_playfieldManager.Teams);
            services.AddSingleton(_playfieldManager.Nanos);
            services.AddSingleton(_playfieldManager.Missions);
            services.AddSingleton(_playfieldManager.AuthoredQuests);
            services.AddSingleton(_playfieldManager.Dialogues);
            services.AddSingleton(_playfieldManager.ItemTemplates);
            services.AddSingleton(_playerHydrator);
            services.AddSingleton(_gameData);
            services.AddSingleton(_items);
            services.AddSingleton(_hashItems);
            services.AddSingleton(_inventoryRepository);
            services.AddSingleton(_instanceIds);
            services.AddSingleton(_inventoryMoves);
            services.AddSingleton(_inventoryFlush);
            services.AddSingleton(_trades);
            services.AddSingleton(_characterSnapshot);
            services.AddSingleton(new WorldSimulationAccess());
            services.Add(new ServiceDescriptor(typeof(Identity), Identity));
            if (MetaData != null)
            {
                services.AddSingleton(MetaData);
            }

            services.AddSingleton(Geometry);
            if (Geometry.Walls != null)
                services.AddSingleton(Geometry.Walls);
            if (Geometry.Dynels != null)
                services.AddSingleton(Geometry.Dynels);
            if (Geometry.Doors != null)
                services.AddSingleton(Geometry.Doors);
            if (Geometry.Tilemap != null)
                services.AddSingleton(Geometry.Tilemap);
            if (Geometry.Surface != null)
                services.AddSingleton(Geometry.Surface);

            services.AddSingleton<IUploadedNanoRepository, MySqlUploadedNanoRepository>();
            services.AddSingleton<DynelRegistry>();
            services.AddSingleton<PlayfieldLocality>(_ => new PlayfieldLocality(Identity.Instance, MetaData));
            services.AddSingleton<SpawnService>();
            services.AddSingleton<AcceptedNpcActivationService>();
            services.AddSingleton<ZoneEngine_New.Core.Missions.AcceptedQuestPropService>();
            services.AddSingleton<HashSpawnSystem>();
            return services;
        }
    }
}
