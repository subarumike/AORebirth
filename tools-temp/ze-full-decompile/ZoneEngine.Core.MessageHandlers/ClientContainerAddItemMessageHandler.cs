using AORebirth.Core.Components;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class ClientContainerAddItemMessageHandler : BaseMessageHandler<ClientContainerAddItemMessage, ClientContainerAddItemMessageHandler>
{
	protected override void Read(ClientContainerAddItemMessage message, IZoneClient client)
	{
		InventoryContainerRuntimeService.Default.HandleClientContainerAddItem(client, message);
	}
}
