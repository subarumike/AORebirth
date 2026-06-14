using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class QuestCharInfo
    {
        [AoMember(0)]
        public Identity CharacteIdentity { get; set; }
        [AoMember(1, SerializeSize = ArraySizeType.Int32)]
        public string CharacterName { get; set; }
    }
}
