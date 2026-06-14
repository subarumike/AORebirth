using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;
    
    public class QuestInfo
    {
        // Unique identity for this quest
        [AoMember(0)]
        public Identity QuestIdentity { get; set; }

        // Always 0x0f?
        [AoMember(1)]
        public int Unknown1 { get; set; }

        [AoMember(2)]
        public int Unknown2 { get; set; }

        [AoMember(3)]
        public int Unknown3 { get; set; }

        [AoMember(4)]
        public int Unknown4 { get; set; }

        [AoMember(5, SerializeSize = ArraySizeType.NoSerialization, FixedSizeLength = 32)]
        public string ShortInfo { get; set; }

        [AoMember(6, SerializeSize = ArraySizeType.Int32)]
        public string Info { get; set; }
        [AoMember(7)]
        public Identity Unknown5 { get; set; }

        // We need to distinguish later for the older versions (actual version=6)
        [AoMember(8)]
        public int RewardDescriptorVersion { get; set; }

        [AoMember(9)]
        public int CashReward { get; set; }
        // a null again?
        [AoMember(10)]
        public int Unknown6 { get; set; }
        [AoMember(11)]
        public int ExperienceReward { get; set; }

        [AoMember(12, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] UnknownIdentities1 { get; set; }
        [AoMember(13, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] UnknownIdentities2 { get; set; }

        [AoMember(14, SerializeSize = ArraySizeType.X3F1)]
        public QuestItemShort[] ItemRewards { get; set; }

        [AoMember(15)]
        public int Unknown7 { get; set; }
        [AoMember(16)]
        public int Unknown8 { get; set; }
        [AoMember(17)]
        public int Unknown9 { get; set; }
        [AoMember(18)]
        public int UnknownHash { get; set; }
        [AoMember(19)]
        public int Quality { get; set; }

        [AoMember(20)]
        public int Unknown10 { get; set; }
        [AoMember(21)]
        public int Unknown11 { get; set; }
        [AoMember(22)]
        public int Unknown12 { get; set; }
        [AoMember(23)]

        public int Unknown13 { get; set; }

        [AoMember(24)]
        public Identity Unknown14 { get; set; }
        [AoMember(25)]
        public int MissionIconId { get; set; }
        [AoMember(26)]
        public int Unknown15 { get; set; }
        [AoMember(27)]
        public int Unknown16 { get; set; }
        [AoMember(28, SerializeSize = ArraySizeType.X3F1)]
        public QuestActionList[] QuestActions { get; set; }
        [AoMember(29, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] Unknown17 { get; set; }

        [AoMember(30, SerializeSize = ArraySizeType.Int32)]
        public int[] Unknown18 { get; set; }
        [AoMember(31, SerializeSize = ArraySizeType.Int32)]
        public int[] Unknown19 { get; set; }

        [AoMember(32, SerializeSize = ArraySizeType.Int32)]
        public QuestCharInfo[] CharInfos { get; set; }

        [AoMember(33)]
        public int Unknown20 { get; set; }
        [AoMember(34, SerializeSize = ArraySizeType.X3F1)]
        public Identity[] UnknownIdentities20 { get; set; }

        [AoMember(35)]
        public int Unknown21 { get; set; }
        [AoMember(36)]
        public int Unknown22 { get; set; }

        [AoMember(37)]
        public Identity Unknown23 { get; set; }

        [AoMember(38)]
        public int Unknown24 { get; set; }
        [AoMember(39)]
        public int Unknown25 { get; set; }

        [AoMember(40, SerializeSize = ArraySizeType.Int32)]
        public QuestIdentity[] QuestIdentities { get; set; }
        [AoMember(41)]
        public int Unknown26 { get; set; }
        [AoMember(42, SerializeSize = ArraySizeType.X3F1)]
        public QuestFaction[] FactionInfo { get; set; }

        [AoMember(43)]
        public byte Unknown27 { get; set; }
    }
}
