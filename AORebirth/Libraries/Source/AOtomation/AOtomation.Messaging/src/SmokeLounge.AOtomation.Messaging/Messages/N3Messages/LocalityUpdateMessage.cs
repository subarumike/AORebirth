namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.LocalityUpdate)]
    public class LocalityUpdateMessage : N3Message
    {
        public LocalityUpdateMessage()
        {
            this.N3MessageType = N3MessageType.LocalityUpdate;
        }

        public Vector3 Position { get; set; }

        public byte LocalityFlag { get; set; }
    }
}
