namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.Resurrect)]
    public class ResurrectMessage : N3Message
    {
        public ResurrectMessage()
        {
            this.N3MessageType = N3MessageType.Resurrect;
        }

        [AoMember(1)]
        public int Unknown1 { get; set; }

        [AoMember(2)]
        public int Unknown2 { get; set; }
    }
}
