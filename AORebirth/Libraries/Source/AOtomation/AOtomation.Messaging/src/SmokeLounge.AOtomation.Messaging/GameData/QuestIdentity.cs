using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class QuestIdentity
    {
        [AoMember(0)]
        public Identity Unknown1 { get; set; }
        [AoMember(1)]
        public int Unknown2 { get; set; }
    }
}
