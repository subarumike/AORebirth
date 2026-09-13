namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using AORebirth.Core.Textures;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.GameData;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Why delayed character actions are being interrupted.
    /// Jump cancels most (equip, nano cast). LeavePlayfield cancels every timed action.
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
        const double SoftRangeGraceMeters = 1.5;
        const double HardRangeMultiplier = 3.0;
        const int NormalAttackInfoAmmoCount = 40;
        const int PlayerUnarmedAttackInfoAmmoCount = -1;
        const int PlayerUnarmedAttackInfoWeaponInstance = 100;
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
        }

        //TODO: Put cooldowns here
        //TODO: Put buffs here
        //TODO: Nano casting should live here

        /// <summary>
        /// Subscribe for delayed actions. Honor <see cref="TimedActionInterrupt.LeavePlayfield"/>
        /// always; honor <see cref="TimedActionInterrupt.Jump"/> unless the action survives jump.
        /// </summary>
        public event Action<Character, TimedActionInterrupt>? TimedActionsInterrupted;

        public void InterruptTimedActions(TimedActionInterrupt reason)
        {
            if (reason != TimedActionInterrupt.Movement)
                Playfield?.GetRequiredService<InventoryMoveService>().CancelPending(Identity.Instance);
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

        void ApplyLevelUp(IGameData gameData, int levelBefore, int levelAfter, int lastGain)
        {
            Stats.Set(CharacterStat.TitleLevel, TitleLevelFor(levelAfter), StatDetail.Base, dirty: true);

            int ipGain = TotalIpEarnedAtLevel(levelAfter) - TotalIpEarnedAtLevel(levelBefore);
            if (ipGain > 0)
                Stats.Set(CharacterStat.IP, Stats.GetOrZero(CharacterStat.IP) + ipGain, StatDetail.Base, dirty: true);

            Rebase();
            int maxHealth = Stats.GetOrZero(CharacterStat.MaxHealth);
            if (maxHealth > 0)
                Stats.Set(CharacterStat.Health, maxHealth, StatDetail.Base, dirty: true);

            int maxNano = Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
            if (maxNano > 0)
                Stats.Set(CharacterStat.CurrentNano, maxNano, StatDetail.Base, dirty: true);

            FlushDirtyStats();

            if (this is not Player player || player.Session == null)
                return;

            for (int gained = levelBefore + 1; gained <= levelAfter; gained++)
                player.Session.Send(BuildNewLevelMessage(gameData, gained, lastGain));
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
        }

        public void SetFightingTarget(Identity identity)
        {
            FightingTarget = identity;
            if (identity.Instance == 0)
                ResetAllWeaponAttacks();
        }

        protected virtual void SpawnDeathCorpse()
            => Playfield?.GetRequiredService<SpawnService>().SpawnCorpse(this);

        /// <summary>
        /// Engage auto-attack: SpecialAttackWeapon first so observers have specials, then Attack.
        /// </summary>
        public virtual void StartFighting(Identity target, byte action)
        {
            if (this is Player player && (player.IsPersistenceQuarantined
                || player.NanoRuntime?.IsFightingRestricted(player) == true))
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

                bool martialArtsItem = item.IsMaCombinedWeapon();
                int can = item.GetStat(CharacterStat.Can);
                LogSaw(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "SAW weapon slot={0} name={1} low={2} high={3} ql={4} can=0x{5:X} specials={6} ma={7}",
                        pair.Key,
                        item.Name,
                        item.LowId,
                        item.HighId,
                        item.Quality,
                        can,
                        FormatSpecialCanFlags((CanFlags)(uint)can),
                        martialArtsItem));

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
            Action handler = () => ProcessWeaponSwing(slot);
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
                PassiveRegenCalculator.ComputeHealthDelta(breed, bodyDevelopment),
                PassiveRegenCalculator.ComputeHealthIntervalSeconds(
                    Stats.GetOrZero(CharacterStat.Stamina),
                    sitting),
                CharacterStat.Health,
                CharacterStat.MaxHealth);

            TickOneRegen(
                ref _nanoRegenElapsed,
                deltaTime,
                PassiveRegenCalculator.ComputeNanoDelta(breed, bodyDevelopment),
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
            CharacterWeapon? charging = null;
            foreach (CharacterWeapon weapon in Weapons.Values)
            {
                if (weapon != null && weapon.State == WeaponState.Attacking)
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
                if (weapon != null && weapon.Tick(deltaTime))
                    break;
            }
        }

        void ProcessWeaponSwing(WeaponSlot slot)
        {
            Character? target = TryResolveFightingTarget();
            if (target == null)
                return;

            if (!Weapons.TryGetValue(slot, out CharacterWeapon? characterWeapon) || characterWeapon == null)
                return;

            Item? weapon = characterWeapon.Item;
            double attackRange = characterWeapon.GetAttackRange();

            double distance = Distance3D(target);
            if (distance > attackRange * HardRangeMultiplier)
            {
                if (IsPlayer)
                    SetFightingTarget(Identity.None);
                return;
            }

            if (distance > attackRange + SoftRangeGraceMeters)
                return;

            DamageCalculator.DamageResult result = DamageCalculator.CalculateFromWeapon(this, target, weapon);
            if (!result.IsHit)
            {
                Cell?.Announce(
                    new MissedAttackInfoMessage
                    {
                        Identity = Identity,
                        Unknown1 = -1,
                        Unknown2 = MapAttackInfoWeaponSlot(slot, weapon),
                        Unknown3 = Identity,
                        Unknown4 = target.Identity,
                        Unknown5 = 0
                    });
                return;
            }

            bool killingHit = target.ApplyDamage(this, result.Damage, result.HitType);
            Cell?.Announce(
                new AttackInfoMessage
                {
                    Identity = Identity,
                    Target = target.Identity,
                    Unknown1 = result.Damage,
                    Unknown2 = weapon != null ? NormalAttackInfoAmmoCount : PlayerUnarmedAttackInfoAmmoCount,
                    Unknown3 = MapAttackInfoWeaponSlot(slot, weapon),
                    Unknown4 = killingHit ? 4 : 0,
                    Unknown5 = (int)result.HitType,
                    Unknown6 = weapon != null ? 0 : PlayerUnarmedAttackInfoWeaponInstance
                });
            AnnounceHealthDamage(target, result.Damage);
        }

        /// <summary>
        /// Applies hit-point damage. Returns true when this hit killed the character.
        /// </summary>
        public virtual bool ApplyDamage(Character attacker, int damage, HitType hitType)
        {
            if (_deathNotified || damage <= 0)
                return false;

            int previousHealth = Math.Max(0, Stats.GetOrZero(CharacterStat.Health));
            int newHealth = Math.Max(0, previousHealth - damage);
            int hpRemoved = previousHealth - newHealth;
            if (hpRemoved > 0 && attacker.IsPlayer && !ReferenceEquals(attacker, this))
                _killRewards.Record(attacker.Identity, hpRemoved);

            Stats.Set(CharacterStat.Health, newHealth, StatDetail.Base, dirty: true);

            if (newHealth > 0)
                return false;

            OnDeath(attacker);
            return true;
        }

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

        static int MapAttackInfoWeaponSlot(WeaponSlot slot, Item? weapon)
        {
            if (weapon == null)
                return 0;

            return slot switch
            {
                WeaponSlot.OffHand => (int)WeaponSlots.LeftHand,
                _ => (int)WeaponSlots.Righthand
            };
        }

        void AnnounceHealthDamage(Character target, int damage)
        {
            Cell?.Announce(
                new HealthDamageMessage
                {
                    Identity = target.Identity,
                    Unknown1 = target.Stats.GetOrZero(CharacterStat.Health),
                    Unknown2 = damage,
                    Unknown3 = (int)CharacterStat.Health,
                    Unknown4 = 0,
                    Target = Identity,
                    Unknown5 = 0
                });
        }

        void OnStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            Motor.OnStatChanged(stat, previous, next, isInitialSet);

            if (stat != CharacterStat.AggDef)
                return;

            foreach (CharacterWeapon weapon in Weapons.Values)
                weapon?.RefreshEffectiveSpeeds();
        }


        public abstract void Rebase();

        public abstract void RebaseWeapons();

        protected static double NormalizeDelayCentisecondsToSeconds(int delayCentiseconds, double fallbackSeconds)
        {
            if (delayCentiseconds <= 0)
                return fallbackSeconds;
            if (delayCentiseconds > 500)
                delayCentiseconds = 100;
            return Math.Max(0.05, delayCentiseconds / 100.0);
        }

        protected void ArmFromItem(WeaponSlot slot, Item item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var weapon = new CharacterWeapon { Item = item };
            weapon.ConfigureBaseSpeeds(
                NormalizeDelayCentisecondsToSeconds(
                    item.GetStat(CharacterStat.AttackDelay),
                    CharacterWeapon.DefaultAttackSpeedSeconds),
                NormalizeDelayCentisecondsToSeconds(
                    item.GetStat(CharacterStat.RechargeDelay),
                    CharacterWeapon.DefaultRechargeSpeedSeconds));
            SetWeapon(slot, weapon);
        }

        protected void ArmMartialArtsFist(IItemBuilder items, WeaponSlot slot)
        {
            ArgumentNullException.ThrowIfNull(items);

            Profession profession = (Profession)Stats.GetOrZero(CharacterStat.Profession);
            int maSkill = Stats.GetOrOne(CharacterStat.MartialArts);

            (int lowId, int highId, int quality) = MartialArtsFistResolver.Resolve(profession, maSkill);
            Item fist = items.Create(lowId, highId, quality, ItemSource.Other);
            ArmFromItem(slot, fist);
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

        public List<AOTextures> Textures { get; } = new();
        public List<Mesh> Meshes { get; } = new();
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
        /// Default empty; <see cref="Player"/> builds from inventory; NPCs stub empty for now.
        /// </summary>
        public virtual List<WeaponItemFullUpdateMessage> BuildWeaponInstanceMessages()
            => new();

        /// <summary>
        /// Builds one WIFU for an equipped hand-slot item, or null when the item should not be announced.
        /// </summary>
        protected WeaponItemFullUpdateMessage? TryBuildWeaponItemFullUpdate(Item item, int equipmentSlot)
        {
            if (item == null
                || item.InstanceId == 0
                || !item.IsWieldableCombatWeapon())
                return null;

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

            // TEMP: playfield-scoped incrementing WeaponInstance id (not inventory item.InstanceId).
            return new WeaponItemFullUpdateMessage
            {
                Identity = new Identity
                {
                    Type = IdentityType.WeaponInstance,
                    Instance = Playfield!.AllocateWeaponInstanceId()
                },
                Unknown = 0,
                Unknown1 = 0x0b,
                Owner = new Identity
                {
                    Type = IdentityType.CanbeAffected,
                    Instance = Identity.Instance
                },
                PlayfieldId = Playfield != null ? Playfield.Identity.Instance : 0,
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
                ActiveNanos = [],
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

            // Grid / fixer-grid: upside-down pyramid mesh.
            if (playfieldId == 152 || playfieldId == 4107)
            {
                scfu.MonsterData = 99902;
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

            // ActiveNanos / Waypoints not wired on Character yet.

            return scfu;
        }

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

            if (!StatCollection.IsUnset(headMesh) && headMesh != 0)
            {
                bool replaced = false;
                for (int i = 0; i < meshes.Count; i++)
                {
                    if (meshes[i].Position != 0 || meshes[i].Layer != (byte)MeshLayer.Equipment)
                        continue;

                    meshes[i] = new Mesh
                    {
                        Position = 0,
                        Id = (uint)headMesh,
                        OverrideTextureId = 0,
                        Layer = (byte)MeshLayer.Equipment
                    };
                    replaced = true;
                    break;
                }

                if (!replaced)
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
            }

            return meshes.ToArray();
        }

        protected static byte ClampToByte(int value)
        {
            if (value < 0)
                return 0;
            if (value > byte.MaxValue)
                return byte.MaxValue;
            return (byte)value;
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
