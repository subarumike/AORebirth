namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Textures;

    using AODB.Common.RDBObjects;

    using Microsoft.Extensions.DependencyInjection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
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
            Identity identity = _registry.AllocateNpcIdentity();
            NpcCharacter npc = new NpcCharacter(identity, _items)
            {
                Playfield = _playfield,
                Name = template.Name,
                MobTemplate = template,
                Attackable = template.Attackable,
                Position = position,
                Rotation = heading ?? new Quaternion(),
                SpawnSource = spawnSource
            };

            _gameData.TryResolveNpcFamilyStatTemplate(
                template.NpcFamily,
                out NpcFamilyStatTemplate family);
            _gameData.TryGetNpcStatTemplate(
                template.NpcStatTemplate,
                out NpcStatTemplate statTemplate);
            foreach (var entry in MobStatResolver.Resolve(template, level, family, statTemplate))
                npc.Stats.Set((CharacterStat)entry.Key, entry.Value);

            ApplyTextures(npc, template);
            npc.Rebase();
            TryAttachShop(npc, template);
            if (npc.Shop == null && npc.Attackable)
                NpcBrain.Create(npc, position, NpcAiProfiles.Resolve(template.Hash));

            _registry.Register(npc);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(npc);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned mob hash={0} name={1} id={2} level={3} at ({4},{5},{6})",
                    template.Hash,
                    template.Name,
                    identity.Instance,
                    npc.Stats.GetOrZero(CharacterStat.Level),
                    position.xf,
                    position.yf,
                    position.zf));

            return npc;
        }

        static void ApplyTextures(NpcCharacter npc, MobTemplate template)
        {
            Dictionary<int, int>? textures = template.Textures;
            if (textures == null)
                return;

            foreach (KeyValuePair<int, int> entry in textures)
            {
                if (entry.Value <= 0)
                    continue;

                npc.Textures.Add(new AOTextures(entry.Key, entry.Value));
            }
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

            corpse.ResolveLoot(_hashItems);

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
            template = DynelEventSpells.WithOnUseFromDynel(template, record);
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

            if (session.IsClosed)
                return;

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

        void DespawnExpiredLinkDeadPlayers()
        {
            DateTime now = DateTime.UtcNow;
            foreach (Player player in _registry.PlayerEntities())
            {
                if (!player.HasLinkDeadExpired(now))
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
        /// Death respawn when the hospital is on the current playfield. Mirrors live in-zone respawn:
        /// N3Teleport, playfield ready block, self spawn packets, then DeathRespawn action.
        /// </summary>
        public void CompleteSamePlayfieldDeathRespawn(Player player, Vector3 landing)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(landing);

            IZoneSession? session = player.Session;
            if (session == null)
                return;

            int characterId = player.Identity.Instance;
            int playfieldId = _playfield.Identity.Instance;

            session.SendSamePlayfieldRespawnTeleport(landing);
            player.Position = landing;

            session.Send(
                _playfield.CreatePlayfieldAnarchyFMessage(
                    new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                    {
                        X = landing.xf,
                        Y = landing.yf,
                        Z = landing.zf
                    }),
                playfieldId,
                characterId);

            SimpleCharFullUpdateMessage spawn = player.BuildSpawnMessage();
            ScfuSendLog.Write(spawn);
            session.Send(spawn);
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
            session.Send(player.BuildFullCharacterMessage());

            session.Send(
                new GameTimeMessage
                {
                    Identity = new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = characterId
                    },
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                },
                playfieldId,
                characterId);

            _playfield.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);
            player.SendDeathRespawnAction();
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

            try
            {
                player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
                // Return offered items before the snapshot so a logout mid-trade cannot eat them.
                _trades.Cancel(player, "logged out");
                if (player.Inventory.IsHydrated)
                    _flush.HardFlush(player);

                _snapshot.Commit(player);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "LinkDead persist failed character={0}; despawning anyway",
                        characterId));
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

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned LinkDead player {0} playfield={1}",
                    characterId,
                    _playfield.Identity.Instance));
        }
    }
}
