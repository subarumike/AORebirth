namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

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

        public Player(Identity identity, IZoneLogger logger, IItemBuilder items)
            : base(identity)
        {
            ArgumentNullException.ThrowIfNull(items);
            Logger = logger;
            _items = items;
            Inventory = new PlayerInventory();
        }

        public override bool IsPlayer => true;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public IZoneSession? Session { get; set; }
        internal ZoneEngine_New.Core.Nanos.NanoService? NanoRuntime { get; set; }

        public PlayerConnectionPhase ConnectionPhase { get; set; } = PlayerConnectionPhase.Online;

        /// <summary>UTC deadline while <see cref="PlayerConnectionPhase.LinkDead"/>; null when Online.</summary>
        public DateTime? LinkDeadUntilUtc { get; set; }

        public PlayerInventory Inventory { get; }

        //TODO: Put perks here


        /// <summary>Current look-at / selection target from the client.</summary>
        public Identity Target { get; set; } = Identity.None;

        internal IZoneLogger Logger { get; set; }

        public override void Rebase()
        {
            RebaseEquipBonuses();
            RebaseMaxHealth();
            RebaseMaxNano();
            NanoRuntime?.ReapplyBonusesAfterRebase(this);
            RebaseWeapons();
        }

        void RebaseMaxHealth()
        {
            if (!MaxHealthCalculator.TryCompute(Stats, out int maxHealth))
                return;

            Stats.Set(CharacterStat.MaxHealth, maxHealth, StatDetail.Base, dirty: true);
        }

        void RebaseMaxNano()
        {
            if (!MaxNanoCalculator.TryCompute(Stats, out int maxNano))
                return;

            Stats.Set(CharacterStat.MaxNanoEnergy, maxNano, StatDetail.Base, dirty: true);
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

            if (!SyncHandWeaponMeshes())
                return;

            if (Session?.State != SessionState.InPlay)
                return;

            AppearanceUpdateMessage appearance = BuildAppearanceUpdateMessage();
            Playfield?.GetRequiredService<PlayfieldLocality>().Announce(this, appearance, includeSelf: true);
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

            int existingIndex = -1;
            for (int i = 0; i < Meshes.Count; i++)
            {
                if (Meshes[i].Position != meshPosition)
                    continue;

                existingIndex = i;
                break;
            }

            if (meshId <= 0)
            {
                if (existingIndex < 0)
                    return false;

                Meshes.RemoveAt(existingIndex);
                Stats.Set(meshStat, 0, StatDetail.Base, dirty: true);
                return true;
            }

            if (existingIndex >= 0)
            {
                Mesh existing = Meshes[existingIndex];
                if (existing.Id == (uint)meshId
                    && existing.OverrideTextureId == overrideTexture
                    && existing.Layer == (byte)MeshLayer.Equipment)
                {
                    return false;
                }

                Meshes[existingIndex] = new Mesh
                {
                    Position = meshPosition,
                    Id = (uint)meshId,
                    OverrideTextureId = overrideTexture,
                    Layer = (byte)MeshLayer.Equipment
                };
            }
            else
            {
                Meshes.Add(
                    new Mesh
                    {
                        Position = meshPosition,
                        Id = (uint)meshId,
                        OverrideTextureId = overrideTexture,
                        Layer = (byte)MeshLayer.Equipment
                    });
            }

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

        static readonly CharacterStat[] FullCharacterStats1 =
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
            //CharacterStat.UnreadMailCount,
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

        static readonly CharacterStat[] FullCharacterStats2 =
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

        static readonly CharacterStat[] FullCharacterStats3 =
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

        static readonly CharacterStat[] FullCharacterStats4 =
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

        public override InfoPacketMessage BuildInfoPacket()
        {
            // Player inspect is Character (0x40), same as ZoneEngine CharacterInfoPacket.
            // N3 Unknown=0. Unknown=1 is the monster path and stuck the official client
            // on "Please wait". Reference Unknowns=1 is CharacterInfoPacket.Unknown1.
            // Do not copy Name into FirstName — official 0x40 uses the DB first/last
            // strings (often empty). Unique name already arrives in SCFU.Name.
            int level = Stats.GetOrOne(CharacterStat.Level);
            int profession = ClampProfession(Stats.GetOrZero(CharacterStat.Profession));
            int visualProfession = ClampProfession(Stats.GetOrZero(CharacterStat.VisualProfession));
            int health = Math.Max(0, Stats.GetOrZero(CharacterStat.Health));
            int maxHealth = Math.Max(1, Stats.GetOrZero(CharacterStat.MaxHealth));
            if (health > maxHealth)
                health = maxHealth;

            string firstName = FirstName ?? string.Empty;
            string lastName = LastName ?? string.Empty;

            Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "InfoPacket player={0} name={1} first='{2}' last='{3}' level={4} prof={5} hp={6}/{7}",
                    Identity.Instance,
                    Name ?? string.Empty,
                    firstName,
                    lastName,
                    level,
                    profession,
                    health,
                    maxHealth));

            return new InfoPacketMessage
            {
                Identity = Identity,
                Unknown = 0,
                Type = InfoPacketType.Character,
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
                    OrganizationId = null,
                    FirstName = firstName,
                    LastName = lastName,
                    LegacyTitle = string.Empty,
                    Unknown2 = 0,
                    OrganizationRank = null,
                    TowerFields = null,
                    CityPlayfieldId = 0,
                    Towers = null,
                    InvadersKilled = 0,
                    KilledByInvaders = 0,
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

        static int ClampProfession(int value)
        {
            if (value < 0)
                return 0;
            if (value > (int)Profession.Shade)
                return (int)Profession.Shade;
            return value;
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
            var tuples = new GameTuple<int, uint>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<int, uint>
                {
                    Value1 = (int)id,
                    Value2 = (uint)stats.GetOrZero(id)
                };
            }

            return tuples;
        }

        static GameTuple<byte, byte>[] BuildFullCharacterByteStats(StatCollection stats, CharacterStat[] ids)
        {
            var tuples = new GameTuple<byte, byte>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<byte, byte>
                {
                    Value1 = (byte)id,
                    Value2 = (byte)stats.GetOrZero(id)
                };
            }

            return tuples;
        }

        static GameTuple<byte, short>[] BuildFullCharacterShortStats(StatCollection stats, CharacterStat[] ids)
        {
            var tuples = new GameTuple<byte, short>[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CharacterStat id = ids[i];
                tuples[i] = new GameTuple<byte, short>
                {
                    Value1 = (byte)id,
                    Value2 = (short)stats.GetOrZero(id)
                };
            }

            return tuples;
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
