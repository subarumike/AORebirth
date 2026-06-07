namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.ClientContainerAddItem)]
    public class ClientContainerAddItemMessage : N3Message
    {
        public ClientContainerAddItemMessage()
        {
            this.N3MessageType = N3MessageType.ClientContainerAddItem;
        }

        public Identity Identity1 { get; set; }

        public Identity Identity2 { get; set; }
    }
}
