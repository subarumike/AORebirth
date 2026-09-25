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

    using Dynel = ZoneEngine_New.Core.Entities.Dynel;
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

        /// <summary>Spawns one randomly resolved NPC from a mob template hash and registers it on this playfield.</summary>
        public NpcCharacter Spawn(
            string hash,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            if (!_gameData.TryResolveMobTemplate(hash, level, out MobTemplate template))
            {
                throw new KeyNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Mob template hash '{0}' not found",
                        hash));
            }

            return SpawnMob(template, position, heading, level, spawnSource);
        }

        /// <summary>
        /// Spawns every branch of <paramref name="hash"/>. A parent with SpawnAll creates one NPC per branch;
        /// any other hash creates the single resolved NPC.
        /// </summary>
        public IReadOnlyList<NpcCharacter> SpawnBranches(
            string hash,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            var templates = new List<MobTemplate>();
            _gameData.CollectMobSpawns(hash, level, templates);
            var spawned = new List<NpcCharacter>(templates.Count);
            for (int i = 0; i < templates.Count; i++)
            {
                if (!NpcTemplateValidation.CanSpawn(templates[i]))
                    continue;

                spawned.Add(SpawnMob(templates[i], position, heading, level, spawnSource));
            }

            return spawned;
        }

        /// <summary>Spawns one already-resolved NPC template and registers it on this playfield.</summary>
        public NpcCharacter SpawnMob(
            MobTemplate template,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(position);

            NpcTemplateValidation.RequireSpawnable(template);
            int? spawnLevel = NpcTemplateLevelPolicy.ClampRequestedLevel(template, level);
            var resolvedStats = _gameData.ComposeNpcStats(template, spawnLevel);
            NpcTemplateLevelPolicy.RequireExactLevel(template, spawnLevel, resolvedStats);
            Identity identity = _registry.AllocateNpcIdentity();
            Vector3 at = _playfield.SnapNpcSpawn(position);
            NpcCharacter npc = new NpcCharacter(identity, _items)
            {
                Playfield = _playfield,
                Name = template.Name,
                MobTemplate = template,
                Attackable = template.Attackable,
                Position = at,
                Rotation = heading ?? new Quaternion(),
                SpawnSource = spawnSource
            };

            foreach (var entry in resolvedStats)
                npc.Stats.Set((CharacterStat)entry.Key, entry.Value);

            ApplyTextures(npc, template);
            npc.FillEquipment(_gameData, _logger);
            WearCastNano.ApplyContainer(
                npc,
                npc.Equipment,
                includeWield: true,
                _items,
                _playfield.GetRequiredService<IInventoryRepository>());
            npc.Rebase();
            TryAttachShop(npc);
            if (npc.Shop == null && npc.Attackable)
                NpcBrain.Create(npc, new Vector3(at.x, at.y, at.z), NpcAiProfiles.Resolve(template.Hash));

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

        /// <summary>
        /// Spawns one randomly resolved item template hash as a static world dynel. <paramref name="level"/> is the
        /// requested item quality.
        /// </summary>
        public StaticDynel SpawnStatic(
            string hash,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            if (!_gameData.TryResolveHashInstance(hash, out HashInstance instance))
            {
                throw new KeyNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Item template hash '{0}' not found",
                        hash));
            }

            StaticDynel? dynel = SpawnStaticInstance(instance, position, heading, level, spawnSource, hash);
            if (dynel == null)
            {
                throw new KeyNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Item template hash '{0}' not found",
                        hash));
            }

            return dynel;
        }

        /// <summary>
        /// Spawns every branch of <paramref name="hash"/> as a world item.
        /// A parent with SpawnAll creates one dynel per branch; any other hash creates one dynel.
        /// </summary>
        public IReadOnlyList<StaticDynel> SpawnStaticBranches(
            string hash,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(position);

            var instances = new List<HashInstance>();
            _gameData.CollectHashSpawns(hash, instances);
            var spawned = new List<StaticDynel>(instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                StaticDynel? dynel = SpawnStaticInstance(
                    instances[i],
                    position,
                    heading,
                    level,
                    spawnSource,
                    instances[i].Hash);
                if (dynel != null)
                    spawned.Add(dynel);
            }

            return spawned;
        }

        /// <summary>
        /// Spawns one resolved item family as a world dynel. Returns null when the quality band cannot be selected.
        /// </summary>
        public StaticDynel? SpawnStaticInstance(
            HashInstance instance,
            Vector3 position,
            Quaternion? heading = null,
            int? level = null,
            SpawnSource spawnSource = SpawnSource.None,
            string? logHash = null)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ArgumentNullException.ThrowIfNull(position);

            if (!_hashItems.TryRollIdsFor(instance, level ?? 1, out int lowId, out int highId, out int quality))
                return null;

            ItemTemplate template = _items.CreateTemplate(lowId, highId, quality);
            string hash = string.IsNullOrEmpty(logHash) ? instance.Hash : logHash;
            Identity identity = _registry.AllocateStaticDynelIdentity();
            var dynel = new WorldItem(identity, template, lowId, highId, quality)
            {
                Playfield = _playfield,
                Position = position,
                Rotation = heading ?? new Quaternion(),
                SpawnSource = spawnSource
            };

            _registry.Register(dynel);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(dynel);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Spawned static hash={0} name={1} id={2} template={3}/{4} ql={5} at ({6},{7},{8})",
                    hash,
                    template.Name,
                    identity.Instance,
                    lowId,
                    highId,
                    quality,
                    position.xf,
                    position.yf,
                    position.zf));

            return dynel;
        }

        /// <summary>Removes a hash-spawned static dynel from the playfield (visibility + registry).</summary>
        public void DespawnStatic(StaticDynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);

            Identity identity = dynel.Identity;
            _playfield.GetRequiredService<PlayfieldLocality>().UnregisterDynel(dynel);
            _registry.Unregister(identity);
            dynel.Playfield = null;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Despawned static id={0} playfield={1}",
                    identity.Instance,
                    _playfield.Identity.Instance));
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

                npc.SetSpawnTexture(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Turns an NPC into a vendor when its equipment carries a shop item. A shop item is an
        /// equipment entry with both vendor price modifiers set, which is how the live templates mark
        /// the machine an NPC is standing behind.
        /// </summary>
        void TryAttachShop(NpcCharacter npc)
        {
            int last = npc.Equipment.Offset + npc.Equipment.Capacity;
            for (int slot = npc.Equipment.Offset; slot < last; slot++)
            {
                if (!npc.Equipment.Content.TryGetValue(slot, out Item? item) || item == null)
                    continue;
                if (!IsShopItem(item.Definition))
                    continue;

                var machine = new VendingMachine(_registry.AllocateVendingMachineIdentity(), item.Definition)
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
                        item.Definition.Id,
                        machine.Identity.Instance));
                return;
            }

            MobTemplate? template = npc.MobTemplate;
            if (template == null)
                return;

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
                dynel = new VendingMachine(_registry.AllocateVendingMachineIdentity(), template)
                {
                    PlacementIdentity = identity
                };
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
                player.SaveState.SeedPersisted(character, hydration.Stats);
                player.Position = _playfield.SnapFeetToFloor(player.Position);
                player.Rebase();
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

        static void SendSpawnMovement(IZoneSession session, Player player)
        {
            foreach (CharDCMoveMessage move in player.Motor.BuildSpawnMoves())
                session.Send(move);
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
            SendSpawnMovement(session, player);
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
            // The client establishes vending dynels at the pre-FullCharacter world-entry
            // boundary. The complete locality activation below sends every remaining dynel.
            _playfield.GetRequiredService<PlayfieldLocality>().PrimeVendingMachineVisibility(player);
            SendRetailWorldEntryReadyBlock(session, player);
            session.Send(full);
            SendRetailWorldEntryCompletion(session, player);
            session.State = SessionState.InPlay;

            _playfieldManager.Teams.AttachPlayer(player);
            _playfieldManager.Teams.RefreshPlayer(player);

            // Visibility must activate even when journal/quest restore fails; otherwise nearby
            // hash-spawns never send SCFU and the client cannot see or tab NPCs.
            _playfield.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);
            TryRestorePostSpawnContent(player);

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
            SendSpawnMovement(session, player);
            foreach (WeaponItemFullUpdateMessage wifu in player.BuildWeaponInstanceMessages())
                session.Send(wifu);
            // Reconnect must use the same pre-FullCharacter vending boundary as initial entry.
            _playfield.GetRequiredService<PlayfieldLocality>().PrimeVendingMachineVisibility(player);
            SendRetailWorldEntryReadyBlock(session, player);
            session.Send(reconnectFull);
            SendRetailWorldEntryCompletion(session, player);
            session.State = SessionState.InPlay;

            _playfieldManager.Teams.AttachPlayer(player);
            _playfieldManager.Teams.RefreshPlayer(player);

            _playfield.GetRequiredService<PlayfieldLocality>().ActivatePlayerVisibility(player);
            TryRestorePostSpawnContent(player);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneReconnect completed character={0} playfield={1}",
                    characterId,
                    _playfield.Identity.Instance));
        }

        /// <summary>
        /// Mission/quest restore must not block locality visibility. A bad frozen offer previously
        /// aborted spawn completion before <see cref="PlayfieldLocality.ActivatePlayerVisibility"/>,
        /// so hash-spawned NPCs never sent SCFU to the joining player.
        /// </summary>
        void TryRestorePostSpawnContent(Player player)
        {
            try
            {
                _playfieldManager.Missions.ReplayJournal(player);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Mission journal restore failed character={0}; continuing with world visibility",
                        player.Identity.Instance));
            }

            try
            {
                _playfieldManager.AuthoredQuests.Restore(player);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Authored quest restore failed character={0}; continuing with world visibility",
                        player.Identity.Instance));
            }
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
                            Value2 = (uint)player.Stats.GetOrZero(CharacterStat.SocialStatus, StatDetail.Base)
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
            player.SetTarget(Identity.None);

            player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
            _trades.Cancel(player, "left playfield");
            _playfieldManager.Dialogues.Detached(player);
            _flush.HardFlush(player);

            PlayfieldLocality locality = _playfield.GetRequiredService<PlayfieldLocality>();
            locality.DeactivatePlayerVisibility(player);
            locality.UnregisterDynel(player);
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

            Vector3 onFloor = _playfield.SnapFeetToFloor(landing);
            session.SendSamePlayfieldRespawnTeleport(onFloor);
            player.Position = onFloor;

            session.Send(
                _playfield.CreatePlayfieldAnarchyFMessage(
                    new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                    {
                        X = onFloor.xf,
                        Y = onFloor.yf,
                        Z = onFloor.zf
                    }),
                playfieldId,
                characterId);

            SimpleCharFullUpdateMessage spawn = player.BuildSpawnMessage();
            ScfuSendLog.Write(spawn);
            session.Send(spawn);
            SendSpawnMovement(session, player);
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

            player.Motor.ResetForPlayfieldTransfer(position);
            player.Playfield = _playfield;
            player.Logger = _logger;
            _registry.Register(player);
            _playfield.GetRequiredService<PlayfieldLocality>().RegisterDynel(player);
            player.SaveState.MarkDirty();
            // The next trigger sample has to start at this landing. Keeping the position from
            // the last visit draws a segment through the pad they left by, which zones them back.
            _playfield.GetService<WorldSimulationAccess>()?.Instance?.DropCharacterTriggerMemory(player.Identity.Instance);

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

            _playfieldManager.Dialogues?.Detached(npc);
            _playfield.GetRequiredService<ZoneEngine_New.Core.Mobs.NpcContentActivationService>().Detached(npc);

            npc.SetFightingTarget(Identity.None);
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
            player.SetFightingTarget(Identity.None);
            _playfieldManager.Dialogues.Detached(player);
            _playfieldManager.Teams.DetachPlayer(player);

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
