namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Textures;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Movement;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    //TODO: Nano casting should live here

    /// <summary>
    /// Character layer between Dynel and Player (shared NPC/player fields).
    /// </summary>
    public class Character : Dynel
    {
        public Character(Identity identity)
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

        /// <summary>Source mob template when this character was spawned from <see cref="Mobs.IMobTemplateCatalog"/>.</summary>
        public Mobs.MobTemplate? MobTemplate { get; set; }

        /// <summary>Raised once when <see cref="NotifyDeath"/> is called.</summary>
        public event Action<Character>? Died;

        bool _deathNotified;

        /// <summary>Notifies spawn/death listeners. Idempotent.</summary>
        public void NotifyDeath()
        {
            if (_deathNotified)
                return;

            _deathNotified = true;
            Died?.Invoke(this);
        }

        public override void Tick(double deltaTime)
        {
            Motor.Tick(deltaTime);
            base.Tick(deltaTime);
        }

        void OnStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            Motor.OnStatChanged(stat, previous, next, isInitialSet);
        }

        public List<AOTextures> Textures { get; } = new();
        public List<Mesh> Meshes { get; } = new();
        public List<int> UploadedNanoIds { get; } = new();

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

            int selectedTarget = Stats.Get(CharacterStat.SelectedTarget);
            if (!StatCollection.IsUnset(selectedTarget) && selectedTarget != 0)
            {
                scfu.FightingTarget = new Identity
                {
                    Type = (IdentityType)Stats.Get(CharacterStat.SelectedTargetType),
                    Instance = selectedTarget
                };
            }

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
            scfu.Unknown1 = isNpc
                ? CreateNpcUnknown1(movementMode)
                : CreatePlayerUnknown1(movementMode);

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

        private static byte[] CreatePlayerUnknown1(int movementMode) =>
        [
            0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            (byte)movementMode, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        private static byte[] CreateNpcUnknown1(int movementMode) =>
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            (byte)movementMode, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
        ];
    }
}
