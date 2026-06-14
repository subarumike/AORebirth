using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using System.Dynamic;

    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class QuestItemShort
    {
        [AoMember(0)]
        public int LowId { get; set; }
        [AoMember(1)]
        public int HighId { get; set; }
    [AoMember(2)]
        public int Quality { get; set; }

        // Always 0?
        [AoMember(3)]
    public int Unknown1 { get; set; }
    }
}
