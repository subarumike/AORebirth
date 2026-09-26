namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using AORebirth.Core.Textures;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.GameData;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Why delayed character actions are being interrupted.
    /// Jump and locomotion cancel most (equip, nano cast). LeavePlayfield cancels every timed action.
    /// </summary>
    public enum TimedActionInterrupt
    {
        Jump,
        LeavePlayfield,
        Movement,
    }

    public enum XpSource
    {
        Kill,
        Quest,
    }

    /// <summary>
    /// Character layer between Dynel and <see cref="Player"/> / <see cref="NpcCharacter"/> (shared fields).
    /// </summary>
    public abstract class Character : Dynel
    {
        const int MaxXpLevel = 220;
        const int KillXpCapPercent = 10;
        const int QuestXpCapPercent = 20;
        const int MartialArtsSpecialLowId = 211357;
        const int MartialArtsSpecialHighId = 211358;
        const int DimachSpecialLowId = 42033;
        const int DimachSpecialHighId = 42032;
        const int BrawlSpecialLowId = 211401;
        const int BrawlSpecialHighId = 211402;

        protected Character(Identity identity)
            : base(identity)
        {
            Motor = new CharacterMotor(this);
            Motor.Jumped += OnJumped;
            Stats.StatChanged += OnStatChanged;
            // Requirement folds use Stats.Get. Unset is not 0, so a missing opponent count
            // reads as "in combat".
            Stats.Set(CharacterStat.NumberOfFightingOpponents, 0, StatDetail.Base);
        }

        public override double GetCollisionRadius()
        {
            int scale = Stats.GetOrZero(CharacterStat.Scale);
            if (scale <= 0)
                scale = 100;
            return (scale * CharacterRadius) / 100.0;
        }

        /// <summary>LockSkill cooldowns by stat id. Only players persist them.</summary>
        public SkillLocks SkillLocks { get; } = new();

        public void LockSkill(int statId, int durationSeconds, DateTime nowUtc)
        {
            SkillLocks.Lock(statId, ScaleSkillLock(statId, durationSeconds), nowUtc);
            if (this is Player player)
                Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(player);
        }

        /// <summary>
        /// SkillLockModifier is a percent of the lock duration. The client floors it at -50.
        /// Special-attack locks stay at the scripted duration.
        /// </summary>
        int ScaleSkillLock(int statId, int durationSeconds)
        {
            if (durationSeconds <= 0 || IgnoresSkillLockModifier(statId))
                return durationSeconds;

            int modifier = Stats.GetOrZero(CharacterStat.SkillLockModifier);
            if (modifier < -50)
                modifier = -50;

            return (int)((durationSeconds * (100L + modifier) + 50) / 100);
        }

        static bool IgnoresSkillLockModifier(int statId)
        {
            switch ((CharacterStat)statId)
            {
                case CharacterStat.Brawl:
                case CharacterStat.Dimach:
                case CharacterStat.SneakAttack:
                case CharacterStat.FastAttack:
                case CharacterStat.Burst:
                case CharacterStat.FlingShot:
                case CharacterStat.AimedShot:
                case CharacterStat.FullAuto:
                case CharacterStat.ShadowBreedTemplate:
                case CharacterStat.VisualFlags:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Client text 1000/222040796.</summary>
        public void SendSkillLocked(int statId, TimeSpan remaining)
        {
            if (this is not Player player || player.Session == null)
                return;

            int totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            player.Session.Send(
                new ChatTextMessage
                {
                    Identity = Identity,
                    Text = string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to perform action, {0} skill is locked, able in {1:00}:{2:00}:{3:00}",
                        (CharacterStat)statId,
                        totalSeconds / 3600,
                        totalSeconds / 60 % 60,
                        totalSeconds % 60)
                });
        }

        /// <summary>
        /// Subscribe for delayed actions. Honor <see cref="TimedActionInterrupt.LeavePlayfield"/>
        /// always; honor <see cref="TimedActionInterrupt.Jump"/> unless the action survives jump.
        /// </summary>
        public event Action<Character, TimedActionInterrupt>? TimedActionsInterrupted;

        public void InterruptTimedActions(TimedActionInterrupt reason)
        {
            if (reason != TimedActionInterrupt.Movement)
                Playfield?.GetRequiredService<InventoryMoveService>().CancelPending(Identity.Instance);
            if (reason == TimedActionInterrupt.LeavePlayfield)
                Playfield?.GetService<ItemUseService>()?.CancelPending(Identity.Instance);
            NanoRuntime.InterruptCast(this);
            TimedActionsInterrupted?.Invoke(this, reason);
        }

        void OnJumped()
            => InterruptTimedActions(TimedActionInterrupt.Jump);

        public CharacterMotor Motor { get; }

        public string? Name { get; set; }

        public Dictionary<WeaponSlot, CharacterWeapon> Weapons { get; } = new();

        readonly Dictionary<WeaponSlot, Action> _weaponAttackHandlers = new();
        readonly KillRewardResolver _killRewards = new();

        /// <summary>Current auto-attack target; <see cref="Identity.None"/> when not fighting.</summary>
        public Identity FightingTarget { get; private set; } = Identity.None;

        /// <summary>Resolved character for <see cref="FightingTarget"/>, when they were on this playfield.</summary>
        Character? _opponent;

        /// <summary>Characters that currently have this character as <see cref="FightingTarget"/>.</summary>
        readonly HashSet<Character> _attackers = new();

        bool _publishingOpponentCount;

        /// <summary>Raised once when the corpse swap completes (after <see cref="CorpseSwapDelayMilliseconds"/>).</summary>
        public event Action<Character>? Died;

        public const int CorpseSwapDelayMilliseconds = 2500;
        protected virtual int CorpseSpawnDelayMilliseconds => CorpseSwapDelayMilliseconds;

        const int DefaultNpcDeathAnimationKey = 0x1F7;
        const int DefaultPlayerDeathAnimationKey = 500;
        protected virtual int DeathAnimationKey => IsPlayer ? DefaultPlayerDeathAnimationKey : DefaultNpcDeathAnimationKey;
        protected virtual bool UsesPassiveRegen => true;

        bool _deathNotified;
        bool _corpseSwapPending;
        double _corpseSwapRemainingSeconds;
        double _healRegenElapsed;
        double _nanoRegenElapsed;

        public bool IsDead => _deathNotified;

        /// <summary>
        /// Idempotent death entry: death action + clear fight state immediately;
        /// corpse spawn and <see cref="Died"/> after <see cref="CorpseSwapDelayMilliseconds"/>.
        /// </summary>
        public virtual void OnDeath(Character? killer = null)
        {
            if (_deathNotified)
                return;

            _deathNotified = true;
            InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
            SetFightingTarget(Identity.None);
            NanoRuntime.ClearBuffsOnDeath(this);

            Cell?.Announce(
                new CharacterActionMessage
                {
                    Identity = Identity,
                    Action = CharacterActionType.Death,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = DeathAnimationKey
                });

            _corpseSwapPending = true;
            _corpseSwapRemainingSeconds = CorpseSpawnDelayMilliseconds / 1000.0;

            AwardKillRewards();
        }

        /// <summary>
        /// Clears death and restores health/nano so the character can live again.
        /// </summary>
        public virtual void Revive()
        {
            _deathNotified = false;
            _corpseSwapPending = false;
            _corpseSwapRemainingSeconds = 0;
            ClearNanoRecharge();

            int maxHealth = Stats.GetOrZero(CharacterStat.MaxHealth);
            Stats.Set(CharacterStat.Health, maxHealth > 0 ? maxHealth : 1, StatDetail.Base, dirty: true);

            int maxNano = Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (maxNano > 0)
                Stats.Set(CharacterStat.CurrentNano, maxNano, StatDetail.Base, dirty: true);

            Stats.Set(CharacterStat.DeadTimer, 0, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.State, 0, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.CurrentState, 0, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.ActionCategory, 0, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.SocialStatus, 0, StatDetail.Base, dirty: true);

            FlushDirtyStats();
        }

        /// <summary>
        /// Adds XP and levels from <c>Xp.json</c> NextLevelXp. One grant is capped at
        /// 10% (kill) or 20% (quest) of the current level bar.
        /// </summary>
        public void AwardXp(int amount, XpSource source)
        {
            if (amount <= 0 || !IsPlayer)
                return;

            IGameData? gameData = Playfield?.GetRequiredService<IGameData>();
            if (gameData == null)
                return;

            int level = Stats.GetOrOne(CharacterStat.Level);
            if (!gameData.TryGetXpLevel(level, out XpLevelEntry current))
                return;

            int capPercent = source == XpSource.Kill ? KillXpCapPercent : QuestXpCapPercent;
            if (current.NextLevelXp > 0)
            {
                int cap = current.NextLevelXp * capPercent / 100;
                if (cap < 1)
                    cap = 1;
                if (amount > cap)
                    amount = cap;
            }

            int levelBefore = level;
            int xp = Stats.GetOrZero(CharacterStat.XP) + amount;
            while (level < MaxXpLevel
                && current.NextLevelXp > 0
                && xp >= current.FloorXp + current.NextLevelXp)
            {
                if (!gameData.TryGetXpLevel(level + 1, out XpLevelEntry next))
                    break;

                level = next.Level;
                current = next;
            }

            Stats.Set(CharacterStat.XP, xp, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.Level, level, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.LastXP, current.FloorXp, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.NextXP, current.NextLevelXp > 0 ? current.FloorXp + current.NextLevelXp : 0, StatDetail.Base, dirty: true);

            if (level > levelBefore)
                ApplyLevelUp(gameData, levelBefore, level, amount);
        }

        /// <summary>
        /// GM/admin level set: snaps XP to the start of <paramref name="level"/> and syncs
        /// LastXP/NextXP, title level, and IP for the level delta.
        /// </summary>
        /// <returns>False if level is out of range or XP table data is missing.</returns>
        public bool TrySetLevel(int level)
        {
            if (!IsPlayer || level < 1 || level > MaxXpLevel)
                return false;

            IGameData? gameData = Playfield?.GetRequiredService<IGameData>();
            if (gameData == null || !gameData.TryGetXpLevel(level, out XpLevelEntry entry))
                return false;

            int levelBefore = Stats.GetOrOne(CharacterStat.Level);

            Stats.Set(CharacterStat.XP, entry.FloorXp, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.Level, level, StatDetail.Base, dirty: true);
            Stats.Set(CharacterStat.LastXP, entry.FloorXp, StatDetail.Base, dirty: true);
            Stats.Set(
                CharacterStat.NextXP,
                entry.NextLevelXp > 0 ? entry.FloorXp + entry.NextLevelXp : 0,
                StatDetail.Base,
                dirty: true);

            if (level > levelBefore)
            {
                ApplyLevelUp(gameData, levelBefore, level, lastGain: 0);
                return true;
            }

            if (level < levelBefore)
            {
                Stats.Set(CharacterStat.TitleLevel, TitleLevelFor(level), StatDetail.Base, dirty: true);

                int ipDelta = TotalIpEarnedAtLevel(level) - TotalIpEarnedAtLevel(levelBefore);
                if (ipDelta != 0)
                {
                    int ip = Math.Max(0, Stats.GetOrZero(CharacterStat.IP) + ipDelta);
                    Stats.Set(CharacterStat.IP, ip, StatDetail.Base, dirty: true);
                }

                ApplyLevelVitals();
            }

            FlushDirtyStats();
            return true;
        }

        void ApplyLevelUp(IGameData gameData, int levelBefore, int levelAfter, int lastGain)
        {
            Stats.Set(CharacterStat.TitleLevel, TitleLevelFor(levelAfter), StatDetail.Base, dirty: true);

            int ipGain = TotalIpEarnedAtLevel(levelAfter) - TotalIpEarnedAtLevel(levelBefore);
            if (ipGain > 0)
                Stats.Set(CharacterStat.IP, Stats.GetOrZero(CharacterStat.IP) + ipGain, StatDetail.Base, dirty: true);

            ApplyLevelVitals();
            FlushDirtyStats();

            if (this is not Player player || player.Session == null)
                return;

            for (int gained = levelBefore + 1; gained <= levelAfter; gained++)
                player.Session.Send(BuildNewLevelMessage(gameData, gained, lastGain));
        }

        void ApplyLevelVitals()
        {
            Rebase();
            int maxHealth = Stats.GetOrZero(CharacterStat.MaxHealth);
            if (maxHealth > 0)
                Stats.Set(CharacterStat.Health, maxHealth, StatDetail.Base, dirty: true);

            int maxNano = Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (maxNano > 0)
                Stats.Set(CharacterStat.CurrentNano, maxNano, StatDetail.Base, dirty: true);
        }

        NewLevelMessage BuildNewLevelMessage(IGameData gameData, int level, int lastGain)
        {
            int lastSaveXp = 0;
            int nextLevelXp = 0;
            if (gameData.TryGetXpLevel(level, out XpLevelEntry current))
            {
                lastSaveXp = current.FloorXp;
                nextLevelXp = current.NextLevelXp > 0 ? current.FloorXp + current.NextLevelXp : 0;
            }

            return new NewLevelMessage
            {
                Identity = Identity,
                Unknown = 0,
                Level = level,
                Ip = Math.Max(0, Stats.GetOrZero(CharacterStat.IP)),
                Xp = Stats.GetOrZero(CharacterStat.XP),
                LastSaveXp = lastSaveXp,
                NextLevelXp = nextLevelXp,
                Unknown1 = 0,
                Unknown2 = 4,
                LastXp = lastGain
            };
        }

        internal static int TitleLevelFor(int level)
        {
            if (level >= 205)
                return 7;
            if (level >= 190)
                return 6;
            if (level >= 150)
                return 5;
            if (level >= 100)
                return 4;
            if (level >= 50)
                return 3;
            if (level >= 15)
                return 2;
            return 1;
        }

        /// <summary>Lifetime IP earned at <paramref name="level"/> (legacy <c>StatIp</c> brackets).</summary>
        internal static int TotalIpEarnedAtLevel(int level)
        {
            if (level < 1)
                return 0;

            int earned = 0;
            int remaining = level;
            if (remaining > 204)
            {
                earned += (remaining - 204) * 600000;
                remaining = 204;
            }

            if (remaining > 189)
            {
                earned += (remaining - 189) * 150000;
                remaining = 189;
            }

            if (remaining > 149)
            {
                earned += (remaining - 149) * 80000;
                remaining = 149;
            }

            if (remaining > 99)
            {
                earned += (remaining - 99) * 40000;
                remaining = 99;
            }

            if (remaining > 49)
            {
                earned += (remaining - 49) * 20000;
                remaining = 49;
            }

            if (remaining > 14)
            {
                earned += (remaining - 14) * 10000;
                remaining = 14;
            }

            return earned + 1500 + (remaining - 1) * 4000;
        }

        void AwardKillRewards()
        {
            Playfield? playfield = Playfield;
            if (playfield == null)
                return;

            List<int> present = CollectPresentQualifiers(playfield);
            if (present.Count == 0)
                return;

            AwardRegularXp(playfield, present);
            AwardAlienXp(present);
            AwardPvpTitle(present);
        }

        List<int> CollectPresentQualifiers(Playfield playfield)
        {
            var present = new List<int>();
            List<int> qualifying = _killRewards.GetQualifyingPlayerInstances();
            if (qualifying.Count == 0)
                return present;

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            for (int i = 0; i < qualifying.Count; i++)
            {
                int instance = qualifying[i];
                if (!_killRewards.TryGetIdentity(instance, out Identity identity))
                    continue;
                if (!registry.TryGet(identity, out Dynel? dynel) || dynel is not Character killer)
                    continue;
                if (!killer.IsPlayer || killer.IsDead || ReferenceEquals(killer, this))
                    continue;

                present.Add(instance);
            }

            return present;
        }

        void AwardRegularXp(Playfield playfield, IReadOnlyList<int> present)
        {
            if (IsPlayer)
                return;

            IGameData gameData = playfield.GetRequiredService<IGameData>();
            int victimLevel = Stats.GetOrOne(CharacterStat.Level);
            if (!gameData.TryGetXpLevel(victimLevel, out XpLevelEntry extract) || extract.KillAward <= 0)
                return;

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            List<AwardShare> shares = _killRewards.Divide(extract.KillAward, present, AwardCredit.Shared);
            for (int i = 0; i < shares.Count; i++)
            {
                AwardShare share = shares[i];
                if (share.Amount <= 0)
                    continue;
                if (!_killRewards.TryGetIdentity(share.PlayerInstance, out Identity identity))
                    continue;
                if (!registry.TryGet(identity, out Dynel? dynel) || dynel is not Character killer)
                    continue;

                int amount = share.Amount;
                int killerLevel = killer.Stats.GetOrOne(CharacterStat.Level);
                if (killerLevel > victimLevel + extract.LevelDelta)
                    amount = 1;

                killer.AwardXp(amount, XpSource.Kill);
            }
        }

        void AwardAlienXp(IReadOnlyList<int> present)
        {
            if (IsPlayer)
                return;

            _killRewards.Divide(ComputeAlienXpPool(), present, AwardCredit.Shared);
        }

        static int ComputeAlienXpPool() => 0;

        void AwardPvpTitle(IReadOnlyList<int> present)
        {
            if (!IsPlayer)
                return;

            _killRewards.Divide(ComputePvpTitlePool(), present, AwardCredit.Shared);
        }

        static int ComputePvpTitlePool() => 0;

        public bool TryGetLootWinner(out Identity identity)
            => _killRewards.TryGetLootWinner(out identity);

        protected void ClearKillRewards()
            => _killRewards.Clear();

        void CompleteCorpseSwap()
        {
            if (!_corpseSwapPending)
                return;

            _corpseSwapPending = false;

            SpawnDeathCorpse();
            ClearKillRewards();
            Died?.Invoke(this);
            RemoveFromWorldAfterDeath();
        }

        /// <summary>
        /// Hash-spawn and content NPCs despawn from <see cref="Died"/> listeners.
        /// Command-spawned and other listener-less NPCs still have to leave the world here.
        /// Mission NPCs override this so the dead body can linger for the accepted visual window.
        /// </summary>
        protected virtual void RemoveFromWorldAfterDeath()
        {
        }

        public void SetFightingTarget(Identity identity)
        {
            if (FightingTarget != identity)
            {
                if (_opponent != null)
                {
                    Character previous = _opponent;
                    _opponent = null;
                    previous.RemoveAttacker(this);
                }

                FightingTarget = identity;

                if (identity.Instance != 0)
                {
                    Character? next = ResolveFightingOpponent(identity);
                    if (next != null && !ReferenceEquals(next, this))
                    {
                        _opponent = next;
                        next.AddAttacker(this);
                    }
                }
            }

            if (identity.Instance == 0)
                ResetAllWeaponAttacks();
        }

        /// <summary>
        /// Drops this character's fight links. Attackers are told to stop, including ones
        /// that are not ticking, so the opponent count cannot outlive the fight.
        /// </summary>
        public void LeaveCombat()
        {
            SetFightingTarget(Identity.None);
            if (_attackers.Count == 0)
                return;

            Character[] attackers = new Character[_attackers.Count];
            _attackers.CopyTo(attackers);
            foreach (Character attacker in attackers)
            {
                if (attacker.FightingTarget == Identity)
                    attacker.SetFightingTarget(Identity.None);
                else
                    RemoveAttacker(attacker);
            }
        }

        Character? ResolveFightingOpponent(Identity identity)
        {
            Playfield? playfield = Playfield;
            if (playfield == null || identity.Instance == 0)
                return null;

            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel))
                return null;

            return dynel as Character;
        }

        void AddAttacker(Character attacker)
        {
            if (!_attackers.Add(attacker))
                return;

            PublishOpponentCount();
        }

        void RemoveAttacker(Character attacker)
        {
            if (!_attackers.Remove(attacker))
                return;

            PublishOpponentCount();
        }

        void PublishOpponentCount()
        {
            _publishingOpponentCount = true;
            try
            {
                int count = _attackers.Count;
                Stats.Set(CharacterStat.NumberOfFightingOpponents, 0, StatDetail.Bonus);
                Stats.Set(CharacterStat.NumberOfFightingOpponents, count, StatDetail.Base, dirty: true);
            }
            finally
            {
                _publishingOpponentCount = false;
            }
        }

        protected virtual void SpawnDeathCorpse()
            => Playfield?.GetRequiredService<SpawnService>().SpawnCorpse(this);

        /// <summary>
        /// Engage auto-attack: SpecialAttackWeapon first so observers have specials, then Attack.
        /// </summary>
        public virtual void StartFighting(Identity target, byte action)
        {
            if (this is Player player && player.IsPersistenceQuarantined)
                return;
            if (Playfield != null
                && Playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                && dynel is Character resolved
                && !CombatRules.CanAttack(this, resolved))
                return;

            SetFightingTarget(target);
            ResetAllWeaponAttacks();
            Cell?.Announce(BuildSpecialAttackWeaponMessage());
            Cell?.Announce(
                new AttackMessage
                {
                    Identity = Identity,
                    Target = target,
                    Action = action
                });
        }

        public virtual SpecialAttackWeaponMessage BuildSpecialAttackWeaponMessage()
        {
            SpecialAttack[] specials = BuildSpecialAttacks();
            var message = new SpecialAttackWeaponMessage
            {
                Identity = Identity,
                Specials = specials,
                CloseCombatInitiative = Stats.GetOrZero(CharacterStat.MeleeInit),
                DistanceWeaponInitiative = Stats.GetOrZero(CharacterStat.RangedInit),
                PhysicalProwessInitiative = Stats.GetOrZero(CharacterStat.PhysicalInit),
                NanoProwessInitiative = Stats.GetOrZero(CharacterStat.NanoCInit),
                AggDef = Stats.GetOrZero(CharacterStat.AggDef)
            };

            // TEMP: SAW dump while specials are being wired.
            LogSpecialAttackWeapon(message);
            return message;
        }

        SpecialAttack[] BuildSpecialAttacks()
        {
            var specials = new List<SpecialAttack>();
            bool maat = false;
            bool brawl = false;
            bool dimach = false;

            // TEMP: SAW weapon scan.
            LogSaw(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SAW scan character={0} player={1} weapons={2}",
                    Identity.Instance,
                    IsPlayer,
                    Weapons.Count));

            // NPC template weapons: emit live-shaped low/high/tag entries first so AttackInfo
            // Unknown6 can match SpecialAttack.Unknown3.
            foreach (KeyValuePair<WeaponSlot, CharacterWeapon> pair in Weapons)
            {
                CharacterWeapon? armed = pair.Value;
                Item? item = armed?.Item;
                if (armed == null || item == null || armed.WireSlot < 0 || armed.SawTag == 0)
                    continue;

                string tagName = string.IsNullOrEmpty(armed.SawTagName)
                    ? "SIW1"
                    : armed.SawTagName;
                specials.Add(
                    new SpecialAttack
                    {
                        Unknown1 = item.LowId,
                        Unknown2 = item.HighId > 0 ? item.HighId : item.LowId,
                        Unknown3 = armed.SawTag,
                        Unknown4 = tagName
                    });
                LogSaw(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SAW npc-weapon slot={0} wire={1} low={2} high={3} tag={4}({5})",
                        pair.Key,
                        armed.WireSlot,
                        item.LowId,
                        item.HighId,
                        tagName,
                        armed.SawTag));
            }

            // NPC SAW lists only monster weapons.
            if (!IsPlayer)
                return specials.ToArray();

            foreach (KeyValuePair<WeaponSlot, CharacterWeapon> pair in Weapons)
            {
                Item? item = pair.Value?.Item;
                if (item == null)
                {
                    LogSaw(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "SAW weapon slot={0} item=null",
                            pair.Key));
                    continue;
                }

                bool martialArtsItem = item.IsMaCombinedWeapon() || (pair.Value?.IsSyntheticFist == true);
                int can = item.GetStat(CharacterStat.Can);
                LogSaw(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SAW weapon slot={0} name={1} low={2} high={3} ql={4} can=0x{5:X} specials={6} ma={7} synthetic={8}",
                        pair.Key,
                        item.Name,
                        item.LowId,
                        item.HighId,
                        item.Quality,
                        can,
                        FormatSpecialCanFlags((CanFlags)(uint)can),
                        martialArtsItem,
                        pair.Value?.IsSyntheticFist == true));

                if (!maat && martialArtsItem)
                {
                    specials.Add(
                        CreateSpecialAttack(
                            MartialArtsSpecialLowId,
                            MartialArtsSpecialHighId,
                            CharacterStat.MartialArts,
                            "MAAT"));
                    maat = true;
                }

                if (!brawl && (martialArtsItem || item.Can(CanFlags.Brawl)))
                {
                    specials.Add(
                        CreateSpecialAttack(
                            BrawlSpecialLowId,
                            BrawlSpecialHighId,
                            CharacterStat.Brawl,
                            "BRAW"));
                    brawl = true;
                }

                if (!dimach && (martialArtsItem || item.Can(CanFlags.Dimach)))
                {
                    specials.Add(
                        CreateSpecialAttack(
                            DimachSpecialLowId,
                            DimachSpecialHighId,
                            CharacterStat.Dimach,
                            "DIIT"));
                    dimach = true;
                }
            }

            // Capture 20260724-001643 / ZoneEngine AttackMessageHandler: players always advertise
            // MAAT/BRAW/DIIT on combat start. Synthetic fists often lack MartialArts>0 in catalog.
            if (IsPlayer)
            {
                if (!maat)
                {
                    specials.Add(
                        CreateSpecialAttack(
                            MartialArtsSpecialLowId,
                            MartialArtsSpecialHighId,
                            CharacterStat.MartialArts,
                            "MAAT"));
                }

                if (!brawl)
                {
                    specials.Add(
                        CreateSpecialAttack(
                            BrawlSpecialLowId,
                            BrawlSpecialHighId,
                            CharacterStat.Brawl,
                            "BRAW"));
                }

                if (!dimach)
                {
                    specials.Add(
                        CreateSpecialAttack(
                            DimachSpecialLowId,
                            DimachSpecialHighId,
                            CharacterStat.Dimach,
                            "DIIT"));
                }
            }

            return specials.ToArray();
        }

        void LogSpecialAttackWeapon(SpecialAttackWeaponMessage message)
        {
            SpecialAttack[] specials = message.Specials ?? [];
            var names = new StringBuilder();
            for (int i = 0; i < specials.Length; i++)
            {
                if (i > 0)
                    names.Append(',');

                SpecialAttack special = specials[i];
                names.Append(special.Unknown4);
                names.Append('(');
                names.Append(special.Unknown1);
                names.Append('/');
                names.Append(special.Unknown2);
                names.Append('/');
                names.Append(special.Unknown3);
                names.Append(')');
            }

            LogSaw(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SAW send character={0} count={1} specials=[{2}] closeCombatInitiative={3} distanceWeaponInitiative={4} physicalProwessInitiative={5} nanoProwessInitiative={6} aggDef={7}",
                    Identity.Instance,
                    specials.Length,
                    names.ToString(),
                    message.CloseCombatInitiative,
                    message.DistanceWeaponInitiative,
                    message.PhysicalProwessInitiative,
                    message.NanoProwessInitiative,
                    message.AggDef));
        }

        static string FormatSpecialCanFlags(CanFlags flags)
        {
            var names = new List<string>(8);
            if ((flags & CanFlags.FlingShot) != 0)
                names.Add("FlingShot");
            if ((flags & CanFlags.Burst) != 0)
                names.Add("Burst");
            if ((flags & CanFlags.FullAuto) != 0)
                names.Add("FullAuto");
            if ((flags & CanFlags.AimedShot) != 0)
                names.Add("AimedShot");
            if ((flags & CanFlags.FastAttack) != 0)
                names.Add("FastAttack");
            if ((flags & CanFlags.Brawl) != 0)
                names.Add("Brawl");
            if ((flags & CanFlags.Dimach) != 0)
                names.Add("Dimach");
            if ((flags & CanFlags.SneakAttack) != 0)
                names.Add("SneakAttack");
            return names.Count == 0 ? "none" : string.Join("|", names);
        }

        void LogSaw(string message)
        {
            if (this is Player player)
                player.Logger.Info(message);
            else
                LogUtil.Debug(DebugInfoDetail.Engine, message);
        }

        static SpecialAttack CreateSpecialAttack(int lowId, int highId, CharacterStat skill, string name)
        {
            return new SpecialAttack
            {
                Unknown1 = lowId,
                Unknown2 = highId,
                Unknown3 = (int)skill,
                Unknown4 = name
            };
        }

        public void SetWeapon(WeaponSlot slot, CharacterWeapon weapon)
        {
            if (slot == WeaponSlot.None || weapon == null)
                return;

            if (Weapons.TryGetValue(slot, out CharacterWeapon? existing) && existing != null
                && _weaponAttackHandlers.TryGetValue(slot, out Action? existingHandler))
            {
                existing.Attacked -= existingHandler;
                _weaponAttackHandlers.Remove(slot);
            }

            weapon.Wielder = this;
            weapon.LogicalSlot = slot;
            CharacterWeapon armed = weapon;
            Action handler = () => ProcessWeaponSwing(armed);
            _weaponAttackHandlers[slot] = handler;
            Weapons[slot] = weapon;
            weapon.Attacked += handler;
            weapon.RefreshEffectiveSpeeds();
        }

        public void ClearWeapons()
        {
            foreach (KeyValuePair<WeaponSlot, CharacterWeapon> pair in Weapons)
            {
                if (pair.Value == null)
                    continue;

                if (_weaponAttackHandlers.TryGetValue(pair.Key, out Action? handler))
                    pair.Value.Attacked -= handler;

                pair.Value.Wielder = null;
                pair.Value.Item = null;
            }

            _weaponAttackHandlers.Clear();
            Weapons.Clear();
        }

        public void ResetAllWeaponAttacks()
        {
            foreach (CharacterWeapon weapon in Weapons.Values)
                weapon?.ResetAttack();
        }

        public override void Tick(double deltaTime)
        {
            if (_corpseSwapPending)
            {
                _corpseSwapRemainingSeconds -= deltaTime;
                if (_corpseSwapRemainingSeconds <= 0.0)
                {
                    CompleteCorpseSwap();
                    // NPC death listeners despawn this dynel; skip further tick work.
                    if (Playfield == null)
                        return;
                }
            }

            Motor.Tick(deltaTime);
            if (FightingTarget.Instance != 0 && TryResolveFightingTarget() != null)
                TickCombat(deltaTime);
            if (!IsDead && UsesPassiveRegen)
                TickPassiveRegen(deltaTime);
            NanoRuntime.Tick(this, DateTime.UtcNow);
            base.Tick(deltaTime);
        }

        void TickPassiveRegen(double deltaTime)
        {
            if (deltaTime <= 0)
                return;

            int breed = Stats.GetOrZero(CharacterStat.Breed);
            int bodyDevelopment = Stats.GetOrZero(CharacterStat.BodyDevelopment);
            bool sitting = Stats.GetOrZero(CharacterStat.CurrentMovementMode) == (int)MovementState.Sit;

            TickOneRegen(
                ref _healRegenElapsed,
                deltaTime,
                PassiveRegenCalculator.ComputeHealthDelta(
                    breed,
                    bodyDevelopment,
                    Stats.GetOrZero(CharacterStat.HealDelta)),
                PassiveRegenCalculator.ComputeHealthIntervalSeconds(
                    Stats.GetOrZero(CharacterStat.Stamina),
                    sitting),
                CharacterStat.Health,
                CharacterStat.MaxHealth);

            TickOneRegen(
                ref _nanoRegenElapsed,
                deltaTime,
                PassiveRegenCalculator.ComputeNanoDelta(
                    breed,
                    Stats.GetOrZero(CharacterStat.NanoPool),
                    Stats.GetOrZero(CharacterStat.NanoDelta)),
                PassiveRegenCalculator.ComputeNanoIntervalSeconds(
                    Stats.GetOrZero(CharacterStat.Psychic),
                    sitting),
                CharacterStat.CurrentNano,
                CharacterStat.MaxNanoEnergy);
        }

        void TickOneRegen(
            ref double elapsed,
            double deltaTime,
            int delta,
            double interval,
            CharacterStat currentStat,
            CharacterStat maxStat)
        {
            if (delta <= 0 || interval <= 0)
            {
                elapsed = 0;
                return;
            }

            int current = Math.Max(0, Stats.GetOrZero(currentStat));
            int max = Math.Max(0, Stats.GetOrZero(maxStat));
            if (current >= max)
            {
                elapsed = 0;
                return;
            }

            elapsed += deltaTime;
            if (elapsed < interval)
                return;

            elapsed = 0;
            int next = Math.Min(current + delta, max);
            if (next == current)
                return;

            Stats.Set(currentStat, next, StatDetail.Base, dirty: true);
        }

        protected virtual void TickCombat(double deltaTime) => TickWeapons(deltaTime);

        void TickWeapons(double deltaTime)
        {
            // Parked full bars (waiting on LOS/range) must still Tick so they can fire,
            // but must not monopolize the exclusive charge slot.
            foreach (CharacterWeapon weapon in Weapons.Values)
            {
                if (weapon == null || !weapon.IsFullyCharged)
                    continue;
                if (weapon.Tick(deltaTime))
                    return;
            }

            CharacterWeapon? charging = null;
            foreach (CharacterWeapon weapon in Weapons.Values)
            {
                if (weapon != null
                    && weapon.State == WeaponState.Attacking
                    && !weapon.IsFullyCharged)
                {
                    charging = weapon;
                    break;
                }
            }

            if (charging != null)
            {
                charging.Tick(deltaTime);
                return;
            }

            foreach (CharacterWeapon weapon in Weapons.Values)
            {
                if (weapon != null && weapon.State == WeaponState.Recharging)
                    weapon.Tick(deltaTime);
            }
        }

        void ProcessWeaponSwing(CharacterWeapon characterWeapon)
        {
            Character? target = TryResolveFightingTarget();
            if (target == null)
                return;

            if (!CombatRules.CanAttack(this, target))
                return;

            if (!HasLineOfSightTo(target))
                return;

            Item? weapon = characterWeapon.Item;
            if (GetEdgeDistanceTo(target) > characterWeapon.GetAttackRange())
                return;

            DamageCalculator.DamageResult result = DamageCalculator.CalculateFromWeapon(
                this,
                target,
                characterWeapon.DamageItem);
            int attackInfoSlot = AttackInfoRules.ResolveWeaponSlot(
                characterWeapon,
                characterWeapon.LogicalSlot,
                weapon,
                IsPlayer);
            // An evading NPC is untouchable on its way home: every swing is a miss.
            if (!result.IsHit || target.IsEvading)
            {
                Cell?.Announce(
                    new MissedAttackInfoMessage
                    {
                        Identity = Identity,
                        Unknown1 = -1,
                        Unknown2 = attackInfoSlot,
                        Unknown3 = Identity,
                        Unknown4 = target.Identity,
                        Unknown5 = 0
                    });
                return;
            }

            target.ApplyDamage(this, result.Damage, result.HitType);
            if (weapon != null)
                ApplyOnHitProcs(target, weapon);

            bool killingHit = target.IsDead;
            Cell?.Announce(
                new AttackInfoMessage
                {
                    Identity = Identity,
                    Target = target.Identity,
                    Unknown1 = result.Damage,
                    Unknown2 = AttackInfoRules.ResolveAmmoCount(characterWeapon, weapon, IsPlayer),
                    Unknown3 = attackInfoSlot,
                    Unknown4 = killingHit ? 4 : 0,
                    Unknown5 = (int)result.HitType,
                    Unknown6 = AttackInfoRules.ResolveWeaponInstance(characterWeapon, weapon, IsPlayer)
                });
            // Weapon/unarmed auto-attacks stay AttackInfo-only. HealthDamage is for Hit/nano/status.
        }

        /// <summary>
        /// Runs the weapon's OnHit functions after a connecting swing. Item 205012's
        /// OnHit is CastChance: a percent chance to land a nano on the recipient.
        /// </summary>
        void ApplyOnHitProcs(Character target, Item weapon)
        {
            if (!weapon.SpellList.ContainsKey(EventType.OnHit))
                return;

            Playfield? playfield = Playfield;
            if (playfield == null)
                return;

            IItemBuilder? items = playfield.GetService<IItemBuilder>();
            IInventoryRepository? inventory = playfield.GetService<IInventoryRepository>();
            if (items == null || inventory == null)
                return;

            weapon.Definition.ExecuteSpells(
                EventType.OnHit,
                target,
                inventory,
                items,
                source: this);
        }

        /// <summary>
        /// Applies hit-point damage. Returns true when this hit killed the character.
        /// </summary>
        public virtual bool ApplyDamage(Character attacker, int damage, HitType hitType)
        {
            if (_deathNotified || damage <= 0 || IsEvading)
                return false;

            // Same-character hits are item and status effects. Cross-character damage is an attack.
            if (!ReferenceEquals(attacker, this) && !CombatRules.CanAttack(attacker, this))
                return false;

            int previousHealth = Math.Max(0, Stats.GetOrZero(CharacterStat.Health));
            int newHealth = Math.Max(0, previousHealth - damage);
            int hpRemoved = previousHealth - newHealth;
            if (hpRemoved > 0 && attacker.IsPlayer && !ReferenceEquals(attacker, this))
                _killRewards.Record(attacker.Identity, hpRemoved);

            Stats.Set(CharacterStat.Health, newHealth, StatDetail.Base, dirty: true);

            if (hpRemoved > 0)
                OnDamaged(attacker, hpRemoved, hitType);

            if (newHealth > 0)
                return false;

            OnDeath(attacker);
            return true;
        }

        /// <summary>
        /// Client combat/status text for FunctionType.Hit health changes (nano damage/heals).
        /// AOEmu field layout: TargetHealth, signed DamageAmount, DamageType=AC on damage else 0.
        /// </summary>
        public void AnnounceHealthDamage(
            Character? source,
            int targetHealthAfter,
            int signedAmount,
            int damageTypeStat)
        {
            if (signedAmount == 0)
                return;

            var message = new HealthDamageMessage
            {
                Identity = Identity,
                Unknown1 = targetHealthAfter,
                Unknown2 = signedAmount,
                Unknown3 = signedAmount < 0 ? damageTypeStat : 0,
                Unknown4 = 0,
                Target = source?.Identity ?? Identity,
                Unknown5 = 0
            };

            if (Cell != null)
            {
                Cell.Announce(message);
                return;
            }

            if (this is Player targetPlayer)
                targetPlayer.Session?.Send(message);

            if (source is Player sourcePlayer && !ReferenceEquals(sourcePlayer, this))
                sourcePlayer.Session?.Send(message);
        }

        protected virtual void OnDamaged(Character attacker, int hpRemoved, HitType hitType)
        {
        }

        /// <summary>Leashing home: takes no damage, attacks miss and hostile nanos do not land.</summary>
        public virtual bool IsEvading => false;

        internal Character? TryResolveFightingTarget()
        {
            if (FightingTarget.Instance == 0 || Playfield == null)
                return null;

            DynelRegistry registry = Playfield.GetRequiredService<DynelRegistry>();
            if (registry.TryGet(FightingTarget, out Dynel? dynel) && dynel is Character target && !target.IsDead)
                return target;

            Cell?.Announce(
                new StopFightMessage
                {
                    Identity = Identity,
                    Unknown1 = 1
                });
            SetFightingTarget(Identity.None);
            return null;
        }

        bool _applyingVitalFromPercent;

        void OnStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            // Hydration loads stats in no fixed order, so only live writes are capped.
            if (!isInitialSet && next > previous && TryCapVital(stat, next))
                return;

            Motor.OnStatChanged(stat, previous, next, isInitialSet);

            if (stat == CharacterStat.NumberOfFightingOpponents)
            {
                if (!_publishingOpponentCount && next != _attackers.Count)
                    PublishOpponentCount();
                return;
            }

            if (!_applyingVitalFromPercent)
            {
                if (stat == CharacterStat.Health)
                    SyncVitalPercent(next, CharacterStat.MaxHealth, CharacterStat.PercentRemainingHealth);
                else if (stat == CharacterStat.CurrentNano)
                    SyncVitalPercent(next, CharacterStat.MaxNanoEnergy, CharacterStat.PercentRemainingNano);
            }

            if (stat != CharacterStat.AggDef)
                return;

            foreach (CharacterWeapon weapon in Weapons.Values)
                weapon?.RefreshEffectiveSpeeds();
        }

        /// <summary>Rewrites a current vital raised above its full max down to the max; true when it did.</summary>
        bool TryCapVital(CharacterStat stat, int next)
        {
            CharacterStat maxStat = stat switch
            {
                CharacterStat.Health => CharacterStat.MaxHealth,
                CharacterStat.CurrentNano => CharacterStat.MaxNanoEnergy,
                _ => CharacterStat.Unset
            };
            if (maxStat == CharacterStat.Unset)
                return false;

            int max = Stats.GetOrZero(maxStat);
            if (max <= 0 || next <= max)
                return false;

            Stats.Set(stat, Stats.GetOrZero(stat, StatDetail.Base) - (next - max), StatDetail.Base, dirty: true);
            return true;
        }

        /// <summary>0–100 remaining fraction of <paramref name="maxStat"/> after a current-vital change.</summary>
        void SyncVitalPercent(int current, CharacterStat maxStat, CharacterStat percentStat)
        {
            int max = Stats.GetOrZero(maxStat);
            if (max <= 0)
                return;

            int percent = (int)Math.Clamp((long)current * 100 / max, 0, 100);
            Stats.Set(percentStat, percent, StatDetail.Base, dirty: true);
        }

        /// <summary>
        /// Stored percent when set; otherwise derive from current/max. Missing max → 100.
        /// </summary>
        protected int ResolveVitalPercent(CharacterStat percentStat, CharacterStat currentStat, CharacterStat maxStat)
        {
            int stored = Stats.Get(percentStat);
            if (!StatCollection.IsUnset(stored))
                return Math.Clamp(stored, 0, 100);

            int max = Stats.GetOrZero(maxStat);
            if (max <= 0)
                return 100;

            return (int)Math.Clamp((long)Stats.GetOrZero(currentStat) * 100 / max, 0, 100);
        }

        /// <summary>Sets current vital from an already-resolved 0–100 percent of <paramref name="newMax"/>.</summary>
        protected void ApplyVitalFromPercent(CharacterStat currentStat, int newMax, int percent)
        {
            int clampedPercent = Math.Clamp(percent, 0, 100);
            int value = newMax <= 0 ? 0 : (int)((long)newMax * clampedPercent / 100);
            if (value < 0)
                value = 0;
            else if (value > newMax)
                value = newMax;

            _applyingVitalFromPercent = true;
            try
            {
                Stats.Set(currentStat, value, StatDetail.Base, dirty: true);
            }
            finally
            {
                _applyingVitalFromPercent = false;
            }
        }


        #region Nano casting and buffs

        readonly List<Buff> _buffs = [];
        DateTime _nanoRechargeUntilUtc = DateTime.MinValue;
        int _nextNanoInstance;
        int _lastLandedNanoId;
        int _lastLandedTargetInstance;
        DateTime _lastLandedAtUtc = DateTime.MinValue;

        /// <summary>Active NCU entries, oldest first.</summary>
        public IReadOnlyList<Buff> Buffs => _buffs;

        /// <summary>NCU consumed by friendly buffs that use NCU; mirrored into CurrentNCU.</summary>
        public int UsedNcu { get; private set; }

        /// <summary>NCU capacity from MaxNCU. Always enforced for friendly buffs.</summary>
        public int MaxNcu => Stats.GetOrZero(CharacterStat.MaxNCU);

        /// <summary>Cast bar in flight, or null when idle.</summary>
        public PendingNanoCast? PendingCast { get; private set; }

        public bool IsCastingNano => PendingCast != null;

        public bool IsInNanoRecharge(DateTime nowUtc) => nowUtc < _nanoRechargeUntilUtc;

        public void BeginNanoCast(PendingNanoCast cast)
        {
            ArgumentNullException.ThrowIfNull(cast);
            PendingCast = cast;
        }

        /// <summary>
        /// Drops a cast bar in flight. An interrupted cast never charges nano and never starts
        /// a recharge lockout, so spam-cancelling a cast buys nothing. Callers that need the
        /// client cast bar cleared should go through <see cref="NanoRuntime.InterruptCast"/>.
        /// </summary>
        public void CancelNanoCast() => PendingCast = null;

        /// <summary>
        /// Post-cast lockout before the next nano. Caster-wide rather than per-nano, a UTC
        /// deadline rather than a countdown, and never persisted: a relog clears it.
        /// It does not gate weapon attacks or specials.
        /// </summary>
        public void StartNanoRecharge(int centiseconds, DateTime nowUtc)
        {
            if (centiseconds <= 0)
                return;

            _nanoRechargeUntilUtc = nowUtc.AddMilliseconds(centiseconds * 10L);
        }

        public void ClearNanoRecharge() => _nanoRechargeUntilUtc = DateTime.MinValue;

        /// <summary>
        /// Client often emits CastNano + CastNanoSpell (or duplicates) for one click. A second
        /// land of the same nano on the same target inside this window is treated as a no-op.
        /// </summary>
        public const int DuplicateNanoLandWindowMilliseconds = 500;

        public void RememberNanoLanded(int nanoId, Identity target, DateTime nowUtc)
        {
            _lastLandedNanoId = nanoId;
            _lastLandedTargetInstance = target.Instance == 0 ? Identity.Instance : target.Instance;
            _lastLandedAtUtc = nowUtc;
        }

        public bool IsDuplicateRecentNanoLand(int nanoId, Identity target, DateTime nowUtc)
        {
            if (_lastLandedNanoId != nanoId)
                return false;

            int targetInstance = target.Instance == 0 ? Identity.Instance : target.Instance;
            if (_lastLandedTargetInstance != targetInstance)
                return false;

            return (nowUtc - _lastLandedAtUtc).TotalMilliseconds < DuplicateNanoLandWindowMilliseconds;
        }

        /// <summary>
        /// Claims a nano land for this window. Returns false when the same nano/target already
        /// landed recently so duplicate Complete paths do not spend nano or hit the wire twice.
        /// </summary>
        public bool TryClaimNanoLand(int nanoId, Identity target, DateTime nowUtc)
        {
            if (IsDuplicateRecentNanoLand(nanoId, target, nowUtc))
                return false;

            RememberNanoLanded(nanoId, target, nowUtc);
            return true;
        }

        /// <summary>
        /// Runs the strain / stacking / NCU gate and lands <paramref name="spell"/> when it passes.
        /// <paramref name="replaced"/> is the entry this buff pushed out, which the caller still
        /// has to announce as removed.
        /// </summary>
        public BuffApplyDecision TryApplyBuff(
            NanoSpell spell,
            Identity source,
            DateTime nowUtc,
            out Buff? applied,
            out Buff? replaced)
        {
            ArgumentNullException.ThrowIfNull(spell);

            applied = null;
            // NPC equipment nanos (Uklesh 205608 → 205606) use catalog NCU costs larger than MaxNCU.
            int ncuCap = this is NpcCharacter ? int.MaxValue : MaxNcu;
            BuffApplyDecision decision = BuffApplyRules.Evaluate(spell, _buffs, ncuCap, out replaced);
            if (decision != BuffApplyDecision.Apply && decision != BuffApplyDecision.Replace)
                return decision;

            if (replaced != null)
                _buffs.Remove(replaced);

            applied = Buff.Create(spell, source, ++_nextNanoInstance, nowUtc);
            _buffs.Add(applied);

            OnBuffsChanged();
            return decision;
        }

        /// <summary>
        /// Puts a persisted buff back in NCU at login. Skips strain and NCU checks: the set was
        /// already legal when it was stored, and an expired deadline is dropped by the caller.
        /// </summary>
        public Buff? TryRestoreBuff(NanoSpell spell, Identity source, int nanoInstance, DateTime expiresAtUtc)
        {
            ArgumentNullException.ThrowIfNull(spell);

            if (!spell.IsBuff || TryGetBuff(spell.Id, out _))
                return null;

            Buff buff = Buff.Restore(spell, source, nanoInstance, expiresAtUtc);
            _buffs.Add(buff);
            if (nanoInstance > _nextNanoInstance)
                _nextNanoInstance = nanoInstance;

            OnBuffsChanged();
            return buff;
        }

        public bool TryGetBuff(int nanoId, out Buff? buff)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id != nanoId)
                    continue;

                buff = _buffs[i];
                return true;
            }

            buff = null;
            return false;
        }

        public BuffRemovalOutcome TryRemoveBuff(int nanoId, BuffRemovalReason reason, out Buff? removed)
        {
            removed = null;
            if (!TryGetBuff(nanoId, out Buff? buff) || buff == null)
                return BuffRemovalOutcome.NotFound;

            if (!buff.TryCancel(reason))
                return BuffRemovalOutcome.NotCancellable;

            _buffs.Remove(buff);
            removed = buff;
            OnBuffsChanged();
            return BuffRemovalOutcome.Removed;
        }

        /// <summary>
        /// Empties NCU. Used by death and playfield exit, which ignore
        /// <see cref="ItemTemplate.CanCancel"/>.
        /// </summary>
        public List<Buff> RemoveAllBuffs(BuffRemovalReason reason)
        {
            if (_buffs.Count == 0)
                return [];

            var removed = new List<Buff>(_buffs);
            _buffs.Clear();
            OnBuffsChanged();
            return removed;
        }

        /// <summary>Removes and returns every buff <paramref name="match"/> selects. Ignores <see cref="ItemTemplate.CanCancel"/>.</summary>
        public List<Buff> RemoveBuffs(Func<Buff, bool> match)
        {
            ArgumentNullException.ThrowIfNull(match);
            List<Buff>? removed = null;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (!match(_buffs[i]))
                    continue;

                removed ??= [];
                removed.Add(_buffs[i]);
                _buffs.RemoveAt(i);
            }

            if (removed == null)
                return [];

            OnBuffsChanged();
            return removed;
        }

        /// <summary>Removes and returns every buff whose deadline has passed.</summary>
        public List<Buff> DrainExpiredBuffs(DateTime nowUtc)
        {
            List<Buff>? expired = null;
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (!_buffs[i].IsExpired(nowUtc))
                    continue;

                expired ??= [];
                expired.Add(_buffs[i]);
                _buffs.RemoveAt(i);
            }

            if (expired == null)
                return [];

            OnBuffsChanged();
            return expired;
        }

        void OnBuffsChanged()
        {
            SyncUsedNcu();
            MarkRebaseDirty();
            SnapshotActiveNanosForPersistence();
        }

        void SyncUsedNcu()
        {
            int used = 0;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].ConsumesNcu)
                    used += _buffs[i].NcuCost;
            }

            if (used == UsedNcu)
                return;

            UsedNcu = used;
            Stats.Set(CharacterStat.CurrentNCU, used, StatDetail.Base, dirty: true);
        }

        readonly object _activeNanoDirtyGate = new();
        List<ActiveNanoRecord>? _dirtyActiveNanos;

        public bool HasDirtyActiveNanos
        {
            get
            {
                lock (_activeNanoDirtyGate)
                    return _dirtyActiveNanos != null;
            }
        }

        /// <summary>
        /// Snapshots NCU for the write-behind flush, which runs off the tick thread and so must
        /// never walk the live buff list. Only players persist NCU.
        /// </summary>
        void SnapshotActiveNanosForPersistence()
        {
            if (this is not Player player)
                return;

            var snapshot = new List<ActiveNanoRecord>(_buffs.Count);
            for (int i = 0; i < _buffs.Count; i++)
            {
                Buff buff = _buffs[i];
                snapshot.Add(
                    new ActiveNanoRecord
                    {
                        NanoId = buff.Id,
                        Strain = buff.NanoStrain,
                        NanoInstance = buff.NanoInstance,
                        DurationCentiseconds = buff.DurationCentiseconds,
                        ExpiresAtUtcTicks = buff.ExpiresAtUtc.Ticks
                    });
            }

            lock (_activeNanoDirtyGate)
                _dirtyActiveNanos = snapshot;

            Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(player);
        }

        /// <summary>Takes ownership of the pending NCU snapshot; null when nothing changed.</summary>
        public List<ActiveNanoRecord>? TakeDirtyActiveNanos()
        {
            lock (_activeNanoDirtyGate)
            {
                List<ActiveNanoRecord>? snapshot = _dirtyActiveNanos;
                _dirtyActiveNanos = null;
                return snapshot;
            }
        }

        /// <summary>
        /// Returns a failed snapshot for a later retry. A newer snapshot wins: it already
        /// describes the current NCU set.
        /// </summary>
        public void RestoreDirtyActiveNanos(List<ActiveNanoRecord>? snapshot)
        {
            if (snapshot == null)
                return;

            lock (_activeNanoDirtyGate)
                _dirtyActiveNanos ??= snapshot;
        }

        /// <summary>
        /// NCU entries for a spawn packet, so a client that just gained visibility sees the same
        /// buffs and remaining durations as one that watched them land.
        /// </summary>
        protected ActiveNano[] BuildActiveNanos()
        {
            if (_buffs.Count == 0)
                return [];

            DateTime nowUtc = DateTime.UtcNow;
            var nanos = new ActiveNano[_buffs.Count];
            for (int i = 0; i < _buffs.Count; i++)
            {
                Buff buff = _buffs[i];
                int remaining = buff.RemainingCentiseconds(nowUtc);
                nanos[i] = new ActiveNano
                {
                    NanoIdentity = new Identity
                    {
                        Type = IdentityType.NanoProgram,
                        Instance = buff.Id
                    },
                    NanoInstance = buff.NanoInstance,
                    Time1 = remaining,
                    Time2 = remaining
                };
            }

            return nanos;
        }

        /// <summary>
        /// Adds active buff bonuses. Runs at the end of a rebase, after the equipment pass has
        /// cleared bonuses, so buffs never need an inverse operation when they drop.
        /// </summary>
        protected void ApplyBuffBonuses()
        {
            for (int i = 0; i < _buffs.Count; i++)
                StatModifierSpells.Apply(_buffs[i].ModifierSpells, Stats);
        }

        /// <summary>
        /// Requests a stat rebase at the next safe point in the tick. Batched so a burst of buff
        /// changes recomputes bonuses once. Playfield-less characters rebase inline.
        /// </summary>
        public void MarkRebaseDirty()
        {
            Playfield? playfield = Playfield;
            if (playfield == null)
            {
                RebaseStats();
                return;
            }

            playfield.QueueRebase(this);
        }

        #endregion

        public abstract void Rebase();

        /// <summary>
        /// Recomputes the bonus layer and anything derived from it, without touching weapons.
        /// Buff changes take this path: re-arming would reset swing timers mid-fight.
        /// </summary>
        public abstract void RebaseStats();

        public abstract void RebaseWeapons();

        protected static double NormalizeDelayCentisecondsToSeconds(int delayCentiseconds, double fallbackSeconds)
        {
            if (delayCentiseconds <= 0)
                return fallbackSeconds;
            if (delayCentiseconds > 500)
                delayCentiseconds = 100;
            return Math.Max(0.05, delayCentiseconds / 100.0);
        }

        protected void ArmFromItem(WeaponSlot slot, Item item, int wireSlot = -1, string? sawHash = null)
        {
            ArgumentNullException.ThrowIfNull(item);

            var weapon = new CharacterWeapon
            {
                Item = item,
                WireSlot = wireSlot
            };

            if (wireSlot >= 0 && TryPackSawHash(sawHash, out int tag, out string tagName))
            {
                weapon.SawTag = tag;
                weapon.SawTagName = tagName;
            }

            weapon.ConfigureBaseSpeeds(
                NormalizeDelayCentisecondsToSeconds(
                    item.GetStat(CharacterStat.AttackDelay),
                    CharacterWeapon.DefaultAttackSpeedSeconds),
                NormalizeDelayCentisecondsToSeconds(
                    item.GetStat(CharacterStat.RechargeDelay),
                    CharacterWeapon.DefaultRechargeSpeedSeconds));
            SetWeapon(slot, weapon);
        }

        /// <summary>Packs a 4-char SAW hash (e.g. SIW1) into the AttackInfo/SAW int + name.</summary>
        protected static bool TryPackSawHash(string? hash, out int tag, out string tagName)
        {
            tag = 0;
            tagName = string.Empty;
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            tagName = hash.Trim().ToUpperInvariant();
            if (tagName.Length > 4)
                tagName = tagName.Substring(0, 4);
            else if (tagName.Length < 4)
                tagName = tagName.PadRight(4);

            byte[] bytes = Encoding.ASCII.GetBytes(tagName);
            tag = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
            return tag != 0;
        }

        protected void ArmMartialArtsFist(IItemBuilder items, WeaponSlot slot)
        {
            ArgumentNullException.ThrowIfNull(items);

            Profession profession = (Profession)Stats.GetOrZero(CharacterStat.Profession);
            int maSkill = Stats.GetOrOne(CharacterStat.MartialArts);

            (int lowId, int highId, int quality) = MartialArtsFistResolver.Resolve(profession, maSkill);
            Item fist = items.Create(lowId, highId, quality, ItemSource.Other);
            ArmFromItem(slot, fist);
            if (Weapons.TryGetValue(slot, out CharacterWeapon? armed) && armed != null)
                armed.IsSyntheticFist = true;
        }

        /// <summary>
        /// Shared end of RebaseWeapons after hand slots are considered.
        /// </summary>
        protected void FinishWeaponRebase(IItemBuilder items, bool armedMain, bool armedOff, bool maCombined)
        {
            ArgumentNullException.ThrowIfNull(items);

            if (!armedMain && !armedOff)
            {
                ArmMartialArtsFist(items, WeaponSlot.MainHand);
                ResetAllWeaponAttacks();
                return;
            }

            if (maCombined)
                ArmMartialArtsFist(items, WeaponSlot.CombinedMA);

            ResetAllWeaponAttacks();
        }

        #region Appearance

        const int ShowSocialVisualFlag = 0x20;
        const int SocialOnlyVisualFlag = 0x40;

        readonly Dictionary<int, int> _spawnTextures = new();
        readonly Dictionary<int, int> _wearTextures = new();
        readonly List<Mesh> _spawnMeshes = new();
        readonly Dictionary<(int Position, int Layer), Mesh> _wearMeshes = new();
        readonly Dictionary<int, Mesh> _handMeshes = new();
        readonly List<AOTextures> _textures = new();
        readonly List<Mesh> _meshes = new();
        byte[]? _extendedTextureOverrideData;
        bool _appearanceViewStale = true;

        /// <summary>
        /// Texture places on the wire: spawn/template places overlaid by worn cloth.
        /// </summary>
        public IReadOnlyList<AOTextures> Textures
        {
            get
            {
                RefreshAppearanceView();
                return _textures;
            }
        }

        /// <summary>
        /// Meshes on the wire: spawn/template meshes, worn cloth, then hand weapons.
        /// </summary>
        public IReadOnlyList<Mesh> Meshes
        {
            get
            {
                RefreshAppearanceView();
                return _meshes;
            }
        }

        /// <summary>True once the wire appearance changed and no announce has claimed it yet.</summary>
        public bool AppearanceDirty { get; private set; }

        public bool ConsumeAppearanceDirty()
        {
            bool dirty = AppearanceDirty;
            AppearanceDirty = false;
            return dirty;
        }

        /// <summary>
        /// Template or authored-content texture, used by characters whose look is declared rather
        /// than derived from worn items.
        /// </summary>
        public void SetSpawnTexture(int place, int textureId)
        {
            if (place < 0)
                return;

            _spawnTextures[place] = textureId;
            _appearanceViewStale = true;
        }

        /// <summary>Template-authored SCFU extended texture override data.</summary>
        public void SetExtendedTextureOverrideData(byte[]? data)
        {
            if (data == null || data.Length == 0)
            {
                _extendedTextureOverrideData = null;
                return;
            }

            _extendedTextureOverrideData = (byte[])data.Clone();
        }

        /// <summary>Template or authored-content mesh.</summary>
        public void AddSpawnMesh(Mesh mesh)
        {
            _spawnMeshes.Add(mesh);
            _appearanceViewStale = true;
        }

        /// <summary>Hand weapon mesh, owned by the weapon pass of a rebase.</summary>
        protected bool SetHandMesh(int position, int meshId, int overrideTextureId)
        {
            var mesh = new Mesh
            {
                Position = (byte)position,
                Id = (uint)meshId,
                OverrideTextureId = overrideTextureId,
                Layer = (byte)MeshLayer.Equipment
            };

            if (_handMeshes.TryGetValue(position, out Mesh existing)
                && existing.Id == mesh.Id
                && existing.OverrideTextureId == mesh.OverrideTextureId
                && existing.Layer == mesh.Layer)
            {
                return false;
            }

            _handMeshes[position] = mesh;
            InvalidateAppearance();
            return true;
        }

        protected bool ClearHandMesh(int position)
        {
            if (!_handMeshes.Remove(position))
                return false;

            InvalidateAppearance();
            return true;
        }

        /// <summary>
        /// Pages whose OnWear spells drive this character's look, applied in order so a later page
        /// overrides an earlier one.
        /// </summary>
        protected abstract IEnumerable<Container> AppearanceWearPages { get; }

        protected bool ShowSocialAppearance => (VisualFlagsOrZero() & ShowSocialVisualFlag) != 0;

        protected bool SocialOnlyAppearance =>
            ShowSocialAppearance && (VisualFlagsOrZero() & SocialOnlyVisualFlag) != 0;

        int VisualFlagsOrZero()
        {
            int flags = Stats.Get(CharacterStat.VisualFlags);
            return StatCollection.IsUnset(flags) || flags < 0 ? 0 : flags;
        }

        /// <summary>
        /// Recomputes worn textures and meshes from <see cref="AppearanceWearPages"/>. Hand
        /// positions are untouched: those follow weapon stats in <see cref="RebaseWeapons"/>, which
        /// a stats-only rebase does not run.
        /// </summary>
        protected void RebaseWearAppearance()
        {
            var textures = new Dictionary<int, int>();
            var meshes = new Dictionary<(int Position, int Layer), Mesh>();

            foreach (Container page in AppearanceWearPages)
            {
                if (page == null)
                    continue;

                foreach (KeyValuePair<int, Item> slot in page.EnumerateSlots())
                    ApplyWearAppearance(slot.Key, slot.Value, textures, meshes);
            }

            if (SameWearAppearance(textures, meshes))
                return;

            _wearTextures.Clear();
            foreach (KeyValuePair<int, int> texture in textures)
                _wearTextures[texture.Key] = texture.Value;

            _wearMeshes.Clear();
            foreach (KeyValuePair<(int Position, int Layer), Mesh> mesh in meshes)
                _wearMeshes[mesh.Key] = mesh.Value;

            InvalidateAppearance();
        }

        void ApplyWearAppearance(
            int slot,
            Item item,
            Dictionary<int, int> textures,
            Dictionary<(int Position, int Layer), Mesh> meshes)
        {
            foreach (ItemSpell spell in item.WearSpells)
            {
                if (!spell.MeetsRequirements(Stats))
                    continue;

                if (spell.Is(FunctionType.Texture))
                {
                    if (spell.TryReadTexture(out int place, out int textureId) && place >= 0 && textureId > 0)
                        textures[place] = textureId;

                    continue;
                }

                if (TryReadWearMesh(slot, item, spell, out Mesh mesh))
                    meshes[(mesh.Position, mesh.Layer)] = mesh;
            }
        }

        static bool TryReadWearMesh(int slot, Item item, ItemSpell spell, out Mesh mesh)
        {
            mesh = default;

            MeshLayer layer;
            if (spell.Is(FunctionType.AttractorMesh))
            {
                // Live SCFU stacks an attractor below the head instead of replacing it.
                layer = MeshLayer.Head;
            }
            else if (spell.Is(FunctionType.Mesh)
                || spell.Is(FunctionType.HeadMesh)
                || spell.Is(FunctionType.BackMesh)
                || spell.Is(FunctionType.Shouldermesh))
            {
                layer = MeshLayer.Equipment;
            }
            else
            {
                return false;
            }

            if (!spell.TryReadMesh(out int meshId, out int overrideTextureId) || meshId <= 0)
                return false;
            if (!TryResolveMeshPosition(slot, spell, out int position))
                return false;

            if (ItemBehaviorContent.Current.TryResolveWornMeshOverride(
                    item,
                    meshId,
                    overrideTextureId,
                    out int resolvedOverrideTextureId))
            {
                overrideTextureId = resolvedOverrideTextureId;
            }

            mesh = new Mesh
            {
                Position = (byte)position,
                Id = (uint)meshId,
                OverrideTextureId = overrideTextureId,
                Layer = (byte)layer
            };
            return true;
        }

        /// <summary>
        /// Wear page slot numbers are the client placements (armor 17-31, social 49-63), so the slot
        /// gives the mesh position directly. Hands are excluded; weapon stats own those.
        /// </summary>
        static bool TryResolveMeshPosition(int slot, ItemSpell spell, out int position)
        {
            switch (slot)
            {
                case 18:
                case 50:
                    position = 0;
                    return true;
                case 19:
                case 51:
                    position = 5;
                    return true;
                case 20:
                case 52:
                    position = 3;
                    return true;
                case 22:
                case 54:
                    position = 4;
                    return true;
                case 6:
                case 8:
                case 56:
                case 58:
                    position = 0;
                    return false;
                default:
                    if (spell.Is(FunctionType.BackMesh))
                    {
                        position = 5;
                        return true;
                    }

                    if (spell.Is(FunctionType.Shouldermesh))
                    {
                        position = 3;
                        return true;
                    }

                    if (spell.Is(FunctionType.HeadMesh) || spell.Is(FunctionType.AttractorMesh))
                    {
                        position = 0;
                        return true;
                    }

                    position = 0;
                    return false;
            }
        }

        bool SameWearAppearance(
            Dictionary<int, int> textures,
            Dictionary<(int Position, int Layer), Mesh> meshes)
        {
            if (textures.Count != _wearTextures.Count || meshes.Count != _wearMeshes.Count)
                return false;

            foreach (KeyValuePair<int, int> texture in textures)
            {
                if (!_wearTextures.TryGetValue(texture.Key, out int current) || current != texture.Value)
                    return false;
            }

            foreach (KeyValuePair<(int Position, int Layer), Mesh> mesh in meshes)
            {
                if (!_wearMeshes.TryGetValue(mesh.Key, out Mesh current)
                    || current.Id != mesh.Value.Id
                    || current.OverrideTextureId != mesh.Value.OverrideTextureId)
                    return false;
            }

            return true;
        }

        void InvalidateAppearance()
        {
            _appearanceViewStale = true;
            AppearanceDirty = true;
        }

        void RefreshAppearanceView()
        {
            if (!_appearanceViewStale)
                return;

            _appearanceViewStale = false;

            _textures.Clear();
            if (_spawnTextures.Count > 0 || _wearTextures.Count > 0)
            {
                var places = new SortedSet<int>(_spawnTextures.Keys);
                foreach (int place in _wearTextures.Keys)
                    places.Add(place);

                foreach (int place in places)
                {
                    int textureId = _wearTextures.TryGetValue(place, out int worn)
                        ? worn
                        : _spawnTextures[place];
                    _textures.Add(new AOTextures(place, textureId));
                }
            }

            _meshes.Clear();
            foreach (Mesh mesh in _spawnMeshes)
            {
                if (!_wearMeshes.ContainsKey((mesh.Position, mesh.Layer)))
                    _meshes.Add(mesh);
            }

            foreach (KeyValuePair<(int Position, int Layer), Mesh> mesh in
                _wearMeshes.OrderBy(entry => entry.Key.Position).ThenBy(entry => entry.Key.Layer))
                _meshes.Add(mesh.Value);

            foreach (KeyValuePair<int, Mesh> mesh in _handMeshes.OrderBy(entry => entry.Key))
                _meshes.Add(mesh.Value);
        }

        #endregion

        public List<int> UploadedNanoIds { get; } = new();

        readonly List<int> _dirtyUploadedNanoIds = [];
        readonly object _uploadedNanoDirtyGate = new();

        public bool HasDirtyUploadedNanos
        {
            get
            {
                lock (_uploadedNanoDirtyGate)
                    return _dirtyUploadedNanoIds.Count > 0;
            }
        }

        /// <summary>Adds <paramref name="nanoId"/> when missing. Returns false for invalid or duplicate ids.</summary>
        public bool TryAddUploadedNano(int nanoId)
        {
            if (nanoId <= 0 || UploadedNanoIds.Contains(nanoId))
                return false;

            UploadedNanoIds.Add(nanoId);
            return true;
        }

        public void MarkUploadedNanoDirty(int nanoId)
        {
            if (nanoId <= 0)
                return;

            lock (_uploadedNanoDirtyGate)
            {
                if (!_dirtyUploadedNanoIds.Contains(nanoId))
                    _dirtyUploadedNanoIds.Add(nanoId);
            }
        }

        public int[] DrainDirtyUploadedNanos()
        {
            lock (_uploadedNanoDirtyGate)
            {
                if (_dirtyUploadedNanoIds.Count == 0)
                    return [];

                int[] drained = _dirtyUploadedNanoIds.ToArray();
                _dirtyUploadedNanoIds.Clear();
                return drained;
            }
        }

        public void RestoreDirtyUploadedNanos(IReadOnlyList<int> nanoIds)
        {
            ArgumentNullException.ThrowIfNull(nanoIds);

            lock (_uploadedNanoDirtyGate)
            {
                for (int i = 0; i < nanoIds.Count; i++)
                {
                    int nanoId = nanoIds[i];
                    if (nanoId > 0 && !_dirtyUploadedNanoIds.Contains(nanoId))
                        _dirtyUploadedNanoIds.Add(nanoId);
                }
            }
        }

        /// <summary>
        /// Equipped-hand WeaponItemFullUpdate messages for observers (after SCFU).
        /// Default empty; <see cref="Player"/> and <see cref="NpcCharacter"/> override.
        /// </summary>
        public virtual List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
            => new();

        /// <summary>
        /// Builds one WeaponInstance for an equipped hand-slot item, or null when it should not be announced.
        /// Only a visible weapon (non-zero weapon mesh on that hand) is sent.
        /// Fists and invisible NPC tag weapons are omitted — AttackInfo/SAW carry those hits.
        /// </summary>
        protected WeaponItemFullUpdateMessage? TryBuildWeaponItemFullUpdate(
            Item item,
            int equipmentSlot,
            CharacterWeapon? armed = null,
            bool visibleHand = false)
        {
            if (!AttackInfoRules.HasVisibleWeaponMesh(item, equipmentSlot))
                return null;
            if (visibleHand && !item.IsWieldableCombatWeapon())
                return null;
            if (!visibleHand && !AttackInfoRules.ShouldAnnounceWeaponItemFullUpdate(item, armed))
                return null;

            int weaponInstanceId = Playfield != null
                ? Playfield.AllocateWeaponInstanceId()
                : item.InstanceId;
            int playfieldId = Playfield != null ? Playfield.Identity.Instance : 0;

            int flags = item.Flags > 0 ? item.Flags : 0x403;
            int multipleCount = item.StackCount > 0 ? item.StackCount : 1;
            var stats = new List<GameTuple<CharacterStat, uint>>
            {
                StatTuple(CharacterStat.Flags, (uint)flags),
                StatTuple(CharacterStat.StaticInstance, (uint)item.LowId),
                StatTuple(CharacterStat.ACGItemLevel, (uint)item.Quality),
                StatTuple(CharacterStat.ACGItemTemplateID, (uint)item.LowId),
                StatTuple(CharacterStat.ACGItemTemplateID2, (uint)item.HighId),
                StatTuple(CharacterStat.MultipleCount, (uint)multipleCount),
                StatTuple(CharacterStat.Energy, 0)
            };

            int attackDelay = item.GetStat(CharacterStat.AttackDelay);
            if (attackDelay > 0)
                stats.Add(StatTuple(CharacterStat.AttackDelay, (uint)attackDelay));

            int rechargeDelay = item.GetStat(CharacterStat.RechargeDelay);
            if (rechargeDelay > 0)
                stats.Add(StatTuple(CharacterStat.RechargeDelay, (uint)rechargeDelay));

            int damageType = item.GetStat(CharacterStat.DamageType);
            if (damageType > 0)
                stats.Add(StatTuple(CharacterStat.DamageType, (uint)damageType));

            return new WeaponItemFullUpdateMessage
            {
                Identity = new Identity
                {
                    Type = IdentityType.WeaponInstance,
                    Instance = weaponInstanceId
                },
                Unknown = 0,
                Unknown1 = 0x0b,
                Owner = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = Identity.Instance
                },
                PlayfieldId = playfieldId,
                StateMachine = new Identity
                {
                    Type = (IdentityType)0x000F424F,
                    Instance = 0
                },
                Unknown2 = (short)(0x0100 | (equipmentSlot & 0xff)),
                Stats = stats.ToArray(),
                Unknown3 = 0
            };
        }

        static GameTuple<CharacterStat, uint> StatTuple(CharacterStat stat, uint value)
            => new() { Value1 = stat, Value2 = value };

        /// <summary>
        /// Builds an AppearanceUpdate from current textures, meshes, and visual flags.
        /// </summary>
        public AppearanceUpdateMessage BuildAppearanceUpdateMessage()
        {
            int visualFlags = Stats.Get(CharacterStat.VisualFlags);
            int headMesh = Stats.Get(CharacterStat.HeadMesh);
            bool isNpc = !IsPlayer;
            short wireVisualFlags = StatCollection.IsUnset(visualFlags)
                ? (isNpc ? (short)31 : (short)0)
                : (short)visualFlags;

            return new AppearanceUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                Textures = BuildTextures(isNpc),
                Meshes = BuildMeshes(headMesh),
                VisualFlags = wireVisualFlags,
                Unknown1 = 0
            };
        }

        /// <summary>
        /// Builds the CharacterAction InfoRequest response for this character.
        /// </summary>
        public abstract InfoPacketMessage BuildInfoPacket();

        protected InfoPacketMessage BuildCharacterInfoPacket(byte n3Unknown, InfoPacketType type, string firstName, string lastName)
        {
            int level = Stats.GetOrOne(CharacterStat.Level);
            int profession = ClampProfession(Stats.GetOrZero(CharacterStat.Profession));
            int visualProfession = ClampProfession(Stats.GetOrZero(CharacterStat.VisualProfession));
            int health = Math.Max(0, Stats.GetOrZero(CharacterStat.Health));
            int maxHealth = Math.Max(1, Stats.GetOrZero(CharacterStat.MaxHealth));
            if (health > maxHealth)
                health = maxHealth;

            return new InfoPacketMessage
            {
                Identity = Identity,
                Unknown = n3Unknown,
                Type = type,
                Info = new CharacterInfoPacket
                {
                    Unknown1 = 0x01,
                    Profession = (Profession)profession,
                    Level = ClampToByte(level),
                    TitleLevel = ClampToByte(Stats.GetOrOne(CharacterStat.TitleLevel)),
                    VisualProfession = (Profession)visualProfession,
                    SideXp = 0,
                    Health = health,
                    MaxHealth = maxHealth,
                    BreedHostility = 0,
                    FirstName = firstName ?? string.Empty,
                    LastName = lastName ?? string.Empty,
                    LegacyTitle = string.Empty,
                    PvpTitle = string.Empty,
                    CityPlayfieldId = 0,
                    InvadersKilled = Stats.GetOrZero(CharacterStat.InvadersKilled),
                    KilledByInvaders = Stats.GetOrZero(CharacterStat.KilledByInvaders),
                    AiLevel = Stats.GetOrZero(CharacterStat.AlienLevel),
                    PvpDuelWins = 0,
                    PvpDuelLoses = 0,
                    PvpProfessionDuelLoses = 0,
                    PvpSoloKills = 0,
                    PvpTeamKills = 0,
                    PvpSoloScore = 0,
                    PvpTeamScore = 0,
                    PvpDuelScore = 0
                }
            };
        }

        /// <summary>
        /// Builds a SimpleCharFullUpdate (SCFU) spawn packet from current character state.
        /// Structure follows ZoneEngine SimpleCharFullUpdate.ConstructMessage without capture/runtime special cases.
        /// </summary>
        public override SimpleCharFullUpdateMessage BuildSpawnMessage()
        {
            int visualFlags = Stats.Get(CharacterStat.VisualFlags);
            // Social cloth/mesh path not implemented yet.
            // bool socialOnly = (visualFlags & 0x40) != 0;
            // bool showSocial = (visualFlags & 0x20) != 0;

            int playfieldId = Playfield != null ? Playfield.Identity.Instance : 0;

            // Predicted movement not implemented; use current transform.
            MsgVector3 coordinates = Position ?? new MsgVector3();
            MsgQuaternion heading = Rotation ?? new MsgQuaternion();

            string? name = Name;
            int characterFlags = Stats.Get(CharacterStat.Flags);
            if (Stats.TryGetValue(CharacterStat.GmLevel, out int gmLevel) && gmLevel > 0)
            {
                characterFlags &= ~(int)CharacterFlags.NpcStyleFlag28;
                characterFlags |= (int)CharacterFlags.HasBlueName;
                if (name != null
                    && name.IndexOf("[GM]", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    name += " [GM]";
                }
            }

            int maxHealth = Stats.GetOrZero(CharacterStat.MaxHealth);
            int currentHealth = Stats.GetOrZero(CharacterStat.Health);
            int monsterData = Stats.Get(CharacterStat.MonsterData);
            int monsterScale = Stats.GetOrZero(CharacterStat.Scale);

            int petMasterInstance = Stats.Get(CharacterStat.PetMaster);
            int headMesh = Stats.Get(CharacterStat.HeadMesh);
            int runSpeedBase = Stats.GetOrZero(CharacterStat.RunSpeed, StatDetail.Base);
            int npcFamily = Stats.Get(CharacterStat.NPCFamily);
            int losHeight = Stats.GetOrZero((CharacterStat)466);
            bool isNpc = !IsPlayer
                && !StatCollection.IsUnset(npcFamily)
                && npcFamily != 0;

            // Unset VisualFlags truncates to 722 on the wire; live NPC SCFUs use 31.
            short wireVisualFlags = StatCollection.IsUnset(visualFlags)
                ? (isNpc ? (short)31 : (short)0)
                : (short)visualFlags;

            int race = Stats.Get(CharacterStat.Race, StatDetail.Base);
            int expansions = Stats.Get(CharacterStat.Expansion);

            var scfu = new SimpleCharFullUpdateMessage
            {
                Identity = Identity,
                Version = 58,
                PlayfieldId = playfieldId,
                Coordinates = coordinates,
                Heading = heading,
                Appearance = new Appearance
                {
                    Side = (Side)Stats.GetOrZero(CharacterStat.Side, StatDetail.Base),
                    Fatness = (Fatness)Stats.GetOrZero(CharacterStat.Fatness, StatDetail.Base),
                    Breed = (Breed)Stats.GetOrZero(CharacterStat.Breed, StatDetail.Base),
                    Gender = (Gender)Stats.GetOrZero(CharacterStat.Sex, StatDetail.Base),
                    // Unset race defaults to 1 (not 0) for a valid Appearance.
                    Race = (uint)(StatCollection.IsUnset(race) ? 1 : race)
                },
                Name = name,
                CharacterFlags = (CharacterFlags)characterFlags,
                AccountFlags = (short)Stats.GetOrZero(CharacterStat.AccountFlags),
                Expansions = StatCollection.IsUnset(expansions)
                    ? (isNpc ? (short)3 : (short)0)
                    : (short)expansions,
                Level = (short)Stats.GetOrZero(CharacterStat.Level),
                MonsterScale = (short)monsterScale,
                VisualFlags = wireVisualFlags,
                VisibleTitle = 0,
                RunSpeedBase = (short)runSpeedBase,
                Flags2 = 0,
                Unknown2 = 0,
                ActiveNanos = BuildActiveNanos(),
                Waypoints = Motor.CopyRemainingWaypoints(),
                ExtendedTextureOverrideData = CopyExtendedTextureOverrideData(),
                Textures = BuildTextures(isNpc),
                Meshes = BuildMeshes(headMesh)
            };

            // Pets keep version 58 (already set for NPCs).
            if (!StatCollection.IsUnset(petMasterInstance) && petMasterInstance != 0)
            {
                scfu.Version = 58;
            }

            if (FightingTarget.Instance != 0)
                scfu.FightingTarget = FightingTarget;

            if (isNpc)
            {
                scfu.CharacterInfo = new SimpleNpcInfo
                {
                    Family = (short)npcFamily,
                    LosHeight = (short)losHeight
                };

                scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownDataFlag;
                scfu.SuppressedFlags |= SimpleCharFullUpdateFlags.UnknownFlag2;
            }
            else
            {
                var pcInfo = new SimplePcInfo
                {
                    CurrentNano = (uint)Stats.GetOrZero(CharacterStat.CurrentNano),
                    Team = 0,
                    Swim = 5,
                    StrengthBase = ClampToShort(Stats.GetOrZero(CharacterStat.Strength, StatDetail.Base)),
                    AgilityBase = ClampToShort(Stats.GetOrZero(CharacterStat.Agility, StatDetail.Base)),
                    StaminaBase = ClampToShort(Stats.GetOrZero(CharacterStat.Stamina, StatDetail.Base)),
                    IntelligenceBase = ClampToShort(Stats.GetOrZero(CharacterStat.Intelligence, StatDetail.Base)),
                    SenseBase = ClampToShort(Stats.GetOrZero(CharacterStat.Sense, StatDetail.Base)),
                    PsychicBase = ClampToShort(Stats.GetOrZero(CharacterStat.Psychic, StatDetail.Base))
                };

                // Existing DAO character projection owns these strings. The codec writes
                // both only when HasVisibleName is set; missing names are legitimate empty strings.
                if (this is Player namedPlayer && scfu.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
                {
                    pcInfo.FirstName = namedPlayer.FirstName ?? string.Empty;
                    pcInfo.LastName = namedPlayer.LastName ?? string.Empty;
                }
                // if (!string.IsNullOrEmpty(OrganizationName))
                // {
                //     pcInfo.OrgName = OrganizationName;
                // }

                scfu.CharacterInfo = pcInfo;
            }

            int displayMaxHealth = maxHealth;
            int displayCurrentHealth = currentHealth;
            if (maxHealth > ushort.MaxValue)
            {
                displayMaxHealth = ushort.MaxValue;
                if (maxHealth > 0)
                {
                    displayCurrentHealth = (int)((long)currentHealth * ushort.MaxValue / maxHealth);
                    if (displayCurrentHealth < 0)
                    {
                        displayCurrentHealth = 0;
                    }
                    else if (displayCurrentHealth > displayMaxHealth)
                    {
                        displayCurrentHealth = displayMaxHealth;
                    }
                }
                else
                {
                    displayCurrentHealth = 0;
                }
            }

            scfu.Health = displayMaxHealth;
            scfu.HealthDamage = displayMaxHealth - displayCurrentHealth;

            if (Playfield?.GetService<IGameData>()
                    ?.TryGetPlayfieldCharacterAppearanceOverride(playfieldId, out uint appearanceMonsterData) == true)
            {
                scfu.MonsterData = appearanceMonsterData;
            }
            else if (!StatCollection.IsUnset(monsterData) && monsterData != 0)
            {
                scfu.MonsterData = (uint)monsterData;
            }
            else
            {
                scfu.MonsterData = 0;
            }

            scfu.MonsterScale = (short)monsterScale;
            scfu.MovementStatus = Motor.BuildMovementStatus();

            if (!StatCollection.IsUnset(petMasterInstance) && petMasterInstance != 0)
            {
                scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6
                    | SimpleCharFullUpdateFlags.IsPet
                    | SimpleCharFullUpdateFlags.UnknownDataFlag;
            }

            if (!StatCollection.IsUnset(headMesh) && headMesh != 0)
            {
                scfu.HeadMesh = (uint)headMesh;
            }

            return scfu;
        }

        internal byte[]? CopyExtendedTextureOverrideData()
            => _extendedTextureOverrideData == null || _extendedTextureOverrideData.Length == 0
                ? null
                : (byte[])_extendedTextureOverrideData.Clone();

        /// <summary>Texture places as they appear on this character's own spawn packet.</summary>
        internal Texture[] BuildWireTextures() => BuildTextures(!IsPlayer);

        private Texture[] BuildTextures(bool isNpc)
        {
            // Live Beach Leet SCFU carries zero textures; five zero placeholders break the tail shape.
            if (Textures.Count == 0)
                return isNpc ? [] : CreateDefaultTextures();

            Texture[] textures = isNpc ? new Texture[Textures.Count] : CreateDefaultTextures();
            if (isNpc)
            {
                for (int i = 0; i < Textures.Count; i++)
                {
                    AOTextures entry = Textures[i];
                    textures[i] = new Texture
                    {
                        Place = entry.place,
                        Id = entry.Texture,
                        Unknown = 0
                    };
                }

                return textures;
            }

            foreach (AOTextures entry in Textures)
            {
                if (entry.place < 0 || entry.place >= textures.Length)
                    continue;

                textures[entry.place] = new Texture
                {
                    Place = entry.place,
                    Id = entry.Texture,
                    Unknown = 0
                };
            }

            return textures;
        }

        private Mesh[] BuildMeshes(int headMesh)
        {
            var meshes = new List<Mesh>(Meshes);

            // The head slot is only filled from the stat when nothing worn occupies it: a helmet
            // mesh must survive, and unequipping it brings the character's own head back.
            if (!StatCollection.IsUnset(headMesh) && headMesh != 0 && !HasHeadSlotMesh(meshes))
            {
                meshes.Add(
                    new Mesh
                    {
                        Position = 0,
                        Id = (uint)headMesh,
                        OverrideTextureId = 0,
                        Layer = (byte)MeshLayer.Equipment
                    });
            }

            return meshes.ToArray();
        }

        /// <summary>
        /// The head the character shows: a worn or template head-slot mesh, else the HeadMesh stat. 0 when none.
        /// </summary>
        internal int ResolveShownHeadMesh()
        {
            IReadOnlyList<Mesh> meshes = Meshes;
            for (int i = 0; i < meshes.Count; i++)
            {
                if (meshes[i].Position == 0 && meshes[i].Layer == (byte)MeshLayer.Equipment && meshes[i].Id != 0)
                    return unchecked((int)meshes[i].Id);
            }

            int headMesh = Stats.Get(CharacterStat.HeadMesh);
            return StatCollection.IsUnset(headMesh) ? 0 : headMesh;
        }

        static bool HasHeadSlotMesh(List<Mesh> meshes)
        {
            for (int i = 0; i < meshes.Count; i++)
            {
                if (meshes[i].Position == 0 && meshes[i].Layer == (byte)MeshLayer.Equipment)
                    return true;
            }

            return false;
        }

        protected static byte ClampToByte(int value)
        {
            if (value < 0)
                return 0;
            if (value > byte.MaxValue)
                return byte.MaxValue;
            return (byte)value;
        }

        protected static int ClampProfession(int value)
        {
            if (value < 0)
                return 0;
            if (value > (int)Profession.Shade)
                return (int)Profession.Shade;
            return value;
        }

        private static short ClampToShort(int value) =>
            (short)Math.Clamp(value, short.MinValue, short.MaxValue);

        private static Texture[] CreateDefaultTextures() =>
        [
            new Texture { Place = 0, Id = 0, Unknown = 0 },
            new Texture { Place = 1, Id = 0, Unknown = 0 },
            new Texture { Place = 2, Id = 0, Unknown = 0 },
            new Texture { Place = 3, Id = 0, Unknown = 0 },
            new Texture { Place = 4, Id = 0, Unknown = 0 }
        ];

    }
}
