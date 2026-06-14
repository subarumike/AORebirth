namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.ClientMoveItemToInventory)]
    public class ClientMoveItemToInventoryMessage : N3Message
    {
        public ClientMoveItemToInventoryMessage()
        {
            this.N3MessageType = N3MessageType.ClientMoveItemToInventory;
        }

        [AoMember(0)]
        public Identity SourceContainer { get; set; }

        [AoMember(1)]
        public int TargetPlacement { get; set; }
    }
}
