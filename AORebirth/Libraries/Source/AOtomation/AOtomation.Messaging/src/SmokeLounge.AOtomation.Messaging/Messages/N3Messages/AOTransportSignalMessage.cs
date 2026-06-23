namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.AoTransportSignal)]
    public class AOTransportSignalMessage : N3Message
    {
        public AOTransportSignalMessage()
        {
            this.N3MessageType = N3MessageType.AoTransportSignal;
        }

        [AoMember(3)]
        public int Signal { get; set; }

        public byte[] Payload { get; set; }
    }

    [AoContract((int)N3MessageType.CityControllerWindowClose)]
    public class CityControllerWindowCloseMessage : N3Message
    {
        public CityControllerWindowCloseMessage()
        {
            this.N3MessageType = N3MessageType.CityControllerWindowClose;
        }

        [AoMember(0)]
        public int WindowInstance { get; set; }
    }
}
