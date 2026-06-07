namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.StopLogout)]
    public class StopLogoutMessage : N3Message
    {
        public StopLogoutMessage()
        {
            this.N3MessageType = N3MessageType.StopLogout;
        }
    }
}
