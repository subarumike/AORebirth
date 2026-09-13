namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AODB.Common.RDBObjects;

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
    using ZoneEngine_New.Core.Trade;

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
        private readonly HashItemMinter _hashItems;
        private readonly IItemInstanceIdAllocator _ids;
        private readonly InventoryFlushService _flush;
        private readonly TradeService _trades;
        private readonly CharacterSnapshotService _snapshot;

        public SpawnService(
            IServiceProvider services,
            DynelRegistry registry,
            IZoneLogger logger,
            Playfield playfield,
            PlayfieldManager playfieldManager,
            IGameData gameData,
            IItemBuilder items,
            HashItemMinter hashItems,
            IItemInstanceIdAllocator ids,
            InventoryFlushService flush,
            TradeService trades,
            CharacterSnapshotService snapshot)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(playfield);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(hashItems);
            ArgumentNullException.ThrowIfNull(ids);
            ArgumentNullException.ThrowIfNull(flush);
            ArgumentNullException.ThrowIfNull(trades);
            ArgumentNullException.ThrowIfNull(snapshot);

            _services = services;
            _registry = registry;
            _logger = logger;
            _playfield = playfield;
            _playfieldManager = playfieldManager;
            _gameData = gameData;
            _hashItems = hashItems;
            _items = items;
            _ids = ids;
            _flush = flush;
            _trades = trades;
            _snapshot = snapshot;
        }

        /// <summary>Spawns an NPC from a mob template hash and registers it on this playfield.</summary>
        public NpcCharacter Spawn(
            string hash,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            MobTemplate template = _gameData.RequireMobTemplate(hash);
            NpcTemplateLevelPolicy.RequireExactLevel(template, level);
            Identity identity = _registry.AllocateNpcIdentity();
            NpcCharacter npc = new NpcCharacter(identity, _items)
            {
                Playfield = _playfield,
                Name = template.Name,
                MobTemplate = template,
                Position = position,
                Rotation = heading ?? new Quaternion(),
                SpawnSource = spawnSource
            };

            foreach (var entry in template.Stats)
                npc.Stats.Set((CharacterStat)entry.Key, entry.Value);

            npc.Rebase();
            TryAttachShop(npc, template);

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
        /// Turns an NPC into a vendor when its equipment carries a shop item. A shop item is an
        /// equipment entry with both vendor price modifiers set, which is how the live templates mark
        /// the machine an NPC is standing behind.
        /// </summary>
        void TryAttachShop(NpcCharacter npc, MobTemplate template)
        {
            List<List<int>> equipment = template.Equipment;
            for (int i = 0; i < equipment.Count; i++)
            {
                List<int> pair = equipment[i];
                if (pair == null || pair.Count < 1 || pair[0] <= 0)
                    continue;

                int lowId = pair[0];
                int highId = pair.Count >= 2 && pair[1] > 0 ? pair[1] : lowId;
                ItemTemplate shopTemplate = _items.CreateTemplate(lowId, highId, 1);
                if (!IsShopItem(shopTemplate))
                    continue;

                var machine = new VendingMachine(_registry.AllocateVendingMachineIdentity(), shopTemplate)
                {
                    Playfield = _playfield,
                    Position = npc.Position,
                    Rotation = npc.Rotation,
                    SpawnSource = SpawnSource.None
                };
                npc.AttachShop(machine);

            _logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Attached shop to NPC id={0} name={1} shopTemplate={2} machine={3}",
                        npc.Identity.Instance,
                        npc.Name,
                        shopTemplate.Id,
                        machine.Identity.Instance));
                return;
            }
        }

        static bool IsShopItem(ItemTemplate template)
            => template.Stats.TryGetValue(CharacterStat.BuyModifier, out int buy)
                && buy > 0
                && template.Stats.TryGetValue(CharacterStat.SellModifier, out int sell)
                && sell > 0;

        /// <summary>
        /// Spawns a corpse for a dead character. Resolves loot before cell registration (spawn packet).
        /// </summary>
        public Corpse SpawnCorpse(Character dead)
        {
            ArgumentNullException.ThrowIfNull(dead);

            Identity identity = _registry.AllocateCorpseIdentity();
            Corpse corpse = new Corpse(identity, dead, _gameData)
            {
                Playfield = _playfield,
                SpawnSource = SpawnSource.Corpse
            };

            if (dead.TryGetLootWinner(out Identity lootWinner) && lootWinner.Instance != 0)
            {
                corpse.LootWinner = lootWinner;
                corpse.ReservedUntilUtc = DateTime.UtcNow.AddSeconds(Corpse.LootReserveSeconds);
            }

            corpse.ResolveLoot(_hashItems, _ids);

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

        /// <summary>
        /// Materializes every Dynels.dat record as a <see cref="StaticDynel"/> on this playfield.
        /// </summary>
        public int LoadStaticDynels()
        {
            PlayfieldDynels? dynels = _playfield.Geometry.Dynels;
            if (dynels?.Dynels == null)
                return 0;

            int spawned = 0;
            for (int i = 0; i < dynels.Dynels.Count; i++)
            {
                PlayfieldDynel record = dynels.Dynels[i];
                StaticDynel dynel = CreateStaticDynel(record);
                _registry.Register(dynel);
                _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(dynel);
                spawned++;
            }

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Loaded static dynels playfield={0} count={1}",
                    _playfield.Identity.Instance,
                    spawned));

            return spawned;
        }

        StaticDynel CreateStaticDynel(PlayfieldDynel record)
        {
            var identity = new Identity
            {
                Type = (IdentityType)record.IdentityType,
                Instance = record.IdentityInstance
            };

            ItemTemplate template = _items.CreateTemplate(record.TemplateId, record.TemplateId, 1);
            StaticDynel dynel;
            if (MissionTerminal.IsMissionTerminalType(identity.Type))
                dynel = new MissionTerminal(identity, template);
            else if (VendingMachine.IsVendingMachineType(identity.Type))
                dynel = new VendingMachine(identity, template);
            else
                dynel = new PlayfieldStaticDynel(identity, template);

            dynel.Playfield = _playfield;
            dynel.Position = new Vector3(record.Position.X, record.Position.Y, record.Position.Z);
            dynel.Rotation = new Quaternion(
                record.Heading.X,
                record.Heading.Y,
                record.Heading.Z,
                record.Heading.W);
            dynel.SpawnSource = SpawnSource.StaticDynel;
            return dynel;
        }

        /// <summary>Lifetime / expiry pass. Called from <see cref="Playfield.Tick"/> after inbound drain.</summary>
        public void Tick()
        {
            DespawnExpiredLinkDeadPlayers();
            DespawnExpiredCorpses();
            DespawnEmptyLootables();
        }

        void DespawnExpiredCorpses()
        {
            foreach (Dynel dynel in _registry.Dynels())
            {
                if (dynel is not Corpse corpse || !corpse.IsExpired)
                    continue;

                DespawnLootable(corpse);
            }
        }

        /// <summary>
        /// Despawns opened lootables whose loot is empty after the 1s delay.
        /// </summary>
        void DespawnEmptyLootables()
        {
            foreach (Dynel dynel in _registry.Dynels())
            {
                if (dynel is not LootableDynel lootable || !lootable.ShouldDespawnEmpty)
                    continue;

                DespawnLootable(lootable);
            }
        }

        /// <summary>Removes a corpse from the playfield (visibility + registry).</summary>
        public void DespawnCorpse(Corpse corpse) => DespawnLootable(corpse);

        /// <summary>Removes a lootable dynel from the playfield (visibility + registry).</summary>
        public void DespawnLootable(LootableDynel lootable)
        {
            ArgumentNullException.ThrowIfNull(lootable);

            if (lootable.IsOpen)
                lootable.Close();

            Identity identity = lootable.Identity;
            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(lootable);
            _registry.Unregister(identity);
            lootable.Playfield = null;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned lootable {0} id={1} playfield={2}",
                    lootable.GetType().Name,
                    identity.Instance,
                    _playfield.Identity.Instance));
        }

        public Player SpawnPlayer(IZoneSession session, CharacterHydrationResult hydration)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(hydration);

            // Validate before constructing a player, acquiring Online ownership or touching inventory.
            CharacterHydrationValidator.RequireValid(hydration);

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
            player.SpawnSource = SpawnSource.Player;
            IDisposable? ownership = _snapshot.AcquireOnlineOwnership(characterId);
            bool registered = false;
            try
            {
                _services.GetRequiredService<PlayerHydrator>().Apply(player, hydration);
                player.Rebase();
                player.NanoRuntime = _playfieldManager.Nanos;
                if (!_playfieldManager.Nanos.AttachPlayer(player))
                    throw new InvalidOperationException("Active nano hydration failed; durable state was not replaced.");
                PlayerSpawnPayloadValidator.RequireValid(player);
                PlayerSpawnPayloadValidator.RequireValidMessages(player.BuildSpawnMessage(), player.BuildFullCharacterMessage());
                _playfieldManager.RegisterPlayer(player);
                _registry.Register(player);
                registered = true;
                _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(player);
                player.EnterOnline(session);
                player.AttachOnlineOwnership(ownership);
                ownership = null;
            }
            catch
            {
                _playfieldManager.Nanos.DetachPlayer(player);
                player.NanoRuntime = null;
                if (registered)
                {
                    _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(player);
                    _registry.Unregister(identity);
                }
                _playfieldManager.UnregisterPlayer(player);
                if (ReferenceEquals(session.Player, player)) session.UnbindPlayer();
                player.Session = null;
                player.Playfield = null;
                throw;
            }
            finally
            {
                if (ownership != null)
                    _snapshot.AbandonOnlineOwnership(characterId, ownership);
            }

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

            lock (session)
            {
                CompletePendingSpawnCore(session, command);
            }
        }

        private void CompletePendingSpawnCore(IZoneSession session, PendingSpawnInboundItem command)
        {
            if (session.State != SessionState.Loading)
            {
                return;
            }

            int characterId = command.Hydration.Character.Id;
            if (_playfieldManager.FindPlayer(characterId, out _))
            {
                CompletePendingReconnect(session, new PendingReconnectInboundItem
                {
                    Session = session,
                    CharacterId = characterId
                });
                return;
            }
            Player player = SpawnPlayer(session, command.Hydration);

            session.State = SessionState.SpawnReady;
            // InitiateCompression + ChatServerInfo + PlayfieldAnarchyF + GameTime are sent from ZoneLoginHandler.

            _playfieldManager.Teams.AttachPlayer(player);
            SimpleCharFullUpdateMessage spawn = player.BuildSpawnMessage();
            FullCharacterMessage full = player.BuildFullCharacterMessage();
            PlayerSpawnPayloadValidator.RequireValidMessages(spawn, full);
            ScfuSendLog.Write(spawn);
            session.Send(spawn);
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
            SendRetailWorldEntryReadyBlock(session, player);
            session.Send(full);
            SendRetailWorldEntryCompletion(session, player);
            session.State = SessionState.InPlay;

            _playfieldManager.Teams.AttachPlayer(player);
            _playfieldManager.Teams.RefreshPlayer(player);

            _playfieldManager.Nanos.RefreshPlayer(player);
            _playfieldManager.Missions.ReplayJournal(player);
            _playfieldManager.AuthoredQuests.Restore(player);

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

            lock (session)
            {
                CompletePendingReconnectCore(session, command);
            }
        }

        private void CompletePendingReconnectCore(IZoneSession session, PendingReconnectInboundItem command)
        {
            if (session.State != SessionState.Loading)
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

            if (player.IsPersistenceQuarantined)
            {
                // An uncertain durable transaction invalidates this in-memory aggregate.
                // Remove it without writing it back; the next login must hydrate storage.
                DespawnPlayer(player);
                session.Close();
                return;
            }

            // Reject a damaged retained aggregate before stealing or publishing session ownership.
            PlayerSpawnPayloadValidator.RequireValid(player);
            PlayerSpawnPayloadValidator.RequireValidMessages(player.BuildSpawnMessage(), player.BuildFullCharacterMessage());
            StealSessionIfNeeded(player, session);
            player.EnterOnline(session);

            session.State = SessionState.SpawnReady;

            PlayerSpawnPayloadValidator.RequireValid(player);

            SimpleCharFullUpdateMessage reconnectSpawn = player.BuildSpawnMessage();
            FullCharacterMessage reconnectFull = player.BuildFullCharacterMessage();
            PlayerSpawnPayloadValidator.RequireValidMessages(reconnectSpawn, reconnectFull);
            ScfuSendLog.Write(reconnectSpawn);
            session.Send(reconnectSpawn);
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
            SendRetailWorldEntryReadyBlock(session, player);
            session.Send(reconnectFull);
            SendRetailWorldEntryCompletion(session, player);
            session.State = SessionState.InPlay;

            _playfieldManager.Teams.AttachPlayer(player);
            _playfieldManager.Teams.RefreshPlayer(player);

            _playfieldManager.Nanos.RefreshPlayer(player);
            _playfieldManager.Missions.ReplayJournal(player);
            _playfieldManager.AuthoredQuests.Restore(player);

            _playfield.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);

                _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneReconnect completed character={0} playfield={1}",
                    characterId,
                    _playfield.Identity.Instance));
        }

        private void SendRetailWorldEntryReadyBlock(IZoneSession session, Player player)
        {
            // Official capture 20260623-042326 sends the entering player's SCFU and
            // weapon state before GameTime, then SocialStatus before FullCharacter.
            if (session is IGameTimeSession clock)
                clock.RecordGameTimeSynchronization(DateTime.UtcNow);

            session.Send(
                new GameTimeMessage
                {
                    Identity = player.Identity,
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                },
                _playfield.Identity.Instance,
                player.Identity.Instance);

            session.Send(
                new StatMessage
                {
                    Identity = player.Identity,
                    Unknown = 1,
                    Stats =
                    [
                        new GameTuple<CharacterStat, uint>
                        {
                            Value1 = CharacterStat.SocialStatus,
                            Value2 = (uint)player.Stats.GetOrZero(CharacterStat.SocialStatus)
                        }
                    ]
                });
        }

        private void SendRetailWorldEntryCompletion(IZoneSession session, Player player)
        {
            // Both captured retail zone transitions finish the ready block with
            // towers, cities and SpecialAttackWeapon before the client sends CharInPlay.
            var playfieldIdentity = new Identity
            {
                Type = IdentityType.Playfield2,
                Instance = _playfield.Identity.Instance
            };

            session.Send(
                new PlayfieldAllTowersMessage
                {
                    Identity = playfieldIdentity,
                    Unknown1 = []
                });
            session.Send(
                new PlayfieldAllCitiesMessage
                {
                    Identity = playfieldIdentity,
                    Unknown = 0,
                    Payload = []
                });
            session.Send(player.BuildSpecialAttackWeaponMessage());
        }

        void DespawnExpiredLinkDeadPlayers()
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

        /// <summary>
        /// Soft-leave for cross-playfield transfer. Keeps <see cref="PlayfieldManager"/> registration and session.
        /// </summary>
        public void LeaveForTransfer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!ReferenceEquals(player.Playfield, _playfield))
                throw new InvalidOperationException("Player is not on this playfield.");

            player.SetFightingTarget(Identity.None);
            player.Target = Identity.None;

            player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
            _trades.Cancel(player, "left playfield");
            _playfieldManager.Dialogues.Detached(player);
            _flush.HardFlush(player);

            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(player);
            _registry.Unregister(player.Identity);
            player.Playfield = null;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player {0} left playfield {1} for transfer",
                    player.Identity.Instance,
                    _playfield.Identity.Instance));
        }

        /// <summary>
        /// Soft-arrive after cross-playfield transfer. Does not activate locality visibility (reconnect does).
        /// </summary>
        public void ArriveFromTransfer(Player player, Vector3 position)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(position);

            player.Position = position;
            player.Playfield = _playfield;
            player.Logger = _logger;
            _registry.Register(player);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(player);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player {0} arrived playfield {1} at ({2},{3},{4})",
                    player.Identity.Instance,
                    _playfield.Identity.Instance,
                    position.xf,
                    position.yf,
                    position.zf));
        }

        /// <summary>Removes an NPC from the playfield (visibility + registry). Does not fire death.</summary>
        public void DespawnNpc(NpcCharacter npc)
        {
            ArgumentNullException.ThrowIfNull(npc);

            _playfieldManager.Dialogues.Detached(npc);
            _playfield.GetRequiredService<ZoneEngine_New.Core.Mobs.AcceptedNpcActivationService>().Detached(npc);

            npc.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
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

            _playfieldManager.Dialogues.Detached(player);

            lock (oldSession)
            {
                // Closing the old socket cannot race the accepted reconnect's ownership.
                if (ReferenceEquals(player.Session, oldSession)) player.Session = null;
                oldSession.UnbindPlayer();
                oldSession.Close();
            }

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Session steal character={0}",
                    player.Identity.Instance));
        }

        private void DespawnPlayer(Player player)
        {
            int characterId = player.Identity.Instance;
            _playfieldManager.Dialogues.Detached(player);
            _playfieldManager.Teams.DetachPlayer(player);
            _playfieldManager.Nanos.DetachPlayer(player);
            player.NanoRuntime = null;

            if (!player.IsPersistenceQuarantined)
            {
                player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
                // Return offered items before the snapshot so a logout mid-trade cannot eat them.
                _trades.Cancel(player, "logged out");
                if (player.Inventory.IsHydrated)
                    _flush.HardFlush(player);

                _snapshot.Commit(player);
            }

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
            player.ReleaseOnlineOwnership();
            if (player.IsPersistenceQuarantined)
                _snapshot.ClearOnlineIfUnowned(characterId);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned LinkDead player {0} playfield={1}",
                    characterId,
                    _playfield.Identity.Instance));
        }
    }
}
