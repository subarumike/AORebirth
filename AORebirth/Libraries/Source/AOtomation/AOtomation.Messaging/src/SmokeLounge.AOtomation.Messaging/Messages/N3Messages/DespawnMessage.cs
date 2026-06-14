namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    public class DespawnMessage : N3Message
    {
        public DespawnMessage()
        {
            this.N3MessageType = N3MessageType.Despawn;
        }
    }
}
