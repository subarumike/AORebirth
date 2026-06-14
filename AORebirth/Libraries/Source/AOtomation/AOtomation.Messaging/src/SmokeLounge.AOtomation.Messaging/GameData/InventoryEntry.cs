using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class InventoryEntry
    {
        [AoMember(0)]
        public int Slotnumber { get; set; }
        [AoMember(1)]
        public short UnknownFlags { get; set; }
        [AoMember(2)]
        // Maybe stack count?
        public short Unknown1 { get; set; }
        [AoMember(3)]
        public Identity Identity { get; set; }
        [AoMember(4)]
        public int LowId { get; set; }
        [AoMember(5)]
        public int HighId { get; set; }
        [AoMember(6)]
        public int Quality { get; set; }
        [AoMember(7)]
        public int Unknown2 { get; set; }
    }
}
