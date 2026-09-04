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
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
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
        private readonly IGameData _gameData;
        private readonly IItemBuilder _items;

        public SpawnService(
            IServiceProvider services,
            DynelRegistry registry,
            IZoneLogger logger,
            Playfield playfield,
            PlayfieldManager playfieldManager,
            IGameData gameData,
            IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);

            _services = services;
            _registry = registry;
            _logger = logger;
            _playfield = playfield;
            _playfieldManager = playfieldManager;
            _gameData = gameData;
            _items = items;
        }

        /// <summary>Spawns an NPC from a mob template hash and registers it on this playfield.</summary>
        public NpcCharacter Spawn(string hash, Vector3 position, Quaternion? heading = null, int? level = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            MobTemplate template = _gameData.RequireMobTemplate(hash);
            Identity identity = _registry.AllocateNpcIdentity();
            NpcCharacter npc = new NpcCharacter(identity, _items)
            {
                Playfield = _playfield,
                Name = template.Name,
                MobTemplate = template,
                Position = position,
                Rotation = heading ?? new Quaternion()
            };

            foreach (var entry in template.Stats)
                npc.Stats.Set((CharacterStat)entry.Key, entry.Value);

            if (level.HasValue)
                npc.Stats.Set(CharacterStat.Level, level.Value);

            npc.Rebase();

            _registry.Register(npc);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(npc);

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

            return npc;
        }

        /// <summary>
        /// Spawns a corpse for a dead character. Resolves loot before cell registration (spawn packet).
        /// </summary>
        public Corpse SpawnCorpse(Character dead)
        {
            ArgumentNullException.ThrowIfNull(dead);

            Identity identity = _registry.AllocateCorpseIdentity();
            Corpse corpse = new Corpse(identity, dead, _gameData)
            {
                Playfield = _playfield
            };

            corpse.ResolveLoot(_gameData, _items);

            _registry.Register(corpse);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(corpse);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned corpse id={0} owner={1} name={2} loot={3}",
                    identity.Instance,
                    dead.Identity.Instance,
                    corpse.Name,
                    corpse.Loot.Content.Count));

            return corpse;
        }

        /// <summary>Called from <see cref="Playfield.Tick"/> after inbound drain.</summary>
        public void DespawnExpiredCorpses()
        {
            foreach (Dynel dynel in _registry.Dynels())
            {
                if (dynel is not Corpse corpse || !corpse.IsExpired)
                    continue;

                DespawnCorpse(corpse);
            }
        }

        /// <summary>Removes a corpse from the playfield (visibility + registry).</summary>
        public void DespawnCorpse(Corpse corpse)
        {
            ArgumentNullException.ThrowIfNull(corpse);

            if (corpse.IsOpen)
                corpse.Close();

            Identity identity = corpse.Identity;
            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(corpse);
            _registry.Unregister(identity);
            corpse.Playfield = null;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned corpse id={0} playfield={1}",
                    identity.Instance,
                    _playfield.Identity.Instance));
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
            player.Rebase();

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
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
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
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
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
        public void DespawnNpc(NpcCharacter npc)
        {
            ArgumentNullException.ThrowIfNull(npc);

            Identity identity = npc.Identity;
            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(npc);
            _registry.Unregister(identity);
            npc.Playfield = null;

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
