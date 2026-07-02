namespace ZoneEngine.Core.MessageHandlers
{
    using AORebirth.Core.Components;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class ClientContainerAddItemMessageHandler :
        BaseMessageHandler<ClientContainerAddItemMessage, ClientContainerAddItemMessageHandler>
    {
        protected override void Read(ClientContainerAddItemMessage message, IZoneClient client)
        {
            InventoryContainerRuntimeService.Default.HandleClientContainerAddItem(client, message);
        }
    }
}
