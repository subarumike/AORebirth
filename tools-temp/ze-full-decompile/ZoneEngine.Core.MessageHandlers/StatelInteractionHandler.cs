using AORebirth.Core.Components;
using AORebirth.Core.Network;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class StatelInteractionHandler
{
	public static readonly StatelInteractionHandler Default = new StatelInteractionHandler();

	private StatelInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (StatelInteractionRules.ResolveRouteMode(higherPriorityRoutesRejected: true) != StatelInteractionRouteMode.StatelFallback)
		{
			return false;
		}
		client.Controller.UseStatel(target, (EventType)0);
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(client.Controller.Character, message);
		return true;
	}
}
