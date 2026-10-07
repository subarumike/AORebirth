namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Teams;

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

        /// <summary>Retail corpse Flags on CorpseFullUpdate.</summary>
        private const int DefaultFlags = 1579013;
        public const int LootReserveSeconds = 60;

        // A player's corpse, from live "Remains of Aagent" (female Nanomage, capture 2026-10-07T17:53:00Z, as seen by
        // another player): CanChangeClothes 1, DeadTimer 75, no cash, the body CAT mesh (5941, nanomage_female.cir),
        // and its ItemAnimEffect differs from an NPC's in HeaderB, Unknown7, Unknown9 and MonsterData.
        private const int PlayerDeadTimer = 75;
        private const int PlayerAnimHeaderB = 0x3A24246F;
        private const int PlayerAnimUnknown7 = 501;
        private const int PlayerAnimUnknown9 = 3;

        private readonly bool _ownerIsPlayer;
        private readonly bool _ownerWoreRobe;

        private readonly IGameData _gameData;
        private bool _cashClaimed;

        public Corpse(Identity identity, Character dead, IGameData gameData)
            : base(identity, IdentityType.Corpse, LootCapacity)
        {
            ArgumentNullException.ThrowIfNull(dead);
            ArgumentNullException.ThrowIfNull(gameData);

            _gameData = gameData;
            _ownerIsPlayer = dead.IsPlayer;
            _ownerWoreRobe = dead.WearsRobeShape;
            Owner = dead.Identity;
            Name = string.IsNullOrEmpty(dead.Name)
                ? "Remains"
                : "Remains of " + dead.Name;
            Position = dead.Position;
            Rotation = dead.Rotation;
            Playfield = dead.Playfield;
            LootLevel = dead.Stats.GetOrOne(CharacterStat.Level);
            if (dead is NpcCharacter npc)
            {
                ItemTable = npc.MobTemplate?.ItemTable;
                CorpseFullUpdateTemplate = CopyCorpseFullUpdateTemplate(npc.MobTemplate?.CorpseFullUpdateTemplate);
            }

            TimeExist = DefaultTimeExist;
            ExpiresAtUtc = DateTime.UtcNow.AddMilliseconds(TimeExist * 10);
            CopySourceStats(dead);
            OwnerTextures = dead.BuildWireTextures();
        }

        public Identity LootWinner { get; set; } = Identity.None;

        public DateTime ReservedUntilUtc { get; set; }

        protected override bool CanOpenLoot(Player player)
        {
            if (LootWinner.Instance == 0 || DateTime.UtcNow >= ReservedUntilUtc)
                return true;

            if (player.Identity.Instance == LootWinner.Instance)
                return true;

            // The winner's teammates share loot rights.
            TeamSnapshot? team = Playfield?.GetService<TeamService>()?.GetTeam(player);
            return team != null && team.MemberIds.Contains(LootWinner.Instance);
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

            int current = Math.Max(0, player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base));

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

        /// <summary>Owner's textures captured at death.</summary>
        Texture[] OwnerTextures { get; }

        MobCorpseFullUpdateTemplate? CorpseFullUpdateTemplate { get; }

        public override byte[]? BuildSpawnPacket(Identity receiver)
        {
            if (CorpseFullUpdateTemplate == null || CorpseFullUpdateTemplate.PacketTemplate.Length == 0)
                return null;

            byte[] packet = CopyBytes(CorpseFullUpdateTemplate.PacketTemplate);
            int sender = Playfield?.Identity.Instance ?? 0;
            int playfieldId = Playfield?.Identity.Instance ?? 0;
            SourceStats.TryGetValue(CharacterStat.Cash, out int cash);
            SourceStats.TryGetValue(CharacterStat.MonsterData, out int monsterData);
            TryResolveCatMesh(out int catMesh);

            WriteUInt16(packet, CorpseFullUpdateTemplate.MessageIdOffset, CorpseFullUpdateTemplate.MessageId, nameof(CorpseFullUpdateTemplate.MessageId));
            WriteUInt16(packet, CorpseFullUpdateTemplate.PacketLengthOffset, packet.Length, nameof(CorpseFullUpdateTemplate.PacketLengthOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.SenderInstanceOffset, sender, nameof(CorpseFullUpdateTemplate.SenderInstanceOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.ReceiverInstanceOffset, receiver.Instance, nameof(CorpseFullUpdateTemplate.ReceiverInstanceOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.CorpseInstanceOffset, Identity.Instance, nameof(CorpseFullUpdateTemplate.CorpseInstanceOffset));
            WriteSingle(packet, CorpseFullUpdateTemplate.PositionXOffset, (float)Position.x, nameof(CorpseFullUpdateTemplate.PositionXOffset));
            WriteSingle(packet, CorpseFullUpdateTemplate.PositionYOffset, (float)Position.y, nameof(CorpseFullUpdateTemplate.PositionYOffset));
            WriteSingle(packet, CorpseFullUpdateTemplate.PositionZOffset, (float)Position.z, nameof(CorpseFullUpdateTemplate.PositionZOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.PlayfieldIdOffset, playfieldId, nameof(CorpseFullUpdateTemplate.PlayfieldIdOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.DeadNpcInstanceOffset, Owner.Instance, nameof(CorpseFullUpdateTemplate.DeadNpcInstanceOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.CatMeshOffset, catMesh, nameof(CorpseFullUpdateTemplate.CatMeshOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.CashOffset, Math.Max(0, cash), nameof(CorpseFullUpdateTemplate.CashOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.MonsterDataOffset, monsterData, nameof(CorpseFullUpdateTemplate.MonsterDataOffset));
            WriteInt32(packet, CorpseFullUpdateTemplate.TailDeadNpcInstanceOffset, Owner.Instance, nameof(CorpseFullUpdateTemplate.TailDeadNpcInstanceOffset));
            return packet;
        }

        static byte[] CopyBytes(byte[] source)
        {
            var copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }

        static MobCorpseFullUpdateTemplate? CopyCorpseFullUpdateTemplate(MobCorpseFullUpdateTemplate? source)
        {
            if (source == null || source.PacketTemplate.Length == 0)
                return null;

            return new MobCorpseFullUpdateTemplate
            {
                PacketTemplate = CopyBytes(source.PacketTemplate),
                MessageId = source.MessageId,
                MessageIdOffset = source.MessageIdOffset,
                PacketLengthOffset = source.PacketLengthOffset,
                SenderInstanceOffset = source.SenderInstanceOffset,
                ReceiverInstanceOffset = source.ReceiverInstanceOffset,
                CorpseInstanceOffset = source.CorpseInstanceOffset,
                PositionXOffset = source.PositionXOffset,
                PositionYOffset = source.PositionYOffset,
                PositionZOffset = source.PositionZOffset,
                PlayfieldIdOffset = source.PlayfieldIdOffset,
                DeadNpcInstanceOffset = source.DeadNpcInstanceOffset,
                CatMeshOffset = source.CatMeshOffset,
                CashOffset = source.CashOffset,
                MonsterDataOffset = source.MonsterDataOffset,
                TailDeadNpcInstanceOffset = source.TailDeadNpcInstanceOffset
            };
        }

        static void WriteUInt16(byte[] packet, int offset, int value, string name)
        {
            if (offset < 0)
                return;

            if (value < ushort.MinValue || value > ushort.MaxValue)
                throw new InvalidOperationException($"{name} value {value} is outside UInt16 range.");

            RequireRange(packet, offset, sizeof(ushort), name);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(offset, sizeof(ushort)), (ushort)value);
        }

        static void WriteInt32(byte[] packet, int offset, int value, string name)
        {
            if (offset < 0)
                return;

            RequireRange(packet, offset, sizeof(int), name);
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(offset, sizeof(int)), value);
        }

        static void WriteSingle(byte[] packet, int offset, float value, string name)
        {
            if (offset < 0)
                return;

            RequireRange(packet, offset, sizeof(float), name);
            BinaryPrimitives.WriteSingleBigEndian(packet.AsSpan(offset, sizeof(float)), value);
        }

        static void RequireRange(byte[] packet, int offset, int count, string name)
        {
            if (offset > packet.Length - count)
                throw new InvalidOperationException($"{name} offset {offset} is outside the corpse packet template.");
        }

        public override MessageBody BuildSpawnMessage()
        {
            int playfieldId = Playfield != null ? Playfield.Identity.Instance : 0;
            MsgVector3 position = Position;
            MsgQuaternion heading = Rotation;

            TryResolveCatMesh(out int catMesh);
            SourceStats.TryGetValue(CharacterStat.MonsterData, out int monsterData);

            // Wire constants from live retail CorpseFullUpdate.
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
                Stats = BuildStats(catMesh),
                NameLength = Name.Length + 1,
                Name = Name,
                NameTerminator = 0,
                Unknown4 = 0x02,
                Unknown5 = 0x32,
                UnknownArray = [],
                Unknown6 = 0x03,
                AnimationEffects = _ownerIsPlayer ? BuildPlayerAnimationEffects() : BuildAnimationEffects(monsterData),
                // Dead character identity (AOSharp IdentityType.Character == CanbeAffected).
                UnknownIdentity = Owner,
                Textures = OwnerTextures.Length > 0 ? OwnerTextures : BuildDefaultTextures(),
                Unknown7 = 0
            };
        }

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

        GameTuple<CharacterStat, uint>[] BuildStats(int catMesh)
        {
            // Live Remains CFU: CATMesh present, MonsterData absent; include zeroed companion stats.
            List<GameTuple<CharacterStat, uint>> stats =
            [
                Tuple(CharacterStat.Flags, DefaultFlags),
                Tuple(CharacterStat.StaticInstance, 0),
                Tuple(CharacterStat.ACGItemLevel, 0),
                Tuple(CharacterStat.ACGItemTemplateID, 0),
                Tuple(CharacterStat.ACGItemTemplateID2, 0),
                Tuple(CharacterStat.MultipleCount, 1),
            ];

            AddCopied(stats, CharacterStat.Scale);
            stats.Add(Tuple(CharacterStat.CanChangeClothes, _ownerIsPlayer ? 1 : 0));
            AddCopied(stats, CharacterStat.Sex);
            AddCopied(stats, CharacterStat.Breed);
            AddCopied(stats, CharacterStat.Race);
            stats.Add(Tuple(CharacterStat.CorpseType, (int)Owner.Type));
            stats.Add(Tuple(CharacterStat.CorpseInstance, Owner.Instance));

            if (_ownerIsPlayer)
            {
                // Live order for a player's corpse: CATMesh, Cash 0, TimeExist, DeadTimer, HeadMesh.
                int playerCatMesh = catMesh != 0 ? catMesh : BreedBodyCatMesh();
                if (playerCatMesh != 0)
                    stats.Add(Tuple(CharacterStat.CATMesh, playerCatMesh));
                stats.Add(Tuple(CharacterStat.Cash, 0));
                stats.Add(Tuple(CharacterStat.TimeExist, TimeExist));
                stats.Add(Tuple(CharacterStat.DeadTimer, PlayerDeadTimer));
                AddCopied(stats, CharacterStat.HeadMesh);
                return stats.ToArray();
            }

            if (catMesh != 0)
                stats.Add(Tuple(CharacterStat.CATMesh, catMesh));

            AddCopied(stats, CharacterStat.Cash);
            AddCopied(stats, CharacterStat.HeadMesh);
            stats.Add(Tuple(CharacterStat.TimeExist, TimeExist));
            stats.Add(Tuple(CharacterStat.DeadTimer, DefaultDeadTimer));

            return stats.ToArray();
        }

        internal static AnimationEffect[] BuildAnimationEffects(int monsterData)
        {
            return
            [
                new AnimationEffect
                {
                    TypeId = (int)FunctionType.ItemAnimEffect,
                    HeaderB = 0,
                    HeaderC = 4,
                    Duration = 0,
                    Interval = 1,
                    Unknown7 = 500,
                    Unknown8 = 1,
                    Unknown9 = 4,
                    MonsterData = monsterData
                }
            ];
        }

        /// <summary>
        /// The body CAT mesh of a player without MonsterData. The client picks the base body from Breed and Sex
        /// (Gamecode.dll 0x1005780e); the RDB also carries a fat, thin and robe .cir for each, picked here by Fatness
        /// (0 thin, 1 normal, 2 fat) and the robe shape. Ids are the .cir names' CatMesh ids (RDB InfoObject names).
        /// Live has shown only the normal body (5941, nanomage_female.cir); the variants are being tried. 0 when none.
        /// </summary>
        int BreedBodyCatMesh()
        {
            SourceStats.TryGetValue(CharacterStat.Breed, out int breed);
            SourceStats.TryGetValue(CharacterStat.Sex, out int sex);
            if (!SourceStats.TryGetValue(CharacterStat.Fatness, out int fatness))
                fatness = 1;

            // normal, fat, thin, robe, fat robe, thin robe
            int[]? bodies = (breed, sex) switch
            {
                (1, 2) => [5907, 17532, 17534, 17870, 17867, 17872],   // solitus_male
                (1, 3) => [5927, 17528, 17530, 17919, 17917, 17921],   // solitus_female
                (2, 2) => [5914, 23377, 23378, 17915, 23376, 23379],   // opifex_male
                (2, 3) => [5934, 23373, 23374, 17913, 23372, 23375],   // opifex_female
                (3, 2) => [5921, 17905, 17909, 17903, 17907, 17911],   // nanomage_male
                (3, 3) => [5941, 23368, 23370, 17901, 23369, 23371],   // nanomage_female
                (4, 1) => [5900, 23365, 23366, 17899, 23364, 23367],   // athrox_male
                (5, 1) => [41432, 41432, 41432, 41432, 41432, 41432],  // unfinished-breed
                _ => null
            };
            if (bodies == null)
                return 0;

            int shape = fatness switch { 2 => 1, 0 => 2, _ => 0 };
            return bodies[shape + (_ownerWoreRobe ? 3 : 0)];
        }

        /// <summary>
        /// A player's corpse effect. Live's MonsterData field is 3 for a Nanomage whose Breed and Sex are both 3; it is
        /// taken as the Breed until a capture of another breed tells them apart.
        /// </summary>
        AnimationEffect[] BuildPlayerAnimationEffects()
        {
            SourceStats.TryGetValue(CharacterStat.Breed, out int breed);
            return
            [
                new AnimationEffect
                {
                    TypeId = (int)FunctionType.ItemAnimEffect,
                    HeaderB = PlayerAnimHeaderB,
                    HeaderC = 4,
                    Duration = 0,
                    Interval = 1,
                    Unknown7 = PlayerAnimUnknown7,
                    Unknown8 = 1,
                    Unknown9 = PlayerAnimUnknown9,
                    MonsterData = breed
                }
            ];
        }

        bool TryResolveCatMesh(out int catMesh)
        {
            catMesh = 0;
            if (!SourceStats.TryGetValue(CharacterStat.MonsterData, out int monsterData))
                return false;

            return _gameData.TryGetCatMesh(monsterData, out catMesh) && catMesh != 0;
        }

        void CopySourceStats(Character dead)
        {
            CharacterStat[] copy =
            [
                CharacterStat.Cash,
                CharacterStat.Sex,
                CharacterStat.Breed,
                CharacterStat.Race,
                CharacterStat.Scale,
                CharacterStat.MonsterData,
                CharacterStat.Fatness
            ];

            foreach (CharacterStat stat in copy)
            {
                // A player's corpse is empty: their cash stays with them, so opening it must not hand out a copy.
                if (stat == CharacterStat.Cash && dead.IsPlayer)
                    continue;

                int value = dead.Stats.Get(stat);
                if (!StatCollection.IsUnset(value))
                    SourceStats[stat] = value;
            }

            // The corpse carries one head mesh: the one the character showed, helmet or template head included.
            int headMesh = dead.ResolveShownHeadMesh();
            if (headMesh != 0)
                SourceStats[CharacterStat.HeadMesh] = headMesh;
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
