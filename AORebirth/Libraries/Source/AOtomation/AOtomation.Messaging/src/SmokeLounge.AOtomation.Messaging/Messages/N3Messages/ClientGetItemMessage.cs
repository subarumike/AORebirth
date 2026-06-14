namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.ClientGetItem)]
    public class ClientGetItemMessage : N3Message
    {
        public ClientGetItemMessage()
        {
            this.N3MessageType = N3MessageType.ClientGetItem;
        }

        public Identity Identity1 { get; set; }
    }
}
