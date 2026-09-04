namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Textures;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    //TODO: Nano casting should live here

    /// <summary>
    /// Character layer between Dynel and <see cref="Player"/> / <see cref="NpcCharacter"/> (shared fields).
    /// </summary>
    public abstract class Character : Dynel
    {
        protected Character(Identity identity)
            : base(identity)
        {
            Motor = new CharacterMotor(this);
            Stats.StatChanged += OnStatChanged;
            Motor.RefreshFromStats();
        }

        //TODO: Put cooldowns here
        //TODO: Put buffs here

        public CharacterMotor Motor { get; }

        public string? Name { get; set; }

        public Dictionary<WeaponSlot, CharacterWeapon> Weapons { get; } = new();

        readonly Dictionary<WeaponSlot, Action> _weaponAttackHandlers = new();

        /// <summary>Current auto-attack target; <see cref="Identity.None"/> when not fighting.</summary>
        public Identity FightingTarget { get; private set; } = Identity.None;

        /// <summary>Raised once when the corpse swap completes (after <see cref="CorpseSwapDelayMilliseconds"/>).</summary>
        public event Action<Character>? Died;

        public const int CorpseSwapDelayMilliseconds = 2500;

        const int DefaultNpcDeathAnimationKey = 0x1F7;
        const int DefaultPlayerDeathAnimationKey = 500;

        bool _deathNotified;
        bool _corpseSwapPending;
        double _corpseSwapRemainingSeconds;

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
            SetFightingTarget(Identity.None);

            Cell?.Announce(
                new CharacterActionMessage
                {
                    Identity = Identity,
                    Action = CharacterActionType.Death,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = IsPlayer ? DefaultPlayerDeathAnimationKey : DefaultNpcDeathAnimationKey
                });

            _corpseSwapPending = true;
            _corpseSwapRemainingSeconds = CorpseSwapDelayMilliseconds / 1000.0;
        }

        void CompleteCorpseSwap()
        {
            if (!_corpseSwapPending)
                return;

            _corpseSwapPending = false;

            Playfield?.GetRequiredService<SpawnService>().SpawnCorpse(this);
            Died?.Invoke(this);
        }

        public void SetFightingTarget(Identity identity)
        {
            FightingTarget = identity;
            if (identity.Instance == 0)
                ResetAllWeaponAttacks();
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
                TickWeapons(deltaTime);
            base.Tick(deltaTime);
        }

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
                weapon?.Tick(deltaTime);
        }

        void ProcessWeaponSwing(WeaponSlot slot)
        {
            Character? target = TryResolveFightingTarget();
            if (target == null)
                return;

            if (!Weapons.TryGetValue(slot, out CharacterWeapon? characterWeapon) || characterWeapon == null)
                return;

            Item? weapon = characterWeapon.Item;
            double attackRange = weapon != null
                ? NormalizeCombatStat(weapon.GetStat(CharacterStat.AttackRange))
                : 0.0;
            if (attackRange <= 0.0)
                attackRange = MaxMeleeCombatDistance;

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
        }

        /// <summary>
        /// Applies hit-point damage. Returns true when this hit killed the character.
        /// </summary>
        public bool ApplyDamage(Character attacker, int damage, HitType hitType)
        {
            if (_deathNotified || damage <= 0)
                return false;

            int previousHealth = NormalizeCombatStat(Stats.Get(CharacterStat.Health));
            int newHealth = Math.Max(0, previousHealth - damage);
            Stats.Set(CharacterStat.Health, newHealth, StatDetail.Base, dirty: true);

            if (newHealth > 0)
                return false;

            OnDeath(attacker);
            return true;
        }

        Character? TryResolveFightingTarget()
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

        static int NormalizeCombatStat(int value)
            => value < 0 || StatCollection.IsUnset(value) ? 0 : value;

        const double MaxMeleeCombatDistance = 4.0;
        const double SoftRangeGraceMeters = 1.5;
        const double HardRangeMultiplier = 3.0;
        const int NormalAttackInfoAmmoCount = 40;
        const int PlayerUnarmedAttackInfoAmmoCount = -1;
        const int PlayerUnarmedAttackInfoWeaponInstance = 100;

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

            Profession profession = (Profession)Stats.Get(CharacterStat.Profession);
            int maSkill = Stats.Get(CharacterStat.MartialArts);
            if (StatCollection.IsUnset(maSkill) || maSkill < 1)
                maSkill = 1;

            (int lowId, int highId, int quality) = MartialArtsFistResolver.Resolve(profession, maSkill);
            Item fist = items.Create(lowId, highId, quality);
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
                || !item.IsWieldableCombatWeapon()
                || item.IsMaCombinedWeapon())
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

            return new WeaponItemFullUpdateMessage
            {
                Identity = new Identity
                {
                    Type = IdentityType.WeaponInstance,
                    Instance = item.InstanceId
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

            int maxHealth = Stats.Get(CharacterStat.MaxHealth);
            int currentHealth = Stats.Get(CharacterStat.Health);
            int monsterData = Stats.Get(CharacterStat.MonsterData);
            int monsterScale = Stats.Get(CharacterStat.Scale);
            int movementMode = (int)Motor.State;

            int petMasterInstance = Stats.Get(CharacterStat.PetMaster);
            int headMesh = Stats.Get(CharacterStat.HeadMesh);
            int runSpeedBase = Stats.Get(CharacterStat.RunSpeed, StatDetail.Base);
            int npcFamily = Stats.Get(CharacterStat.NPCFamily);
            int losHeight = Stats.Get((CharacterStat)466);
            bool isNpc = !IsPlayer
                && !StatCollection.IsUnset(npcFamily)
                && npcFamily != 0;

            // Unset VisualFlags truncates to 722 on the wire; live NPC SCFUs use 31.
            short wireVisualFlags = StatCollection.IsUnset(visualFlags)
                ? (isNpc ? (short)31 : (short)0)
                : (short)visualFlags;

            int side = Stats.Get(CharacterStat.Side, StatDetail.Base);
            int fatness = Stats.Get(CharacterStat.Fatness, StatDetail.Base);
            int breed = Stats.Get(CharacterStat.Breed, StatDetail.Base);
            int gender = Stats.Get(CharacterStat.Sex, StatDetail.Base);
            int race = Stats.Get(CharacterStat.Race, StatDetail.Base);

            int accountFlags = Stats.Get(CharacterStat.AccountFlags);
            int expansions = Stats.Get(CharacterStat.Expansion);
            int level = Stats.Get(CharacterStat.Level);

            var scfu = new SimpleCharFullUpdateMessage
            {
                Identity = Identity,
                Version = 58,
                PlayfieldId = playfieldId,
                Coordinates = coordinates,
                Heading = heading,
                Appearance = new Appearance
                {
                    Side = (Side)(StatCollection.IsUnset(side) ? 0 : side),
                    Fatness = (Fatness)(StatCollection.IsUnset(fatness) ? 0 : fatness),
                    Breed = (Breed)(StatCollection.IsUnset(breed) ? 0 : breed),
                    Gender = (Gender)(StatCollection.IsUnset(gender) ? 0 : gender),
                    Race = (uint)(StatCollection.IsUnset(race) ? 1 : race)
                },
                Name = name,
                CharacterFlags = (CharacterFlags)characterFlags,
                AccountFlags = StatCollection.IsUnset(accountFlags) ? (short)0 : (short)accountFlags,
                Expansions = StatCollection.IsUnset(expansions)
                    ? (isNpc ? (short)3 : (short)0)
                    : (short)expansions,
                Level = StatCollection.IsUnset(level) ? (short)0 : (short)level,
                VisualFlags = wireVisualFlags,
                VisibleTitle = 0,
                RunSpeedBase = StatCollection.IsUnset(runSpeedBase) ? (short)0 : (short)runSpeedBase,
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
                    LosHeight = StatCollection.IsUnset(losHeight) ? (short)0 : (short)losHeight
                };

                scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownDataFlag;
                scfu.SuppressedFlags |= SimpleCharFullUpdateFlags.UnknownFlag2;
            }
            else
            {
                var pcInfo = new SimplePcInfo
                {
                    CurrentNano = (uint)Stats.Get(CharacterStat.CurrentNano),
                    Team = 0,
                    Swim = 5,
                    StrengthBase = ClampToShort(Stats.Get(CharacterStat.Strength, StatDetail.Base)),
                    AgilityBase = ClampToShort(Stats.Get(CharacterStat.Agility, StatDetail.Base)),
                    StaminaBase = ClampToShort(Stats.Get(CharacterStat.Stamina, StatDetail.Base)),
                    IntelligenceBase = ClampToShort(Stats.Get(CharacterStat.Intelligence, StatDetail.Base)),
                    SenseBase = ClampToShort(Stats.Get(CharacterStat.Sense, StatDetail.Base)),
                    PsychicBase = ClampToShort(Stats.Get(CharacterStat.Psychic, StatDetail.Base))
                };

                // FirstName / LastName / OrganizationName not on Character yet.
                // if (scfu.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
                // {
                //     pcInfo.FirstName = FirstName;
                //     pcInfo.LastName = LastName;
                // }
                // if (!string.IsNullOrEmpty(OrganizationName))
                // {
                //     pcInfo.OrgName = OrganizationName;
                // }

                scfu.CharacterInfo = pcInfo;
            }

            int displayMaxHealth = maxHealth;
            int displayCurrentHealth = currentHealth;
            if (!StatCollection.IsUnset(maxHealth) && maxHealth > ushort.MaxValue)
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

            scfu.MonsterScale = StatCollection.IsUnset(monsterScale) ? (short)0 : (short)monsterScale;
            scfu.Unknown1 = CreateMovementStatus(movementMode);

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
                    if (meshes[i].Position != 0 || meshes[i].Layer != 4)
                        continue;

                    meshes[i] = new Mesh
                    {
                        Position = 0,
                        Id = (uint)headMesh,
                        OverrideTextureId = 0,
                        Layer = 4
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
                            Layer = 4
                        });
                }
            }

            return meshes.ToArray();
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

        /// <summary>SCFU Unknown1 movement-status blob (player vs NPC layouts differ).</summary>
        // TODO: Convert the arrays to a CharacterMovementStatus object.
        protected abstract byte[] CreateMovementStatus(int movementMode);
    }
}
