using AORebirth.Core.Components;
using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class RexB18DInteractionHandler
{
	public static readonly RexB18DInteractionHandler Default = new RexB18DInteractionHandler();

	private RexB18DInteractionHandler()
	{
	}

	public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (!RexMarcusChainCoordinator.IsCargoBoxTarget(target))
		{
			return false;
		}
		if (RexMarcusChainCoordinator.OnCargoUse(client.Controller.Character, target))
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(client.Controller.Character, message);
			return true;
		}
		if (RexMarcusChainCoordinator.TryRejectCargoWithoutQuest(client, message, target))
		{
			return true;
		}
		return false;
	}
}
