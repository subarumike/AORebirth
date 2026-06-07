namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.StartLogout)]
    public class StartLogoutMessage : N3Message
    {
        public StartLogoutMessage()
        {
            this.N3MessageType = N3MessageType.StartLogout;
        }
    }
}
