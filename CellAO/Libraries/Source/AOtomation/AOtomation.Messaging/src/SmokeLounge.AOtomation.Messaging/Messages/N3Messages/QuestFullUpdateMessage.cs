using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;
    using SmokeLounge.AOtomation.Messaging.Serialization.Serializers;

    [AoContract((int)N3MessageType.QuestFullUpdate)]
    public class QuestFullUpdateMessage : N3Message
    {
        public QuestFullUpdateMessage()
        {
            this.N3MessageType = N3MessageType.QuestFullUpdate;
        }


        [AoMember(1, SerializeSize = ArraySizeType.X3F1)]
        public QuestInfo[] QuestInfos { get; set; }

    }
}
