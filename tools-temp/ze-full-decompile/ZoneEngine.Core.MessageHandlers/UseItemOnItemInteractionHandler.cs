using AORebirth.Core.Network;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.MessageHandlers;

public sealed class UseItemOnItemInteractionHandler
{
	public static readonly UseItemOnItemInteractionHandler Default = new UseItemOnItemInteractionHandler();

	private UseItemOnItemInteractionHandler()
	{
	}

	public bool TryHandle(IZoneClient client, GenericCmdMessage message)
	{
		if (MissionRepairService.TryHandleUseItemOnItem(client, message))
		{
			return true;
		}
		if (MarcusB194GasFireProgressTracker.TryHandleUseItemOnItem(client, message))
		{
			return true;
		}
		if (SurveillanceUplinkQuestRuntime.TryHandleUseItemOnItem(client, message))
		{
			return true;
		}
		if (InventoryContainerRuntimeService.Default.TryHandleUseItemOnItem(client, message))
		{
			return true;
		}
		return NascenceStatueTeleportInteractionHandler.Default.TryHandleUseItemOnItem(client, message);
	}
}
