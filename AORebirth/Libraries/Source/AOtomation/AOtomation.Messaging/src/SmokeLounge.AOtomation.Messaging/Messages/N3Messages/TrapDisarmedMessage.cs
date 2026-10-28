namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>Gamecode+A272B has no custom tail; inherited Identity targets the trap.</summary>
    [AoContract((int)N3MessageType.TrapDisarmed)]
    public class TrapDisarmedMessage : N3Message
    {
        public TrapDisarmedMessage()
        {
            this.N3MessageType = N3MessageType.TrapDisarmed;
        }
    }
}
