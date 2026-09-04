namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility.Config;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Network;

    public sealed class PlayfieldManager : IDisposable
    {
        public const int DefaultLinkDeadTimeoutSeconds = 60;

        private readonly Lock _sync = new();
        private readonly Dictionary<int, Playfield> _playfields = new();
        private readonly Dictionary<int, Player> _playersByCharacterId = new();
        private readonly IZoneLogger _logger;
        private readonly IMessageRouter _router;
        private readonly PlayerHydrator _playerHydrator;
        private readonly IMobTemplateCatalog _mobTemplates;
        private bool _disposed;

        public PlayfieldManager(
            IZoneLogger logger,
            IMessageRouter router,
            PlayerHydrator playerHydrator,
            IMobTemplateCatalog mobTemplates)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(router);
            ArgumentNullException.ThrowIfNull(playerHydrator);
            ArgumentNullException.ThrowIfNull(mobTemplates);

            _logger = logger;
            _router = router;
            _playerHydrator = playerHydrator;
            _mobTemplates = mobTemplates;
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

                IZoneLogger playfieldLogger = _logger.CreateForPlayfield(playfieldId);
                Identity identity = new Identity
                {
                    Type = IdentityType.Playfield,
                    Instance = playfieldId
                };

                Playfield playfield = new Playfield(
                    identity,
                    playfieldLogger,
                    _router,
                    this,
                    _playerHydrator,
                    _mobTemplates);
                _playfields[playfieldId] = playfield;

                _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "PlayfieldManager created playfield {0}",
                        playfieldId));

                return playfield;
            }
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
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                foreach (Playfield playfield in _playfields.Values)
                {
                    playfield.Dispose();
                }

                _playfields.Clear();
                _playersByCharacterId.Clear();
            }
        }
    }
}
