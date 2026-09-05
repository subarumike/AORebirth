namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Mobs;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Corpse dynel left when a character dies. Holds rolled loot in <see cref="LootableDynel.Loot"/>.
    /// </summary>
    public class Corpse : LootableDynel
    {
        public const int LootCapacity = 9;

        /// <summary>Lifetime in centiseconds (1/100 s). 18000 = 3 minutes.</summary>
        private const int DefaultTimeExist = 18000;
        private const int DefaultDeadTimer = 60;

        /// <summary>Live Biofreak-style Flags when source Flags is missing/zero.</summary>
        private const int DefaultCorpseFlags = 1579013;

        private readonly IGameData _gameData;
        private bool _cashClaimed;

        public Corpse(Identity identity, Character dead, IGameData gameData)
            : base(identity, IdentityType.Corpse, LootCapacity)
        {
            ArgumentNullException.ThrowIfNull(dead);
            ArgumentNullException.ThrowIfNull(gameData);

            _gameData = gameData;
            Owner = dead.Identity;
            Name = string.IsNullOrEmpty(dead.Name)
                ? "Remains"
                : "Remains of " + dead.Name;
            Position = dead.Position;
            Rotation = dead.Rotation;
            Playfield = dead.Playfield;
            LootLevel = NormalizeLevel(dead.Stats.Get(CharacterStat.Level));
            ItemTable = dead is NpcCharacter npc ? npc.MobTemplate?.ItemTable : null;
            TimeExist = DefaultTimeExist;
            ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(TimeExist * 10);
            CopySourceStats(dead);
        }

        /// <summary>
        /// First opener receives any cash on the corpse; later openers get none.
        /// </summary>
        protected override void OnOpened(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (_cashClaimed)
                return;

            _cashClaimed = true;

            if (!SourceStats.TryGetValue(CharacterStat.Cash, out int cash) || cash <= 0)
                return;

            SourceStats[CharacterStat.Cash] = 0;

            int current = player.Stats.Get(CharacterStat.Cash, StatDetail.Base);
            if (StatCollection.IsUnset(current) || current < 0)
                current = 0;

            long sum = (long)current + cash;
            int newCash = sum > int.MaxValue ? int.MaxValue : (int)sum;

            player.Stats.Set(CharacterStat.Cash, newCash, StatDetail.Base, dirty: false); // Client somehow knows?
        }

        public Identity Owner { get; }

        public string Name { get; }

        /// <summary>Lifetime sent on CFU; units are centiseconds.</summary>
        public int TimeExist { get; }

        public DateTime ExpiresAtUtc { get; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

        Dictionary<CharacterStat, int> SourceStats { get; } = new();

        public override MessageBody BuildSpawnMessage()
        {
            int playfieldId = Playfield != null ? Playfield.Identity.Instance : 0;
            MsgVector3 position = Position;
            MsgQuaternion heading = Rotation;

            // Wire constants from live CFU / AOSharp CorpseFullUpdate body writer.
            return new CorpseFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                Unknown1 = 0x08,
                Unknown2 = 0x0B,
                Owner = Identity.None,
                Position = position,
                Heading = heading,
                PlayfieldId = playfieldId,
                StateMachine = Identity.None,
                Unknown3 = 0x6F,
                Stats = BuildStats(),
                // AOSharp: WriteInt32(Name.Length + 1); WriteString(Name); WriteByte(0);
                NameLength = Name.Length + 1,
                Name = Name,
                NameTerminator = 0,
                Unknown4 = 0x02,
                Unknown5 = 0x32,
                UnknownArray = [],
                Unknown6 = 0x03,
                // TEMP: live Biofreak Remains anim/spell row
                AnimationEffects = [BuildHardcodedAnimEffect()],
                // Dead character identity (AOSharp IdentityType.Character == CanbeAffected).
                UnknownIdentity = Owner,
                Textures = BuildDefaultTextures(),
                Unknown7 = 0
            };
        }

        /// <summary>TEMP: one GfxEffect-style row from live Biofreak Remains CFU.</summary>
        static AnimationEffect BuildHardcodedAnimEffect() =>
            new()
            {
                IdentityType = 0xCF27,
                NanoId = unchecked((int)0x39EFD385),
                NanoInstance = 4,
                Time1 = 0,
                Time2 = 1,
                Unknown2 = 0,
                Unknown3 = 0,
                Unknown4 = 0,
                Unknown5 = 0,
                Unknown6 = 0,
                Unknown7 = 0x1F4,
                Unknown8 = 1,
                Unknown9 = 4,
                VisualDataId = 0x7632,
                Unknown10 = 0
            };

        static Texture[] BuildDefaultTextures()
        {
            var textures = new Texture[5];
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = new Texture
                {
                    Place = i,
                    Id = 0,
                    Unknown = 0
                };
            }

            return textures;
        }

        GameTuple<CharacterStat, uint>[] BuildStats()
        {
            // Live Remains CFU: CATMesh present, MonsterData absent; include zeroed companion stats.
            List<GameTuple<CharacterStat, uint>> stats =
            [
                Tuple(CharacterStat.Flags, ResolveFlags()),
                Tuple(CharacterStat.StaticInstance, 0),
                Tuple(CharacterStat.ACGItemLevel, 0),
                Tuple(CharacterStat.ACGItemTemplateID, 0),
                Tuple(CharacterStat.ACGItemTemplateID2, 0),
                Tuple(CharacterStat.MultipleCount, 1),
                Tuple(CharacterStat.CanChangeClothes, 0),
                Tuple(CharacterStat.TimeExist, TimeExist),
                Tuple(CharacterStat.DeadTimer, DefaultDeadTimer),
                Tuple(CharacterStat.CorpseType, (int)Owner.Type),
                Tuple(CharacterStat.CorpseInstance, Owner.Instance),
                // TEMP: live Biofreak Remains Sex/Breed until source copy is proven
                Tuple(CharacterStat.Sex, 2),
                Tuple(CharacterStat.Breed, 7),
            ];

            if (TryResolveCatMesh(out int catMesh))
                stats.Add(Tuple(CharacterStat.CATMesh, catMesh));

            AddCopied(stats, CharacterStat.Race);
            AddCopied(stats, CharacterStat.Scale);
            AddCopied(stats, CharacterStat.Cash);

            return stats.ToArray();
        }

        bool TryResolveCatMesh(out int catMesh)
        {
            catMesh = 0;
            if (!SourceStats.TryGetValue(CharacterStat.MonsterData, out int monsterData))
                return false;

            //DefaultMob Override
            if (monsterData == 26902)
                monsterData = 30258;

            return _gameData.TryGetCatMesh(monsterData, out catMesh);
        }

        int ResolveFlags()
        {
            if (SourceStats.TryGetValue(CharacterStat.Flags, out int flags) && flags != 0)
                return flags;

            return DefaultCorpseFlags;
        }

        void CopySourceStats(Character dead)
        {
            CharacterStat[] copy =
            [
                CharacterStat.Cash,
                CharacterStat.Race,
                CharacterStat.Scale,
                CharacterStat.Flags,
                CharacterStat.MonsterData
            ];

            foreach (CharacterStat stat in copy)
            {
                int value = dead.Stats.Get(stat);
                if (!StatCollection.IsUnset(value))
                    SourceStats[stat] = value;
            }
        }

        void AddCopied(List<GameTuple<CharacterStat, uint>> stats, CharacterStat stat)
        {
            if (SourceStats.TryGetValue(stat, out int value))
                stats.Add(Tuple(stat, value));
        }

        static GameTuple<CharacterStat, uint> Tuple(CharacterStat stat, int value) =>
            new() { Value1 = stat, Value2 = (uint)value };
    }
}
