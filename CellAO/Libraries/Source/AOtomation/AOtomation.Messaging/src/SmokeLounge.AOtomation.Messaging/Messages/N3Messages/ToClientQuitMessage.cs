namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.ToClientQuit)]
    public class ToClientQuitMessage : N3Message
    {
        public ToClientQuitMessage()
        {
            this.N3MessageType = N3MessageType.ToClientQuit;
        }
    }
}
