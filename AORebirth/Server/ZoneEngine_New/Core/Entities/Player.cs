namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
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
        double _respawnRemainingSeconds;
        DateTime _diedAtUtc;

        public Player(Identity identity, IZoneLogger logger, IItemBuilder items)
            : base(identity)
        {
            ArgumentNullException.ThrowIfNull(items);
            Logger = logger;
            _items = items;
            Inventory = new PlayerInventory();
            // Requirement folds use Stats.Get; keep this at 0 so Unset never fails NotBitAnd checks.
            Stats.Set(CharacterStat.SelectedTargetType, 0, StatDetail.Base);
            Stats.BaseChanged += stat =>
            {
                if (!CharacterSaveState.IsCheckpointOnly(stat))
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

        //TODO: Put perks here


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

            Playfield destination = playfield.GetRequiredService<PlayfieldManager>().GetOrCreate(respawn.PlayfieldId);
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
            RebaseEquipBonuses();
            ApplyBuffBonuses();
            SkillCatalog.ApplyTrickle(Stats);
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
        }

        /// <summary>
        /// Armor carries the worn look, Social replaces it per slot once the client asks for social
        /// clothes, and social-only drops the armor layer entirely.
        /// </summary>
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
            AnnounceAppearance();
            return true;
        }

        void AnnounceAppearanceIfChanged()
        {
            if (ConsumeAppearanceDirty())
                SendAppearanceUpdate();
        }

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

            int percent = ResolveVitalPercent(
                CharacterStat.PercentRemainingHealth,
                CharacterStat.Health,
                CharacterStat.MaxHealth);
            Stats.Set(CharacterStat.MaxHealth, maxHealth, StatDetail.Base, dirty: true);
            // Regen, heals and revive cap at the full max; scaling against the base alone would
            // drop a full character below it on every rebase and restart regen.
            ApplyVitalFromPercent(CharacterStat.Health, Stats.GetOrZero(CharacterStat.MaxHealth), percent);
        }

        void RebaseMaxNano()
        {
            if (!MaxNanoCalculator.TryCompute(Stats, out int maxNano))
                return;

            int percent = ResolveVitalPercent(
                CharacterStat.PercentRemainingNano,
                CharacterStat.CurrentNano,
                CharacterStat.MaxNanoEnergy);
            Stats.Set(CharacterStat.MaxNanoEnergy, maxNano, StatDetail.Base, dirty: true);
            ApplyVitalFromPercent(CharacterStat.CurrentNano, Stats.GetOrZero(CharacterStat.MaxNanoEnergy), percent);
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
        /// Hand weapons announce WIFU.
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

            bool maCombined = (right?.IsMaCombinedWeapon() == true) || (left?.IsMaCombinedWeapon() == true);
            FinishWeaponRebase(_items, armedMain, armedOff, maCombined);

            SyncHandWeaponMeshes();

            if (!_inFullRebase)
                AnnounceAppearanceIfChanged();
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
        }

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
            CharacterStat.UnreadMailCount,
            //CharacterStat.LastMailCheckTime,
            CharacterStat.SavedXP,
            CharacterStat.Flags,
            //CharacterStat.Features,
            CharacterStat.ApartmentsAllowed,
            CharacterStat.ApartmentsOwned,
            CharacterStat.Scale,
            CharacterStat.VisualProfession,
            // CharacterStat.NanoAC,
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
            // CharacterStat.MissionBits3,
            // CharacterStat.MissionBits4,
            // CharacterStat.MissionBits5,
            // CharacterStat.MissionBits6,
            // CharacterStat.MissionBits7,
            // CharacterStat.MissionBits8,
            // CharacterStat.MissionBits9,
            // CharacterStat.MissionBits10,
            // CharacterStat.MissionBits11,
            // CharacterStat.MissionBits12,
            CharacterStat.SessionTime,
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
            CharacterStat.CurrentNano,
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
            message.Unknown13 = [];

            // Team / raid conditional blocks not wired yet.
            // message.Unknown10 = ...
            // message.Unknown11 = ...
            // message.Unknown12 = ...
            // message.Unknown13 = ...

            LogFullCharacterInventory(message);
            return message;
        }

        static GameTuple<int, uint>[] BuildFullCharacterIntStats(StatCollection stats, CharacterStat[] ids)
        {
            // These are counted (stat id, value) arrays. Absent optional stats are omitted;
            // an explicit stored zero remains distinct from an absent value.
            ids = ids.Where(id => stats.TryGetValue(id, out _)).ToArray();
            var tuples = new GameTuple<int, uint>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<int, uint>
                {
                    Value1 = (int)id,
                    Value2 = (uint)RequireWireStat(stats, id)
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
                    Value2 = (byte)Math.Clamp(RequireWireStat(stats, id), byte.MinValue, byte.MaxValue)
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
                // Stats4 wire is Int16. Large bases (e.g. InsuranceTime unix timestamps) must not
                // throw OverflowException and block login — same clamp as Character appearance shorts.
                int raw = RequireWireStat(stats, id);
                tuples[i] = new GameTuple<byte, short>
                {
                    Value1 = (byte)id,
                    Value2 = (short)Math.Clamp(raw, short.MinValue, short.MaxValue)
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
