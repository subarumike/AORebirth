using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class FullCharacterSub
    {
        [AoMember(1)]
        public byte Unknown1 { get; set; }
        [AoMember(2)]
        public byte Unknown2 { get; set; }
        [AoMember(3)]
        public byte Unknown3 { get; set; }
    }
}
