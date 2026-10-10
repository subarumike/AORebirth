namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;

    using AORebirth.Database.Domain.Quests;
    using AORebirth.Enums;
    using AORebirth.Interfaces.Persistence.Missions;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility.GameData.Missions;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Terminal missions as generated quests with an ACG dungeon. Accepting an offer stores a generated quest (the
    /// offer's type, text and reward), a dungeon seed and the offer's entrance, and gives the owner a mission key,
    /// all in one transaction. Anyone holding a linked key enters through that entrance; the dungeon is generated
    /// from the seed on entry and released after it has been empty for ten minutes. When the quest ends every key
    /// link goes, the keys are retired and whoever is inside is sent back out.
    /// Every request is checked on the server against its own records; clients only name slots and targets.
    /// </summary>
    public sealed class QuestDungeonService : IDisposable
    {
        public const int KeyLowId = 28577;

        /// <summary>Mission key item identity type (0xC76D).</summary>
        public const int KeyItemType = 0xC76D;

        public const int DuplicatorLowId = 28564;

        /// <summary>Most keys one dungeon can have, copies included: bounds a duplicator bug or exploit.</summary>
        public const int MaxKeysPerQuest = 12;

        const string KeyNamePrefix = "Mission key to ";

        /// <summary>Shown when a key can not be copied: its dungeon is over or already has the most keys it can.</summary>
        const string KeyRefused = "This mission key can not be copied.";

        static readonly TimeSpan EmptyDungeonLifetime = TimeSpan.FromMinutes(10);

        static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

        /// <summary>How long an ended generated quest stays stored (lets a late login return to its entrance).</summary>
        static readonly TimeSpan EndedQuestRetention = TimeSpan.FromDays(1);

        /// <summary>Minimum time between one player's key or entrance requests: they cost a database read.</summary>
        static readonly TimeSpan RequestCooldown = TimeSpan.FromMilliseconds(750);

        const float EntryHorizontalRange = 10f;

        const float EntryVerticalRange = 14f;

        const int SweepBatch = 50;

        static readonly int[] OwnedContainerTypes =
        [
            (int)IdentityType.Inventory, (int)IdentityType.WeaponPage, (int)IdentityType.ArmorPage,
            (int)IdentityType.ImplantPage, (int)IdentityType.SocialPage, (int)IdentityType.BankByRef
        ];

        /// <summary>
        /// A quest's dungeon: its parameters, expiry, the NPC hash a kill-target quest wants (null otherwise), and whether
        /// the quest is over (completed): an ended dungeon keeps whoever is inside but can no longer be entered or rebuilt.
        /// </summary>
        sealed record CachedDungeon(QuestDungeonParameters Parameters, long ExpiresAtUtcTicks, string? TargetHash, bool Ended = false);

        readonly ICharacterQuestStore _store;
        readonly QuestService _quests;
        readonly MissionDestinationCatalog _entrances;
        readonly DungeonLayoutGenerator _layouts;
        readonly IGeneratedMissionDao _offers;
        readonly IItemBuilder _items;
        readonly IItemInstanceIdAllocator _ids;
        readonly InventoryFlushService _flush;
        readonly InventoryActionService _inventoryActions;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IGameData _gameData;
        readonly IZoneLogger _logger;

        readonly ConcurrentDictionary<string, CachedDungeon> _dungeons = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<int, long> _lastRequest = new();
        readonly ConcurrentDictionary<int, string> _pendingRelease = new();
        readonly Timer _sweep;
        int _sweeping;

        public QuestDungeonService(ICharacterQuestStore store, QuestService quests, MissionDestinationCatalog entrances,
            DungeonLayoutGenerator layouts, IGeneratedMissionDao offers, IItemBuilder items, IItemInstanceIdAllocator ids,
            InventoryFlushService flush, InventoryActionService inventoryActions, Lazy<PlayfieldManager> playfields,
            IGameData gameData, IZoneLogger logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _entrances = entrances ?? throw new ArgumentNullException(nameof(entrances));
            _layouts = layouts ?? throw new ArgumentNullException(nameof(layouts));
            _offers = offers ?? throw new ArgumentNullException(nameof(offers));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _inventoryActions = inventoryActions ?? throw new ArgumentNullException(nameof(inventoryActions));
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _quests.QuestEnded += OnQuestEnded;
            _sweep = new Timer(_ => Sweep(), null, SweepInterval, SweepInterval);
        }

        public void Dispose()
        {
            _quests.QuestEnded -= OnQuestEnded;
            _sweep.Dispose();
        }

        /// <summary>
        /// Accepts one of the player's current terminal offers as a dungeon quest. Returns null on success, else the
        /// reason to show. The offer is claimed in the same transaction as the quest and key, so a repeated or raced
        /// accept finds it taken and writes nothing.
        /// </summary>
        public string? AcceptOffer(Player player, Identity offerIdentity)
        {
            ArgumentNullException.ThrowIfNull(player);
            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined || player.Session == null || player.Playfield == null || !player.Inventory.IsHydrated)
                    return "Mission acceptance is unavailable right now.";
                if (!_layouts.IsAvailable)
                    return "Mission dungeons are unavailable.";

                DateTime now = DateTime.UtcNow;
                GeneratedMissionOffer? offer;
                using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.accept.read-offers", player.Identity.Instance))
                    offer = _offers.ReadOffers(player.Identity.Instance).FirstOrDefault(value =>
                        value.OfferType == (int)offerIdentity.Type && value.OfferInstance == offerIdentity.Instance
                        && value.State == GeneratedMissionState.Offered && value.ExpiresAtUtcTicks > now.Ticks);
                if (offer == null)
                    return "That mission is no longer offered.";

                if (!TryResolveOfferEntrance(_entrances, offer, out MissionEntrancePlacement entrance))
                {
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Mission offer {0}:{1} has an unavailable or mismatched entrance {2:X8}:{3:X8} at pf={4}",
                        offer.OfferType, offer.OfferInstance, offer.EntranceType, offer.EntranceInstance, offer.DestinationPlayfield));
                    return "That mission's location is unavailable.";
                }

                if (!QuestDungeonIds.TryCreate(_ids.Allocate(), out string questId, out int dungeonPlayfield))
                    return "Mission acceptance is unavailable right now.";

                var parameters = new QuestDungeonParameters
                {
                    Seed = RandomNumberGenerator.GetInt32(int.MaxValue),
                    GeneratorVersion = DungeonLayoutGenerator.CurrentVersion,
                    DungeonPlayfield = dungeonPlayfield,
                    EntranceType = unchecked((int)entrance.IdentityType),
                    EntranceInstance = unchecked((int)entrance.IdentityInstance),
                    EntrancePlayfield = entrance.PlayfieldId,
                    EntranceName = entrance.DisplayName,
                    EntranceX = entrance.LocalX,
                    EntranceY = entrance.LocalY,
                    EntranceZ = entrance.LocalZ,
                    DestinationType = offer.DestinationType,
                    BuildingLowId = offer.EntranceLow,
                    BuildingHighId = offer.EntranceHigh,
                    MissionType = offer.MissionType,
                    Quality = offer.Quality,
                    MissionIconId = MissionRollPolicy.Current.Icon((MissionRollType)offer.MissionType)
                };
                using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.accept.layout", player.Identity.Instance))
                {
                    if (!_layouts.TryGenerate(parameters.Seed, parameters.GeneratorVersion, parameters.EntranceInstance, out _))
                        return "Mission dungeons are unavailable.";
                }

                QuestTemplate template = BuildTemplate(questId, offer);
                DateTime expires = now.AddSeconds(MissionRollPolicy.Current.AcceptedLifetimeSeconds);
                string parametersJson = parameters.ToJson();

                try
                {
                    using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.accept.flush", player.Identity.Instance))
                        _flush.HardFlush(player);
                    Item key = CreateKey();
                    if (!InventoryGrantPlan.TryCreate(player, [key], out InventoryGrantPlan plan))
                        return "Your inventory is full.";

                    var quest = new GeneratedQuestRow
                    {
                        QuestId = questId,
                        OwnerType = (int)QuestOwnerType.Character,
                        OwnerId = player.Identity.Instance,
                        DefinitionJson = System.Text.Json.JsonSerializer.Serialize(template, QuestTemplate.JsonOptions),
                        AcgBuildingGeneratorJson = parametersJson,
                        CreatedAtUtcTicks = now.Ticks,
                        ExpiresAtUtcTicks = expires.Ticks,
                        UpdatedAtUtcTicks = now.Ticks
                    };
                    var characterRow = new CharacterQuestRow
                    {
                        CharacterId = player.Identity.Instance,
                        QuestId = questId,
                        Source = (int)QuestSource.Generated,
                        State = (int)QuestState.Active,
                        Progress = 0,
                        RequiredCount = template.RequiredCount,
                        AssignedAtUtcTicks = now.Ticks,
                        UpdatedAtUtcTicks = now.Ticks
                    };

                    bool accepted;
                    using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.accept.commit", player.Identity.Instance))
                        accepted = _store.TryAcceptOfferQuest(player.Identity.Instance, offer.OfferType, offer.OfferInstance, quest,
                            characterRow, KeyRow(plan.Rows[0], now), now.Ticks);
                    if (!accepted)
                        return "That mission is no longer offered.";

                    _dungeons[questId] = new CachedDungeon(parameters, expires.Ticks, KillTarget(template));
                    plan.PublishAfterCommit(notify: false);
                    SendKey(player, key, plan.Rows[0].ContainerPlacement, parameters.EntranceName);
                    using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.accept.adopt", player.Identity.Instance))
                        _quests.AdoptGenerated(player, questId, template, now, expires, parametersJson);
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Dungeon quest accepted char={0} quest={1} offer={2}:{3} entrance={4} ({5}) dungeon={6}",
                        player.Identity.Instance, questId, offer.OfferType, offer.OfferInstance, parameters.EntranceInstance, entrance.DisplayName, dungeonPlayfield));
                    return null;
                }
                catch (DatabaseCommitOutcomeUnknownException exception)
                {
                    Quarantine(player, exception);
                    return "Mission acceptance needs to be reconciled; please reconnect.";
                }
            }
        }

        /// <summary>Resolves only the selected placement identity and verifies its exact offered WorldPos.</summary>
        internal static bool TryResolveOfferEntrance(MissionDestinationCatalog catalog, GeneratedMissionOffer offer,
            out MissionEntrancePlacement entrance)
        {
            entrance = null!;
            if (offer.EntranceType != (int)IdentityType.MissionEntrance
                || !catalog.TryGetByIdentity(unchecked((uint)offer.EntranceType), unchecked((uint)offer.EntranceInstance), out MissionEntrancePlacement selected)
                || !catalog.TryGetWorldPosition(selected.Identity, out MissionDestinationWorldPosition worldPosition)
                || unchecked((uint)offer.DestinationType) != worldPosition.PlayfieldIdentityType
                || offer.DestinationPlayfield != selected.PlayfieldId || offer.DestinationInstance != selected.PlayfieldId
                || offer.EntranceLow != worldPosition.WorldOffsetX || offer.EntranceHigh != worldPosition.WorldOffsetZ
                || BitConverter.SingleToUInt32Bits(offer.DestinationX) != selected.LocalXBits
                || BitConverter.SingleToUInt32Bits(offer.DestinationY) != selected.LocalYBits
                || BitConverter.SingleToUInt32Bits(offer.DestinationZ) != selected.LocalZBits)
                return false;

            entrance = selected;
            return true;
        }

        /// <summary>
        /// The player used a MissionEntrance. When a key they carry opens a dungeon behind this entrance they are
        /// sent into it (created from its seed if needed). False when no carried key matches, so other entrance
        /// handling can take the use.
        /// </summary>
        public bool TryEnter(Player player, Identity target)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (target.Type != IdentityType.MissionEntrance
                || !_entrances.TryGetByIdentity(unchecked((uint)target.Type), unchecked((uint)target.Instance), out MissionEntrancePlacement entrance))
                return false;

            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined || player.IsDead || player.Session is not IZoneSession session
                    || player.Playfield is not Playfield playfield || playfield is QuestDungeonPlayfield
                    || playfield.Identity.Instance != entrance.PlayfieldId || !WithinEntrance(player, entrance)
                    || !TryBeginRequest(player))
                    return false;

                List<Item> keys = CarriedKeys(player);
                if (keys.Count == 0)
                    return false;

                IDictionary<int, string> links = _store.LoadKeyQuests(keys.ConvertAll(key => key.InstanceId));
                RemoveDeadKeys(player, keys.Where(key => !links.ContainsKey(key.InstanceId)));

                long now = DateTime.UtcNow.Ticks;
                foreach (string questId in links.Values.Distinct(StringComparer.Ordinal))
                {
                    if (!TryGetDungeon(questId, out CachedDungeon dungeon) || dungeon.Ended || dungeon.ExpiresAtUtcTicks <= now
                        || unchecked((uint)dungeon.Parameters.EntranceType) != entrance.IdentityType
                        || unchecked((uint)dungeon.Parameters.EntranceInstance) != entrance.IdentityInstance)
                        continue;

                    if (!_layouts.TryGenerate(dungeon.Parameters.Seed, dungeon.Parameters.GeneratorVersion, dungeon.Parameters.EntranceInstance, out DungeonLayout layout))
                        return false;

                    QuestDungeonPlayfield world = _playfields.Value.GetOrCreateQuestDungeon(dungeon.Parameters.DungeonPlayfield, questId, layout, entrance,
                        dungeon.TargetHash, dungeon.Parameters.Quality);
                    session.TransferToPlayfield(world, layout.Spawn);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Item on item: a Mission Key Duplicator and a mission key, in either order, copy the key for the same
        /// dungeon. The duplicator is not used up. False when these are not a duplicator and a key.
        /// </summary>
        public bool TryDuplicate(Player player, Identity sourceSlot, Identity targetSlot)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!player.Inventory.IsHydrated
                || !player.Inventory.TryGetItem(sourceSlot.Type, sourceSlot.Instance, out Item source)
                || !player.Inventory.TryGetItem(targetSlot.Type, targetSlot.Instance, out Item target))
                return false;

            Item? key = source.LowId == KeyLowId ? source : target.LowId == KeyLowId ? target : null;
            Item? duplicator = source.LowId == DuplicatorLowId ? source : target.LowId == DuplicatorLowId ? target : null;
            if (key == null || duplicator == null || ReferenceEquals(key, duplicator))
                return false;

            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined || player.IsDead || player.Session == null)
                    return true;
                if (key.Locked || duplicator.Locked || !key.IsPersisted || !duplicator.IsPersisted || key.InstanceId <= 0
                    || !TryBeginRequest(player))
                    return true;

                try
                {
                    _flush.HardFlush(player);
                    Item copy = CreateKey();
                    if (!InventoryGrantPlan.TryCreate(player, [copy], out InventoryGrantPlan plan))
                    {
                        SendText(player, "Your inventory is full.");
                        return true;
                    }

                    DateTime now = DateTime.UtcNow;
                    string? questId = _store.TryAddDuplicateKey(key.InstanceId, KeyRow(plan.Rows[0], now), MaxKeysPerQuest, now.Ticks);
                    if (questId == null)
                    {
                        SendText(player, KeyRefused);
                        return true;
                    }

                    plan.PublishAfterCommit(notify: false);
                    string name = TryGetDungeon(questId, out CachedDungeon dungeon) ? dungeon.Parameters.EntranceName : string.Empty;
                    SendKey(player, copy, plan.Rows[0].ContainerPlacement, name);
                    _logger.Info(string.Format(CultureInfo.InvariantCulture, "Mission key copied char={0} quest={1} from={2} to={3}",
                        player.Identity.Instance, questId, key.InstanceId, copy.InstanceId));
                    return true;
                }
                catch (DatabaseCommitOutcomeUnknownException exception)
                {
                    Quarantine(player, exception);
                    return true;
                }
            }
        }

        /// <summary>
        /// Login inside a dungeon: the live dungeon when it still exists (the stored position stands); otherwise a
        /// new one from the quest's seed, arriving at its spawn; otherwise (quest over) its entrance, or the
        /// respawn point when the quest is gone.
        /// </summary>
        public (Playfield Playfield, Vector3? Position) ResolveLogin(int storedPlayfield)
        {
            PlayfieldManager playfields = _playfields.Value;
            if (!QuestDungeonIds.TryGetQuestId(storedPlayfield, out string questId))
                throw new ArgumentOutOfRangeException(nameof(storedPlayfield));

            if (playfields.TryGetQuestDungeon(storedPlayfield, out QuestDungeonPlayfield live))
                return (live, null);

            if (TryGetDungeon(questId, out CachedDungeon dungeon)
                && _entrances.TryGetByIdentity(unchecked((uint)dungeon.Parameters.EntranceType), unchecked((uint)dungeon.Parameters.EntranceInstance), out MissionEntrancePlacement entrance))
            {
                if (!dungeon.Ended && dungeon.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks && dungeon.Parameters.DungeonPlayfield == storedPlayfield
                    && _layouts.TryGenerate(dungeon.Parameters.Seed, dungeon.Parameters.GeneratorVersion, dungeon.Parameters.EntranceInstance, out DungeonLayout layout))
                    return (playfields.GetOrCreateQuestDungeon(storedPlayfield, questId, layout, entrance, dungeon.TargetHash,
                        dungeon.Parameters.Quality), layout.Spawn);

                return (playfields.GetOrCreate(entrance.PlayfieldId), new Vector3(entrance.LocalX, entrance.LocalY, entrance.LocalZ));
            }

            RespawnContentCatalog respawn = _gameData.RespawnContent;
            if (respawn.PlayfieldId <= 0 || respawn.Position is not { Length: 3 })
                throw new InvalidOperationException("No respawn destination is configured for a login in an ended dungeon.");
            return (playfields.GetOrCreate(respawn.PlayfieldId), new Vector3(respawn.Position[0], respawn.Position[1], respawn.Position[2]));
        }

        /// <summary>
        /// World entry (login or zoning): re-sends each carried mission key with its dungeon's name, and retires
        /// any whose dungeon is gone. One indexed read for all of the player's keys.
        /// </summary>
        public void RestoreKeys(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined || player.Session == null || !player.Inventory.IsHydrated)
                    return;

                List<Item> keys = CarriedKeys(player);
                if (keys.Count == 0)
                    return;

                IDictionary<int, string> links = _store.LoadKeyQuests(keys.ConvertAll(key => key.InstanceId));
                RemoveDeadKeys(player, keys.Where(key => !links.ContainsKey(key.InstanceId)));
                foreach (KeyValuePair<int, Item> entry in player.Inventory.Inventory.Content)
                {
                    Item key = entry.Value;
                    if (key.LowId == KeyLowId && links.TryGetValue(key.InstanceId, out string? questId)
                        && TryGetDungeon(questId, out CachedDungeon dungeon))
                        SendKeyItem(player, key, dungeon.Parameters.EntranceName, entry.Key);
                }
            }
        }

        /// <summary>Debug view of a player's mission keys: where each is, and the quest its stored link opens.</summary>
        public IReadOnlyList<object> DescribeKeys(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            var held = new List<(string Page, int Slot, Item Item)>();
            if (player.Inventory.IsHydrated)
            {
                foreach ((IdentityType pageType, Container page) in DeletablePages(player))
                {
                    foreach (KeyValuePair<int, Item> entry in page.Content.ToArray())
                    {
                        if (entry.Value.LowId == KeyLowId || entry.Value.LowId == DuplicatorLowId)
                            held.Add((pageType.ToString(), entry.Key, entry.Value));
                    }
                }
            }

            IDictionary<int, string> links = _store.LoadKeyQuests(held.ConvertAll(entry => entry.Item.InstanceId));
            var rows = new List<object>(held.Count);
            foreach ((string page, int slot, Item item) in held)
            {
                string? questId = links.TryGetValue(item.InstanceId, out string? linked) ? linked : null;
                string? entrance = questId != null && TryGetDungeon(questId, out CachedDungeon dungeon) ? dungeon.Parameters.EntranceName : null;
                rows.Add(new
                {
                    page,
                    slot,
                    kind = item.LowId == KeyLowId ? "key" : "duplicator",
                    instance = item.InstanceId,
                    identity = string.Format(CultureInfo.InvariantCulture, "{0:X}:{1}", (int)item.Identity.Type, item.Identity.Instance),
                    persisted = item.IsPersisted,
                    locked = item.Locked,
                    questId,
                    name = entrance == null ? null : KeyNamePrefix + entrance
                });
            }

            return rows;
        }

        /// <summary>The stored dungeon parameters of a quest (seed, entrance, playfield), when the quest has a dungeon.</summary>
        public bool TryGetParameters(string questId, out QuestDungeonParameters parameters)
        {
            parameters = null!;
            if (string.IsNullOrEmpty(questId) || !TryGetDungeon(questId, out CachedDungeon dungeon))
                return false;
            parameters = dungeon.Parameters;
            return true;
        }

        /// <summary>How many mission keys for <paramref name="questId"/> the player carries on their pages. One database read.</summary>
        public int CountKeysFor(Player player, string questId)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (string.IsNullOrEmpty(questId) || !player.Inventory.IsHydrated)
                return 0;

            var keys = new List<int>();
            foreach ((IdentityType _, Container page) in DeletablePages(player))
            {
                foreach (Item item in page.Content.Values)
                {
                    if (item.LowId == KeyLowId)
                        keys.Add(item.InstanceId);
                }
            }

            if (keys.Count == 0)
                return 0;

            int count = 0;
            foreach (string linked in _store.LoadKeyQuests(keys).Values)
            {
                if (string.Equals(linked, questId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        /// <summary>The entrance a quest's dungeon is reached through, when the quest has one.</summary>
        public bool TryGetEntrance(string questId, out MissionEntrancePlacement entrance)
        {
            entrance = null!;
            return !string.IsNullOrEmpty(questId) && TryGetDungeon(questId, out CachedDungeon dungeon)
                && _entrances.TryGetByIdentity(unchecked((uint)dungeon.Parameters.EntranceType), unchecked((uint)dungeon.Parameters.EntranceInstance), out entrance);
        }

        /// <summary>
        /// Before a character's items load: retires their mission keys (pages, bank and bags) whose dungeon is gone,
        /// so dead keys never reach memory or the client.
        /// </summary>
        public void RetireDeadKeysOnLogin(int characterId)
        {
            int retired = _store.RetireDeadKeysForCharacter(characterId, KeyLowId, OwnedContainerTypes, (int)IdentityType.Container);
            if (retired > 0)
                _logger.Info(string.Format(CultureInfo.InvariantCulture, "Retired {0} dead mission keys for char={1}", retired, characterId));
        }

        void OnQuestEnded(Player player, PlayerQuest quest)
        {
            if (!QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out QuestDungeonParameters parameters))
                return;

            try
            {
                // Completing the quest lets whoever is inside stay (and leave by the exit); expiry and abandoning empty it.
                EndDungeon(quest.QuestId, parameters, evict: quest.State != QuestState.Completed);
            }
            catch (Exception exception)
            {
                // The expiry sweep retries: the quest's key links are still there.
                _logger.Error(exception, "Ending dungeon for quest " + quest.QuestId + " failed; the sweep will retry");
            }
        }

        /// <summary>
        /// Removes every key link and key. With <paramref name="evict"/> everyone inside is sent back to the entrance and
        /// the dungeon released; without it (a completed quest) they stay, nobody can enter any more, and the dungeon is
        /// released once the last of them has left.
        /// </summary>
        void EndDungeon(string questId, QuestDungeonParameters parameters, bool evict)
        {
            if (evict)
                _dungeons.TryRemove(questId, out _);
            else
                _dungeons[questId] = _dungeons.TryGetValue(questId, out CachedDungeon? cached)
                    ? cached with { Ended = true }
                    : new CachedDungeon(parameters, 0, null, Ended: true);

            IList<RetiredDungeonKey> retired = _store.EndQuestKeys(questId);
            PlayfieldManager playfields = _playfields.Value;
            foreach (RetiredDungeonKey key in retired)
            {
                int holder = HolderOf(key);
                if (holder > 0 && playfields.FindPlayer(holder, out Player online) && online.Playfield is Playfield at)
                {
                    int instance = key.KeyInstanceId;
                    at.DispatchPlayerProjection(online, () => RemoveKeyInMemory(online, instance));
                }
            }

            if (playfields.TryGetQuestDungeon(parameters.DungeonPlayfield, out QuestDungeonPlayfield dungeon))
            {
                if (evict)
                {
                    Playfield exterior = playfields.GetOrCreate(dungeon.Entrance.PlayfieldId);
                    var landing = new Vector3(dungeon.Entrance.LocalX, dungeon.Entrance.LocalY, dungeon.Entrance.LocalZ);
                    foreach (Player inside in dungeon.GetRequiredService<DynelRegistry>().PlayerEntities().ToArray())
                        dungeon.DispatchPlayerProjection(inside, () => inside.Session?.TransferToPlayfield(exterior, landing));
                }

                // Occupied: the sweep releases it once empty.
                if (!playfields.TryReleaseQuestDungeon(parameters.DungeonPlayfield, questId))
                    _pendingRelease[parameters.DungeonPlayfield] = questId;
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Dungeon quest ended quest={0} keys={1} evicted={2}", questId, retired.Count, evict));
        }

        /// <summary>Periodic work off every playfield thread: idle dungeons, expired quests, stored-quest cleanup.</summary>
        void Sweep()
        {
            if (Interlocked.Exchange(ref _sweeping, 1) == 1)
                return;

            try
            {
                DateTime now = DateTime.UtcNow;
                PlayfieldManager playfields = _playfields.Value;
                playfields.SweepQuestDungeons(now, EmptyDungeonLifetime);
                foreach (KeyValuePair<int, string> pending in _pendingRelease.ToArray())
                {
                    if (playfields.TryReleaseQuestDungeon(pending.Key, pending.Value))
                        _pendingRelease.TryRemove(pending.Key, out _);
                }

                long cutoff = now.Ticks - RequestCooldown.Ticks;
                foreach (KeyValuePair<int, long> request in _lastRequest.ToArray())
                {
                    if (request.Value < cutoff)
                        _lastRequest.TryRemove(request.Key, out _);
                }

                foreach (string questId in _store.LoadExpiredQuestsWithKeys(now.Ticks, SweepBatch))
                {
                    GeneratedQuestRow? row = _store.LoadGenerated(questId);
                    if (row != null && QuestDungeonParameters.TryParse(row.AcgBuildingGeneratorJson, out QuestDungeonParameters parameters))
                        EndDungeon(questId, parameters, evict: true);
                    else
                        _store.EndQuestKeys(questId);

                    _store.CloseCharacterQuests(questId, (int)QuestState.Expired, now.Ticks);

                    // An online owner's log closes it (journal entry, "quest expired") on their own playfield.
                    if (row != null && playfields.FindPlayer(row.OwnerId, out Player owner) && owner.Playfield is Playfield at)
                        at.DispatchPlayerProjection(owner, () => _quests.GetLog(owner));
                }

                _store.PurgeEndedGeneratedQuests(now.Ticks - EndedQuestRetention.Ticks, SweepBatch);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Quest dungeon sweep failed; it runs again next interval");
            }
            finally
            {
                Volatile.Write(ref _sweeping, 0);
            }
        }

        bool TryGetDungeon(string questId, out CachedDungeon dungeon)
        {
            if (_dungeons.TryGetValue(questId, out dungeon!))
                return true;

            GeneratedQuestRow? row = _store.LoadGenerated(questId);
            if (row == null || !QuestDungeonParameters.TryParse(row.AcgBuildingGeneratorJson, out QuestDungeonParameters parameters))
                return false;

            QuestTemplate? template = null;
            try
            {
                template = System.Text.Json.JsonSerializer.Deserialize<QuestTemplate>(row.DefinitionJson ?? string.Empty, QuestTemplate.JsonOptions);
            }
            catch (System.Text.Json.JsonException exception)
            {
                _logger.Error(exception, "Quest " + questId + " has an unreadable definition; its dungeon gets no kill target");
            }

            bool ended = false;
            foreach (CharacterQuestRow owned in _store.Load(row.OwnerId))
            {
                if (string.Equals(owned.QuestId, questId, StringComparison.Ordinal))
                {
                    ended = owned.State != (int)QuestState.Active;
                    break;
                }
            }

            dungeon = new CachedDungeon(parameters, row.ExpiresAtUtcTicks, KillTarget(template), ended);
            if (row.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks)
                _dungeons[questId] = dungeon;
            return true;
        }

        /// <summary>The NPC hash a kill quest wants, or null for other quests.</summary>
        static string? KillTarget(QuestTemplate? template)
            => template != null && template.IsKill && !string.IsNullOrWhiteSpace(template.Objective?.Npc) ? template.Objective.Npc : null;

        /// <summary>Keys the player carries: main inventory and bags opened from it.</summary>
        static List<Item> CarriedKeys(Player player)
        {
            var keys = new List<Item>();
            foreach (Item item in player.Inventory.EnumerateHeldItems())
            {
                if (item.LowId == KeyLowId && item.InstanceId > 0 && item.IsPersisted && !item.Locked)
                    keys.Add(item);
                if (keys.Count >= MaxKeysPerQuest * 4)
                    break;
            }

            return keys;
        }

        void RemoveDeadKeys(Player player, IEnumerable<Item> dead)
        {
            var ids = dead.Select(item => item.InstanceId).ToList();
            if (ids.Count == 0)
                return;

            foreach (int instance in _store.RetireUnlinkedKeys(ids, KeyLowId))
                RemoveKeyInMemory(player, instance);
        }

        /// <summary>
        /// Drops a retired key from the holder's pages through the normal inventory delete (so no pending save can
        /// write it back) and tells the client. Keys inside bags are left until that bag next loads, where the
        /// retired row is no longer read.
        /// </summary>
        void RemoveKeyInMemory(Player player, int keyInstanceId)
        {
            if (!player.Inventory.IsHydrated)
                return;

            foreach ((IdentityType pageType, Container page) in DeletablePages(player))
            {
                foreach (KeyValuePair<int, Item> entry in page.Content)
                {
                    if (entry.Value.InstanceId != keyInstanceId || entry.Value.LowId != KeyLowId)
                        continue;

                    var slot = new Identity { Type = pageType, Instance = entry.Key };
                    if (_inventoryActions.TryDelete(player, slot, keyInstanceId))
                        player.Session?.Send(new CharacterActionMessage
                        {
                            Identity = player.Identity,
                            Action = CharacterActionType.DeleteItem,
                            Target = slot
                        });
                    return;
                }
            }
        }

        static IEnumerable<(IdentityType, Container)> DeletablePages(Player player)
        {
            PlayerInventory inventory = player.Inventory;
            yield return (IdentityType.Inventory, inventory.Inventory);
            yield return (IdentityType.OverflowWindow, inventory.Overflow);
            yield return (IdentityType.WeaponPage, inventory.Equipment);
            yield return (IdentityType.ArmorPage, inventory.Armor);
            yield return (IdentityType.ImplantPage, inventory.Implant);
            yield return (IdentityType.SocialPage, inventory.Social);
            if (inventory.Bank.IsHydrated)
                yield return (IdentityType.BankByRef, inventory.Bank);
        }

        /// <summary>The character a retired key was stored with: directly on their pages or bank, or in a bag there.</summary>
        static int HolderOf(RetiredDungeonKey key)
        {
            if (key.ContainerType == (int)IdentityType.Container)
                return Array.IndexOf(OwnedContainerTypes, key.ParentContainerType) >= 0 ? key.ParentContainerInstance : 0;
            return Array.IndexOf(OwnedContainerTypes, key.ContainerType) >= 0 ? key.ContainerInstance : 0;
        }

        static bool WithinEntrance(Player player, MissionEntrancePlacement entrance)
        {
            double dx = player.Position.x - entrance.LocalX;
            double dz = player.Position.z - entrance.LocalZ;
            return (dx * dx) + (dz * dz) <= EntryHorizontalRange * EntryHorizontalRange
                && Math.Abs(player.Position.y - entrance.LocalY) <= EntryVerticalRange;
        }

        bool TryBeginRequest(Player player)
        {
            long now = DateTime.UtcNow.Ticks;
            int id = player.Identity.Instance;
            if (_lastRequest.TryGetValue(id, out long last) && now - last < RequestCooldown.Ticks)
                return false;
            _lastRequest[id] = now;
            return true;
        }

        Item CreateKey()
        {
            int id = _ids.Allocate();
            return _items.Create(KeyLowId, KeyLowId, 1, ItemSource.Quest, 1, id, new Identity { Type = (IdentityType)KeyItemType, Instance = id });
        }

        static QuestDungeonKeyItemRow KeyRow(ItemInstanceRecord row, DateTime now) => new()
        {
            InstanceId = row.InstanceId,
            ContainerType = row.ContainerType,
            ContainerInstance = row.ContainerInstance,
            ContainerPlacement = row.ContainerPlacement,
            ItemType = row.ItemType,
            LowId = row.LowId,
            HighId = row.HighId,
            Quality = row.Quality,
            Source = (int)row.Source,
            CreatedAtUtcTicks = now.Ticks
        };

        /// <summary>Overflow window slot the key is created in before it moves to its inventory slot.</summary>
        const int OverflowSlot = 0x6F;

        /// <summary>
        /// A new key, in the captured live order (capture 20260724-134055, the retired engine's
        /// MissionKeyGrantService): SimpleItemFullUpdate creates the named key in the overflow window,
        /// ContainerAddItem routes it through overflow, then TemplateAction Overflow and TemplateAction Use move it to
        /// its inventory slot. The client only places an item it already knows, so the item update comes first.
        /// </summary>
        static void SendKey(Player player, Item key, int slot, string entranceName)
        {
            if (player.Session == null || player.Playfield == null)
                return;

            SendKeyItem(player, key, entranceName, OverflowSlot);
            player.Session.Send(new ContainerAddItemMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                Target = new Identity { Type = IdentityType.OverflowWindow, Instance = player.Identity.Instance },
                TargetPlacement = OverflowSlot
            });
            player.Session.Send(KeyTemplateAction(player, key, TemplateActionType.Overflow,
                new Identity { Type = IdentityType.OverflowWindow, Instance = 0 }, 0, 0));
            player.Session.Send(KeyTemplateAction(player, key, TemplateActionType.Use,
                new Identity { Type = IdentityType.Inventory, Instance = slot }, (int)player.Identity.Type, player.Identity.Instance));
        }

        static TemplateActionMessage KeyTemplateAction(Player player, Item key, TemplateActionType action, Identity placement, int arg3, int arg4) => new()
        {
            Identity = player.Identity,
            Unknown = 0,
            ItemLowId = key.LowId,
            ItemHighId = key.HighId,
            Quality = key.Quality,
            Unknown1 = 1,
            Action = action,
            Placement = placement,
            Unknown3 = arg3,
            Unknown4 = arg4
        };

        /// <summary>
        /// The key's SimpleItemFullUpdate: its stats and its name, "Mission key to" the entrance, at
        /// <paramref name="placement"/> (the overflow slot for a new key, its inventory slot when renaming a held one).
        /// The name's length prefix counts its trailing NUL; the writer does not add one.
        /// </summary>
        static void SendKeyItem(Player player, Item key, string entranceName, int placement)
        {
            if (player.Session == null || player.Playfield == null)
                return;

            static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value) => new() { Value1 = id, Value2 = value };
            player.Session.Send(new SimpleItemFullUpdateMessage
            {
                Identity = key.Identity,
                Unknown = 0,
                MsgVersion = 0x0B,
                Identitytype = (int)player.Identity.Type,
                Instance = player.Identity.Instance,
                Playfield = player.Playfield.Identity.Instance,
                Unknown1 = new Identity { Type = (IdentityType)0xF424F, Instance = 0 },
                Unknown2 = 0x71,
                Unknown3 = (byte)placement,
                Stats =
                [
                    Stat(CharacterStat.Flags, unchecked((uint)key.GetStat(CharacterStat.Flags))),
                    Stat(CharacterStat.StaticInstance, (uint)key.LowId),
                    Stat(CharacterStat.ACGItemLevel, (uint)key.Quality),
                    Stat(CharacterStat.ACGItemTemplateID, (uint)key.LowId),
                    Stat(CharacterStat.ACGItemTemplateID2, (uint)key.HighId),
                    Stat(CharacterStat.MultipleCount, (uint)key.StackCount)
                ],
                Name = KeyNamePrefix + entranceName + '\0'
            });
        }

        /// <summary>
        /// TEMPORARY: the NPC hash every kill-target mission asks for, until mission content picks the target (and
        /// places it in the dungeon).
        /// </summary>
        const string TemporaryKillTargetHash = "LE01";

        /// <summary>The offer's type, text and reward in Quests.json form; a kill-target mission names its target hash.</summary>
        static QuestTemplate BuildTemplate(string questId, GeneratedMissionOffer offer)
        {
            List<List<QuestRewardItem>>? records = offer.RewardCount > 0 && offer.RewardLowId > 0
                ? [[new QuestRewardItem { Id = offer.RewardLowId, HighId = offer.RewardHighId, Quality = Math.Max(1, offer.RewardQuality) }]]
                : null;

            return new QuestTemplate
            {
                Hash = questId,
                Name = offer.Title ?? string.Empty,
                Summary = offer.Description ?? string.Empty,
                Scope = "Solo",
                Action = offer.MissionType switch
                {
                    0 => QuestTemplate.KillOneAction,
                    1 => "TargetNpc",
                    2 => "FindItem",
                    3 => "UseItemOnCharacter",
                    4 => "FindItem2",
                    _ => "TalkTo"
                },
                Objective = offer.MissionType == 0 ? new QuestObjective { Npc = TemporaryKillTargetHash } : new QuestObjective(),
                Flags = "None",
                Reward = new QuestReward
                {
                    Hash = string.Empty,
                    Credits = Math.Max(0, offer.CashReward),
                    Xp = Math.Max(0, offer.ExperienceReward),
                    Records = records
                }
            };
        }

        static void SendText(Player player, string text)
            => player.Session?.Send(new ChatTextMessage { Identity = player.Identity, Text = text });

        void Quarantine(Player player, Exception exception)
        {
            player.QuarantinePersistence();
            player.Session?.Close();
            _logger.Error(exception, "Dungeon quest commit outcome unknown; player persistence quarantined");
        }
    }
}
