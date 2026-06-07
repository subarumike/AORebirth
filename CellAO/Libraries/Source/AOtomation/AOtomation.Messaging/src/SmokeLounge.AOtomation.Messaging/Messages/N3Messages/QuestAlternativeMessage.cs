using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using System.Dynamic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.QuestAlternative)]
    public class QuestAlternativeMessage:N3Message
    {
        public QuestAlternativeMessage()
        {
            this.N3MessageType = N3MessageType.QuestAlternative;
        }

        [AoMember(0)]
        public byte VersionId { get; set; } 

        [AoMember(1)]
        public byte LevelSlider { get; set; }
        [AoMember(2)]
        public byte GoodBadSlider { get; set; }
        [AoMember(3)]
        public byte OrderChaosSlider { get; set; }
        [AoMember(4)]
        public byte OpenHiddenSlider { get; set; }
        [AoMember(5)]
        public byte PhysicalMysticalSlider { get; set; }
        [AoMember(6)]
        public byte HeadOnStealthSlider { get; set; }
        [AoMember(7)]
        public byte MoneyExperienceSlider { get; set; }
        
        // Maybe this is the last Random which generated this mission packet
        [AoMember(8)]
        public int Unknown4 { get; set; }

        [AoMember(9)]
        public byte Unknown5 { get; set; }
        [AoMember(10)]
        public Identity MissionTerminalIdentity { get; set; }

        [AoMember(11,SerializeSize = ArraySizeType.Byte)]
        public QuestInfo[] QuestInfos { get; set; }




    }
}
