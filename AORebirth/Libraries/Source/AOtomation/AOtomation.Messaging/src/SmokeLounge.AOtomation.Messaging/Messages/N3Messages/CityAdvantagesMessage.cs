namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.CityAdvantages)]
    public class CityAdvantagesMessage : N3Message
    {
        public CityAdvantagesMessage()
        {
            this.N3MessageType = N3MessageType.CityAdvantages;
            this.Advantages = new CityAdvantage[0];
        }

        [AoMember(1, SerializeSize = ArraySizeType.Int32)]
        public CityAdvantage[] Advantages { get; set; }
    }

    public class CityAdvantage
    {
        [AoMember(0)]
        public int LowId { get; set; }

        [AoMember(1)]
        public int HighId { get; set; }

        [AoMember(2)]
        public int QualityLevel { get; set; }

        [AoMember(3)]
        public int Unknown { get; set; }
    }
}
