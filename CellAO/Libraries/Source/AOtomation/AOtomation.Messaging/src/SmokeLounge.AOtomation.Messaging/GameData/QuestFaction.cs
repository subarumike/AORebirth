using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using System.Dynamic;

    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class QuestFaction
    {
        [AoMember(0)]
        public int Unknown1 { get; set; }
        [AoMember(1)]
        public int Unknown2 { get; set; }
    }
}
