namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Teams;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Online player character. Session is attached at login via SpawnService.
    /// </summary>
    public class Player : Character
    {
        readonly IItemBuilder _items;
        private IDisposable? _onlineOwnership;
        private volatile bool _persistenceQuarantined;

        /// <summary>Serializes durable snapshots, write-behind, and economic transactions.</summary>
        public object PersistenceGate { get; } = new();

        public bool IsPersistenceQuarantined => _persistenceQuarantined;

        public CharacterSaveState SaveState { get; } = new();

        /// <summary>Rebuilt from active <see cref="FunctionType.ChangeActionRestriction"/> buffs.</summary>
        public ActionRestrictionFlags ActionRestrictionFlags { get; internal set; }

        /// <summary>Quests from characterquests; loaded by <see cref="Quests.QuestService"/> on first use.</summary>
        public Quests.QuestLog? QuestLog { get; set; }

        /// <summary>Skill trickle and training costs used by rebase and the trainer.</summary>
        public SkillCatalog SkillCatalog { get; init; } = SkillCatalog.Default;

        /// <summary>An indeterminate commit must be reloaded from storage, never overwritten from memory.</summary>
        public void QuarantinePersistence()
        {
            lock (PersistenceGate)
                _persistenceQuarantined = true;
        }

        public void AttachOnlineOwnership(IDisposable ownership)
        {
            ArgumentNullException.ThrowIfNull(ownership);
            if (Interlocked.CompareExchange(ref _onlineOwnership, ownership, null) != null)
                throw new InvalidOperationException("Player already has online ownership.");
        }

        public void ReleaseOnlineOwnership()
            => Interlocked.Exchange(ref _onlineOwnership, null)?.Dispose();

        // Temporary hardcoded hospital until real respawn tables exist.
        public const int RespawnGracePeriodMilliseconds = 3000;

        // Live DeathRespawn action parameters (CharacterAction 0xAB).
        const int DeathRespawnActionParameter1 = 1000020;
        const int DeathRespawnActionParameter2 = 295830;

        bool _respawnPending;

        /// <summary>
        /// Set when this player lands on a playfield by transfer and its client is about to reconnect for the zone
        /// change; the reconnect clears it. Such a client keeps its team window, so the roster is not sent again.
        /// </summary>
        internal bool ZoneReconnectPending { get; set; }

        /// <summary>
        /// The client said it is in play (CharInPlay) since the server last put this session in play. Until then its
        /// moves are not applied: while it loads a zone it reports where it was, not where it landed, and such a stale
        /// position swept the character over a zone border (Lost Eden 2026-10-25).
        /// </summary>
        internal bool ClientInPlay { get; set; }

        /// <summary>When the server last put this player's session in play.</summary>
        internal DateTime InPlaySinceUtc { get; private set; }

        /// <summary>A client that never sends CharInPlay has its moves applied after this long anyway.</summary>
        const double ClientInPlayFallbackSeconds = 10.0;

        /// <summary>The session is in play from the server's side; the client has yet to say so.</summary>
        internal void MarkServerInPlay(DateTime nowUtc)
        {
            ClientInPlay = false;
            InPlaySinceUtc = nowUtc;
        }

        internal bool AcceptsClientMoves(DateTime nowUtc)
            => ClientInPlay || (nowUtc - InPlaySinceUtc).TotalSeconds >= ClientInPlayFallbackSeconds;

        /// <summary>When the client last said it is in play (CharInPlay).</summary>
        DateTime _clientInPlaySinceUtc;

        /// <summary>CharInPlay: the client has finished loading the zone.</summary>
        internal void MarkClientInPlay(DateTime nowUtc)
        {
            if (ClientInPlay)
                return;
            ClientInPlay = true;
            _clientInPlaySinceUtc = nowUtc;
        }

        /// <summary>
        /// Still zoning in: awaiting the zone reconnect, the client's CharInPlay (up to the fallback), or within
        /// Gameplay.json zoneInGraceSeconds of it. NPCs neither aggro on nor attack the player meanwhile.
        /// </summary>
        public bool InZoneInGrace(DateTime nowUtc)
        {
            if (ZoneReconnectPending)
                return true;
            double grace = Playfield?.GetService<GameplaySettings>()?.ZoneInGraceSeconds ?? 0;
            return ClientInPlay
                ? (nowUtc - _clientInPlaySinceUtc).TotalSeconds < grace
                : (nowUtc - InPlaySinceUtc).TotalSeconds < ClientInPlayFallbackSeconds + grace;
        }

        /// <summary>An in-zone respawn has despawned this player for observers; the respawn itself runs next tick.</summary>
        bool _respawnObserversCleared;
        double _respawnRemainingSeconds;
        DateTime _diedAtUtc;

        public Player(Identity identity, IZoneLogger logger, IItemBuilder items)
            : base(identity)
        {
            ArgumentNullException.ThrowIfNull(items);
            Logger = logger;
            _items = items;
            Inventory = new PlayerInventory();
            // The client's IP is what is left to spend; it never sees the level bonus or UsedIP split.
            Stats.WireValueOverride = stat => stat == CharacterStat.IP ? Math.Max(0, AvailableIp) : null;
            // Requirement folds use Stats.Get; keep this at 0 so Unset never fails NotBitAnd checks.
            Stats.Set(CharacterStat.SelectedTargetType, 0, StatDetail.Base);
            Stats.BaseChanged += stat =>
            {
                if (!CharacterSaveState.IsCheckpointOnly(stat) && !StatCollection.IsRuntimeOnly(stat))
                    SaveState.MarkDirty();
            };
            Stats.StatChanged += OnShadowBreedStatChanged;
        }

        /// <summary>Between death and respawn the live position is not a valid place to resume.</summary>
        public bool IsRespawnPending => _respawnPending;

        public override bool IsPlayer => true;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public IZoneSession? Session { get; set; }

        public PlayerConnectionPhase ConnectionPhase { get; set; } = PlayerConnectionPhase.Online;

        /// <summary>UTC deadline while <see cref="PlayerConnectionPhase.LinkDead"/>; null when Online.</summary>
        public DateTime? LinkDeadUntilUtc { get; set; }

        public PlayerInventory Inventory { get; }

        /// <summary>Perk definitions (perk id to perk item) used to build <see cref="PerkTemplates"/>.</summary>
        public PerkCatalog PerkCatalog { get; init; } = PerkCatalog.Default;

        /// <summary>Perk action templates, tiers and skill rates (PerkActions.json) used by <see cref="TryUsePerkAction"/>.</summary>
        public PerkActionCatalog PerkActionCatalog { get; init; } = PerkActionCatalog.Default;

        /// <summary>Trained perk ids: the persistent perk state (charactersperks).</summary>
        public TrainedPerks TrainedPerks { get; } = new();

        /// <summary>LockPerk cooldowns keyed by perk id (characterperklocks).</summary>
        public SkillLocks PerkLocks { get; } = new();

        /// <summary>LockPerk: the perk's actions are unusable for <paramref name="durationSeconds"/>; zero or less clears it.</summary>
        public void LockPerk(int perkId, int durationSeconds, DateTime nowUtc)
        {
            PerkLocks.Lock(perkId, durationSeconds, nowUtc);
            Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(this);
            if (durationSeconds > 0)
                AnnouncePerkLock(perkId, durationSeconds, nowUtc);
            else if (_announcedPerkLocks.Remove(perkId))
                AnnouncePerkAvailable(perkId);
        }

        /// <summary>Perk locks shown on the client, by expiry; each gets PerkAvailable when it runs out.</summary>
        readonly Dictionary<int, DateTime> _announcedPerkLocks = new();

        /// <summary>
        /// Shows the lock on the client: PerkUnavailable (CharacterAction 0xCF, Parameter1 = perk id,
        /// Parameter2 = seconds; live capture 2026-10-02T03:54:58Z). Client case 0x59 (Gamecode.dll 0x1005393d)
        /// starts its own timer for the perk and greys the perk's action button until it runs out.
        /// </summary>
        void AnnouncePerkLock(int perkId, int seconds, DateTime nowUtc)
        {
            if (Session?.State != SessionState.InPlay)
                return;

            _announcedPerkLocks[perkId] = nowUtc.AddSeconds(seconds);

            Session.Send(new CharacterActionMessage
            {
                Identity = Identity,
                Action = CharacterActionType.PerkUnavailable,
                Target = Identity.None,
                Parameter1 = perkId,
                Parameter2 = seconds
            });
        }

        /// <summary>
        /// The lock ran out: PerkAvailable (CharacterAction 0xCE, Parameter1 0, Parameter2 = perk id; live capture
        /// sends it when a 65 s Dance of Fools lock ends) re-enables the perk's action buttons.
        /// </summary>
        void AnnouncePerkAvailable(int perkId)
        {
            if (Session?.State != SessionState.InPlay)
                return;

            Session.Send(new CharacterActionMessage
            {
                Identity = Identity,
                Action = CharacterActionType.PerkAvailable,
                Target = Identity.None,
                Parameter1 = 0,
                Parameter2 = perkId
            });
        }

        void TickPerkLocks()
        {
            if (_announcedPerkLocks.Count == 0)
                return;

            DateTime nowUtc = DateTime.UtcNow;
            List<int>? expired = null;
            foreach ((int perkId, DateTime expiresUtc) in _announcedPerkLocks)
            {
                if (expiresUtc <= nowUtc && !PerkLocks.IsLocked(perkId, nowUtc))
                    (expired ??= []).Add(perkId);
            }

            if (expired == null)
                return;

            foreach (int perkId in expired)
            {
                _announcedPerkLocks.Remove(perkId);
                AnnouncePerkAvailable(perkId);
            }
        }

        /// <summary>
        /// Requirement leaves a stat value cannot answer, resolved against this player: HasPerk / HasNotPerk
        /// (trained perks), IsPerkLocked / IsPerkUnlocked (LockPerk cooldowns), pet and allied-combat state, worn and
        /// wielded items (HasWornItem / HasWieldedItem and their negations; social clothing never counts), then
        /// the NCU leaves of <see cref="Character.ResolveRequirement"/> (Hecatomb needs Performed Gore 234025 running).
        /// Null leaves every other leaf to the stat comparison.
        /// </summary>
        public override bool? ResolveRequirement(ItemRequirement requirement) => ResolvePerkRequirement(requirement);

        /// <inheritdoc cref="ResolveRequirement"/>
        public bool? ResolvePerkRequirement(ItemRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            int id = requirement.Value;
            return (Operator)requirement.Operator switch
            {
                Operator.HasPerk => TrainedPerks.Contains(id),
                Operator.HasNotPerk => !TrainedPerks.Contains(id),
                Operator.IsPerkLocked => PerkLocks.IsLocked(id, DateTime.UtcNow),
                Operator.IsPerkUnlocked => !PerkLocks.IsLocked(id, DateTime.UtcNow),
                Operator.IsPetOverEquipped => OwnedPets.All.Any(pet => pet.Pet?.IsOverEquipped == true),
                // Value is the item id. Social clothing is never equipped for these (PlayerInventory.IsEquipped).
                Operator.HasWornItem => Inventory.IsEquipped(id, wieldedOnly: false),
                Operator.HasNotWornItem => !Inventory.IsEquipped(id, wieldedOnly: false),
                Operator.HasWieldedItem => Inventory.IsEquipped(id, wieldedOnly: true),
                Operator.HasNotWieldedItem => !Inventory.IsEquipped(id, wieldedOnly: true),
                Operator.MustNotAlliedCombat => !IsAlliedInCombat(),
                Operator.MustAlliedCombat => IsAlliedInCombat(),
                // The client passes ObtainedItem without looking (Gamecode.dll FUN_100065d8 case 0x6c), so an effect
                // never depends on it here either: a perk trained with Bazzit's Alien Library keeps working when the
                // library is gone. Training checks it (ResolveTrainRequirement).
                Operator.ObtainedItem => true,
                // Value = a quest hash (or a template's Aliases entry): that quest is in the journal and active.
                Operator.HasActiveQuest => NpcCharacter.TryDecodeAoHash(id, out string questHash)
                    && QuestLog?.FindActive(questHash) != null,
                _ => base.ResolveRequirement(requirement)
            };
        }

        /// <summary>
        /// The ObtainedItem leaves (value = item id) of a perk's ToWear action, checked only after every other
        /// requirement passed: the item must be owned now, carried, in a carried bag, equipped, in the bank or in a bag
        /// kept in the bank (Alien Technology Expertise 3, 256082, needs Bazzit's Alien Library 256081). Only the
        /// server decides it; the client always lets the train through. Ownership can cost database reads, so it is
        /// the last check and its answer is cached (<see cref="OwnsItem"/>).
        /// </summary>
        bool MeetsObtainedItems(ItemTemplate perk)
        {
            foreach (ItemAction action in perk.Actions)
            {
                if (action.ActionType != (int)ActionType.ToWear)
                    continue;
                foreach (ItemRequirement requirement in action.Requirements)
                {
                    if ((Operator)requirement.Operator == Operator.ObtainedItem && !OwnsItem(requirement.Value))
                        return false;
                }
            }

            return true;
        }

        /// <summary>How long an <see cref="OwnsItem"/> answer stands when nothing in the inventory changed meanwhile.</summary>
        static readonly TimeSpan OwnedItemCacheLifetime = TimeSpan.FromSeconds(30);

        Dictionary<int, (bool Owned, DateTime AtUtc)>? _ownedItemCache;

        /// <summary>Forgets cached <see cref="OwnsItem"/> answers; every inventory commit and move calls this.</summary>
        public void InvalidateOwnedItemCache() => _ownedItemCache?.Clear();

        /// <summary>
        /// An item of template <paramref name="itemId"/> (low or high id) is carried, equipped, in a carried bag, in the
        /// bank or in a bag kept in the bank. The bank is loaded here as OpenBank would load it when it has not been
        /// opened this session; banked bags never opened are read from their stored rows in one query. The answer is
        /// kept for <see cref="OwnedItemCacheLifetime"/> or until the inventory changes, so repeated train requests
        /// cost nothing.
        /// </summary>
        bool OwnsItem(int itemId)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (_ownedItemCache != null && _ownedItemCache.TryGetValue(itemId, out (bool Owned, DateTime AtUtc) cached)
                && nowUtc - cached.AtUtc < OwnedItemCacheLifetime)
                return cached.Owned;

            bool owned = FindOwnedItem(itemId);
            (_ownedItemCache ??= new Dictionary<int, (bool, DateTime)>())[itemId] = (owned, nowUtc);
            return owned;
        }

        bool FindOwnedItem(int itemId)
        {
            foreach (Item item in Inventory.EnumerateHeldItems())
            {
                if (IsTemplate(item, itemId))
                    return true;
            }

            if (Playfield?.GetService<IInventoryRepository>() is not IInventoryRepository repository)
                return false;
            if (!Inventory.Bank.IsHydrated)
                Inventory.EnsureBankHydrated(Identity.Instance, repository, _items);
            if (!Inventory.Bank.IsHydrated)
                return false;

            List<int>? unopenedBags = null;
            foreach (Item item in Inventory.Bank.Content.Values)
            {
                if (IsTemplate(item, itemId))
                    return true;
                if (!InventoryMoveService.IsBagItem(item))
                    continue;

                if (Inventory.TryGetBackpackPage(item.Identity, out Container bag) && bag.IsHydrated)
                {
                    foreach (Item content in bag.Content.Values)
                    {
                        if (IsTemplate(content, itemId))
                            return true;
                    }

                    continue;
                }

                int containerInstance = item.Identity.Instance > 0 ? item.Identity.Instance : item.InstanceId;
                if (containerInstance > 0)
                    (unopenedBags ??= new List<int>()).Add(containerInstance);
            }

            if (unopenedBags == null)
                return false;
            foreach (ItemInstanceRecord row in repository.GetContainerItems(unopenedBags))
            {
                if (row.LowId == itemId || row.HighId == itemId)
                    return true;
            }

            return false;
        }

        static bool IsTemplate(Item item, int itemId) => item.LowId == itemId || item.HighId == itemId;

        /// <summary>
        /// Moves this player to <paramref name="landing"/> on its current playfield, as a LineTeleport lift does: the motor
        /// is warped (path and speed dropped), the client snaps in place from an intrazone N3Teleport without unloading
        /// the zone, and observers see a full stop at the landing.
        /// </summary>
        public void TeleportWithinPlayfield(Vector3 landing)
        {
            ArgumentNullException.ThrowIfNull(landing);
            if (Playfield is not Playfield playfield)
                return;

            Motor.Warp(landing);
            Session?.SendIntrazoneTeleport(landing, Rotation, playfield.ClientPlayfieldId);
            playfield.GetRequiredService<PlayfieldLocality>().Announce(this, new CharDCMoveMessage
            {
                Identity = Identity,
                Unknown = 0x00,
                MoveType = (byte)Movement.MovementAction.FullStop,
                Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion
                {
                    X = Rotation.xf, Y = Rotation.yf, Z = Rotation.zf, W = Rotation.wf
                },
                Coordinates = new SmokeLounge.AOtomation.Messaging.GameData.Vector3 { X = landing.xf, Y = landing.yf, Z = landing.zf },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            });
        }

        /// <summary>
        /// Shows a skill lock on the client as live does: SpecialUsed (CharacterAction 0xAA, Parameter1 = stat,
        /// Parameter2 = seconds; capture 2026-10-02T15:43:13Z), then SpecialAvailable when it runs out
        /// (capture 2026-10-02T16:41:09Z).
        /// </summary>
        public void ShowSkillLock(int statId, int seconds) => ShowSkillLock(statId, seconds, seconds);

        /// <summary>
        /// <see cref="ShowSkillLock(int, int)"/> for a lock scaled by SkillLockModifier: the client applies the modifier to
        /// SpecialUsed itself (382 = -30 turned a 40 s Health and Nano Stim lock sent as 28 s into ~19 s), so it is sent
        /// the unscaled <paramref name="clientSeconds"/>; SpecialAvailable follows the server's <paramref name="lockedSeconds"/>.
        /// </summary>
        public void ShowSkillLock(int statId, int clientSeconds, int lockedSeconds)
        {
            if (lockedSeconds <= 0 || Session == null)
                return;

            Session.Send(new CharacterActionMessage
            {
                Identity = Identity,
                Action = CharacterActionType.SpecialUsed,
                Target = Identity.None,
                Parameter1 = statId,
                Parameter2 = clientSeconds
            });
            ScheduleSpecialAvailable(statId, DateTime.UtcNow.AddSeconds(lockedSeconds));
        }

        /// <summary>The character this player's client last had a /follow acknowledged for.</summary>
        Identity _followAckTarget = Identity.None;

        DateTime _followAckedUtc = DateTime.MinValue;

        /// <summary>
        /// A pass-on FollowTarget comes back from the client after it executes the server's ack. A request for the
        /// target just acknowledged inside this window is that reflection, not a new /follow.
        /// </summary>
        const double FollowReflectionWindowSeconds = 1.0;

        /// <summary>
        /// Records a /follow acknowledgement. False when <paramref name="target"/> was acknowledged a moment ago, so the
        /// request is the client reflecting the ack and must not be answered again.
        /// </summary>
        public bool TryBeginFollowAck(Identity target, DateTime nowUtc)
        {
            if (target == _followAckTarget && (nowUtc - _followAckedUtc).TotalSeconds < FollowReflectionWindowSeconds)
                return false;

            _followAckTarget = target;
            _followAckedUtc = nowUtc;
            return true;
        }

        /// <summary>
        /// Allied combat (MustAlliedCombat / MustNotAlliedCombat): this player, its pets, its teammates on this
        /// playfield and their pets: true when any of them is attacking something or being attacked.
        /// </summary>
        public bool IsAlliedInCombat()
        {
            if (InCombatWithPets(this))
                return true;

            Playfield? playfield = Playfield;
            TeamSnapshot? team = playfield?.GetService<TeamService>()?.GetTeam(this);
            if (playfield == null || team == null)
                return false;

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            foreach (int memberId in team.MemberIds)
            {
                if (memberId == Identity.Instance)
                    continue;
                if (registry.TryGet(new Identity { Type = IdentityType.CanbeAffected, Instance = memberId }, out Dynel? dynel)
                    && dynel is Player mate && InCombatWithPets(mate))
                    return true;
            }

            return false;
        }

        static bool InCombatWithPets(Character character)
        {
            if (character.IsInCombat)
                return true;

            foreach (NpcCharacter pet in character.OwnedPets.All)
            {
                if (pet.IsInCombat)
                    return true;
            }

            return false;
        }

        ItemTemplate[] _perkTemplates = [];
        PerkAction[] _perkActions = [];

        /// <summary>
        /// One Perk Actions grant from a perk item's OnWear AddAction (53182) function:
        /// [slot = 10000 + perk id, 4-char hash, flag, action template id].
        /// </summary>
        public readonly record struct PerkAction(int ActionTemplateId, int Slot, int Hash);

        /// <summary>Perk actions granted by <see cref="PerkTemplates"/>. Rebuilt with the templates.</summary>
        public IReadOnlyList<PerkAction> PerkActions => _perkActions;

        /// <summary>
        /// Item templates of <see cref="TrainedPerks"/>. Rebuilt only on train, untrain and login;
        /// <see cref="RebaseStats"/> reapplies their stats from here.
        /// </summary>
        public IReadOnlyList<ItemTemplate> PerkTemplates => _perkTemplates;

        /// <summary>Login: the stored perks become the trained set and their templates are built.</summary>
        public void LoadTrainedPerks(IEnumerable<int> perkIds)
        {
            ArgumentNullException.ThrowIfNull(perkIds);
            TrainedPerks.Restore(perkIds);
            RebasePerkTemplates();
            _perksLoaded = true;
        }

        /// <summary>Set once login has built the perk templates; stat writes before that are hydration.</summary>
        bool _perksLoaded;

        /// <summary>Regular perk points spent: trained perks that are neither alien nor research.</summary>
        public int UsedPerkPoints => TrainedPerks.Snapshot().Count(PerkCatalog.CostsPerkPoint);

        public int UsedAlienPerkPoints => TrainedPerks.Snapshot().Count(PerkCatalog.CostsAlienPerkPoint);

        public int AvailablePerkPoints
            => PerkCatalog.EarnedPerkPoints(Stats.GetOrZero(CharacterStat.Level)) - UsedPerkPoints;

        public int AvailableAlienPerkPoints
            => PerkCatalog.EarnedAlienPerkPoints(Stats.GetOrZero(CharacterStat.AlienLevel)) - UsedAlienPerkPoints;

        /// <summary>Expansion bit for Shadowlands; regular perks need it (Gamecode.dll 0x100536d7).</summary>
        const int ShadowlandsExpansionBit = 0x02;

        /// <summary>Expansion bit for Alien Invasion; alien perks need it.</summary>
        const int AlienInvasionExpansionBit = 0x08;

        /// <summary>
        /// Trains <paramref name="perkId"/> under the client's own rules (Gamecode.dll 0x100536d7): a known,
        /// untrained Perk or Alien category perk whose previous tier is trained, whose expansion is owned, with a free
        /// perk point (alien perks: alien perk point; research: none) and whose perk item ToWear requirements
        /// pass. Refreshes the perk templates and actions and rebases stats.
        /// </summary>
        public bool TryTrainPerk(int perkId, Action? confirm = null)
        {
            if (!PerkCatalog.TryGetItemId(perkId, out int itemId) || TrainedPerks.Contains(perkId))
                return false;

            if (!PerkCatalog.IsTrainable(perkId))
                return false;
            if (PerkCatalog.TryGetPreviousId(perkId, out int previousId) && !TrainedPerks.Contains(previousId))
                return false;

            int expansion = Stats.GetOrZero(CharacterStat.Expansion);
            if (PerkCatalog.CostsPerkPoint(perkId)
                && ((expansion & ShadowlandsExpansionBit) == 0 || AvailablePerkPoints <= 0))
                return false;
            if (PerkCatalog.CostsAlienPerkPoint(perkId)
                && ((expansion & AlienInvasionExpansionBit) == 0 || AvailableAlienPerkPoints <= 0))
                return false;

            try
            {
                // Stats and perks first (ObtainedItem passes here); the ownership check, which may read the bank, last.
                ItemTemplate perk = _items.CreateTemplate(itemId, itemId, quality: 1);
                if (!perk.MeetsActionRequirements(stat => Stats.Get(stat), ActionType.ToWear, ResolvePerkRequirement)
                    || !MeetsObtainedItems(perk))
                    return false;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, string.Format(CultureInfo.InvariantCulture,
                    "Perk {0} item {1} has no item template; it cannot be trained.", perkId, itemId));
                return false;
            }

            if (!TrainedPerks.Add(perkId))
                return false;

            OnTrainedPerksChanged(confirm);
            return true;
        }

        /// <summary>
        /// One perk untrain (reset) per two hours: the client's perk reset timer is hard coded to 7200 seconds
        /// (GUI.dll 0x10060d7e).
        /// </summary>
        public const int PerkResetCooldownSeconds = 7200;

        /// <summary>
        /// Untrains <paramref name="perkId"/> when it is the highest trained tier of its line and no untrain happened
        /// in the last <see cref="PerkResetCooldownSeconds"/>. The cooldown is a lock on LastPerkResetTime in
        /// <see cref="Character.SkillLocks"/> (persisted with the other locks, never scaled). The stat itself carries
        /// the reset's Unix time for the client's timer. Refreshes the perk templates and actions and rebases.
        /// </summary>
        public bool TryUntrainPerk(int perkId, Action? confirm = null)
        {
            // Research is earned with research XP and permanent: it is never reset like a perk.
            if (!TrainedPerks.Contains(perkId) || PerkCatalog.IsResearch(perkId))
                return false;
            foreach (int trained in TrainedPerks.Snapshot())
            {
                if (PerkCatalog.TryGetPreviousId(trained, out int previousId) && previousId == perkId)
                    return false;
            }

            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan wait = SkillLocks.Remaining((int)CharacterStat.LastPerkResetTime, nowUtc);
            if (wait > TimeSpan.Zero)
            {
                RequirementFeedback.SendText(this, string.Format(CultureInfo.InvariantCulture,
                    "You can reset another perk in {0:00}:{1:00}:{2:00}.", (int)wait.TotalHours, wait.Minutes, wait.Seconds));
                return false;
            }

            if (!TrainedPerks.Remove(perkId))
                return false;

            SkillLocks.Lock((int)CharacterStat.LastPerkResetTime, PerkResetCooldownSeconds, nowUtc);
            SyncLastPerkResetTime();
            OnTrainedPerksChanged(confirm);
            FlushDirtyStats();
            return true;
        }

        /// <summary>
        /// GM setup: the trained perks become exactly <paramref name="perkIds"/> (regular, alien and finished research),
        /// with no perk point, requirement or reset rules, and research in progress and the research goal are dropped.
        /// Refreshes the perk templates and actions and rebases.
        /// </summary>
        public void ReplacePerksAndResearch(IEnumerable<int> perkIds)
        {
            ArgumentNullException.ThrowIfNull(perkIds);
            TrainedPerks.Replace(perkIds);
            Research.Clear();
            if (Stats.Get(CharacterStat.PersonalResearchGoal, StatDetail.Base) != 0)
                Stats.Set(CharacterStat.PersonalResearchGoal, 0, StatDetail.Base);
            OnTrainedPerksChanged(null);
        }

        // GUI DisableXPGain reads stat 349 bit 0x10; research remains active.
        internal bool IsNormalXpSuppressed
            => (Stats.GetOrZero(CharacterStat.AutoAttackFlags, StatDetail.Base) & 0x10) != 0;

        public void SetXpGainOptions(int flags)
        {
            // The client preserves other AutoAttackFlags bits and updates the same stat locally.
            Stats.Set(CharacterStat.AutoAttackFlags, flags, StatDetail.Base, dirty: true);
            FlushDirtyStats();
        }

        /// <summary>Personal research tiers (Research.json).</summary>
        public ResearchCatalog ResearchCatalog { get; init; } = ResearchCatalog.Default;

        /// <summary>Personal research tiers in progress and the research XP each still needs (characterresearch).</summary>
        public ResearchProgress Research { get; } = new();

        /// <summary>/pet rename names per pet type; a summoned pet of that type takes its name (characterpetnames).</summary>
        public PetNames PetNames { get; } = new();

        /// <summary>Login: the stored progress, minus any tier already trained (a crash between the two writes).</summary>
        public void LoadResearch(IEnumerable<ResearchProgressRecord> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);
            Research.Restore(rows);
            Research.DropFinished(TrainedPerks);
        }

        /// <summary>
        /// The client's research slider (SetStat PersonalResearchLevel 263): the share of earned XP that goes to personal
        /// research, 0-100. The client applies it locally first; a value out of range is clamped and sent back.
        /// </summary>
        public void SetPersonalResearchShare(int percent)
        {
            int clamped = Math.Clamp(percent, 0, 100);
            if (Stats.Get(CharacterStat.PersonalResearchLevel, StatDetail.Base) != clamped)
                Stats.Set(CharacterStat.PersonalResearchLevel, clamped, StatDetail.Base);
            if (clamped != percent)
                SendOwnStat(CharacterStat.PersonalResearchLevel, clamped);
        }

        /// <summary>
        /// The client's research choice (SetStat PersonalResearchGoal 265): the research id to train, or 0 for none. The
        /// tier must be one this character may research now (<see cref="CanResearch"/>); otherwise the stored goal stands
        /// and goes back to the client. An accepted goal is answered with its PerkUpdate, which gives the client the tier's
        /// progress entry.
        /// </summary>
        public bool TrySelectPersonalResearch(int researchId)
        {
            if (researchId != 0 && !CanResearch(researchId))
            {
                SendOwnStat(CharacterStat.PersonalResearchGoal, Math.Max(0, Stats.GetOrZero(CharacterStat.PersonalResearchGoal)));
                return false;
            }

            if (Stats.Get(CharacterStat.PersonalResearchGoal, StatDetail.Base) != researchId)
                Stats.Set(CharacterStat.PersonalResearchGoal, researchId, StatDetail.Base);
            if (researchId != 0 && ResearchCatalog.TryGet(researchId, out ResearchTier tier))
                SendPerkUpdate(researchId, researchId, Research.TryGetRemaining(researchId, out int remaining) ? remaining : tier.Xp);
            return true;
        }

        /// <summary>
        /// A personal research tier this character may train now: a known tier for its profession, not yet finished,
        /// whose previous tier is finished and whose minimum level is reached. Rechecked whenever research XP lands, so a
        /// goal that stopped qualifying earns nothing.
        /// </summary>
        public bool CanResearch(int researchId)
            => ResearchCatalog.TryGet(researchId, out ResearchTier tier)
                && tier.AllowsProfession(Stats.GetOrZero(CharacterStat.Profession))
                && !TrainedPerks.Contains(researchId)
                && (tier.PreviousId == 0 || TrainedPerks.Contains(tier.PreviousId))
                && Stats.GetOrOne(CharacterStat.Level) >= tier.MinLevel;

        /// <summary>
        /// The research share of an XP grant: PersonalResearchLevel percent of <paramref name="amount"/>, rounded down,
        /// when a research goal is selected and may still be trained; otherwise nothing.
        /// </summary>
        internal long ResearchShareOf(long amount)
        {
            if (amount <= 0 || !CanResearch(Stats.GetOrZero(CharacterStat.PersonalResearchGoal)))
                return 0;

            int percent = Math.Clamp(Stats.GetOrZero(CharacterStat.PersonalResearchLevel), 0, 100);
            long share = amount * percent / 100;
            // Gameplay.json xp.caps.research: one grant's research share is capped at a percent of the XP the selected
            // tier needs in all.
            if (share > 0 && ResearchCatalog.TryGet(Stats.GetOrZero(CharacterStat.PersonalResearchGoal), out ResearchTier tier))
                share = Math.Min(share, GameplayXpCaps.Of(XpCaps.Research, tier.Xp));
            return share;
        }

        /// <summary>
        /// Without changing anything: the research XP a grant of <paramref name="amount"/> would put into the selected goal,
        /// its <see cref="ResearchShareOf"/> share capped at what the tier still needs. Planned rewards (missions, quests)
        /// take this off their XP and hand it to <see cref="ApplyResearchXp"/> once committed.
        /// </summary>
        internal long PlanResearchXp(long amount)
        {
            long share = ResearchShareOf(amount);
            int goal = Stats.GetOrZero(CharacterStat.PersonalResearchGoal);
            if (share <= 0 || !ResearchCatalog.TryGet(goal, out ResearchTier tier))
                return 0;

            int remaining = Research.TryGetRemaining(goal, out int stored) ? stored : tier.Xp;
            return Math.Min(share, remaining);
        }

        /// <summary>
        /// Puts <paramref name="amount"/> research XP into the selected goal and returns what it did not use: everything
        /// when there is no trainable goal, the excess past a finished tier otherwise. A finished tier becomes a trained
        /// perk (permanent; its item's bonuses apply) and the goal moves to the next tier when that one may be trained.
        /// The client hears every change as a PerkUpdate (research id, new goal, XP still needed); at 0 it shows the
        /// tier as researched.
        /// </summary>
        internal long ApplyResearchXp(long amount)
        {
            int goal = Stats.GetOrZero(CharacterStat.PersonalResearchGoal);
            if (amount <= 0 || !CanResearch(goal) || !ResearchCatalog.TryGet(goal, out ResearchTier tier))
                return Math.Max(0, amount);

            int remaining = Research.TryGetRemaining(goal, out int stored) ? stored : tier.Xp;
            // Live: the allocation line, then the PerkUpdate (capture 2026-10-13 22:48:10).
            ClientFeedback.SendFormattedWithUnknown1(this, ClientFeedback.ResearchAllocatedChannel,
                ClientFeedback.XpAllocatedToResearch, (int)Math.Min(amount, remaining));
            if (amount < remaining)
            {
                remaining -= (int)amount;
                Research.Set(goal, remaining);
                SendPerkUpdate(goal, goal, remaining);
                Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(this);
                return 0;
            }

            long excess = amount - remaining;
            if (!TrainedPerks.Add(goal))
                return amount;

            Research.Set(goal, 0);
            int next = tier.NextId != 0 && CanResearch(tier.NextId) ? tier.NextId : 0;
            Stats.Set(CharacterStat.PersonalResearchGoal, next, StatDetail.Base);
            SendPerkUpdate(goal, next, 0);
            OnTrainedPerksChanged(null);
            Logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Research finished character={0} research={1} ({2} tier {3}) next={4}",
                Identity.Instance, goal, tier.LineName, tier.Tier, next));
            return excess;
        }

        /// <summary>
        /// PerkUpdate (PerkUpdateIIR, Gamecode.dll 0x10063c68): research id, the new PersonalResearchGoal, the research
        /// XP still needed. The client adds the tier to its research list, sets the goal stat and, at 0, marks it
        /// researched ("got_tech").
        /// </summary>
        void SendPerkUpdate(int researchId, int goal, int remaining)
        {
            if (Session?.State != SessionState.InPlay)
                return;

            Session.Send(new PerkUpdateMessage
            {
                Identity = Identity,
                Unknown1 = researchId,
                Unknown2 = goal,
                Unknown3 = Math.Max(0, remaining)
            });
        }

        /// <summary>One of this player's own stats, to its client only.</summary>
        void SendOwnStat(CharacterStat stat, int value)
            => Session?.Send(new StatMessage
            {
                Identity = Identity,
                Stats = [new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = unchecked((uint)value) }]
            });

        /// <summary>
        /// LastPerkResetTime (577) for the client's perk reset timer (GUI.dll 0x10060d7e), which shows
        /// 7200 - (clientServerTime - this) and nothing for 0. The client's server time is the last GameTime's server
        /// time plus the seconds since it arrived (GameTime_t::Update / RunFunction), so the reset is given in that
        /// clock, from the reset cooldown lock. Runtime only: it means something only against this session's GameTime.
        /// </summary>
        internal void SyncLastPerkResetTime()
        {
            int value = 0;
            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan remaining = SkillLocks.Remaining((int)CharacterStat.LastPerkResetTime, nowUtc);
            if (remaining > TimeSpan.Zero
                && Session is Network.IGameTimeSession { GameTimeSynchronizedAtUtc: DateTime syncedUtc } clock)
            {
                DateTime resetUtc = nowUtc - (TimeSpan.FromSeconds(PerkResetCooldownSeconds) - remaining);
                value = clock.GameTimeServerSeconds + (int)Math.Floor((resetUtc - syncedUtc).TotalSeconds);
            }

            if (Stats.Get(CharacterStat.LastPerkResetTime, StatDetail.Base) != value)
                Stats.Set(CharacterStat.LastPerkResetTime, value, StatDetail.Base, dirty: true);
        }

        /// <summary><paramref name="confirm"/> (the client's train/untrain echo) goes out before the perk action changes.</summary>
        void OnTrainedPerksChanged(Action? confirm)
        {
            PerkAction[] before = _perkActions;
            RebasePerkTemplates();
            confirm?.Invoke();
            AnnouncePerkActionChanges(before, _perkActions);
            RebaseStats();
            Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(this);
        }

        /// <summary>Rebuilds <see cref="PerkTemplates"/> and <see cref="PerkActions"/> from <see cref="TrainedPerks"/>.</summary>
        public void RebasePerkTemplates()
        {
            int[] perkIds = TrainedPerks.Snapshot();
            var templates = new List<ItemTemplate>(perkIds.Length);
            foreach (int perkId in perkIds)
            {
                if (!PerkCatalog.TryGetItemId(perkId, out int itemId))
                {
                    Logger.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Trained perk {0} on character {1} is not in Perks.json; it grants nothing.", perkId, Identity.Instance));
                    continue;
                }

                try
                {
                    templates.Add(_items.CreateTemplate(itemId, itemId, quality: 1));
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, string.Format(CultureInfo.InvariantCulture,
                        "Perk {0} item {1} has no item template; it grants nothing.", perkId, itemId));
                }
            }

            // The Shadowbreed is worn like a perk: ShadowBreedTemplate (579) names the current phase's item, set by
            // uploading "The Lighter Side" / "The Darker Side". Its OnWear grants the Shadowbreed perk action and the
            // ShadowBreed (532) stat.
            int shadowBreedItemId = Stats.GetOrZero(CharacterStat.ShadowBreedTemplate, StatDetail.Base);
            _wornShadowBreedItemId = shadowBreedItemId;
            if (shadowBreedItemId > 0)
            {
                try
                {
                    templates.Add(_items.CreateTemplate(shadowBreedItemId, shadowBreedItemId, quality: 1));
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, string.Format(CultureInfo.InvariantCulture,
                        "Shadowbreed item {0} on character {1} has no item template; it grants nothing.",
                        shadowBreedItemId, Identity.Instance));
                }
            }

            _perkTemplates = templates.ToArray();
            RebasePerkActions();
            RebasePerkPulses();
        }

        /// <summary>The ShadowBreedTemplate item <see cref="PerkTemplates"/> was last built with (0 for none).</summary>
        int _wornShadowBreedItemId;

        /// <summary>
        /// A new Shadowbreed phase is worn at once. Changing sides loses the Shadowbreed: it belongs to the side whose
        /// item granted it. Hydration (before the login perk build) is not a change: that build wears what is stored.
        /// A character's first Shadowbreed is the stat's first write, so that is not skipped.
        /// </summary>
        void OnShadowBreedStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            if (!_perksLoaded)
                return;

            if (stat == CharacterStat.ShadowBreedTemplate)
            {
                if (Stats.GetOrZero(CharacterStat.ShadowBreedTemplate, StatDetail.Base) != _wornShadowBreedItemId)
                    OnTrainedPerksChanged(null);
            }
            else if (stat == CharacterStat.Side && !isInitialSet && previous != next
                && Stats.GetOrZero(CharacterStat.ShadowBreedTemplate, StatDetail.Base) != 0)
            {
                Stats.Set(CharacterStat.ShadowBreedTemplate, 0, StatDetail.Base, dirty: true);
            }
        }

        /// <summary>Rebuilds <see cref="PerkActions"/> from the perk templates' OnWear AddAction functions.</summary>
        void RebasePerkActions()
        {
            var actions = new List<PerkAction>();
            foreach (ItemTemplate perk in _perkTemplates)
            {
                if (!perk.SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear))
                    continue;

                foreach (ItemSpell spell in wear)
                {
                    if (!spell.Is(FunctionType.AddAction)
                        || !spell.TryReadInt(0, out int slot)
                        || !spell.TryReadInt(3, out int actionTemplateId)
                        || !TryReadPerkActionHash(spell, out int hash))
                        continue;

                    actions.Add(new PerkAction(actionTemplateId, slot, hash));
                }
            }

            _perkActions = actions.ToArray();
        }

        /// <summary>
        /// Login / zone / FullCharacter: every held perk action, running perk lock and running skill lock goes to the
        /// client. The client keeps its cooldown timers only for the playfield it built them in, so a lock started before
        /// a zone change is shown again with its remaining seconds.
        /// </summary>
        public void SendPerkActions()
        {
            AnnouncePerkActionChanges([], _perkActions);

            DateTime nowUtc = DateTime.UtcNow;
            foreach ((int perkId, TimeSpan remaining) in PerkLocks.Active(nowUtc))
                AnnouncePerkLock(perkId, (int)Math.Ceiling(remaining.TotalSeconds), nowUtc);

            // The client gets running skill locks in FullCharacter (BuildSkillLockEntries); here the server's own
            // SpecialAvailable for each is scheduled again, which a login has no record of.
            foreach ((int statId, TimeSpan remaining) in SkillLocks.Active(nowUtc))
            {
                if (IsClientSkillLock(statId))
                    ScheduleSpecialAvailable(statId, nowUtc + remaining);
            }
        }

        /// <summary>
        /// Skill locks the client shows as a cooldown. LastPerkResetTime has its own stat timer and the /stuck Stability
        /// lock is never shown.
        /// </summary>
        static bool IsClientSkillLock(int statId)
            => statId is not ((int)CharacterStat.LastPerkResetTime or (int)CharacterStat.Stability);

        /// <summary>
        /// FullCharacter's skill lock list (the first FullCharacterSub2 array): per running lock 1, Identity (0, stat id),
        /// the seconds it was locked for, the seconds left. Live after a Dimach and a zone change: 1, (0, 144), 1800, 1798
        /// (capture 2026-10-14T00:26:23Z); retail sends no SpecialUsed for it again.
        /// </summary>
        FullCharacterSub2[] BuildSkillLockEntries()
        {
            var entries = new List<FullCharacterSub2>();
            foreach ((int statId, int duration, int remaining) in SkillLocks.ActiveWithDuration(DateTime.UtcNow))
            {
                if (!IsClientSkillLock(statId))
                    continue;

                entries.Add(new FullCharacterSub2
                {
                    Unknown1 = 1,
                    Unknown2 = new Identity { Type = (IdentityType)0, Instance = statId },
                    Unknown3 = duration,
                    Unknown4 = remaining
                });
            }

            return [.. entries];
        }

        /// <summary>
        /// FullCharacter's nano line lock list (the third FullCharacterSub2 array): per running lock 1, Identity (1,
        /// nano strain), the seconds it was locked for, the seconds left. Live after a Lesser Healing Touch and a zone
        /// change: 1, (1, 951), 8, 5 (capture 2026-10-17T01:38:54Z). The client only times the lines it cast itself, so
        /// this is how a lock survives zoning and login on its side.
        /// </summary>
        FullCharacterSub2[] BuildNanoLineLockEntries()
        {
            var entries = new List<FullCharacterSub2>();
            foreach ((int strain, int duration, int remaining) in NanoLineLocks.ActiveWithDuration(DateTime.UtcNow))
            {
                entries.Add(new FullCharacterSub2
                {
                    Unknown1 = 1,
                    Unknown2 = new Identity { Type = (IdentityType)1, Instance = strain },
                    Unknown3 = duration,
                    Unknown4 = remaining
                });
            }

            return [.. entries];
        }

        /// <summary>
        /// RemovePerkAction for actions no longer held, AddPerkAction for new ones. Client case 0x43
        /// (Gamecode.dll 0x10043026) keys the button on Parameter1 (slot), builds its identity from
        /// Parameter2 (hash) and stores Target.Instance (action template); Target.Type is not read.
        /// </summary>
        void AnnouncePerkActionChanges(PerkAction[] before, PerkAction[] after)
        {
            IZoneSession? session = Session;
            if (session?.State != SessionState.InPlay)
                return;

            foreach (PerkAction action in before)
            {
                if (Array.IndexOf(after, action) < 0)
                    session.Send(BuildPerkActionMessage(CharacterActionType.RemovePerkAction, action));
            }

            foreach (PerkAction action in after)
            {
                if (Array.IndexOf(before, action) < 0)
                    session.Send(BuildPerkActionMessage(CharacterActionType.AddPerkAction, action));
            }
        }

        CharacterActionMessage BuildPerkActionMessage(CharacterActionType type, PerkAction action)
            => new()
            {
                Identity = Identity,
                Action = type,
                Target = new Identity { Instance = action.ActionTemplateId },
                Parameter1 = action.Slot,
                Parameter2 = action.Hash
            };

        /// <summary>
        /// FullCharacter perk map (Gamecode.dll 0x10053ac9 / 0x10052b7d): per trained perk the key id, then
        /// version marker 0xFFFFFF00, the perk id and a value the client keeps only for research perks.
        /// </summary>
        public PerkMapEntry[] BuildPerkMap()
        {
            // Research tiers in progress ride along with the research XP they still need: the client keeps that value
            // only for research (0x10052b7d) and treats a research entry as researched only at 0.
            var entries = TrainedPerks.Snapshot()
                .Select(perkId => new PerkMapEntry { Key = perkId, Marker = PerkMapEntry.VersionZero, PerkId = perkId })
                .ToList();
            foreach ((int researchId, int remaining) in Research.Snapshot())
            {
                if (remaining > 0 && !TrainedPerks.Contains(researchId))
                    entries.Add(new PerkMapEntry
                    {
                        Key = researchId,
                        Marker = PerkMapEntry.VersionZero,
                        PerkId = researchId,
                        Value = remaining
                    });
            }

            return entries.ToArray();
        }

        /// <summary>
        /// A perk action use waiting out its AttackDelay: the resolved template (low/high pair at Quality) and
        /// who it lands on.
        /// </summary>
        public sealed record PerkActionUse(int Hash, ItemTemplate Template, int LowId, int HighId, int Quality, Character Target);

        /// <summary>
        /// UsePerk start: the perk action <paramref name="hash"/> only when a trained perk grants it. A tiered
        /// action picks its tier and QL from the action's Attack skills; its cooldown, ToUse requirements, target
        /// and AttackRange must pass. The use runs after the template's AttackDelay (ItemUseService).
        /// </summary>
        public bool TryPreparePerkAction(int hash, out PerkActionUse? use)
        {
            use = null;
            if (Session == null || Playfield == null || IsDead)
                return false;

            int index = Array.FindIndex(_perkActions, action => action.Hash == hash);
            if (index < 0)
                return false;

            ItemTemplate template;
            int lowId, highId, quality;
            try
            {
                (template, lowId, highId, quality) = ResolvePerkActionTemplate(_perkActions[index]);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, string.Format(CultureInfo.InvariantCulture,
                    "Perk action {0:X8} has no usable template.", hash));
                return false;
            }

            // ApplyOnFightingTarget actions need the character being fought; ApplyOnHostile ones any attackable character,
            // without having to be fighting it (UseTargetRules). Otherwise Target / Fightingtarget functions (Pulverize's
            // hit) land on who the player is fighting, else the selected character; User / Wearer functions (LockPerk,
            // feedback) resolve to this player as source.
            Character target = this;
            CanFlags can = UseTargetRules.CanOf(template);
            if (UseTargetRules.IsHostileTargeted(can))
            {
                if (!UseTargetRules.TryResolve(this, can, out target, out UseTargetRules.Failure targetFailure))
                {
                    UseTargetRules.SendFailure(this, targetFailure, TryResolveFightingTarget());
                    return false;
                }
            }
            else if (template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells)
                && spells.Exists(spell => spell.Target is (int)ItemTarget.Target or (int)ItemTarget.Fightingtarget))
            {
                bool friendly = (can & CanFlags.ApplyOnFriendly) != 0;
                bool preferSelected = friendly
                    || template.Actions.Exists(action => action.ActionType == (int)ActionType.ToUse
                        && action.Requirements.Exists(requirement => (Operator)requirement.Operator == Operator.OnTarget));
                // A friendly action (Lay on Hands 215791) is used on whoever is selected, the player included.
                Character? resolved = ResolvePerkActionTarget(preferSelected, allowSelf: friendly);
                if (resolved == null)
                {
                    RequirementFeedback.SendText(this, "You need a target to use this.");
                    return false;
                }

                // ...and only on a friendly one, as a friendly nano: never on a monster or an enemy.
                if (friendly && (can & CanFlags.ApplyOnHostile) == 0)
                {
                    Nanos.NanoCastRefusal friendlyRefusal = Nanos.NanoRuntime.FriendlyNanoTargetRefusal(this, resolved);
                    if (friendlyRefusal != Nanos.NanoCastRefusal.None)
                    {
                        Nanos.NanoRuntime.Refuse(this, friendlyRefusal);
                        return false;
                    }
                }

                target = resolved;
            }

            var candidate = new PerkActionUse(hash, template, lowId, highId, quality, target);
            SyncCharState();
            string? failure = RevalidatePerkAction(candidate, starting: true);
            if (failure != null)
            {
                if (failure == OutOfRangeFailure)
                    RequirementFeedback.SendText(this, "Your target is out of range.");
                else if (failure == NoLineOfSightFailure)
                    RequirementFeedback.SendText(this, "Your target is not in line of sight.");
                else if (failure == NotAttackableFailure)
                    ClientFeedback.Send(this, CombatRules.IsPvpAttackBlocked(this, target)
                        ? "Feedback_PvpNotAllowedInThisDistrict"
                        : "Feedback_StartingAttackFailed");
                else if (failure == UseRequirementsFailure)
                    RequirementFeedback.SendIfUnmet(this, template, ActionType.ToUse,
                        stat => PerkActionStat(stat, target), ResolvePerkRequirement,
                        stat => target.Stats.Get(stat), resolveTarget: target.ResolveRequirement);
                return false;
            }

            // A hostile perk action aimed at a player, or a friendly one on a PvP-enabled player, flags this player for PvP.
            if (!ReferenceEquals(target, this))
            {
                if (IsHostilePerkAction(template) || UseTargetRules.IsHostileTargeted(UseTargetRules.CanOf(template)))
                    PvpAttackerFlag.OnHostileAction(this, target);
                else
                    PvpAttackerFlag.OnFriendlyAction(this, target);
            }

            use = candidate;
            return true;
        }

        /// <summary>
        /// A stat as a perk action's ToUse criteria read it. TargetFacing is not stored: it is 1 while this player stands
        /// behind the action's target (Stab: [TargetFacing EqualTo 1]) and 0 otherwise, self-targeted included.
        /// </summary>
        int PerkActionStat(CharacterStat stat, Character target)
            => stat == CharacterStat.TargetFacing
                ? (!ReferenceEquals(target, this) && SpecialAttacks.IsBehind(this, target) ? 1 : 0)
                : Stats.Get(stat);

        const string OutOfRangeFailure = "target out of range";

        const string NoLineOfSightFailure = "target not in line of sight";

        const string NotAttackableFailure = "target not attackable";

        const string UseRequirementsFailure = "use requirements failed";

        /// <summary>
        /// Gates checked when a perk action is asked for and again when its AttackDelay ends: still held, not locked, and
        /// a living, attackable target (the fighting target when the action needs one). ToUse requirements, AttackRange
        /// and line of sight only when <paramref name="starting"/>: met then, the action fires. Null when still valid.
        /// </summary>
        public string? RevalidatePerkAction(PerkActionUse use, bool starting)
        {
            ArgumentNullException.ThrowIfNull(use);
            if (Array.FindIndex(_perkActions, action => action.Hash == use.Hash) < 0)
                return "perk action no longer held";
            if (IsPerkActionLocked(use.Template))
                return "perk locked";
            if (starting && !use.Template.MeetsActionRequirements(stat => PerkActionStat(stat, use.Target),
                ActionType.ToUse, ItemTemplate.WithActionTarget(ResolvePerkRequirement, this, use.Target),
                stat => use.Target.Stats.Get(stat), use.Target.ResolveRequirement))
                return UseRequirementsFailure;
            if (!ReferenceEquals(use.Target, this))
            {
                if (use.Target.IsDead || !ReferenceEquals(use.Target.Playfield, Playfield))
                    return "target gone";
                CanFlags can = UseTargetRules.CanOf(use.Template);
                if ((IsHostilePerkAction(use.Template) || UseTargetRules.IsHostileTargeted(can))
                    && !CombatRules.CanAttack(this, use.Target))
                    return NotAttackableFailure;
                if (UseTargetRules.NeedsFightingTarget(can) && !ReferenceEquals(TryResolveFightingTarget(), use.Target))
                    return UseTargetRules.LostFightingTarget;
                if (starting && GetEdgeDistanceTo(use.Target) > PerkActionRange(use.Template))
                    return OutOfRangeFailure;
                if (starting && !HasLineOfSightTo(use.Target))
                    return NoLineOfSightFailure;
            }

            return null;
        }

        /// <summary>
        /// A perk action that did not land (<see cref="PerkActionLands"/>), as live answers it straight away (capture
        /// 2026-10-02T13:02:33Z): "Target evaded your &lt;action&gt;!" (FormatFeedback 110/79653355 with the "evaded"
        /// reference and the action name), "Target resisted." (Feedback 110/205237300), then the template's OnFailure
        /// functions (Perforate / Pulverize LockPerk, sent as PerkUnavailable).
        /// </summary>
        public void FailPerkAction(PerkActionUse use, IInventoryRepository inventoryRepository)
        {
            ArgumentNullException.ThrowIfNull(use);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            ClientFeedback.SendFormatted(this, ClientFeedback.TargetVerbYourAction,
                new ClientFeedback.TextReference(ClientFeedback.CategoryId, ClientFeedback.Evaded), use.Template.Name ?? string.Empty);
            ClientFeedback.Send(this, ClientFeedback.TargetResisted);
            use.Template.ExecuteSpells(EventType.OnFailure, use.Target, inventoryRepository, _items, source: this);
        }

        /// <summary>
        /// Performs a perk action that landed: its OnUse functions run with this player as the source and always hit;
        /// then it is announced as live does after the hit and feedback: TemplateAction PerkAction (low/high, QL,
        /// performer, target).
        /// </summary>
        public bool ExecutePerkAction(PerkActionUse use, IInventoryRepository inventoryRepository)
        {
            ArgumentNullException.ThrowIfNull(use);
            ArgumentNullException.ThrowIfNull(inventoryRepository);
            if (!use.Template.ExecuteOnUseSpells(use.Target, inventoryRepository, _items, source: this))
                return false;

            Character shown = ReferenceEquals(use.Target, this) ? TryResolveFightingTarget() ?? this : use.Target;
            var performed = new TemplateActionMessage
            {
                Identity = Identity,
                ItemLowId = use.LowId,
                ItemHighId = use.HighId,
                Quality = use.Quality,
                Unknown1 = 1,
                Action = TemplateActionType.PerkAction,
                Placement = Identity,
                Unknown3 = (int)shown.Identity.Type,
                Unknown4 = shown.Identity.Instance
            };
            Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, performed, includeSelf: true);
            return true;
        }

        /// <summary>
        /// Whether a perk action aimed at another character succeeds, deterministically: this player's attack rating
        /// (the action's Attack skills weighted against this player, plus Add All Offense) must reach the target's
        /// defense rating (the action's Defend skills weighted against the target, plus Add All Defense). Pulverize:
        /// 2H Blunt 100% + AAO vs skill 155 at 85% + AAD. Self-only actions and actions without Attack or Defend skills
        /// always succeed. Decided when the action is used: live answers a miss at once, with no QueuePerk.
        /// </summary>
        public bool PerkActionLands(PerkActionUse use)
        {
            // TODO: Placeholder. This is not even close to the correct perk action landing formula.
            if (ReferenceEquals(use.Target, this) || use.Template.Attack.Count == 0 || use.Template.Defend.Count == 0)
                return true;

            long attackRating = WeightedSkill(use.Template.Attack) + Stats.GetOrZero(CharacterStat.AMSModifier);

            long defenseSkill = 0;
            foreach ((CharacterStat stat, int percent) in use.Template.Defend)
                defenseSkill += (long)use.Target.Stats.GetOrZero(stat) * percent;
            long defenseRating = defenseSkill / 100 + use.Target.Stats.GetOrZero(CharacterStat.DMSModifier);

            return attackRating >= defenseRating;
        }

        /// <summary>Template AttackRange in meters; a missing or zero range means touch range.</summary>
        static double PerkActionRange(ItemTemplate template)
        {
            double range = template.Stats.TryGetValue(CharacterStat.AttackRange, out int value)
                ? StatCollection.Normalize(value)
                : 0;
            return range > 0 ? range : 1;
        }

        /// <summary>
        /// Explicit friendly/target criteria use selection; other actions prefer the fighting target. With
        /// <paramref name="allowSelf"/>, selecting yourself targets yourself.
        /// </summary>
        Character? ResolvePerkActionTarget(bool preferSelected = false, bool allowSelf = false)
        {
            Character? fighting = TryResolveFightingTarget();
            if (!preferSelected && fighting != null)
                return fighting;

            if (allowSelf && Target == Identity)
                return this;

            if (Target.Instance == 0 || Target == Identity || Playfield == null
                || !Playfield.GetRequiredService<DynelRegistry>().TryGet(Target, out Dynel? dynel)
                || dynel is not Character selected || selected.IsDead)
                return preferSelected ? null : fighting;

            return selected;
        }

        /// <summary>
        /// An attack: an OnUse damage function (Hit, SpecialHit, DrainHit, AreaHit with a negative amount) aimed at
        /// the target. Such a perk action, like any attack, needs a target the player may engage (CombatRules).
        /// </summary>
        static bool IsHostilePerkAction(ItemTemplate template)
        {
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            foreach (ItemSpell spell in spells)
            {
                if (spell.Target is not ((int)ItemTarget.Target or (int)ItemTarget.Fightingtarget))
                    continue;
                if (!spell.Is(FunctionType.Hit) && !spell.Is(FunctionType.SpecialHit)
                    && !spell.Is(FunctionType.DrainHit) && !spell.Is(FunctionType.AreaHit))
                    continue;
                if (spell.TryReadInt(1, out int amount) && amount < 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True while a perk the action would lock (its OnUse LockPerk) is still locked, so a use cannot
        /// bypass the cooldown even when the action's ToUse criteria omit IsPerkUnlocked.
        /// </summary>
        bool IsPerkActionLocked(ItemTemplate template)
        {
            if (!template.SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return false;

            DateTime nowUtc = DateTime.UtcNow;
            foreach (ItemSpell spell in spells)
            {
                if (spell.Is(FunctionType.LockPerk)
                    && ItemUseFunctions.TryReadPerkLock(spell, out int perkId, out _)
                    && PerkLocks.IsLocked(perkId, nowUtc))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The granted template, or for a tiered action (PerkActions.json) the tier pair interpolated at the QL
        /// the player's weighted Attack skill reaches.
        /// </summary>
        (ItemTemplate Template, int LowId, int HighId, int Quality) ResolvePerkActionTemplate(PerkAction action)
        {
            if (!PerkActionCatalog.TryGet(action.Hash, out PerkActionDefinition definition) || definition.Tiers.Length == 0)
            {
                int id = action.ActionTemplateId;
                return (_items.CreateTemplate(id, id, quality: 1), id, id, 1);
            }

            PerkActionTier first = definition.Tiers[0];
            ItemTemplate skillSource = _items.CreateTemplate(first.LowId, first.LowId, first.LowQl);
            (PerkActionTier tier, int quality) = definition.Resolve(WeightedSkill(skillSource.Attack));
            return (_items.CreateTemplate(tier.LowId, tier.HighId, quality), tier.LowId, tier.HighId, quality);
        }

        /// <summary>
        /// Attack skill map (stat to percent) weighted against this player's skills. AMS (22) is the full attack rating
        /// of the wielded weapon (<see cref="DamageCalculator.ResolveAttackRating(Character)"/>), Add All Off. included.
        /// </summary>
        int WeightedSkill(IReadOnlyDictionary<CharacterStat, int> attack)
        {
            long total = 0;
            foreach ((CharacterStat stat, int percent) in attack)
            {
                int skill = stat == CharacterStat.AMS ? DamageCalculator.ResolveAttackRating(this) : Stats.GetOrZero(stat);
                total += (long)skill * percent;
            }
            return (int)Math.Clamp(total / 100, 0, int.MaxValue);
        }

        /// <summary>The 4-char hash argument as stored (int) or as text packed big-endian ("CNRE" = 0x434E5245).</summary>
        static bool TryReadPerkActionHash(ItemSpell spell, out int hash)
        {
            if (spell.TryReadInt(1, out hash))
                return true;
            if (!spell.TryReadString(1, out string text) || text.Length != 4)
                return false;

            hash = (text[0] << 24) | (text[1] << 16) | (text[2] << 8) | text[3];
            return true;
        }

        /// <summary>Applies the OnWear stat functions of every perk template as bonuses.</summary>
        void RebasePerks()
            => RebasePerks(Stats);

        /// <summary>
        /// Applies each trained perk whose ToWear requirements pass. The client counts a perk only while they do (a
        /// character below a perk's level keeps it trained but gets none of its bonuses), so the server matches it.
        /// </summary>
        void RebasePerks(StatCollection stats)
        {
            foreach (ItemTemplate perk in _perkTemplates)
            {
                if (!perk.MeetsActionRequirements(stat => stats.Get(stat), ActionType.ToWear, ResolvePerkRequirement))
                    continue;
                if (perk.SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear))
                    StatModifierSpells.Apply(wear, stats);
            }
        }


        /// <summary>
        /// A perk's pulsing OnWear cast: Duration -1 with an Interval in centiseconds, e.g. Bureaucrat Directorship 223630
        /// TeamCastNano every 180 s and Commanding Presence 210931 every 60 s. Each tier's function requires HasPerk of its
        /// tier and not the next, so only the highest trained tier fires.
        /// </summary>
        readonly record struct PerkPulse(int PerkItemId, int SpellIndex, ItemTemplate Perk, ItemSpell Spell, TimeSpan Interval);

        const double CentisecondsPerMillisecond = 0.1;

        PerkPulse[] _perkPulses = [];
        DateTime[] _perkPulseDueUtc = [];
        DateTime _nextPerkPulseUtc = DateTime.MaxValue;

        /// <summary>
        /// Rebuilds the pulse list from <see cref="_perkTemplates"/> (one entry per perk item, though each trained tier
        /// lists it again). A pulse that was already scheduled keeps its time; a new one fires on the next tick.
        /// </summary>
        void RebasePerkPulses()
        {
            var previous = new Dictionary<(int, int), DateTime>();
            for (int i = 0; i < _perkPulses.Length; i++)
                previous[(_perkPulses[i].PerkItemId, _perkPulses[i].SpellIndex)] = _perkPulseDueUtc[i];

            var pulses = new List<PerkPulse>();
            var seen = new HashSet<int>();
            foreach (ItemTemplate perk in _perkTemplates)
            {
                if (!seen.Add(perk.Id) || !perk.SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear))
                    continue;
                for (int i = 0; i < wear.Count; i++)
                {
                    ItemSpell spell = wear[i];
                    if (spell.TickCount < 0 && spell.TickInterval > 0 && WearCastNano.IsCastFunction(spell))
                        pulses.Add(new PerkPulse(perk.Id, i, perk, spell,
                            TimeSpan.FromMilliseconds(spell.TickInterval / CentisecondsPerMillisecond)));
                }
            }

            _perkPulses = pulses.ToArray();
            _perkPulseDueUtc = new DateTime[_perkPulses.Length];
            for (int i = 0; i < _perkPulses.Length; i++)
                _perkPulseDueUtc[i] = previous.TryGetValue((_perkPulses[i].PerkItemId, _perkPulses[i].SpellIndex), out DateTime due)
                    ? due : DateTime.MinValue;
            _nextPerkPulseUtc = _perkPulseDueUtc.Length == 0 ? DateTime.MaxValue : _perkPulseDueUtc.Min();
        }

        /// <summary>Fires each due perk pulse whose perk and function requirements pass, then schedules its next one.</summary>
        void TickPerkPulses()
        {
            if (_perkPulses.Length == 0 || IsDead || Playfield == null)
                return;
            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc < _nextPerkPulseUtc)
                return;
            IInventoryRepository? inventory = Playfield.GetService<IInventoryRepository>();
            if (inventory == null)
                return;

            DateTime next = DateTime.MaxValue;
            for (int i = 0; i < _perkPulses.Length; i++)
            {
                if (_perkPulseDueUtc[i] <= nowUtc)
                {
                    PerkPulse pulse = _perkPulses[i];
                    _perkPulseDueUtc[i] = nowUtc + pulse.Interval;
                    if (pulse.Perk.MeetsActionRequirements(stat => Stats.Get(stat), ActionType.ToWear, ResolvePerkRequirement)
                        && ItemTemplate.MeetsRequirements(pulse.Spell.Requirements, stat => Stats.Get(stat), ResolvePerkRequirement))
                    {
                        using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("perk.pulse", pulse.PerkItemId))
                            Nanos.NanoCastFunctions.TryExecute(this, this, pulse.Spell, _items, inventory);
                    }
                }

                if (_perkPulseDueUtc[i] < next)
                    next = _perkPulseDueUtc[i];
            }

            _nextPerkPulseUtc = next;
        }

        /// <summary>
        /// Bit on <see cref="CharacterStat.SelectedTargetType"/> when the look-at target is an
        /// <see cref="NpcCharacter"/>. Item ToUse rows such as Health and Nano Stim require
        /// <c>NotBitAnd</c> this flag.
        /// </summary>
        const int SelectedTargetTypeNpcFlag = 16;

        /// <summary>Current look-at / selection target from the client.</summary>
        public Identity Target { get; private set; } = Identity.None;

        /// <summary>
        /// Updates look-at selection and mirrors NPC vs non-NPC onto SelectedTargetType bit 16.
        /// </summary>
        public void SetTarget(Identity target)
        {
            Target = target;

            int flags = Stats.GetOrZero(CharacterStat.SelectedTargetType) & ~SelectedTargetTypeNpcFlag;
            if (target.Instance != 0
                && Playfield != null
                && Playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                && dynel is NpcCharacter)
                flags |= SelectedTargetTypeNpcFlag;

            Stats.Set(CharacterStat.SelectedTargetType, flags, StatDetail.Base);
        }

        internal IZoneLogger Logger { get; set; }

        public override void OnDeath(Character? killer = null)
        {
            if (IsDead)
                return;

            base.OnDeath(killer);
            ApplyDeathXpLoss();
            _respawnObserversCleared = false;
            _respawnPending = true;
            _respawnRemainingSeconds = RespawnGracePeriodMilliseconds / 1000.0;
            _diedAtUtc = DateTime.UtcNow;
        }

        public override void Revive()
        {
            _respawnPending = false;
            base.Revive();
        }

        /// <summary>
        /// Official client requests respawn with <see cref="CharacterActionType.Die"/> after death.
        /// Grace still applies; a request before 3s waits, a request after 3s runs immediately.
        /// </summary>
        internal void RequestRespawn()
        {
            if (!_respawnPending && !IsDead)
                return;

            if (!IsRespawnGraceElapsed())
                return;

            _respawnPending = false;
            TryRespawn();
        }

        public override void Tick(double deltaTime)
        {
            base.Tick(deltaTime);
            ZoneEngine_New.Core.Metrics.TickStallWatch.Stage("player.tick", Identity.Instance);
            if (_linkDeadStopPending)
            {
                _linkDeadStopPending = false;
                StopInPlace();
            }

            TickPerkLocks();
            TickPerkPulses();
            TickSelfTermination();
            SuppressionGas.Update(this, DateTime.UtcNow);
            if (QuestLog?.Quests.Values.Any(quest => quest.IsRewardPending
                || quest.IsActive && quest.NpcObjectiveCommitPending) == true)
                Playfield?.GetService<Quests.QuestService>()?.RetryPendingRewards(this);
            if (!_respawnPending)
                return;

            _respawnRemainingSeconds -= deltaTime;
            if (!IsRespawnGraceElapsed())
                return;

            _respawnPending = false;
            TryRespawn();
        }

        /// <summary>PvP grace on entering suppression gas that allows PvP, disgrace on leaving it.</summary>
        public ZoneEngine_New.Core.Playfield.SuppressionGasPeriod SuppressionGas { get; } = new();

        /// <summary>How long /terminate waits before the character dies.</summary>
        public static readonly TimeSpan SelfTerminationDelay = TimeSpan.FromMinutes(1);

        DateTime? _selfTerminateAtUtc;
        Playfield? _selfTerminatePlayfield;

        public bool IsSelfTerminating => _selfTerminateAtUtc != null;

        /// <summary>GmLevel above 0, as <see cref="Characters.CharacterHydrationValidator"/> judges it.</summary>
        public bool IsGm => Stats.GetOrZero(CharacterStat.GmLevel) > 0;

        /// <summary>Characters (by id) hidden with <c>.visibility off</c>; kept per character until the engine restarts.</summary>
        static readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> HiddenCharacters = new();

        /// <summary>
        /// <c>.visibility off</c>: this character is not spawned on players' clients at all (GMs still see it), like a
        /// sneaker they cannot perceive. It survives zoning and relogging, not an engine restart.
        /// </summary>
        /// <summary>Whether character <paramref name="characterId"/> is hidden with <c>.visibility off</c>.</summary>
        public static bool IsHiddenCharacter(int characterId) => HiddenCharacters.ContainsKey(characterId);

        public bool HiddenFromPlayers
        {
            get => HiddenCharacters.ContainsKey(Identity.Instance);
            set
            {
                if (value)
                    HiddenCharacters[Identity.Instance] = 0;
                else
                    HiddenCharacters.TryRemove(Identity.Instance, out _);
            }
        }

        /// <summary>
        /// /terminate: the character dies <see cref="SelfTerminationDelay"/> from now unless it zones, logs off or dies
        /// first. False when it is dead or already counting down.
        /// </summary>
        public bool BeginSelfTermination(DateTime nowUtc)
        {
            if (IsDead || _selfTerminateAtUtc != null || Playfield == null)
                return false;

            _selfTerminateAtUtc = nowUtc + SelfTerminationDelay;
            _selfTerminatePlayfield = Playfield;
            return true;
        }

        public void CancelSelfTermination()
        {
            _selfTerminateAtUtc = null;
            _selfTerminatePlayfield = null;
        }

        void TickSelfTermination()
        {
            if (_selfTerminateAtUtc is not DateTime at)
                return;

            // Leaving the playfield (zoning, logging off), dropping link-dead or dying some other way ends the countdown.
            if (IsDead || ConnectionPhase == PlayerConnectionPhase.LinkDead
                || !ReferenceEquals(Playfield, _selfTerminatePlayfield))
            {
                CancelSelfTermination();
                return;
            }

            if (DateTime.UtcNow < at)
                return;

            CancelSelfTermination();
            // Retail (capture 2026-10-20T18:12:17Z): DeathReason 1 ("You died of self termination!"), then Death 504.
            Session?.Send(new CharacterActionMessage
            {
                Identity = Identity,
                Action = CharacterActionType.DeathReason,
                Target = Identity.None,
                Parameter1 = 0,
                Parameter2 = SelfTerminationDeathReason
            });
            _selfTerminationDeath = true;
            try
            {
                // A live Health write to 0 kills (Character.OnStatChanged).
                Stats.Set(CharacterStat.Health, 0, StatDetail.Base, dirty: true);
            }
            finally
            {
                _selfTerminationDeath = false;
            }
        }

        const int SelfTerminationDeathReason = 1;
        const int SelfTerminationDeathAnimationKey = 504;
        bool _selfTerminationDeath;

        protected override int DeathAnimationKey
            => _selfTerminationDeath ? SelfTerminationDeathAnimationKey : base.DeathAnimationKey;

        bool IsRespawnGraceElapsed()
            => _respawnRemainingSeconds <= 0.0
                || (DateTime.UtcNow - _diedAtUtc).TotalSeconds >= RespawnGracePeriodMilliseconds / 1000.0;

        /// <summary>
        /// Set when the normal respawn could not be resolved: the character comes back at the stuck location (Gameplay.json
        /// stuckLocation) instead. Kept until the revive lands, across any wait for that playfield to build.
        /// </summary>
        bool _respawnAtStuckLocation;

        void TryRespawn()
        {
            try
            {
                Respawn();
                SaveState.MarkDirty();
            }
            catch (Exception exception) when (!_respawnAtStuckLocation)
            {
                // A respawn that cannot be resolved (a save point whose playfield is gone, no respawn configured) must
                // not leave the character dead: forget the save point and send them to the stuck location.
                Logger.Error(exception, string.Format(CultureInfo.InvariantCulture,
                    "Player respawn failed character={0} savePointPf={1}; respawning at the stuck location.",
                    Identity.Instance, Stats.GetOrZero(CharacterStat.ResurrectDest, StatDetail.Base)));
                Stats.Set(CharacterStat.ResurrectDest, 0, StatDetail.Base);
                _respawnAtStuckLocation = true;
                TryRespawn();
            }
            catch (Exception exception)
            {
                _respawnAtStuckLocation = false;
                Logger.Error(exception, "Player respawn at the stuck location failed character=" + Identity.Instance + ".");
            }
        }

        /// <summary>
        /// Where a death brings this character back: the stuck location after a failed resolution, else the last save
        /// point, else the new-character start (Arete Landing), else Respawn.json. Throws when none is configured.
        /// </summary>
        (int Playfield, Vector3 Landing) ResolveRespawn(Playfield playfield)
        {
            if (_respawnAtStuckLocation)
            {
                if (playfield.GetService<GameplaySettings>()?.StuckLocation is not GameplayLocation stuck || !stuck.IsValid)
                    throw new InvalidOperationException("No stuck location is configured.");
                return (stuck.Playfield, new Vector3(stuck.X, stuck.Y, stuck.Z));
            }

            return ResolveSavePointDestination(playfield);
        }

        /// <summary>
        /// The character's save point (an insurance terminal's SaveChar), else the new-character start (Arete Landing),
        /// else Respawn.json. Throws when none is configured.
        /// </summary>
        internal (int Playfield, Vector3 Landing) ResolveSavePointDestination(Playfield playfield)
        {
            ArgumentNullException.ThrowIfNull(playfield);
            if (TryGetSavePoint(out int savedPlayfield, out Vector3 saved))
                return (savedPlayfield, saved);

            RespawnContentCatalog respawn = playfield.GetRequiredService<IGameData>().RespawnContent;
            if (respawn.StartPlayfieldId > 0 && respawn.StartPosition is { Length: 3 } start)
                return (respawn.StartPlayfieldId, new Vector3(start[0], start[1], start[2]));

            if (respawn.PlayfieldId <= 0 || respawn.Position is not { Length: 3 })
                throw new InvalidOperationException("No respawn destination is configured.");
            return (respawn.PlayfieldId, new Vector3(respawn.Position[0], respawn.Position[1], respawn.Position[2]));
        }

        /// <summary>
        /// Remembers where this character stands as its save point (an insurance terminal's SaveChar): death brings it
        /// back here (live 2026-10-07: saved at the Arete Cell Structure Scanner, killed, N3Teleport to the same spot).
        /// Kept in server-only stats (never sent, so not marked for the wire; the base change marks the character for
        /// saving): ResurrectDest = playfield, SavePointX / SavePointY / SavePointZ in centimetres.
        /// </summary>
        public void StoreSavePoint()
        {
            if (Playfield == null)
                return;

            Stats.Set(CharacterStat.ResurrectDest, Playfield.Identity.Instance, StatDetail.Base);
            Stats.Set(CharacterStat.SavePointX, ToCentimetres(Position.x), StatDetail.Base);
            Stats.Set(CharacterStat.SavePointY, ToCentimetres(Position.y), StatDetail.Base);
            Stats.Set(CharacterStat.SavePointZ, ToCentimetres(Position.z), StatDetail.Base);
            SaveState.MarkDirty();
            Logger.Info(string.Format(CultureInfo.InvariantCulture, "Save point character={0} pf={1} at ({2},{3},{4})",
                Identity.Instance, Playfield.Identity.Instance, Position.xf, Position.yf, Position.zf));
        }

        /// <summary>
        /// A coordinate as whole centimetres for an int stat: rounded, not truncated, so a save point is never off by a
        /// full centimetre, and clamped so a corrupt position cannot overflow the stat.
        /// </summary>
        static int ToCentimetres(double metres)
            => double.IsFinite(metres) ? (int)Math.Clamp(Math.Round(metres * 100), int.MinValue, int.MaxValue) : 0;

        /// <summary>
        /// An insurance terminal's save (SaveChar), as live did it (2026-10-07, Arete Cell Structure Scanner, level 4):
        /// Cash down by level x 100 and "400 credits were deducted from your account.", then the save point, then
        /// "Character stored. 1235 XP saved." where the number is the XP earned since the last save (or since the start
        /// of the level, which saves itself). A character that cannot pay is not saved.
        /// </summary>
        public bool InsuranceSave()
        {
            IGameData? gameData = Playfield?.GetService<IGameData>();
            if (gameData == null)
                return false;

            int level = Stats.GetOrOne(CharacterStat.Level);
            int fee = level * 100;
            int cash = Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base);
            if (cash < fee)
            {
                ClientFeedback.SendFormatted(this, ClientFeedback.PlainText,
                    string.Format(CultureInfo.InvariantCulture, "Saving costs {0} credits.", fee));
                return false;
            }

            Stats.Set(CharacterStat.Cash, cash - fee, StatDetail.Base, dirty: true);
            FlushDirtyStats();
            ClientFeedback.SendFormatted(this, ClientFeedback.CreditsDeducted, fee);

            StoreSavePoint();

            int xp = Stats.GetOrZero(CharacterStat.XP, StatDetail.Base);
            int saved = Math.Max(0, xp - SavedXpWatermark(gameData));
            Stats.Set(CharacterStat.SavedXP, xp, StatDetail.Base, dirty: true);
            FlushDirtyStats();
            SaveState.MarkDirty();
            ClientFeedback.SendFormatted(this, ClientFeedback.CharacterStored, saved);
            Logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Insurance save character={0} level={1} fee={2} xp={3} saved={4} deathPool={5}",
                Identity.Instance, level, fee, xp, saved, Stats.GetOrZero(CharacterStat.DeathXpPool, StatDetail.Base)));
            return true;
        }

        /// <summary>
        /// The XP a death cannot take: the last insurance save, or the start of the current level if later. A save above
        /// the current XP cannot be real (a save stores the XP of the moment, and a death never goes below it), so it counts
        /// as no save: one such stale value (2076225398 on a level 21 character) made every death free.
        /// </summary>
        int SavedXpWatermark(IGameData gameData)
        {
            int floor = gameData.TryGetXpLevel(Stats.GetOrOne(CharacterStat.Level), out XpLevelEntry entry) ? entry.FloorXp : 0;
            int saved = Stats.GetOrZero(CharacterStat.SavedXP, StatDetail.Base);
            int xp = Stats.GetOrZero(CharacterStat.XP, StatDetail.Base);
            return StatCollection.IsUnset(saved) || saved > xp ? floor : Math.Max(floor, saved);
        }

        /// <summary>
        /// A death takes the XP earned since the last save (never below the level's start; nothing below Gameplay.json
        /// xp.deathXpLossGraceLevel, nor at shadow levels, which earn SK). The lost XP goes into the death pool, which
        /// later kills give back (<see cref="TakeDeathPoolRecovery"/>).
        /// </summary>
        void ApplyDeathXpLoss()
        {
            IGameData? gameData = Playfield?.GetService<IGameData>();
            int level = Stats.GetOrOne(CharacterStat.Level);
            if (gameData == null || level < XpRules.DeathXpLossGraceLevel || level >= 200)
                return;

            int xp = Stats.GetOrZero(CharacterStat.XP, StatDetail.Base);
            int keep = Math.Min(xp, SavedXpWatermark(gameData));
            int lost = xp - keep;
            if (lost <= 0)
                return;

            Stats.Set(CharacterStat.XP, keep, StatDetail.Base, dirty: true);
            int pool = AddToDeathXpPool(lost);
            Logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Death XP loss character={0} level={1} xp={2}->{3} lost={4} pool={5}",
                Identity.Instance, level, xp, keep, lost, pool));
        }

        /// <summary>
        /// Adds <paramref name="xp"/> to the death pool (XP lost on a death, or earned with XP gain turned off) and tells
        /// the client: "This XP was added to the pool of unsaved experience points … Your current pool … is N." (live
        /// capture 2026-10-27T16:10:11Z, after the UnsavedXP stat). Returns the new pool.
        /// </summary>
        internal int AddToDeathXpPool(int xp)
        {
            int pool = Math.Max(0, Stats.GetOrZero(CharacterStat.DeathXpPool, StatDetail.Base));
            if (xp <= 0)
                return pool;

            int grown = (int)Math.Min(int.MaxValue, (long)pool + xp);
            SetDeathXpPool(grown);
            FlushDirtyStats();
            SaveState.MarkDirty();
            ClientFeedback.SendFormatted(this, ClientFeedback.DeathXpAddedToPool, grown);
            return grown;
        }

        /// <summary>
        /// The XP one kill gives back from the death pool, taken out of it: Gameplay.json xp.defaultXpPoolRecoveryPercent
        /// plus Regain XP Percentage (stat 593) of the kill's own XP <paramref name="killXp"/>, at least 1, at most what
        /// is left. Live gives back half with no bonus (capture 2026-10-27T16:10:51Z: a 662 XP kill recouped 331, the pool
        /// going 691 to 360). Regain XP Percentage comes from Insurance Claim (+50 to +150), Insurance Agent (+280 to +650),
        /// Explorer (+10 to +40) and similar; it adds percentage points to the default. 0 when the sum is 0.
        /// </summary>
        internal int TakeDeathPoolRecovery(int killXp)
        {
            int pool = Stats.GetOrZero(CharacterStat.DeathXpPool, StatDetail.Base);
            long percent = (long)XpRules.DefaultXpPoolRecoveryPercent + Math.Max(0, Stats.GetOrZero(CharacterStat.RegainXPPercentage));
            if (pool <= 0 || percent <= 0 || killXp <= 0)
                return 0;

            int recovered = (int)Math.Min(pool, Math.Max(1L, (long)killXp * percent / 100));
            SetDeathXpPool(pool - recovered);
            return recovered;
        }

        /// <summary>
        /// The death pool. The client is shown it as UnsavedXP (live sends the stat before the XP stat on every change),
        /// the server keeps its own copy in DeathXpPool.
        /// </summary>
        void SetDeathXpPool(int pool)
        {
            Stats.Set(CharacterStat.DeathXpPool, pool, StatDetail.Base);
            Stats.Set(CharacterStat.UnsavedXP, pool, StatDetail.Base, dirty: true);
        }

        /// <summary>Gameplay.json xp, or its defaults off a playfield.</summary>
        GameplayXp XpRules => Playfield?.GetService<GameplaySettings>()?.Xp ?? new GameplayXp();

        /// <summary>The stored save point, if any (see <see cref="StoreSavePoint"/>).</summary>
        bool TryGetSavePoint(out int playfieldId, out Vector3 position)
        {
            playfieldId = Stats.GetOrZero(CharacterStat.ResurrectDest, StatDetail.Base);
            position = new Vector3(
                Stats.GetOrZero(CharacterStat.SavePointX, StatDetail.Base) / 100.0,
                Stats.GetOrZero(CharacterStat.SavePointY, StatDetail.Base) / 100.0,
                Stats.GetOrZero(CharacterStat.SavePointZ, StatDetail.Base) / 100.0);
            // Unset reads as the client's 1234567890 sentinel or 0; a real playfield id is small.
            return playfieldId > 0 && playfieldId < 1_000_000;
        }

        void Respawn()
        {
            Playfield? playfield = Playfield;
            if (playfield == null)
            { Revive(); return; }

            (int respawnPlayfield, Vector3 landing) = ResolveRespawn(playfield);

            // Never build the respawn playfield on this tick: stay dead while it builds in the background, and
            // respawn once it is ready.
            PlayfieldManager manager = playfield.GetRequiredService<PlayfieldManager>();
            if (playfield.Identity.Instance != respawnPlayfield && !manager.TryGet(respawnPlayfield, out _))
            {
                bool queued = manager.WithPlayfield(respawnPlayfield, this, _ =>
                {
                    if (IsDead && Playfield != null)
                        TryRespawn();
                }, new ZoneEngine_New.Core.World.RemoteArrival(landing, Kind: AORebirth.World.Contracts.V1.ArrivalKind.ArrivalRespawn,
                    Prepare: () =>
                    {
                        // The respawn point runs on another zone instance: revive here, then move there alive.
                        if (!IsDead || Playfield == null) return false;
                        _respawnObserversCleared = false;
                        _respawnAtStuckLocation = false;
                        Revive();
                        AnnounceDeathCleared();
                        Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                        Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
                        return true;
                    }));

                // Another move is already waiting on a build: try again next tick rather than drop the respawn.
                if (!queued)
                {
                    _respawnPending = true;
                    _respawnRemainingSeconds = 0;
                }

                return;
            }

            // In-zone respawn: observers in range keep the dead copy unless it is despawned, and they only show the
            // fresh spawn when it comes on a later tick than the despawn. Despawn now, respawn next tick.
            if (playfield.Identity.Instance == respawnPlayfield && !_respawnObserversCleared)
            {
                playfield.GetRequiredService<PlayfieldLocality>().DespawnForObservers(this);
                _respawnObserversCleared = true;
                _respawnPending = true;
                _respawnRemainingSeconds = 0;
                return;
            }

            _respawnObserversCleared = false;
            _respawnAtStuckLocation = false;
            Revive();

            Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player respawn character={0} to ({1},{2},{3}) pf={4}",
                    Identity.Instance,
                    landing.xf,
                    landing.yf,
                    landing.zf,
                    respawnPlayfield));

            if (playfield.Identity.Instance == respawnPlayfield)
            {
                playfield.GetRequiredService<SpawnService>().CompleteSamePlayfieldDeathRespawn(this, landing);
                return;
            }

            AnnounceDeathCleared();

            // Respawning elsewhere is not a proxy entry: an exit there must not send this character back through the
            // entrance they took before dying.
            Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);

            using PlayfieldAdmission destinationAdmission = manager.AcquirePlayfield(respawnPlayfield);
            Playfield destination = destinationAdmission.Playfield;
            if (Session != null)
            {
                Session.TransferToPlayfield(destination, landing);
                return;
            }

            playfield.LeaveTransferredPlayer(this);
            destination.ArriveTransferredPlayer(this, landing);
        }

        void AnnounceDeathCleared() => SendDeathRespawnAction();

        internal void SendDeathRespawnAction()
        {
            IZoneSession? session = Session;
            if (session == null)
                return;

            session.Send(
                new CharacterActionMessage
                {
                    Identity = Identity,
                    Unknown = 0x00,
                    Action = CharacterActionType.DeathRespawn,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = DeathRespawnActionParameter1,
                    Parameter2 = DeathRespawnActionParameter2,
                    Unknown2 = 0
                });
        }

        bool _inFullRebase;

        /// <summary>Bonus rebuilds allowed for worn OE levels to settle. Each item can rise at most four levels.</summary>
        const int MaxOverEquipPasses = 8;

        public override void Rebase()
        {
            _inFullRebase = true;
            try
            {
                RebaseStats();
                RebaseWeapons();
            }
            finally
            {
                _inFullRebase = false;
            }

            AnnounceAppearanceIfChanged();
        }

        public override void RebaseStats()
        {
            int previousShape = Stats.GetOrZero(CharacterStat.MonsterData);
            // Bonuses first: skill trickle, max health and max nano read the full (base + bonus)
            // ability values, so equipment and buffs have to be in place before those are
            // recomputed. Worn appearance follows the bonus pass because its spells carry stat
            // requirements.
            // The client judges each worn item's OE level on the current skills, which already carry the other OE
            // items' penalties, and re-judges whenever they change (Gamecode.dll FUN_1004d003 / FUN_10081e10). Starting
            // from every item at full strength, the bonus layers are rebuilt until no item's level changes. Penalties
            // shrink bonuses, so levels normally only rise; the pass cap ends the rare oscillation a penalised negative
            // modifier could cause.
            if (Inventory.IsHydrated)
                Inventory.ResetOverEquip();
            ApplyBonusLayers();
            for (int pass = 0; pass < MaxOverEquipPasses && Inventory.IsHydrated && Inventory.ApplyOverEquip(Stats); pass++)
                ApplyBonusLayers();
            ActionRestrictionFlags = CombatRules.CollectActionRestrictions(Buffs, Stats);
            RebaseWearAppearance();
            RebaseMaxHealth();
            RebaseMaxNano();

            int currentShape = Stats.GetOrZero(CharacterStat.MonsterData);
            if (currentShape != previousShape && Session?.State == SessionState.InPlay)
                Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this,
                    new StatMessage
                    {
                        Identity = Identity,
                        Stats = [new GameTuple<CharacterStat, uint>
                        {
                            Value1 = CharacterStat.MonsterData,
                            Value2 = unchecked((uint)currentShape),
                        }],
                    }, includeSelf: true);

            if (!_inFullRebase)
                AnnounceAppearanceIfChanged();

            SyncPetRunSpeeds();
            if (OwnedPets.Count > 0)
                Playfield?.GetService<Pets.PetService>()?.RefreshOverEquip(this);

            SyncXpKillRange();
            SyncLastPerkResetTime();

            // A buff or debuff that moves Martial Arts across a weapon's combined-attack requirement changes the stance;
            // only then are the weapons re-armed, so ordinary buffs keep their swing timers.
            if (!_inFullRebase && Inventory.IsHydrated && QualifiesForCombinedMartialArts() != _combinedMartialArts)
                RebaseWeapons();
        }

        /// <summary>
        /// XPKillRange (275) follows the level's Xp.json row. The client needs it to color targets; without it every
        /// target shows gray.
        /// </summary>
        void SyncXpKillRange()
        {
            IGameData? gameData = Playfield?.GetService<IGameData>();
            if (gameData == null
                || !gameData.TryGetXpLevel(Stats.GetOrOne(CharacterStat.Level), out XpLevelEntry entry)
                || entry.XpKillRange <= 0
                || Stats.Get(CharacterStat.XPKillRange, StatDetail.Base) == entry.XpKillRange)
                return;

            Stats.Set(CharacterStat.XPKillRange, entry.XpKillRange, StatDetail.Base, dirty: true);
        }

        /// <summary>
        /// Combined martial arts needs every wielded weapon to allow it (its MartialArts stat, "MA for combined attack")
        /// and the character's Martial Arts skill to meet each of those requirements.
        /// </summary>
        bool QualifiesForCombinedMartialArts()
        {
            Item? right = Inventory.Equipment.Content.GetValueOrDefault((int)WeaponSlots.Righthand);
            Item? left = Inventory.Equipment.Content.GetValueOrDefault((int)WeaponSlots.LeftHand);
            bool armedMain = right?.IsWieldableCombatWeapon() == true;
            bool armedOff = left?.IsWieldableCombatWeapon() == true;
            return (armedMain || armedOff)
                && (!armedMain || MeetsCombinedMartialArts(right!))
                && (!armedOff || MeetsCombinedMartialArts(left!));
        }

        bool MeetsCombinedMartialArts(Item weapon)
            => weapon.IsMaCombinedWeapon()
               && Stats.Get(CharacterStat.MartialArts) >= weapon.GetStat(CharacterStat.MartialArts);

        /// <summary>
        /// Armor carries the worn look, Social replaces it per slot once the client asks for social
        /// clothes, and social-only drops the armor layer entirely.
        /// </summary>
        /// <summary>Utility slot items, whose worn BackMesh shows on the back (the Light Bar, 252157).</summary>
        protected override IEnumerable<Item> AppearanceBackMeshItems
        {
            get
            {
                if (!Inventory.IsHydrated)
                    yield break;

                foreach (WeaponSlots slot in (WeaponSlots[])[WeaponSlots.Util1, WeaponSlots.Util2, WeaponSlots.Util3])
                {
                    if (Inventory.Equipment.Content.TryGetValue((int)slot, out Item? item))
                        yield return item;
                }
            }
        }

        /// <summary>A player's armor and social pieces stack by the client's rules (glasses on glasses, pads per shoulder).</summary>
        protected override bool StacksWornMeshes => true;

        /// <summary>A gun spends a round per swing, hit or miss (<see cref="WeaponAmmo"/>).</summary>
        protected override int? ConsumeRound(CharacterWeapon weapon) => WeaponAmmo.Spend(weapon, 1);

        /// <summary>An empty or reloading clip does not fire; an empty one with ammo to hand starts a reload.</summary>
        protected override bool HasRound(CharacterWeapon weapon) => WeaponAmmo.CanFire(this, weapon);

        /// <summary>A clip the shot emptied reloads after its AttackInfo, as retail orders them.</summary>
        protected override void OnSwingShown(CharacterWeapon weapon) => WeaponAmmo.ReloadIfEmptied(this, weapon);

        protected override IEnumerable<Container> AppearanceWearPages
        {
            get
            {
                if (!Inventory.IsHydrated)
                    yield break;

                if (!SocialOnlyAppearance)
                    yield return Inventory.Armor;

                if (ShowSocialAppearance)
                    yield return Inventory.Social;
            }
        }

        /// <summary>
        /// Client pad/social toggle from CharacterAction ChangeVisualFlag. The social bits choose
        /// the wear pages, so the look is rebuilt before the flags go back out on the wire.
        /// </summary>
        public bool TryApplyVisualFlags(int visualFlags)
        {
            if (visualFlags < 0 || visualFlags > short.MaxValue)
                return false;

            if (Stats.Get(CharacterStat.VisualFlags) == visualFlags)
                return true;

            Stats.Set(CharacterStat.VisualFlags, visualFlags, StatDetail.Base, dirty: true);
            RebaseWearAppearance();
            // Live answers a toggle with the VisualFlags stat, then AppearanceUpdate (capture 2026-10-06T19:22:51Z).
            FlushDirtyStats();
            AnnounceAppearance();
            return true;
        }

        void AnnounceAppearanceIfChanged()
        {
            if (_appearanceDeferred)
                return;
            if (ConsumeAppearanceDirty())
                SendAppearanceUpdate();
        }

        bool _appearanceDeferred;

        /// <summary>
        /// Full rebase that holds back a changed look; <see cref="AnnounceDeferredAppearance"/> sends it. An equipment
        /// move uses it so AppearanceUpdate follows the move's ContainerAddItem, as live sends them.
        /// </summary>
        public void RebaseDeferringAppearance()
        {
            _appearanceDeferred = true;
            try
            {
                Rebase();
            }
            finally
            {
                _appearanceDeferred = false;
            }
        }

        /// <summary>Sends the look held back by <see cref="RebaseDeferringAppearance"/>, if it changed.</summary>
        public void AnnounceDeferredAppearance() => AnnounceAppearanceIfChanged();

        void AnnounceAppearance()
        {
            ConsumeAppearanceDirty();
            SendAppearanceUpdate();
        }

        void SendAppearanceUpdate()
        {
            if (Session?.State != SessionState.InPlay)
                return;

            AppearanceUpdateMessage appearance = BuildAppearanceUpdateMessage();
            Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, appearance, includeSelf: true);
        }

        void RebaseMaxHealth()
        {
            if (!MaxHealthCalculator.TryCompute(Stats, out int maxHealth))
                return;

            // Regen, heals and revive cap at the full max; scaling against the base alone would
            // drop a full character below it on every rebase and restart regen.
            RebaseVital(CharacterStat.Health, CharacterStat.MaxHealth, CharacterStat.PercentRemainingHealth, maxHealth);
        }

        void RebaseMaxNano()
        {
            if (!MaxNanoCalculator.TryCompute(Stats, out int maxNano))
                return;

            RebaseVital(CharacterStat.CurrentNano, CharacterStat.MaxNanoEnergy, CharacterStat.PercentRemainingNano, maxNano);
        }

        void RebaseEquipBonuses()
        {
            if (!Inventory.IsHydrated)
            {
                Stats.ClearBonuses(dirty: true);
                return;
            }

            Inventory.ApplyWearBonuses(Stats);
        }

        /// <summary>Every bonus layer from scratch: equipment (clears the layer first), perks, buffs, level IP, trickle.</summary>
        void ApplyBonusLayers()
        {
            RebaseEquipBonuses();
            RebasePerks();
            ApplyBuffBonuses();
            ApplyTimedEffectBonuses();
            ApplyLevelIpBonus();
            SkillCatalog.ApplyTrickle(Stats);
        }

        /// <summary>Prospective level-up bonus layers, without changing worn items or the live actor.</summary>
        internal StatCollection ProjectLevelUpStats(IReadOnlyDictionary<CharacterStat, int> values)
        {
            var projected = new StatCollection();
            foreach (var entry in Stats.GetEntries())
                projected.Set(entry.Stat, entry.Base);
            foreach (var pair in values)
                projected.Set(pair.Key, pair.Value);

            Container[] pages = Inventory.IsHydrated
                ? [Inventory.Equipment, Inventory.Armor, Inventory.Implant] : [];
            var overEquip = new Dictionary<Item, int>();
            ApplyLayers();
            bool anyOverEquip = false;
            foreach (Container page in pages)
                foreach (var slot in page.EnumerateSlots())
                {
                    int level = OverEquip.ComputeLevel(slot.Value, page.Identity.Type, slot.Key, projected);
                    overEquip[slot.Value] = level;
                    anyOverEquip |= level > 0;
                }
            if (anyOverEquip)
                ApplyLayers();

            if (MaxHealthCalculator.TryCompute(projected, out int health))
                projected.Set(CharacterStat.MaxHealth, health);
            if (MaxNanoCalculator.TryCompute(projected, out int nano))
                projected.Set(CharacterStat.MaxNanoEnergy, nano);
            return projected;

            void ApplyLayers()
            {
                projected.ClearBonuses();
                foreach (Container page in pages)
                    foreach (var slot in page.EnumerateSlots())
                        if (slot.Value.Definition != null)
                            WearBonusApplier.ApplyItem(slot.Value, page == Inventory.Equipment, projected,
                                overEquip.TryGetValue(slot.Value, out int level) ? level : 0);
                RebasePerks(projected);
                ApplyBuffStatBonuses(projected);
                ApplyTimedEffectBonuses(projected);
                projected.AddBonus(CharacterStat.IP, TotalIpEarnedAtLevel(projected.GetOrOne(CharacterStat.Level)));
                SkillCatalog.ApplyTrickle(projected);
            }
        }

        /// <summary>True while a combined-MA weapon is in either hand: the MA fist swings beside the weapons.</summary>
        bool _combinedMartialArts;

        /// <summary>
        /// CharacterAction ChangeAnimationAndStance (0xA7). The client's CharacterAction handler (Gamecode.dll
        /// 0x1005d3ff, case 0xA7) sets character flag 0x800 when the parameter is non-zero and clears it on 0. With
        /// weapons in hand, the client only mounts SAW's MAAT fist (key 100) at slot 0 when that flag is set
        /// (0x1006af29), and AttackInfo slot 0 / Unknown6 100 animates only a mounted weapon (0x1006a55f). Both
        /// parameters carry the value; the case reads the second.
        /// </summary>
        CharacterActionMessage BuildCombinedMartialArtsStance()
        {
            int on = _combinedMartialArts ? 1 : 0;
            return new CharacterActionMessage
            {
                Identity = Identity,
                Action = CharacterActionType.ChangeAnimationAndStance,
                Target = Identity.None,
                Parameter1 = on,
                Parameter2 = on
            };
        }

        /// <summary>Stance state a client needs right after this player's spawn and weapon instances.</summary>
        public IEnumerable<MessageBody> BuildCombatStanceMessages()
        {
            if (_combinedMartialArts)
                yield return BuildCombinedMartialArtsStance();
        }

        public override IEnumerable<MessageBody> BuildSpawnCompanionMessages()
        {
            foreach (MessageBody message in base.BuildSpawnCompanionMessages())
                yield return message;
            foreach (MessageBody message in BuildCombatStanceMessages())
                yield return message;
        }

        public override List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
        {
            var messages = new List<WeaponItemFullUpdateMessage>();
            if (!Inventory.IsHydrated)
                return messages;

            TryAddEquippedHandWifu(messages, (int)WeaponSlots.Righthand);
            TryAddEquippedHandWifu(messages, (int)WeaponSlots.LeftHand);
            return messages;
        }

        /// <summary>
        /// Called after a Weapons / Armor / Implant / Social slot changes.
        /// A visible hand weapon announces its WeaponInstance.
        /// </summary>
        public void OnEquipmentChanged(EquipSlot slot)
        {
            if (slot.Page != IdentityType.WeaponPage)
                return;

            if (slot.Placement is not ((int)WeaponSlots.Righthand or (int)WeaponSlots.LeftHand))
                return;

            WeaponItemFullUpdateMessage? wifu = TryBuildEquippedHandWifu(slot.Placement);
            if (wifu == null)
                return;

            Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, wifu, includeSelf: false);

            // This client already holds the weapon under its own identity (its inventory registered it), and a second
            // object under that identity mid-session crashed the client (the social-hand case), so it gets the weapon
            // under a fresh instance id as before.
            if (Session != null && Playfield != null
                && Inventory.Equipment.Content.GetValueOrDefault(slot.Placement) is Item item)
            {
                var fresh = new Identity { Type = IdentityType.WeaponInstance, Instance = Playfield.AllocateWeaponInstanceId() };
                WeaponItemFullUpdateMessage? own = TryBuildWeaponItemFullUpdate(item, slot.Placement, carriedIdentity: fresh);
                if (own != null)
                    Session.Send(own);
            }
        }

        WeaponItemFullUpdateMessage? TryBuildEquippedHandWifu(int equipmentSlot)
        {
            if (!Inventory.IsHydrated)
                return null;

            Item? item = Inventory.Equipment.Content.GetValueOrDefault(equipmentSlot);
            if (item == null)
                return null;

            return TryBuildWeaponItemFullUpdate(item, equipmentSlot);
        }

        void TryAddEquippedHandWifu(List<WeaponItemFullUpdateMessage> messages, int equipmentSlot)
        {
            WeaponItemFullUpdateMessage? message = TryBuildEquippedHandWifu(equipmentSlot);
            if (message != null)
                messages.Add(message);
        }

        public override void RebaseWeapons()
        {
            ClearWeapons();

            if (!Inventory.IsHydrated)
            {
                ArmMartialArtsFist(_items, WeaponSlot.MainHand);
                ResetAllWeaponAttacks();
                RebaseEquippedWeaponStats(martialArts: true);
                return;
            }

            Item? right = Inventory.Equipment.Content.GetValueOrDefault((int)WeaponSlots.Righthand);
            Item? left = Inventory.Equipment.Content.GetValueOrDefault((int)WeaponSlots.LeftHand);

            bool armedMain = false;
            bool armedOff = false;
            if (right?.IsWieldableCombatWeapon() == true)
            {
                ArmFromItem(WeaponSlot.MainHand, right);
                armedMain = true;
            }

            if (left?.IsWieldableCombatWeapon() == true)
            {
                ArmFromItem(WeaponSlot.OffHand, left);
                armedOff = true;
            }

            // Combined MA only when every wielded weapon allows it and the MA skill meets each one's requirement: one
            // MA-combined weapon next to a normal one does not.
            bool maCombined = QualifiesForCombinedMartialArts();
            FinishWeaponRebase(_items, armedMain, armedOff, maCombined);
            RebaseEquippedWeaponStats(martialArts: maCombined || (!armedMain && !armedOff));
            if (maCombined != _combinedMartialArts)
            {
                _combinedMartialArts = maCombined;
                if (Session?.State == SessionState.InPlay)
                    Cell?.Announce(BuildCombinedMartialArtsStance());
            }

            SyncHandWeaponMeshes();

            if (!_inFullRebase)
                AnnounceAppearanceIfChanged();
        }

        /// <summary>
        /// The client's own derivation (Gamecode.dll 0x1006a3f7) on every weapon page change: EquippedWeapons =
        /// weapon page slot 0 held | right hand and left hand weapon type flags; EquippedRHWeapon = slot 0 held |
        /// right hand flags. Item criteria such as Pulverize's [EquippedRHWeapon BitAnd 256] read them. Runtime
        /// only, never persisted and not sent: the client keeps its own copy.
        /// The 0x1 bit is martial arts: set with no weapon in either hand or with combined martial arts weapons, for
        /// criteria such as [EquippedWeapons BitAnd 1].
        /// </summary>
        void RebaseEquippedWeaponStats(bool martialArts)
        {
            int equipped = martialArts ? 1 : 0;
            int rightHand = equipped;
            if (Inventory.IsHydrated)
            {
                IReadOnlyDictionary<int, Item> weapons = Inventory.Equipment.Content;
                int slotZero = equipped | (weapons.ContainsKey(0) ? 1 : 0);
                int right = weapons.GetValueOrDefault((int)WeaponSlots.Righthand)?.Definition.GetWeaponTypeFlags() ?? 0;
                int left = weapons.GetValueOrDefault((int)WeaponSlots.LeftHand)?.Definition.GetWeaponTypeFlags() ?? 0;
                equipped = slotZero | right | left;
                rightHand = slotZero | right;
            }

            Stats.Set(CharacterStat.EquippedWeapons, equipped, StatDetail.Base);
            Stats.Set(CharacterStat.EquippedRHWeapon, rightHand, StatDetail.Base);
        }

        bool SyncHandWeaponMeshes()
        {
            bool changed = SyncOneHandWeaponMesh(
                (int)WeaponSlots.Righthand,
                meshPosition: 1,
                CharacterStat.WeaponMeshRight,
                CharacterStat.OverrideTextureWeaponRight);
            changed |= SyncOneHandWeaponMesh(
                (int)WeaponSlots.LeftHand,
                meshPosition: 2,
                CharacterStat.WeaponMeshLeft,
                CharacterStat.OverrideTextureWeaponLeft);
            return changed;
        }

        bool SyncOneHandWeaponMesh(
            int equipmentSlot,
            byte meshPosition,
            CharacterStat meshStat,
            CharacterStat overrideTextureStat)
        {
            Item? item = Inventory.Equipment.Content.GetValueOrDefault(equipmentSlot);
            int meshId = 0;
            int overrideTexture = 0;

            if (item != null)
            {
                meshId = NormalizeVisualValue(item.GetStat(meshStat));
                if (meshId <= 0)
                    meshId = NormalizeVisualValue(item.GetStat(CharacterStat.WeaponMesh));
                overrideTexture = NormalizeVisualValue(item.GetStat(overrideTextureStat));
            }

            if (meshId <= 0)
            {
                if (!ClearHandMesh(meshPosition))
                    return false;

                Stats.Set(meshStat, 0, StatDetail.Base, dirty: true);
                return true;
            }

            if (!SetHandMesh(meshPosition, meshId, overrideTexture))
                return false;

            Stats.Set(meshStat, meshId, StatDetail.Base, dirty: true);
            return true;
        }

        /// <summary>
        /// WeaponItemFullUpdate for a weapon going into a social hand, sent at equip time under a fresh weapon-instance id.
        /// The client mounts a social weapon only on a weapon object it holds; the item's own identity cannot be reused
        /// mid-session (the inventory already registered it, and the duplicate crashed the client), so the Equip action
        /// names this instance instead. Null when the weapon is not a wieldable combat weapon.
        /// </summary>
        public WeaponItemFullUpdateMessage? BuildSocialWeaponItemFullUpdate(Item item, int socialSlot)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (Playfield == null)
                return null;
            var instance = new Identity { Type = IdentityType.WeaponInstance, Instance = Playfield.AllocateWeaponInstanceId() };
            return TryBuildWeaponItemFullUpdate(item, socialSlot, carriedIdentity: instance);
        }

        static int NormalizeVisualValue(int value)
        {
            value = StatCollection.Normalize(value);
            return value <= 0 ? 0 : value;
        }

        public void EnterLinkDead(TimeSpan timeout)
        {
            ConnectionPhase = PlayerConnectionPhase.LinkDead;
            LinkDeadUntilUtc = DateTime.UtcNow + timeout;
            Session = null;
            _linkDeadStopPending = true;
        }

        /// <summary>
        /// Set when the transport drops (on its thread); the next tick, on the playfield thread, stops the character
        /// in place. The released keys of a crashed client never arrive, so its last movement input would otherwise
        /// carry on (and come back as a run animation on reconnect).
        /// </summary>
        volatile bool _linkDeadStopPending;

        public bool HasLinkDeadExpired(DateTime now)
            => ConnectionPhase == PlayerConnectionPhase.LinkDead
                && LinkDeadUntilUtc is DateTime deadline
                && now >= deadline;

        public void EnterOnline(IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            lock (session)
            {
                // Bind first: a transport closed during hydration must not alter the player.
                session.BindPlayer(this);
                ConnectionPhase = PlayerConnectionPhase.Online;
                LinkDeadUntilUtc = null;
                Session = session;
            }
        }

        internal static readonly CharacterStat[] FullCharacterStats1 =
        [
            CharacterStat.State,
            CharacterStat.UnarmedTemplateInstance,
            CharacterStat.InvadersKilled,
            CharacterStat.KilledByInvaders,
            CharacterStat.AccountFlags,
            CharacterStat.VP,
            CharacterStat.UnsavedXP,
            CharacterStat.NanoFocusLevel,
            CharacterStat.Specialization,
            CharacterStat.ShadowBreedTemplate,
            CharacterStat.ShadowBreed,
            CharacterStat.LastPerkResetTime,
            CharacterStat.SocialStatus,
            CharacterStat.PlayerOptions,
            // CharacterStat.TempSaveTeamID,
            // CharacterStat.SavePointY,
            // CharacterStat.SavePointX,
            // CharacterStat.SavePointZ,
            CharacterStat.VisualFlags,
            CharacterStat.PVPDuelKills,
            CharacterStat.PVPDuelDeaths,
            CharacterStat.PVPProfessionDuelKills,
            CharacterStat.PVPProfessionDuelDeaths,
            CharacterStat.PVPRankedSoloKills,
            CharacterStat.PVPRankedSoloDeaths,
            CharacterStat.PVPRankedTeamKills,
            CharacterStat.PVPRankedTeamDeaths,
            CharacterStat.PVPSoloScore,
            CharacterStat.PVPTeamScore,
            CharacterStat.PVPDuelScore,
            CharacterStat.Commendations,
            CharacterStat.DailyMissionResets,
            CharacterStat.UnreadMailCount,
            CharacterStat.LastMailCheckTime,
            CharacterStat.SavedXP,
            CharacterStat.Flags,
            //CharacterStat.Features,
            CharacterStat.ApartmentsAllowed,
            CharacterStat.ApartmentsOwned,
            CharacterStat.Scale,
            CharacterStat.VisualProfession,
            CharacterStat.NanoAC,
            CharacterStat.CurrentNano,
            CharacterStat.MaxNanoEnergy,
            CharacterStat.LastConcretePlayfieldInstance,
            CharacterStat.MapOptions,
            CharacterStat.MapsA,
            CharacterStat.MapsB,
            CharacterStat.MapsC,
            CharacterStat.MapsD,
            CharacterStat.MissionBits1,
            CharacterStat.MissionBits2,
            CharacterStat.MissionBits3,
            CharacterStat.MissionBits4,
            CharacterStat.MissionBits5,
            CharacterStat.MissionBits6,
            CharacterStat.MissionBits7,
            CharacterStat.MissionBits8,
            CharacterStat.MissionBits9,
            CharacterStat.MissionBits10,
            CharacterStat.MissionBits11,
            CharacterStat.MissionBits12,
            CharacterStat.MissionBits13,
            CharacterStat.MissionBits14,
            CharacterStat.MissionBits15,
            CharacterStat.MissionBits16,
            CharacterStat.MissionBits17,
            CharacterStat.MissionBits18,
            CharacterStat.AutoAttackFlags,
            CharacterStat.PersonalResearchLevel,
            CharacterStat.GlobalResearchLevel,
            CharacterStat.PersonalResearchGoal,
            CharacterStat.GlobalResearchGoal,
            CharacterStat.BattlestationSide,
            CharacterStat.BattlestationRep,
            CharacterStat.Members,
        ];

        internal static readonly CharacterStat[] FullCharacterStats2 =
        [
            // CharacterStat.VeteranPoints,
            // CharacterStat.MonthsPaid,
            CharacterStat.PaidPoints,
            CharacterStat.XPKillRange,
            CharacterStat.InPlay,
            CharacterStat.Health,
            CharacterStat.MaxHealth,
            CharacterStat.Psychic,
            CharacterStat.Sense,
            CharacterStat.Intelligence,
            CharacterStat.Stamina,
            CharacterStat.Agility,
            CharacterStat.Strength,
            CharacterStat.Attitude,
            CharacterStat.AlignmentClanTokens,
            CharacterStat.Cash,
            CharacterStat.Profession,
            CharacterStat.AggDef,
            CharacterStat.Icon,
            CharacterStat.Mesh,
            CharacterStat.RunSpeed,
            CharacterStat.DeadTimer,
            CharacterStat.Team,
            CharacterStat.Breed,
            CharacterStat.Sex,
            CharacterStat.LastSaveXP,
            CharacterStat.NextXP,
            CharacterStat.LastXP,
            CharacterStat.Level,
            CharacterStat.XP,
            CharacterStat.IP,
            CharacterStat.Mass,
            CharacterStat.CurrentMass,
            CharacterStat.ItemType,
            CharacterStat.PreviousHealth,
            CharacterStat.CurrentState,
            CharacterStat.Age,
            CharacterStat.Side,
            CharacterStat.WaitState,
            CharacterStat.VehicleWater,
            CharacterStat.MultiMelee,
            CharacterStat.MultiRanged,
            CharacterStat.RangedEnergy,
            CharacterStat.RadiationAC,
            CharacterStat.SensoryImprovement,
            CharacterStat.BowSpecialAttack,
            CharacterStat.Burst,
            CharacterStat.FullAuto,
            CharacterStat.MapNavigation,
            CharacterStat.VehicleAir,
            CharacterStat.VehicleGround,
            CharacterStat.BreakingEntry,
            CharacterStat.Concealment,
            CharacterStat.Chemistry,
            CharacterStat.Psychology,
            CharacterStat.ComputerLiteracy,
            CharacterStat.NanoProgramming,
            CharacterStat.Pharmaceuticals,
            CharacterStat.WeaponSmithing,
            CharacterStat.QuantumFT,
            CharacterStat.AttackSpeed,
            CharacterStat.EvadeClsC,
            CharacterStat.DodgeRanged,
            CharacterStat.DuckExp,
            CharacterStat.BodyDevelopment,
            CharacterStat.AimedShot,
            CharacterStat.FlingShot,
            CharacterStat.NanoCInit,
            CharacterStat.FastAttack,
            CharacterStat.SneakAttack,
            CharacterStat.Parry,
            CharacterStat.Dimach,
            CharacterStat.Riposte,
            CharacterStat.Brawl,
            CharacterStat.Tutoring,
            CharacterStat.Swimming,
            CharacterStat.Adventuring,
            CharacterStat.Perception,
            CharacterStat.TrapDisarm,
            CharacterStat.NanoPool,
            CharacterStat.SpaceTime,
            CharacterStat.MaterialCreation,
            CharacterStat.PsychologicalModification,
            CharacterStat.BiologicalMetamorphosis,
            CharacterStat.MaterialMetamorphosis,
            CharacterStat.ElectricalEngineering,
            CharacterStat.MechanicalEngineering,
            CharacterStat.Treatment,
            CharacterStat.FirstAid,
            CharacterStat.PhysicalInit,
            CharacterStat.RangedInit,
            CharacterStat.MeleeInit,
            CharacterStat.AssaultRifle,
            CharacterStat.Shotgun,
            CharacterStat.MGSMG,
            CharacterStat.Rifle,
            CharacterStat.Pistol,
            CharacterStat.Bow,
            CharacterStat.HeavyWeapons,
            CharacterStat.Grenade,
            CharacterStat.SharpObject,
            CharacterStat._2hBlunt,
            CharacterStat.Piercing,
            CharacterStat.Skill2hEdged,
            CharacterStat.MeleeEnergy,
            CharacterStat._1hEdged,
            CharacterStat._1hBlunt,
            CharacterStat.MartialArts,
            CharacterStat.MetaType,
            CharacterStat.TitleLevel,
            CharacterStat.GmLevel,
            CharacterStat.FireAC,
            CharacterStat.PoisonAC,
            CharacterStat.ColdAC,
            CharacterStat.ChemicalAC,
            CharacterStat.EnergyAC,
            CharacterStat.MeleeAC,
            CharacterStat.ProjectileAC,
            CharacterStat.RP,
            // CharacterStat.SpecialCondition,
            CharacterStat.SK,
            CharacterStat.Expansion,
            CharacterStat.ClanRedeemed,
            CharacterStat.ClanConserver,
            CharacterStat.ClanDevoted,
            CharacterStat.OTUnredeemed,
            CharacterStat.OTOperator,
            CharacterStat.OTFollowers,
            CharacterStat.GOS,
            CharacterStat.ClanVanguards,
            CharacterStat.OTTrans,
            CharacterStat.ClanGaia,
            CharacterStat.OTMed,
            CharacterStat.ClanSentinels,
            CharacterStat.OTArmedForces,
            CharacterStat.SocialStatus,
            CharacterStat.PlayerID,
            CharacterStat.KilledByInvaders,
            CharacterStat.InvadersKilled,
            CharacterStat.AlienLevel,
            CharacterStat.AlienNextXP,
            CharacterStat.AlienXP,
        ];

        internal static readonly CharacterStat[] FullCharacterStats3 =
        [
            CharacterStat.InsurancePercentage,
            CharacterStat.ProfessionLevel,
            CharacterStat.PrevMovementMode,
            CharacterStat.CurrentMovementMode,
            CharacterStat.Fatness,
            CharacterStat.Race,
            CharacterStat.TeamSide,
            CharacterStat.BeltSlots,
        ];

        internal static readonly CharacterStat[] FullCharacterStats4 =
        [
            CharacterStat.AbsorbProjectileAC,
            CharacterStat.AbsorbMeleeAC,
            CharacterStat.AbsorbEnergyAC,
            CharacterStat.AbsorbChemicalAC,
            CharacterStat.AbsorbRadiationAC,
            CharacterStat.AbsorbColdAC,
            CharacterStat.AbsorbNanoAC,
            CharacterStat.AbsorbFireAC,
            CharacterStat.AbsorbPoisonAC,
            CharacterStat.TemporarySkillReduction,
            CharacterStat.InsuranceTime,
            CharacterStat.MaxNCU,
            // CurrentNano goes only in the 32-bit Stats1 block: a 16-bit copy here cannot carry a nano pool above
            // 32767 (gear such as Blackmane's Stat Buffer adds 2,000,000) and aborted the spawn.
            CharacterStat.MapFlags,
            CharacterStat.ChangeSideCount,
        ];

        /// <summary>FullCharacter spawn sheet groups (Sets 1–4), single source of truth.</summary>
        internal static IReadOnlyList<CharacterStat[]> FullCharacterStatSets { get; } =
        [
            FullCharacterStats1,
            FullCharacterStats2,
            FullCharacterStats3,
            FullCharacterStats4,
        ];

        /// <summary>Chat labels for <see cref="FullCharacterStatSets"/>, same order.</summary>
        internal static IReadOnlyList<string> FullCharacterStatSetNames { get; } =
        [
            "Account / Flags",
            "Skills / Core",
            "Appearance",
            "Absorb / NCU",
        ];

        public override InfoPacketMessage BuildInfoPacket()
        {
            // Player inspect is Character (0x40). N3 Unknown=0 — Unknown=1 is the
            // monster path and stuck the official client on "Please wait".
            // Do not copy Name into FirstName — official 0x40 uses the DB first/last
            // strings (often empty). Unique name already arrives in SCFU.Name.
            InfoPacketMessage message = BuildCharacterInfoPacket(
                0,
                InfoPacketType.Character,
                FirstName,
                LastName);
            var info = (CharacterInfoPacket)message.Info;
            Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "InfoPacket player={0} name={1} first='{2}' last='{3}' level={4} prof={5} hp={6}/{7}",
                    Identity.Instance,
                    Name ?? string.Empty,
                    info.FirstName,
                    info.LastName,
                    info.Level,
                    (int)info.Profession,
                    info.Health,
                    info.MaxHealth));
            return message;
        }

        /// <summary>
        /// Builds a FullCharacter login packet from current player state.
        /// Structure follows ZoneEngine FullCharacterMessageHandler.Filler without capture/runtime special cases.
        /// </summary>
        public FullCharacterMessage BuildFullCharacterMessage()
        {
            StatCollection stats = Stats;

            var message = new FullCharacterMessage
            {
                Identity = Identity,
                MsgVersion = 26,
                InventorySlots = [.. Inventory.BuildInventorySlots()],
                UploadedNanoIds = [.. UploadedNanoIds],
                Unknown2 = [],
                Unknown3 = 1,
                Unknown4 = BuildSkillLockEntries(),
                UnknownI2 = 1,
                Unknown5 = [],
                UnknownI3 = 1,
                Unknown6 = BuildNanoLineLockEntries()
            };

            message.Stats1 = BuildFullCharacterIntStats(stats, FullCharacterStats1);
            message.Stats2 = BuildFullCharacterIntStats(stats, FullCharacterStats2);
            message.Stats3 = BuildFullCharacterByteStats(stats, FullCharacterStats3);
            message.Stats4 = BuildFullCharacterShortStats(stats, FullCharacterStats4);

            message.Unknown9 = 0;
            // The client rebuilds its team and raid windows from this on every playfield it enters.
            int teamFlags = 0;
            message.TeamData = Playfield?.GetService<Teams.TeamService>()?.BuildFullCharacterTeam(this, out teamFlags) ?? [];
            message.Unknown10 = teamFlags;
            message.Unknown11 = [];
            message.Unknown12 = [];
            message.Unknown13 = BuildPerkMap();

            LogFullCharacterInventory(message);
            return message;
        }

        /// <summary>
        /// Stats a new character does not store: never having been set means 0, and the client is sent 0 rather
        /// than nothing.
        /// </summary>
        static readonly HashSet<CharacterStat> ZeroWhenUnsetStats =
        [
            CharacterStat.XP,
            CharacterStat.LastSaveXP,
            CharacterStat.UnsavedXP,
            CharacterStat.SavedXP,
            CharacterStat.Specialization,
            CharacterStat.VP,
            CharacterStat.Commendations,
            CharacterStat.DailyMissionResets,
            CharacterStat.PVPDuelKills,
            CharacterStat.PVPDuelDeaths,
            CharacterStat.PVPProfessionDuelKills,
            CharacterStat.PVPProfessionDuelDeaths,
            CharacterStat.PVPRankedSoloKills,
            CharacterStat.PVPRankedSoloDeaths,
            CharacterStat.PVPRankedTeamKills,
            CharacterStat.PVPRankedTeamDeaths,
            CharacterStat.PVPSoloScore,
            CharacterStat.PVPTeamScore,
            CharacterStat.PVPDuelScore,
            // The client shows no perk reset timer for 0; left out, its default reads as a reset far in the future.
            CharacterStat.LastPerkResetTime,
        ];

        static GameTuple<int, uint>[] BuildFullCharacterIntStats(StatCollection stats, CharacterStat[] ids)
        {
            // These are counted (stat id, value) arrays. Absent optional stats are omitted;
            // an explicit stored zero remains distinct from an absent value. Stats in ZeroWhenUnsetStats are
            // always sent, as 0 when unset.
            ids = ids.Where(id => stats.TryGetValue(id, out _) || ZeroWhenUnsetStats.Contains(id)
                                  || stats.WireValueOverride?.Invoke(id) != null).ToArray();
            var tuples = new GameTuple<int, uint>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<int, uint>
                {
                    Value1 = (int)id,
                    Value2 = stats.WireValueOverride?.Invoke(id) is int overridden
                        ? (uint)overridden
                        : ZeroWhenUnsetStats.Contains(id)
                            ? (uint)stats.GetOrZero(id)
                            : (uint)RequireWireStat(stats, id)
                };
            }

            return tuples;
        }

        static GameTuple<byte, byte>[] BuildFullCharacterByteStats(StatCollection stats, CharacterStat[] ids)
        {
            ids = ids.Where(id => stats.TryGetValue(id, out _)).ToArray();
            var tuples = new GameTuple<byte, byte>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<byte, byte>
                {
                    Value1 = (byte)id,
                    Value2 = checked((byte)RequireWireStat(stats, id))
                };
            }

            return tuples;
        }

        static GameTuple<byte, short>[] BuildFullCharacterShortStats(StatCollection stats, CharacterStat[] ids)
        {
            ids = ids.Where(id => stats.TryGetValue(id, out _)).ToArray();
            var tuples = new GameTuple<byte, short>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<byte, short>
                {
                    Value1 = (byte)id,
                    Value2 = checked((short)RequireWireStat(stats, id))
                };
            }

            return tuples;
        }

        static int RequireWireStat(StatCollection stats, CharacterStat id)
        {
            // FullCharacter carries base only; the client derives full from gear/buffs.
            int value = stats.Get(id, StatDetail.Base);
            if (StatCollection.IsUnset(value))
                throw new InvalidOperationException("Unset FullCharacter stat: " + id);
            return value;
        }

        void LogFullCharacterInventory(FullCharacterMessage message)
        {
            InventorySlot[] slots = message.InventorySlots ?? [];
            Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "FullCharacter inventory character={0} count={1}",
                    Identity.Instance,
                    slots.Length));

            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlot slot = slots[i];
                Logger.Info(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "FullCharacter item[{0}] placement={1} identity={2}:{3} low={4} high={5} ql={6} count={7} flags={8}",
                        i,
                        slot.Placement,
                        slot.Identity.Type,
                        slot.Identity.Instance,
                        slot.ItemLowId,
                        slot.ItemHighId,
                        slot.Quality,
                        slot.Count,
                        slot.Flags));
            }
        }

    }
}
