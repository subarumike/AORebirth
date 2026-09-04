namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Globalization;

    using Microsoft.Extensions.DependencyInjection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Playfield-scoped spawn. Constructs players via DI; session/DB row are method args.
    /// </summary>
    public sealed class SpawnService
    {
        private readonly IServiceProvider _services;
        private readonly DynelRegistry _registry;
        private readonly IZoneLogger _logger;
        private readonly Playfield _playfield;
        private readonly PlayfieldManager _playfieldManager;
        private readonly IMobTemplateCatalog _mobTemplates;

        public SpawnService(
            IServiceProvider services,
            DynelRegistry registry,
            IZoneLogger logger,
            Playfield playfield,
            PlayfieldManager playfieldManager,
            IMobTemplateCatalog mobTemplates)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(mobTemplates);

            _services = services;
            _registry = registry;
            _logger = logger;
            _playfield = playfield;
            _playfieldManager = playfieldManager;
            _mobTemplates = mobTemplates;
        }

        /// <summary>Spawns an NPC from a mob template hash and registers it on this playfield.</summary>
        public Character Spawn(string hash, Vector3 position, Quaternion? heading = null, int? level = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            MobTemplate template = _mobTemplates.Require(hash);
            Identity identity = _registry.AllocateNpcIdentity();
            Character character = new Character(identity)
            {
                Playfield = _playfield,
                Name = template.Name,
                MobTemplate = template,
                Position = position,
                Rotation = heading ?? new Quaternion()
            };

            foreach (MobStatEntry entry in template.Stats)
                character.Stats.Set((CharacterStat)entry.Key, entry.Value);

            if (level.HasValue)
                character.Stats.Set(CharacterStat.Level, level.Value);

            _registry.Register(character);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(character);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned mob hash={0} name={1} id={2} at ({3},{4},{5})",
                    template.Hash,
                    template.Name,
                    identity.Instance,
                    position.xf,
                    position.yf,
                    position.zf));

            return character;
        }

        public Player SpawnPlayer(IZoneSession session, CharacterHydrationResult hydration)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(hydration);

            CharacterRecord character = hydration.Character;
            int characterId = character.Id;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);

            Identity identity = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = characterId
            };

            Player player = ActivatorUtilities.CreateInstance<Player>(_services, identity);
            player.Playfield = _playfield;
            player.EnterOnline(session);

            _services.GetRequiredService<PlayerHydrator>().Apply(player, hydration);

            _registry.Register(player);
            _playfieldManager.RegisterPlayer(player);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(player);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned player {0} name={1} at ({2},{3},{4})",
                    characterId,
                    character.Name,
                    character.X,
                    character.Y,
                    character.Z));

            return player;
        }

        /// <summary>Called from <see cref="Playfield.Tick"/> via inbound drain only.</summary>
        public void CompletePendingSpawn(IZoneSession session, PendingSpawnInboundItem command)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(command);

            if (session.State is SessionState.InPlay or SessionState.SpawnReady)
            {
                return;
            }

            int characterId = command.Hydration.Character.Id;
            Player player = SpawnPlayer(session, command.Hydration);

            session.State = SessionState.SpawnReady;
            // InitiateCompression + ChatServerInfo + PlayfieldAnarchyF + GameTime are sent from ZoneLoginHandler.

            SimpleCharFullUpdateMessage spawn = player.BuildSpawnMessage();
            ScfuSendLog.Write(spawn);
            session.Send(spawn);
            session.Send(player.BuildFullCharacterMessage());
            session.State = SessionState.InPlay;

            _playfield.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneLogin completed character={0} playfield={1}",
                    characterId,
                    command.Hydration.Character.Playfield));
        }

        /// <summary>Called from <see cref="Playfield.Tick"/> via inbound drain only.</summary>
        public void CompletePendingReconnect(IZoneSession session, PendingReconnectInboundItem command)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(command);

            if (session.State is SessionState.InPlay or SessionState.SpawnReady)
                return;

            int characterId = command.CharacterId;
            if (!_playfieldManager.FindPlayer(characterId, out Player player)
                || !ReferenceEquals(player.Playfield, _playfield))
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Reconnect failed character={0}: not present on playfield {1}",
                        characterId,
                        _playfield.Identity.Instance));
                session.Close();
                return;
            }

            StealSessionIfNeeded(player, session);
            player.EnterOnline(session);

            session.State = SessionState.SpawnReady;

            SimpleCharFullUpdateMessage reconnectSpawn = player.BuildSpawnMessage();
            ScfuSendLog.Write(reconnectSpawn);
            session.Send(reconnectSpawn);
            session.Send(player.BuildFullCharacterMessage());
            session.State = SessionState.InPlay;

            _playfield.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneReconnect completed character={0} playfield={1}",
                    characterId,
                    _playfield.Identity.Instance));
        }

        /// <summary>Called from <see cref="Playfield.Tick"/> after inbound drain.</summary>
        public void DespawnExpiredLinkDeadPlayers()
        {
            DateTime now = DateTime.UtcNow;
            foreach (Player player in _registry.PlayerEntities())
            {
                if (player.ConnectionPhase != PlayerConnectionPhase.LinkDead)
                    continue;
                if (player.LinkDeadUntilUtc == null || player.LinkDeadUntilUtc > now)
                    continue;

                DespawnPlayer(player);
            }
        }

        /// <summary>Intentional logout / remove player from the playfield and close the session.</summary>
        public void LogoutPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            DespawnPlayer(player);
        }

        /// <summary>Removes an NPC from the playfield (visibility + registry). Does not fire death.</summary>
        public void DespawnNpc(Character character)
        {
            ArgumentNullException.ThrowIfNull(character);
            if (character is Player)
                throw new ArgumentException("Use player despawn path for players.", nameof(character));

            Identity identity = character.Identity;
            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(character);
            _registry.Unregister(identity);
            character.Playfield = null;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned NPC id={0} playfield={1}",
                    identity.Instance,
                    _playfield.Identity.Instance));
        }

        private void StealSessionIfNeeded(Player player, IZoneSession newSession)
        {
            IZoneSession? oldSession = player.Session;
            if (oldSession == null || ReferenceEquals(oldSession, newSession))
                return;

            player.Session = null;
            oldSession.UnbindPlayer();
            oldSession.Close();

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Session steal character={0}",
                    player.Identity.Instance));
        }

        private void DespawnPlayer(Player player)
        {
            int characterId = player.Identity.Instance;

            IZoneSession? session = player.Session;
            if (session != null)
            {
                player.Session = null;
                session.UnbindPlayer();
                session.Close();
            }

            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(player);
            _registry.Unregister(player.Identity);
            _playfieldManager.UnregisterPlayer(player);
            player.Playfield = null;
            player.ConnectionPhase = PlayerConnectionPhase.LinkDead;
            player.LinkDeadUntilUtc = null;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned LinkDead player {0} playfield={1}",
                    characterId,
                    _playfield.Identity.Instance));
        }
    }
}
