using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.Action)]
    public class ActionMessage : N3Message
    {
        public ActionMessage()
        {
            this.N3MessageType = N3MessageType.Action;
        }

        [AoMember(0)]
        public int ActionCode { get; set; }

        [AoMember(1)]
        public int ActionIdentity { get; set; }

        [AoMember(2)]
        public Identity Target { get; set; }

    }
}
