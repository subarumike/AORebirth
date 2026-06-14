namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.DropDynel)]
    public class DropDynelMessage : N3Message
    {
        public DropDynelMessage()
        {
            this.N3MessageType = N3MessageType.DropDynel;
        }

        public Vector3 Position { get; set; }
    }
}
