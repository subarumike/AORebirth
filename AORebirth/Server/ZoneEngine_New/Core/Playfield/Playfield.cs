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
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
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
        private readonly IMobTemplateCatalog _mobTemplates;
        private readonly ServiceProvider _serviceProvider;
        private readonly DynelRegistry _dynelRegistry;
        private readonly PlayfieldInboundQueue _inbound = new();
        private readonly PlayfieldHeartbeat _heartBeat;
        private readonly Lock _tickSync = new();
        private bool _disposed;

        public Playfield(
            Identity playfieldIdentity,
            IZoneLogger playfieldLogger,
            IMessageRouter router,
            PlayfieldManager playfieldManager,
            PlayerHydrator playerHydrator,
            IMobTemplateCatalog mobTemplates)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                playfieldIdentity.Instance,
                nameof(playfieldIdentity));
            ArgumentNullException.ThrowIfNull(playfieldLogger);
            ArgumentNullException.ThrowIfNull(router);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(playerHydrator);
            ArgumentNullException.ThrowIfNull(mobTemplates);

            Identity = playfieldIdentity;
            _logger = playfieldLogger;
            _router = router;
            _playfieldManager = playfieldManager;
            _playerHydrator = playerHydrator;
            _mobTemplates = mobTemplates;
            MetaData = GameDataLoader.LoadPlayfieldMetaData(playfieldIdentity.Instance);

            _serviceProvider = BuildServices().BuildServiceProvider();
            _dynelRegistry = _serviceProvider.GetRequiredService<DynelRegistry>();
            _serviceProvider.GetRequiredService<HashSpawnSystem>().Initialize(
                _serviceProvider.GetRequiredService<PlayfieldLocality>());

            _heartBeat = new PlayfieldHeartbeat(playfieldIdentity, Tick);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield created metadata={0}",
                    MetaData == null ? "null(indoor)" : "loaded"));
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

        /// <summary>Called from async I/O tasks. Handlers run on the playfield tick thread.</summary>
        public bool TryEnqueue(PlayfieldInboundItem item) => _inbound.TryEnqueue(item);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _heartBeat.Dispose();

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
                spawn.DespawnExpiredLinkDeadPlayers();

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
            services.AddSingleton(_mobTemplates);
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
