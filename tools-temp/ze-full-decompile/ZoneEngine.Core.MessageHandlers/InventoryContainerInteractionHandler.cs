using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class InventoryContainerInteractionHandler
{
	public static readonly InventoryContainerInteractionHandler Default = new InventoryContainerInteractionHandler();

	private InventoryContainerInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return InventoryContainerRuntimeService.Default.TryHandleGenericCmdUse(client, message, target);
	}
}
