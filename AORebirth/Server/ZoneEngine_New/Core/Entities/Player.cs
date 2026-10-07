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
                _ => base.ResolveRequirement(requirement)
            };
        }

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
            Session?.SendIntrazoneTeleport(landing, Rotation, playfield.Identity.Instance);
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
        public void ShowSkillLock(int statId, int seconds)
        {
            if (seconds <= 0 || Session == null)
                return;

            Session.Send(new CharacterActionMessage
            {
                Identity = Identity,
                Action = CharacterActionType.SpecialUsed,
                Target = Identity.None,
                Parameter1 = statId,
                Parameter2 = seconds
            });
            ScheduleSpecialAvailable(statId, DateTime.UtcNow.AddSeconds(seconds));
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
            if (IsInCombat(character))
                return true;

            foreach (NpcCharacter pet in character.OwnedPets.All)
            {
                if (IsInCombat(pet))
                    return true;
            }

            return false;
        }

        /// <summary>Attacking something (a fighting target) or being attacked (on someone's attacker list).</summary>
        static bool IsInCombat(Character character)
            => !character.IsDead && (character.FightingTarget.Instance != 0 || character.Attackers.Count > 0);

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
        }

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
                if (!_items.CreateTemplate(itemId, itemId, quality: 1).MeetsActionRequirements(stat => Stats.Get(stat), ActionType.ToWear, ResolvePerkRequirement))
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
            if (!TrainedPerks.Contains(perkId))
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

            _perkTemplates = templates.ToArray();
            RebasePerkActions();
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

        /// <summary>Login / FullCharacter: every held perk action and running perk lock goes to the client.</summary>
        public void SendPerkActions()
        {
            AnnouncePerkActionChanges([], _perkActions);

            DateTime nowUtc = DateTime.UtcNow;
            foreach ((int perkId, TimeSpan remaining) in PerkLocks.Active(nowUtc))
                AnnouncePerkLock(perkId, (int)Math.Ceiling(remaining.TotalSeconds), nowUtc);
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
            => TrainedPerks.Snapshot()
                .Select(perkId => new PerkMapEntry { Key = perkId, Marker = PerkMapEntry.VersionZero, PerkId = perkId })
                .ToArray();

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
                Character? resolved = ResolvePerkActionTarget();
                if (resolved == null)
                {
                    RequirementFeedback.SendText(this, "You need a target to use this.");
                    return false;
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
                        stat => PerkActionStat(stat, target), ResolvePerkRequirement);
                return false;
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
            if (starting && !use.Template.MeetsActionRequirements(stat => PerkActionStat(stat, use.Target), ActionType.ToUse, ResolvePerkRequirement))
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

        /// <summary>The fighting target, else the selected living character other than this player.</summary>
        Character? ResolvePerkActionTarget()
        {
            Character? fighting = TryResolveFightingTarget();
            if (fighting != null)
                return fighting;

            if (Target.Instance == 0 || Target == Identity || Playfield == null
                || !Playfield.GetRequiredService<DynelRegistry>().TryGet(Target, out Dynel? dynel)
                || dynel is not Character selected || selected.IsDead)
                return null;

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

        /// <summary>Attack skill map (stat to percent) weighted against this player's skills.</summary>
        int WeightedSkill(IReadOnlyDictionary<CharacterStat, int> attack)
        {
            long total = 0;
            foreach ((CharacterStat stat, int percent) in attack)
                total += (long)Stats.GetOrZero(stat) * percent;
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
        {
            foreach (ItemTemplate perk in _perkTemplates)
            {
                if (perk.SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear))
                    StatModifierSpells.Apply(wear, Stats);
            }
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
            if (!_respawnPending)
                return;

            _respawnRemainingSeconds -= deltaTime;
            if (!IsRespawnGraceElapsed())
                return;

            _respawnPending = false;
            TryRespawn();
        }

        bool IsRespawnGraceElapsed()
            => _respawnRemainingSeconds <= 0.0
                || (DateTime.UtcNow - _diedAtUtc).TotalSeconds >= RespawnGracePeriodMilliseconds / 1000.0;

        void TryRespawn()
        {
            try
            {
                Respawn();
                SaveState.MarkDirty();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Player respawn failed.");
            }
        }

        void Respawn()
        {
            Playfield? playfield = Playfield;
            if (playfield == null)
            { Revive(); return; }

            RespawnContentCatalog respawn = playfield.GetRequiredService<IGameData>().RespawnContent;
            if (respawn.PlayfieldId <= 0 || respawn.Position is not { Length: 3 })
                throw new InvalidOperationException("No respawn destination is configured.");

            Vector3 landing = new Vector3(respawn.Position[0], respawn.Position[1], respawn.Position[2]);

            // Never build the respawn playfield on this tick: stay dead while it builds in the background, and
            // respawn once it is ready.
            PlayfieldManager manager = playfield.GetRequiredService<PlayfieldManager>();
            if (playfield.Identity.Instance != respawn.PlayfieldId && !manager.TryGet(respawn.PlayfieldId, out _))
            {
                bool queued = manager.WithPlayfield(respawn.PlayfieldId, this, _ =>
                {
                    if (IsDead && Playfield != null)
                        TryRespawn();
                });

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
            if (playfield.Identity.Instance == respawn.PlayfieldId && !_respawnObserversCleared)
            {
                playfield.GetRequiredService<PlayfieldLocality>().DespawnForObservers(this);
                _respawnObserversCleared = true;
                _respawnPending = true;
                _respawnRemainingSeconds = 0;
                return;
            }

            _respawnObserversCleared = false;
            Revive();

            Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player respawn character={0} to ({1},{2},{3}) pf={4}",
                    Identity.Instance,
                    landing.xf,
                    landing.yf,
                    landing.zf,
                    respawn.PlayfieldId));

            if (playfield.Identity.Instance == respawn.PlayfieldId)
            {
                playfield.GetRequiredService<SpawnService>().CompleteSamePlayfieldDeathRespawn(this, landing);
                return;
            }

            AnnounceDeathCleared();

            // Respawning elsewhere is not a proxy entry: an exit there must not send this character back through the
            // entrance they took before dying.
            Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);

            Playfield destination = manager.GetOrCreate(respawn.PlayfieldId);
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
            // Over-equipped items are judged on the full buffed skills with every item at full strength, then the
            // bonus layers are rebuilt once with their penalties (one extra pass, only while something is OE).
            if (Inventory.IsHydrated)
                Inventory.ResetOverEquip();
            ApplyBonusLayers();
            if (Inventory.IsHydrated && Inventory.ApplyOverEquip(Stats))
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

        const CharacterStat WeaponMeshRightStat = (CharacterStat)1006;
        const CharacterStat WeaponMeshLeftStat = (CharacterStat)1007;
        const CharacterStat OverrideTextureWeaponRightStat = (CharacterStat)1009;
        const CharacterStat OverrideTextureWeaponLeftStat = (CharacterStat)1010;

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

            Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, wifu, includeSelf: true);
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
                WeaponMeshRightStat,
                OverrideTextureWeaponRightStat);
            changed |= SyncOneHandWeaponMesh(
                (int)WeaponSlots.LeftHand,
                meshPosition: 2,
                WeaponMeshLeftStat,
                OverrideTextureWeaponLeftStat);
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
            // CharacterStat.TempSavePlayfield,
            // CharacterStat.TempSaveX,
            // CharacterStat.TempSaveY,
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
            // CharacterStat.AutoAttackFlags,
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
            // CharacterStat.AutoAttackFlags,
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
            // CharacterStat.MetaType,
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
                Unknown4 = [],
                UnknownI2 = 1,
                Unknown5 = [],
                UnknownI3 = 1,
                Unknown6 = []
            };

            message.Stats1 = BuildFullCharacterIntStats(stats, FullCharacterStats1);
            message.Stats2 = BuildFullCharacterIntStats(stats, FullCharacterStats2);
            message.Stats3 = BuildFullCharacterByteStats(stats, FullCharacterStats3);
            message.Stats4 = BuildFullCharacterShortStats(stats, FullCharacterStats4);

            message.Unknown9 = 0;
            message.Unknown10 = 0;
            message.Unknown11 = [];
            message.Unknown12 = [];
            message.Unknown13 = BuildPerkMap();

            // Team / raid conditional blocks not wired yet.
            // message.Unknown10 = ...
            // message.Unknown11 = ...
            // message.Unknown12 = ...
            // message.Unknown13 = ...

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
