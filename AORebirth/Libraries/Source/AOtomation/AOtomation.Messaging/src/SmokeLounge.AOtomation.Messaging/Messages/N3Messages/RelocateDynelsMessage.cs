namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.RelocateDynels)]
    public class RelocateDynelsMessage : N3Message
    {
        public RelocateDynelsMessage()
        {
            this.N3MessageType = N3MessageType.RelocateDynels;
            this.RelocatedIdentities = new Identity[0];
        }

        public Identity[] RelocatedIdentities { get; set; }
    }
}
