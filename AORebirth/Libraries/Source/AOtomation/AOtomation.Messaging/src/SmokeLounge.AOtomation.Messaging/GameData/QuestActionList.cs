using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using System.Dynamic;

    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class QuestActionList
    {
        // Maybe Version id again?
        [AoMember(0)]
        public int Version { get; set; }
        [AoMember(1)]
        public Identity Action { get; set; }

        [AoMember(2)]
        public Identity Unknown1 { get; set; }
        [AoMember(3)]
        public Identity Unknown2{ get; set; }
        [AoMember(4)]
        public Identity Unknown3 { get; set; }
        [AoMember(5)]
        public Identity Unknown4 { get; set; }

        [AoMember(6)]
        public float Unknown5 { get; set; }
        [AoMember(7)]
        public float Unknown6 { get; set; }
        [AoMember(8)]
        public float Unknown7 { get; set; }
        [AoMember(9)]
        public float Unknown8 { get; set; }
        [AoMember(10)]

        public Identity Unknown9 { get; set; }
        [AoMember(11)]
        public float Unknown10 { get; set; }
        [AoMember(12)]
        public float Unknown11 { get; set; }
        [AoMember(13)]
        public float Unknown12 { get; set; }
        [AoMember(14)]
        public float Unknown13 { get; set; }
        [AoMember(15)]
        public Identity Unknown14 { get; set; }

        [AoMember(16)]
        public int UnknownHash15 { get; set; }
        [AoMember(17)]
        public int Unknown16 { get; set; }
        [AoMember(18)]
        public Identity Unknown17 { get; set; }

        [AoMember(19)]
        public Identity Playfield { get; set; }

        // Probably low and high id of the entrance
        [AoMember(20)]
        public int Unknown18 { get; set; }
        [AoMember(21)]
        public int Unknown19 { get; set; }
        [AoMember(22)]
        public float X { get; set; }
        [AoMember(23)]
        public float Y { get; set; }
        [AoMember(24)]
        public float Z { get; set; }

    }
}
