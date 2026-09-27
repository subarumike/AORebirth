namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class Quest
    {
        [AoMember(0)]
        public Identity QuestId { get; set; }

        [AoMember(1)]
        /// <summary>Quest_t stream version; the client accepts 7-15 (Gamecode.dll FUN_100abc80).</summary>
        public int Version { get; set; }

        [AoMember(2)]
        public int Unknown2 { get; set; }

        [AoMember(3)]
        public int Unknown3 { get; set; }

        [AoMember(4)]
        /// <summary>Quest flags (Quest_t +0xa4); bit 0x100 is a team mission (N3Msg_IsTeamMission).</summary>
        public int Flags { get; set; }

        [AoMember(5, SerializeSize = ArraySizeType.NullTerminated)]
        public string ShortInfo { get; set; }

        [AoMember(6, SerializeSize = ArraySizeType.Int32)]
        public string LongInfo { get; set; }

        [AoMember(7)]
        public Identity UnknownId1 { get; set; }

        [AoMember(8)]
        /// <summary>RewardBox_t stream version, 3-6 (Gamecode.dll FUN_10086e85).</summary>
        public int RewardDescriptorVersion { get; set; }

        [AoMember(9)]
        /// <summary>Credits reward (RewardBox_t[0], N3Msg_QuestGetCashReward).</summary>
        public int CashReward { get; set; }

        [AoMember(10)]
        public int Unknown7 { get; set; }

        [AoMember(11)]
        /// <summary>XP reward (RewardBox_t[1], N3Msg_QuestGetXPReward).</summary>
        public int ExperienceReward { get; set; }

        [AoMember(12)]
        public int Unknown9 { get; set; }

        [AoMember(13)]
        public int Unknown10 { get; set; }

        [AoMember(14, SerializeSize = ArraySizeType.X3F1)]
        public MissionItemReward[] MissionItemData { get; set; }

        [AoMember(15)]
        public int Unknown11 { get; set; }

        [AoMember(16)]
        public int Unknown12 { get; set; }

        [AoMember(17)]
        public int Unknown13 { get; set; }

        [AoMember(18, SerializeSize = ArraySizeType.NoSerialization, FixedSizeLength = 4, IsFixedSize = true)]
        public string UnknownHash1 { get; set; }

        [AoMember(19)]
        public int Unknown14 { get; set; }

        [AoMember(20)]
        public int Unknown15 { get; set; }

        [AoMember(21)]
        public int Unknown16 { get; set; }

        [AoMember(22)]
        public int Unknown17 { get; set; }

        [AoMember(23)]
        public int Unknown18 { get; set; }

        [AoMember(24)]
        public Identity UnknownId2 { get; set; }

        [AoMember(25)]
        public int MissionIconId { get; set; }

        [AoMember(26)]
        public int Unknown20 { get; set; }

        [AoMember(27)]
        public int Unknown21 { get; set; }

        [AoMember(28, SerializeSize = ArraySizeType.X3F1)]
        public QuestActionInfo[] QuestActions { get; set; }

        [AoMember(29, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] PlayerIds { get; set; }

        [AoMember(30, SerializeSize = ArraySizeType.Int32)]
        public int[] UnknownArray1 { get; set; }

        [AoMember(31, SerializeSize = ArraySizeType.Int32)]
        public int[] UnknownArray2 { get; set; }

        [AoMember(32, SerializeSize = ArraySizeType.Int32)]
        public CharacterInfo[] CharacterInfos { get; set; }

        [AoMember(33)]
        public int Unknown22 { get; set; }

        [AoMember(34, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] PlayerIds2 { get; set; }

        [AoMember(35)]
        public int Unknown23 { get; set; }

        [AoMember(36)]
        public int Unknown24 { get; set; }

        [AoMember(37)]
        public Identity UnknownId3 { get; set; }

        [AoMember(38)]
        public int Unknown25 { get; set; }

        [AoMember(39)]
        public int Unknown26 { get; set; }

        [AoMember(40, SerializeSize = ArraySizeType.Int32)]
        public QuestIdentity[] QuestIdentities { get; set; }

        [AoMember(41)]
        public int Unknown27 { get; set; }

        [AoMember(42, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] FactionInfos { get; set; }

        /// <summary>
        /// Not part of Quest_t: the byte after the quest list (QuestFullUpdateIIR_t +0x28, Gamecode.dll FUN_100acd41),
        /// so it only lines up for a one-quest update. Non-zero makes the client print Feedback_YouGotANewMission and
        /// refresh the journal (FUN_10056abb); retail sends 1 on assignment and 0 when re-sending on zone entry.
        /// </summary>
        [AoMember(43)]
        public byte Unknown28 { get; set; }
    }
}
