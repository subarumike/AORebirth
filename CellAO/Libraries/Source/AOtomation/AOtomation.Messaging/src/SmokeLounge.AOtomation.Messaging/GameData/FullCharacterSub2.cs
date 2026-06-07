using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class FullCharacterSub2
    {
        [AoMember(1)]
        public int Unknown1 { get; set; }
        [AoMember(2)]
        public Identity Unknown2 { get; set; }
        [AoMember(3)]
        public int Unknown3 { get; set; }
        [AoMember(4)]
        public int Unknown4 { get; set; }
    }
}
